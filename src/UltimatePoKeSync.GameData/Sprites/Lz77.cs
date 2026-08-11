namespace UltimatePoKeSync.GameData.Sprites;

/// <summary>
/// The GBA BIOS LZ77 decompressor, type <c>0x10</c>.
/// </summary>
/// <remarks>
/// Every Gen 3 sprite and palette in the ROM is stored this way. The format is a header
/// giving the decompressed size, then blocks of eight chunks preceded by a flag byte: a
/// clear bit means one literal byte, a set bit means a back-reference of 3 to 18 bytes.
/// Back-references may overlap what they are still writing, which is how runs are encoded,
/// so the copy has to be byte at a time rather than a block move.
/// </remarks>
public static class Lz77
{
    private const byte CompressionType = 0x10;

    /// <summary>Largest output accepted, a guard against a corrupt or misread header.</summary>
    private const int MaximumOutput = 1 << 20;

    /// <summary>
    /// Decompresses the block starting at <paramref name="offset"/>, or returns
    /// <see langword="false"/> when the bytes there are not a well-formed block. A wrong
    /// address looks exactly like a valid one until it does not, so failing is normal
    /// control flow here rather than an exception.
    /// </summary>
    public static bool TryDecompress(ReadOnlySpan<byte> source, int offset, out byte[] result)
    {
        result = [];

        if (offset < 0 || offset + 4 > source.Length || source[offset] != CompressionType)
        {
            return false;
        }

        int size = source[offset + 1] | (source[offset + 2] << 8) | (source[offset + 3] << 16);
        if (size is <= 0 or > MaximumOutput)
        {
            return false;
        }

        var output = new byte[size];
        int written = 0;
        int position = offset + 4;

        while (written < size)
        {
            if (position >= source.Length)
            {
                return false;
            }

            byte flags = source[position++];

            for (int bit = 0; bit < 8 && written < size; bit++)
            {
                bool isReference = (flags & (0x80 >> bit)) != 0;

                if (!isReference)
                {
                    if (position >= source.Length)
                    {
                        return false;
                    }

                    output[written++] = source[position++];
                    continue;
                }

                if (position + 1 >= source.Length)
                {
                    return false;
                }

                byte high = source[position++];
                byte low = source[position++];
                int length = (high >> 4) + 3;
                int distance = (((high & 0x0F) << 8) | low) + 1;

                if (distance > written || written + length > size)
                {
                    return false;
                }

                for (int i = 0; i < length; i++)
                {
                    output[written] = output[written - distance];
                    written++;
                }
            }
        }

        result = output;
        return true;
    }
}
