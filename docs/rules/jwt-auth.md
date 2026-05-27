# JWT Auth

**When this applies:** Anything that mints, signs, validates, transports, or reads JWTs.

## The rule

Two distinct signing keys, used for two distinct purposes. Never mix them.

### End-user key

- Issued by `IAuthService.LoginWithDeviceIdAsync` (phase 1) and future provider logins.
- Carried by clients on every Services call.
- Validated by Services (for unary RPCs) and by GameServer (in `OnConnecting` of `MatchHub`).
- Standard claims: `iss=clashup-services`, `aud=clashup-client`, `sub=playerId`, `exp`, plus refresh-token rotation.

### Inter-tier key

- Used only between Services and GameServer.
- Mints two things:
  - **`MatchToken`** — a per-match JWT handed back to the client in `MatchHandoff`. Carries `matchId`, `gsInstanceId`, `sticky=true`. The client treats it as opaque and presents it to the GS hub on connect.
  - **GS registry tokens** — short-lived auth between GS and Services (`IGameServerRegistry`, `IMatchAdminService`).
- The end user never sees the raw inter-tier key, only the `MatchToken` it produced.
- Rotated independently from the end-user key.

## Normative claim names

Don't invent new claim names without updating this list.

| Claim          | Meaning                                                 | Where it appears                                   |
|----------------|---------------------------------------------------------|----------------------------------------------------|
| `sub`          | Player ID (Mongo `accounts._id`)                        | end-user tokens, `MatchToken`                      |
| `matchId`      | Match this token authorises                             | `MatchToken`                                       |
| `gsInstanceId` | GS instance that hosts the match (sticky reconnect key) | `MatchToken`                                       |
| `sticky`       | `true` if this token may be re-used for reconnect       | `MatchToken`                                       |
| `gsId`         | GS instance identity (for registry tokens)              | GS registry / `IMatchAdminService` inter-tier auth |
| `exp`          | Expiry (match end + grace for `MatchToken`)             | all tokens                                         |

## Validation

All token validators use:

- `ValidateIssuerSigningKey = true`
- `ValidateIssuer = true`, `ValidateAudience = true` (issuer/audience constants live in `ClashUp.Server.Common.Auth`)
- `ValidateLifetime = true`
- `ClockSkew = TimeSpan.FromSeconds(15)`

## Logging

- Never log token contents.
- Log the `sub` claim (player ID) only. For `MatchToken`, you may additionally log `matchId`.
- Never log the device ID for an account — log the `playerId` it resolved to.

## Storage

- Keys live in environment configuration (env vars, vault), never in source.
- Each environment (dev / staging / prod) has its own key material. No shared "test key" leaking into prod.
- Refresh tokens stored as salted hashes in `sessions` (Mongo). See [`mongo-data.md`](mongo-data.md).
