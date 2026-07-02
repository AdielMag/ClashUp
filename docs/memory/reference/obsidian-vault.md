# Obsidian Vault, Agent Skills & CLI

The repo has an Obsidian vault for browsing project markdown. Set up 2026-07-02.

## Vault
- **Vault root = `docs/`** (NOT repo root — keeps Obsidian's index away from the large Unity/.NET trees).
- Config committed in `docs/.obsidian/` (app.json, appearance.json, community-plugins.json, core-plugins.json). Per-machine UI state is gitignored: `docs/.obsidian/workspace*.json`.
- Opening it: in Obsidian, File → "Open folder as vault" → select `docs/` (this GUI step can't be scripted).
- Because `docs/memory/` lives inside the vault, the Claude memory files are browsable in Obsidian too. See [[claude-memory-system]].

## Agent Skills (NOT MCP)
- Installed from **kepano/obsidian-skills** (Obsidian creator's repo, Agent Skills spec) into **`.claude/skills/`**: `obsidian-markdown`, `obsidian-bases`, `json-canvas`, `obsidian-cli`, `defuddle`.
- These are plain SKILL.md-driven definitions — no MCP server, no runtime dependency. Claude Code's Skill tool auto-discovers them.
- Install technique: `git clone --depth 1 --filter=blob:none --sparse <repo>` then `git sparse-checkout set skills`, and copy `skills/<name>/` into `.claude/skills/`.
- `.claude/skills/` and `.claude/commands/` coexist fine (commands = custom slash commands, skills = model-invoked).

## Obsidian CLI (`obsidian`)
- The official CLI is **bundled inside the Obsidian desktop app** (v1.12.4+) — there is nothing to `npm install`.
- Enable once via **Settings → General → "Command line interface"** (adds `obsidian` to PATH). GUI toggle, not scriptable.
- It's a **remote-control client to a running Obsidian instance** — Obsidian must be open. Syntax: `obsidian <command> [param=value] [flag]` (e.g. `obsidian read file="My Note"`, `obsidian search query="x" limit=10`). `obsidian help` lists all.
