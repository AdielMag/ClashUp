---
model: claude-opus-4-6
description: Fetch a Monday ticket, plan, and implement it
argument-hint: "<ticket_number> [extra context]"
---

Implement a Monday.com ticket. The user provides a ticket number and optionally extra context.

**Input**: `$ARGUMENTS` — format: `<ticket_number> [extra context]`

If `$ARGUMENTS` is empty, ask the user for a ticket number.

## Step 1: Fetch Ticket

Use the Monday.com GraphQL API to fetch the ticket details. The API token is in `$MONDAY_API_TOKEN` env var. If the env var is not set, use the token from `.claude/settings.local.json`.

```bash
curl -s -X POST https://api.monday.com/v2 \
  -H "Content-Type: application/json" \
  -H "Authorization: $MONDAY_API_TOKEN" \
  -d '{"query": "{ items(ids: [TICKET_ID]) { id name column_values { id type text value } updates(limit: 5) { text_body } subitems { id name column_values { id type text value } } } }"}'
```

Replace `TICKET_ID` with the number from `$ARGUMENTS`.

## Step 2: Understand the Ticket

Parse the response:
- **Name**: the ticket title
- **Updates**: detailed descriptions/specs (these contain the rich detail we wrote)
- **Subitems**: subtasks if any
- **Column values**: status, priority, category, assignee, timeline

Display a summary of the ticket to the user: title, category, priority, current status, and the key requirements from the update body.

## Step 3: Assess Readiness

Before planning, check if you have enough information to implement. Consider:
- Is the ticket a design/decision ticket (e.g., "What is the match loop?") or an implementation ticket?
- Are there dependencies on other tickets that aren't built yet?
- Is the scope clear enough to write code?

If the ticket is a **design ticket** (question mark in title, or TBD status), tell the user this is a design decision, not an implementation task, and offer to help think through it instead.

If there are **blocking dependencies** that don't exist in the codebase yet, list them and ask the user how to proceed.

If anything is **unclear or ambiguous**, ask the user targeted questions before proceeding. Do NOT guess — ask.

## Step 4: Plan

Enter plan mode. Create a detailed implementation plan that:

1. **Lists affected files** — existing files to modify and new files to create, with full paths
2. **Describes each change** — what specifically changes in each file and why
3. **Orders by dependency** — what must be built first for later steps to compile
4. **Identifies shared code** — changes in `ClashUp.Shared` that both client and server need
5. **Considers the architecture rules**:
   - Server-authoritative: game logic lives on the server, client is a dumb display layer
   - No singletons: use DI-registered services (ASP.NET Core DI on server, VContainer on client)
   - Fix vendored packages at the source, not with workarounds
   - Wire protocol changes need MessagePack attributes with correct Key indices
6. **Calls out risks** — things that might break, edge cases, or follow-up work needed

Present the plan to the user and wait for approval before implementing.

## Step 5: Implement

After the user approves the plan (or you adjust based on their feedback):

1. **Re-read every file** you plan to edit before editing it — never edit from memory or from the plan phase's cached view (see feedback-reread-before-edit.md)
2. Implement changes in dependency order (Shared → Server → Client)
3. Build after each logical group of changes to catch errors early:
   - Server: `dotnet build src/Server/ClashUp.Server.sln`
   - For Shared changes that affect Unity: check that the code is netstandard2.1 compatible and C# 9 compatible (no file-scoped namespaces)
4. If the ticket has subitems, work through them one at a time
5. Run tests if they exist for the affected area

## Step 6: Update Ticket Status

After successful implementation, update the ticket status to "Done" on Monday:

```bash
curl -s -X POST https://api.monday.com/v2 \
  -H "Content-Type: application/json" \
  -H "Authorization: $MONDAY_API_TOKEN" \
  -d '{"query": "mutation { change_simple_column_value(board_id: \"5098044909\", item_id: \"TICKET_ID\", column_id: \"project_status\", value: \"Done\") { id } }"}'
```

Then tell the user what was done and suggest running `/reflect` if significant architectural decisions were made.
