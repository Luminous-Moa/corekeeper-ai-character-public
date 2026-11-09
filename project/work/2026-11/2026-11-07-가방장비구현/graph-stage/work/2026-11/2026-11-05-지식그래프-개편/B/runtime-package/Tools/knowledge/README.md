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
- `rules.json` (in git): hand-written game rules (spawning, base safety, enemy paths, wall breaking), each with its
  level (게임 데이터·웹·사용자·추정) and sources. `build.mjs` puts them in the graph as `r:<id>` nodes with
  `rule_about` edges to the objects they name; rebuild after editing it. `node kg.mjs rule [words|id]` finds them;
  `how`/`uses` of an object show the rules about it. Rules are open to every character (`--as` too).
- `data/` is not in git (game data).
