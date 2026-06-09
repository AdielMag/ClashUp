---
name: monday-api
description: "Monday.com API integration — endpoint, auth, GraphQL patterns, status column gotchas"
metadata: 
  node_type: memory
  type: reference
  originSessionId: 53d337e4-9754-428d-a73c-ce1d6a8bab4d
---

## Monday.com API Access

- **Endpoint**: `https://api.monday.com/v2` (GraphQL POST)
- **Auth**: `Authorization` header with API token (no Bearer prefix)
- **Token storage**: `MONDAY_API_TOKEN` env var in `.claude/settings.local.json` (gitignored)
- **Board**: "ClashUp" board ID `5098044909`, subitem board `5098064337`
- **MVP group**: `new_group29179` ("MVP 1 - No Visuals")

## Key Mutations

- `create_update(item_id, body)` — add update/comment to an item (supports HTML)
- `change_simple_column_value(board_id, item_id, column_id, value)` — set column by label text (status columns only accept existing labels)
- `change_column_value(board_id, item_id, column_id, value)` — set column by JSON value (e.g., `{"index": 0}` for status)

## Status Column Gotcha

**Status columns only accept pre-defined labels.** You cannot use `change_simple_column_value` with a label that doesn't exist in the column's settings. To create a status column with custom labels:

1. Use `create_column` with a `defaults` JSON param that includes `labels` and `labels_colors`
2. Then use `change_column_value` with `{"index": N}` to set values

If you try to set a non-existent label, you get `ColumnValueException` with `missingLabel` error code.

## Column IDs (ClashUp board)

- `project_owner` — People (assignee)
- `project_status` — Status (Working on it / Done / Not Started)
- `timerange` — Timeline
- `color_mm43ec8h` — Category (Server / Client / Global)
- `color_mm451tj1` — Priority (Critical=0 / High=1 / Medium=2 / Low=3)

## Tips

- Use Python scripts (write to file, execute) for batch operations — bash heredocs break with complex JSON/GraphQL escaping
- Monday API uses `ID!` type for board/item IDs — pass as strings in variables
- `items_page` with `query_params` filters by group: `{rules: [{column_id: "group", compare_value: ["group_id"]}]}`
- Column values use `text` for display, `value` for raw JSON — `title` field doesn't exist (use `type` instead)
