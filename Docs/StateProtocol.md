# Boss Raid State Protocol

Unity receives one complete JSON state object per WebSocket message.

```json
{
  "version": 1,
  "animationNonce": 0,
  "eventTitle": "BOSS RAID",
  "screen": "standby",
  "currentTeamId": "team-1",
  "difficulty": "normal",
  "bossHp": 1400000,
  "totalScore": 0,
  "prizePool": 0,
  "burgerCount": 0,
  "burgerMissCount": 0,
  "clearCount": 0,
  "failCount": 0,
  "roundIndex": 0,
  "selectedMode": "Aim",
  "selectedMapId": "aim-1",
  "lastResult": "none",
  "resultMessage": "",
  "connectionLabel": "BRIDGE ONLINE",
  "chatStatus": "IRC DISABLED",
  "scoreSourceStatus": "TOURNEY IPC DISABLED",
  "obsStatus": "OBS DISABLED",
  "chatMessages": [
    {
      "time": "15:00:00",
      "sender": "player",
      "message": "hello",
      "kind": "chat"
    }
  ],
  "teams": [],
  "mapPool": [
    {
      "id": "NM1",
      "mode": "NM",
      "title": "Map title",
      "artist": "Artist",
      "mapper": "Mapper",
      "difficultyName": "Difficulty",
      "link": "https://osu.ppy.sh/beatmapsets/1#osu/123456",
      "beatmapId": 123456,
      "isBurger": false,
      "played": false
    }
  ],
  "selectedRoundMapIds": [],
  "difficulties": []
}
```

## Screens

- `standby`
- `burgerReveal`
- `difficultySelect`
- `rouletteMode`
- `rouletteMap`
- `mapReady`
- `inGame`
- `result`

## Difficulties

- `easy`: 1,000,000 HP, 3,000 KRW
- `normal`: 1,400,000 HP, 5,000 KRW
- `hard`: 2,000,000 HP, 15,000 KRW

## HTTP Commands

POST JSON to `/command`.

```json
{ "type": "spin_mode" }
```

Supported command types:

- `reset_event`
- `reload_settings`
- `set_screen`, with `screen`
- `set_current_team`, with `teamId`
- `set_team_name`, with `teamId`, `name`
- `set_difficulty`, with `difficulty`
- `set_team_score`, with `teamId`, `score`
- `set_scores`, with `scores` object keyed by team id
- `assign_burgers`
- `set_burger_maps`, with `mapIds`
- `pick_round_maps`
- `enter_difficulty_select`
- `confirm_difficulty`, with `difficulty`
- `spin_mode`
- `spin_map`
- `complete_mode_roulette`, with `mode`
- `complete_map_roulette`, with `mapId`
- `send_mp_setup`
- `send_mp_command`, with `message` starting with `!mp`
- `ready_map`
- `start_map`
- `finish_map`
- `next_round`
- `load_maps`, with a `maps` array
- `add_chat_message`, with `sender`, `message`
- `clear_chat`

## Result Rules

On `finish_map`:

- Clear when `totalScore >= bossHp`.
- When live player scores are available, `team.score` and `totalScore` are damage values: P1 + P2 + P3 * 1.2. Player rows still keep raw player scores.
- Clear adds the difficulty prize to `prizePool`.
- Clear on a burger map at `hard` difficulty adds 1 to `burgerCount`.
- Clear on a burger map below `hard` adds 1 to `burgerMissCount`.
- Fail adds 1 to `failCount`.
- Fail on a burger map also adds 1 to `burgerMissCount`.
- The selected map is marked `played`.

## Operator Data Entry

- Team names and scores are edited from the bridge page under `Scores`.
- If `API.Json` has `tourneyIpcEnabled=true`, Bridge reads osu! tournament IPC scores from `ipc-scores.txt` and updates team scores automatically.
- If `API.Json` has `obsWebSocketEnabled=true`, Bridge toggles configured OBS spectator sources or per-screen source groups on for `mapReady` and `inGame` screens and off on other screens.
- If `API.Json` has `mpAutoInGameAfterStart=true`, Bridge switches from `mapReady` to `inGame` after it sends `!mp start`, using `mpStartDelaySeconds + mpAutoInGameExtraDelaySeconds`.
- Map data is edited from the bridge page under `Map Pool JSON`.
- Persistent team/map setup is edited in `Setting.Json`.
- `Bridge/map_pool.example.json` shows the expected map object fields.
- The bridge keeps data in memory until the Python process exits.
