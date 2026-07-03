# INIS network protocol

One protocol serves all networked modes (online via INISServer, LAN via an embedded
host in the client). Transport: **WebSocket**, **JSON** messages. The host is always
**authoritative**: clients send *intents*, the host validates with `Inis.Core` and
broadcasts *state*.

## Connection
- Online: `wss://inis.aricummings.com/ws/game/{gameId}?access_token=<JWT>`.
- LAN: `ws://<host-ip>:<port>/ws/game/{gameId>` (discovery via UDP broadcast).
- On connect the host sends `StateSync` (the player's redacted view) + a `TurnPrompt`.

## Envelope
```json
{ "v": 2, "type": "PlayCard", "seq": 42, "payload": { } }
```
`v` = protocol version, `seq` = client sequence for ack/idempotency.

### Versioning
The current version is **2** (v1 → v2 added the reaction/Triskel windows, the Seasons
of Inis subsystems, and their appended enum members — a v1 peer would fail to parse the
new names). Hosts **reject** any envelope whose `v` differs from theirs with an
`Error { code: "version_mismatch" }`; clients surface a mismatch the same way. Enum
members and state properties are only ever appended, so a future v3 host can keep
reading v2 saves.

## Client → host (intents)
| type | payload |
|------|---------|
| `Intent` | a full engine `Move` (the canonical encoding; `TurnPrompt.legalMoves` entries are echoed back through this) |
| `DraftPick` | `{ cardId }` |
| `PlayCard` | `{ cardId, territoryId?, fromTerritoryId?, toTerritoryId?, targetPlayerId?, targetColor?, amount?, cardIds? }` |
| `Pass` | `{}` |
| `TakePretender` | `{}` |
| `Resign` | `{}` |
| `Chat` | `{ text }` |
| `DebugCommand` | `{ command, args }` — cheat menu; applied authoritatively (see below) |

Clash maneuvers, attack responses, reaction plays (`PlayReaction` / `PassReaction`) and
the Seasons `SummerMove` all travel as full `Move`s under `Intent`.

## Host → client
| type | payload |
|------|---------|
| `StateSync` | full **redacted** state for the recipient (opponents' hands hidden) |
| `Event` | animatable fact: `CardPlayed`, `ReactionPlayed`, `CardCancelled`, `ClashPrevented`, `BuildingPlaced`, `DeedGained`, `Festival`, … |
| `TurnPrompt` | `{ playerId, legalMoves: [...] }` |
| `Error` | `{ code, message }` |
| `Chat` | `{ fromPlayerId, text }` |

## Pending decisions
`state.pending.kind` tells the client what decision the prompt is for: `Draft`,
`SeasonTurn`, `ClashShelter`, `ClashManeuver`, `AttackResponse`, `Reaction`, `GameOver`.
For `Reaction`, `pending.trigger` carries the window's trigger name (e.g.
`ActionCardPlayed`, `ClashStarted`, `SacredFestival`) and `pending.cardId` the card that
opened it, for labeling.

## Redaction
The host computes a **per-player view**: a player sees their own hand and all public
state; opponents' hand contents are hidden (counts only). The staged Assembly deck
(only present while a Cathbad's Word window is open) is masked like the draw decks.
Hidden information never leaves the host in online play — this is the anti-cheat
boundary.

**Known information leak (by design):** being *prompted* in a reaction window reveals
that the prompted player holds at least one matching Triskel card — the standard
digital-adaptation compromise, since windows only open when an eligible holder exists.

## Debug / cheat commands
`DebugCommand` (unlocked client-side by the code `INIS`) is applied **authoritatively**
by the host through `Inis.Core/Debug`: it mutates canonical state (e.g. grant/edit a
card in the requester's hand) and the host broadcasts the resulting state/`Event` to
**all** clients, so the change is recognized everywhere. It is server-logged (audit)
but not blocked, including in real online games (per product decision).

## Reconnection & determinism
Reconnect → host replays `StateSync` + pending `TurnPrompt`. The engine is seeded and
deterministic; the host keeps an ordered intent log enabling full replay from the seed.
Reaction windows persist on `state.reactionStack`, so a reload mid-window resumes at
the same prompt.
