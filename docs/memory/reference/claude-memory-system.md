# Claude Code Auto-Memory System (this project's setup)

How Claude Code's auto-memory works, and how it's configured in ClashUp. Source: https://code.claude.com/docs/en/memory (requires Claude Code v2.1.59+).

## Location
- **Default**: `~/.claude/projects/<repo-derived>/memory/` — path derived from the git repo, machine-local, shared across worktrees, NOT synced across machines.
- **Configurable** via `autoMemoryDirectory` in any settings scope (user/project/local/policy). Value must be an absolute path or start with `~/`. When set in project `.claude/settings.local.json`, it's honored **only after you accept the workspace-trust dialog** (same gate as hooks) — so a fresh set requires a session restart + trust accept to take effect.
- **This project**: relocated to **`docs/memory/`** (inside the Obsidian vault → browsable AND git-tracked). Set in `.claude/settings.local.json`: `"autoMemoryDirectory": "~/Documents/ClashUp/docs/memory"`.

## Recall mechanics (why subfolders are safe)
- Only **`MEMORY.md`** auto-loads each session — its first **200 lines or 25KB**, whichever comes first. Keep it a concise index under that.
- **Topic files are NOT loaded at startup.** Claude reads them **on demand by following the links in `MEMORY.md`** using normal file tools. Recall is **index-driven, not a recursive folder scan.**
- Therefore **subfolders don't break recall** as long as `MEMORY.md`'s links are **subfolder-qualified** (e.g. `[title](reference/foo.md)`). Body `[[wikilinks]]` resolve by filename in Obsidian regardless of folder.

## This project's memory layout
- Foldered into: `architecture/ netcode/ abilities/ characters/ maps/ ui/ gameplay/ ops/ boot/ feedback/ reference/`, with `MEMORY.md` at the root as the index. See the layout note at the top of `MEMORY.md`.
- **When adding a memory**: put the file in the matching subfolder and link it from `MEMORY.md` with the subfolder-qualified path.
- Since memory is now **in-repo**, memory writes show up as uncommitted git changes — commit them periodically (the `/reflect` command does this; see [[reflect-command]] note).
- The old default external dir (`~/.claude/projects/.../memory/`) may still exist as a pre-migration backup; it can be deleted once `/memory` confirms the active dir is `docs/memory`.

## Obsidian + skills
Vault/skills/CLI setup that this memory dir lives inside: see [[obsidian-vault]].
