# Game knowledge graph

Game facts as a graph (objects, recipes, stations, loot, spawns, effects, tiles), from the game's own data only.
Design and records: session 코어키퍼-AI-캐릭터, work/2026-11/2026-11-04-게임-지식그래프/.

- `node build.mjs [--dump <knowledge-dump.json>]` builds `data/<steam buildid>/graph.json` from the install's Conf
  JSON, plus the mod's dump when there is one (`/ai kgdump`, test switch on; file `AIPlayers/knowledge-dump.json`).
  Without a dump only names, loot tables and spawns are in the graph.
- `node kg.mjs uses|how|station|find <name|id>`: developer view (everything).
  `--as <member> [--world <id>]`: only what that character has opened (its `known.json`, written by the mod).
- `node kg.mjs prefill --as <member>`: makes a known.json from the character's memory.json into
  `data/known/...` (same first-fill rule as the mod), for trying `--as` before the mod has written one.
- `data/` is not in git (game data).
