#!/usr/bin/env python3
"""Measures whether melonDS can be read from while it runs, and what it costs.

Gen 4 and 5 are Nintendo DS games, so mGBA cannot run them. melonDS can, and it
ships a GDB stub instead of Lua scripting. Before writing a provider around that
stub, three things have to be true, and none of them can be assumed:

  1. The emulator keeps running after we connect. A GDB stub halts its target on
     connection and waits; this sends the continue that releases it.
  2. Memory can be read *while* it runs. A debugger normally stops the target
     first, which we cannot do four times a second. This checks by reading the
     same address repeatedly and seeing whether the contents change.
  3. The reads are cheap enough. melonDS only polls the stub when the JIT is off,
     so the whole approach already costs the recompiler; if polling costs much
     more on top, the idea is dead.

Usage:
    python3 tools/measure-melonds-gdb.py                 # defaults below
    python3 tools/measure-melonds-gdb.py --seconds 30 --rate 4

In melonDS first: Config -> Emu settings -> turn *off* the JIT recompiler and
turn *on* the GDB stub, then restart the game. Watch the window title while this
runs: it shows [fps/target], and the fps must not fall while this is polling.
"""

import argparse
import socket
import statistics
import sys
import time

# The ARM7, not the ARM9. Both CPUs get a stub, and the ARM9 one (the obvious
# choice, since it runs the game logic) completes the handshake and then closes
# the connection on the first command, whatever the command is. The ARM7 stub
# answers properly, and main RAM is shared between the two processors, so it can
# read everything we care about anyway.
DEFAULT_PORT = 3334

# Main RAM on the DS. The party lives somewhere in here, but this tool does not
# need to know where: it is measuring the channel, not reading Pokemon.
MAIN_RAM = 0x02000000

# The stub allows up to half its 1152-byte buffer, but that limit is wrong: 576
# bytes become 1152 hex characters, which no longer leave room for the framing in
# a response buffer of exactly 1152. Anything near the limit times out. This is
# the size that was actually observed to work.
MAX_READ = 256


class GdbError(Exception):
    pass


class GdbClient:
    """Just enough of the GDB remote serial protocol to read memory."""

    def __init__(self, host: str, port: int, timeout: float = 5.0) -> None:
        self.sock = socket.create_connection((host, port), timeout=timeout)
        self.sock.setsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY, 1)
        self.buffer = b""

        # melonDS waits one second for a bare '+' before it will speak to anyone,
        # and swallows whatever arrives first as that acknowledgement. Sending a
        # packet straight away loses its opening '$' to the handshake and the rest
        # arrives as garbage, which the stub answers by hanging up.
        self.sock.sendall(b"+")

    def close(self) -> None:
        # Detach first. Connecting halts the emulated CPU, and merely dropping the
        # socket can leave the game frozen for the person playing it.
        try:
            self.send("D")
            self.sock.settimeout(1.0)
            self.receive()
        except (OSError, GdbError):
            pass

        self.sock.close()

    def send(self, payload: str) -> None:
        checksum = sum(payload.encode()) & 0xFF
        self.sock.sendall(b"$" + payload.encode() + b"#" + f"{checksum:02x}".encode())

    def receive(self) -> str:
        """Reads one packet, skipping the acknowledgements that frame it."""
        while True:
            start = self.buffer.find(b"$")
            end = self.buffer.find(b"#", start + 1)
            if start >= 0 and end >= 0 and len(self.buffer) >= end + 3:
                payload = self.buffer[start + 1 : end]
                self.buffer = self.buffer[end + 3 :]
                self.sock.sendall(b"+")
                return payload.decode(errors="replace")

            chunk = self.sock.recv(4096)
            if not chunk:
                raise GdbError("the emulator closed the connection")
            self.buffer += chunk

    def command(self, payload: str) -> str:
        self.send(payload)
        return self.receive()

    def read_memory(self, address: int, length: int) -> bytes:
        reply = self.command(f"m{address:x},{length:x}")
        if reply.startswith("E") or not reply:
            raise GdbError(f"read at {address:#010x} refused: {reply or '(empty)'}")
        return bytes.fromhex(reply)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=DEFAULT_PORT)
    parser.add_argument(
        "--seconds", type=float, default=20.0, help="how long to keep polling")
    parser.add_argument(
        "--rate", type=float, default=4.0, help="reads per second, as the app would poll")
    parser.add_argument(
        "--bytes", type=int, default=MAX_READ, help=f"bytes per read, at most {MAX_READ}")
    args = parser.parse_args()

    if args.bytes > MAX_READ:
        print(f"The stub refuses more than {MAX_READ} bytes per read.", file=sys.stderr)
        return 2

    try:
        client = GdbClient(args.host, args.port)
    except OSError as error:
        print(f"Could not reach the stub on {args.host}:{args.port} — {error}")
        print("In melonDS: Config -> Emu settings -> enable the GDB stub, disable the JIT.")
        return 1

    print(f"Connected to {args.host}:{args.port}.")

    # 1. Connecting halts the target. Release it before measuring anything. There is
    # no reply to a continue until the target stops again, so nothing is read here.
    client.send("c")
    print("Sent continue. The game should be running again — check the window.")
    time.sleep(0.5)

    # 2. Does it answer at all while running?
    try:
        first = client.read_memory(MAIN_RAM, args.bytes)
    except (GdbError, socket.timeout) as error:
        print(f"\nFAILED: no reply while running — {error}")
        print("The stub only serves reads while halted, so this approach will not work.")
        client.close()
        return 1

    print(f"Read {len(first)} bytes while running. The channel is open.\n")

    # 3. What does it cost, and does the machine keep moving underneath?
    interval = 1.0 / args.rate
    deadline = time.monotonic() + args.seconds
    timings: list[float] = []
    snapshots: set[bytes] = set()
    failures = 0

    while time.monotonic() < deadline:
        started = time.perf_counter()
        try:
            data = client.read_memory(MAIN_RAM, args.bytes)
            timings.append((time.perf_counter() - started) * 1000)
            snapshots.add(data)
        except (GdbError, socket.timeout) as error:
            failures += 1
            print(f"  read failed: {error}")

        remaining = interval - (time.perf_counter() - started)
        if remaining > 0:
            time.sleep(remaining)

    client.close()

    if not timings:
        print("Every read failed. Nothing to measure.")
        return 1

    print(f"{len(timings)} reads over {args.seconds:.0f}s at {args.rate}/s, {failures} failed")
    print(f"  round trip   median {statistics.median(timings):.1f} ms"
          f"   worst {max(timings):.1f} ms")
    print(f"  distinct results: {len(snapshots)} of {len(timings)}")

    if len(snapshots) == 1:
        print("\nWARNING: every read returned identical bytes. Either the emulator is")
        print("frozen while we poll — which is the thing that would kill this — or the")
        print("address happens to hold something that never changes. Check the FPS in")
        print("the window title before concluding either way.")
    else:
        print("\nThe contents changed between reads, so the emulator kept running.")

    print("\nNow compare the FPS in the title against the same scene with this tool")
    print("stopped. That difference is what our polling costs.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
