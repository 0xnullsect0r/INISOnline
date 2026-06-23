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
{ "v": 1, "type": "PlayCard", "seq": 42, "payload": { } }
```
`v` = protocol version, `seq` = client sequence for ack/idempotency.

## Client → host (intents)
| type | payload |
|------|---------|
| `DraftPick` | `{ cardId }` |
| `PlayCard` | `{ cardId, targets: { territory?, player?, ... } }` |
| `Pass` | `{}` |
| `TakePretender` | `{}` |
| `ClashManeuver` | `{ kind, target }` |
| `ChooseTarget` | `{ choiceId, value }` |
| `Resign` | `{}` |
| `Chat` | `{ text }` |
| `DebugCommand` | `{ command, args }` — cheat menu; applied authoritatively (see below) |

## Host → client
| type | payload |
|------|---------|
| `StateSync` | full **redacted** state for the recipient (opponents' hands hidden) |
| `Diff` | minimal delta since last state |
| `Event` | animatable fact: `CardPlayed`, `ClashHit`, `BuildingPlaced`, `DeedGained`, … |
| `TurnPrompt` | `{ playerId, legalMoves: [...] }` |
| `Error` | `{ code, message }` |

## Redaction
The host computes a **per-player view**: a player sees their own hand and all public
state; opponents' hand contents are hidden (counts only). Hidden information never
leaves the host in online play — this is the anti-cheat boundary.

## Debug / cheat commands
`DebugCommand` (unlocked client-side by the code `INIS`) is applied **authoritatively**
by the host through `Inis.Core/Debug`: it mutates canonical state (e.g. grant/edit a
card in the requester's hand) and the host broadcasts the resulting `Diff`/`Event` to
**all** clients, so the change is recognized everywhere. It is server-logged (audit)
but not blocked, including in real online games (per product decision).

## Reconnection & determinism
Reconnect → host replays `StateSync` + pending `TurnPrompt`. The engine is seeded and
deterministic; the host keeps an ordered intent log enabling full replay from the seed.
