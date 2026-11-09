# Embedded compact Korean font

The AI mod bundles its own font payload; players do not install a separate font or mod.

Original font: [Galmuri9 by quiple](https://github.com/quiple/galmuri/tree/71e1cacf1437a11220307120e63e30bc275312d4), pinned commit `71e1cacf1437a11220307120e63e30bc275312d4`, `dist/Galmuri9.bdf` and `dist/OFL.txt`. The unmodified source and SIL Open Font License 1.1 are in `source/`. The derivative subset/format is named **AI Compact Korean**. The generated C# embeds the complete copyright and license notice so the existing script-only mod export carries them.

Run `node Tools/ui-font/build.mjs` to regenerate `Assets/AIPlayerSpike/Scripts/AICompactFontData.cs`, or append `--check` to verify exact reproducibility, supported text, and unclipped bitmap bounds. No package installation is needed. The generator retains ASCII, modern Hangul syllables, available Hangul jamo, and panel punctuation. The pinned BDF's SHA256 is recorded in the generated file. The current result is 11,369 glyphs in a 10px cell, gzip payload 64,742 bytes.

Runtime `AICompactFont` creates a point-filtered atlas and PugFont in memory. Only existing chat output, input, prompt and the AI mention hint receive that font; the game-wide TextManager is untouched. PugText continues measuring and positioning each chat message with the new font, so message height and multiline spacing follow the smaller cell. Non-bitmap dynamic-font paths and the game's unsupported-character fallback are preserved. The AI information panel draws labels from the same atlas. Font/sprites/UI are released on hosted-world exit and mod shutdown.

[Smaller Korean Font](https://mod.io/g/corekeeper/m/smaller-korean-font) established that Galmuri9 is useful for this game. Its implementation was not copied because source reuse permission was not established. [CombatMeter](https://github.com/Shiroko7/CoreKeeperCombatMeter) provides a public MIT IMGUI precedent; the panel and input handling here are original code, with no CombatMeter source or dependency included.

Runtime log lines `AI-UI font ready`, `AI-UI chat metrics`, and `AI-UI selected` expose startup time, original/replacement metrics, and selected snapshot row counts for real-game verification. These are observations, not automated proof of visual quality. Verify Korean input/IME, mention completion, multiline chat density, tabs, selection, click-through, menu/map hide, world exit/re-entry, and unchanged LLM traffic in the actual game.
