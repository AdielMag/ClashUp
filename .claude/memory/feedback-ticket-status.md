---
name: feedback-ticket-status
description: "Don't mark Monday tickets as Done without user confirmation that it's working"
metadata: 
  node_type: memory
  type: feedback
  originSessionId: 485d39a2-50ac-4889-a5bf-f8959d430705
---

Never set a Monday.com ticket status to "Done" automatically after implementation. Wait for the user to explicitly confirm the feature is working before updating the ticket status.

**Why:** The user wants to verify things work before closing tickets. Marking Done prematurely misrepresents actual status.

**How to apply:** After implementing a ticket, tell the user what was done and ask them to test. Only update the ticket status when they say it's working or explicitly ask to mark it Done.
