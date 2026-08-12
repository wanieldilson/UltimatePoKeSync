# Prompt da incollare in Claude Code

Apri il repo `UltimatePoKeSync`, copia dentro la cartella `design_handoff_pokedex_ui/`
(README.md + design/) e incolla questo messaggio:

---

I want to restyle the UltimatePoKeSync dashboard exactly as designed in
`design_handoff_pokedex_ui/`.

Read `design_handoff_pokedex_ui/README.md` first — it is the spec, and it is exact:
colours, fonts, border widths, radii, flat shadow offsets, spacing, copy. Then open
`design_handoff_pokedex_ui/design/UltimatePoKeSync UI.dc.html` in a browser and look at it;
the tabs work, so you can see all six screens.

Rules:

1. The HTML is a **design reference, not code to port**. Rebuild it in Avalonia XAML inside
   `src/UltimatePoKeSync.App`, using the existing view-models, bindings and services. No
   WebView, no new UI framework, no new dependency beyond the fonts.
2. Match it **pixel for pixel**. Every border is 3 px or 4 px solid `#08070C`, every shadow is
   a hard offset with zero blur, every panel title is a tilted pill straddling the top border.
   If something looks "cleaner" without those, it is wrong — the whole point is the comic ink
   look.
3. All numbers in the mock are sample data. Bind everything to the real snapshot and the real
   analysis output; nothing on screen may be hard-coded.
4. Keep the existing behaviour and guarantees intact: read-only, no network, type colour is
   never the only signal (every chip prints its type name), eggs excluded from analysis.
5. Start with the design tokens and the shell (title bar, header, tab strip, party rail),
   then the Pokémon screen, then Stats & IV/EV, Best set, Learnset, Team, Bridge — one commit
   per screen, and show me a screenshot after each.
6. Extend `TypePalette.cs` with the brightened values in the README rather than adding a
   second palette, and add the three fonts to `Assets/Fonts` plus `THIRD_PARTY_NOTICES.md`.

Ask me before inventing any UI that is not in the design.

---

## Se Claude Code non riesce ad aprire l'HTML

Fagli fare degli screenshot: `design/UltimatePoKeSync UI.dc.html` si apre in qualsiasi
browser, e il README da solo basta comunque per ricostruire tutto.
