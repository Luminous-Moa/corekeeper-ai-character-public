# Core Keeper AI character

Source of an AI player mod for Core Keeper, built on the official
[Core Keeper ModSDK](https://github.com/Pugstorm/CoreKeeperModSDK).
The SDK itself is not included.

- `Assets/AIPlayerSpike/` — the mod: thin-client AI player body, OpenAI
  Decisions action selection, exploration/crafting/combat skills.
- `Assets/HeadlessTools/` — editor helper for headless build and install.
- `Tools/ai-bridge/` — local bridge that asks a subscription CLI member
  for the next goal (`node bridge.mjs`).
- `Tools/knowledge/` — game knowledge graph built from the game data
  (`node build.mjs`, query with `node kg.mjs`).

To build, copy `Assets/*` into a ModSDK checkout and build the
`AIPlayerSpike` mod from the SDK.
