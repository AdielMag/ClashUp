---
name: feedback-client-authority
description: Never add client-side fallbacks for server-authoritative state transitions — user rejected this hard
metadata: 
  node_type: memory
  type: feedback
  originSessionId: f5ff7e9b-f92b-4575-a305-2a0c38999722
---

Never synthesize server-authoritative events on the client as a fallback.

**Why:** User explicitly corrected this when I added a `RunCountdownAsync` fallback that would call `OnMatchEnded` locally if the server signal didn't arrive within 3 seconds. The client is deliberately dumb — it only renders what the server tells it. Adding client-side synthesis breaks this contract and masks real server bugs instead of fixing them.

**How to apply:** When a client is "stuck" because it missed a server broadcast, fix the SERVER to reliably deliver the message (replay on reconnect, early DB notification, etc.). Never add a timeout-based fallback on the client that guesses at server state. This applies to: match end, match start, score updates, phase changes — anything the server owns.

See [[patterns]] for the Dumb Client Principle. See [[debugging]] for the match-end fix sequence.
