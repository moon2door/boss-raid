# Boss Raid Overlay

osu! tourney 화면을 대체하기 위한 보스전 방송 오버레이 + 운영자 Bridge 프로젝트입니다.

## 구성

- `Assets/BossRaid/Scripts`: Unity 방송 오버레이 런타임 코드
- `Bridge/bridge.py`: 운영자용 웹 관리자 페이지와 Unity로 상태를 보내는 WebSocket Bridge
- `Setting.Json`: 행사 기본 설정 파일. 맵 링크, 팀 정보, 난이도, 상금 정보를 여기에 입력합니다.
- `Docs/StateProtocol.md`: Unity와 Bridge가 주고받는 상태/명령 구조

## 실행 방법

1. Bridge를 실행합니다.

   빌드 폴더를 사용하는 경우:

   ```text
   Build/BossRaidOverlay/BossRaidBridge.exe
   ```

   이 exe는 콘솔 창으로 켜지며, 시작할 때 `Setting.Json` 로드 경로, 팀, 맵 수, 모드별 맵 수를 출력합니다.

   Unity 프로젝트에서 Python으로 직접 실행하는 경우:

   ```powershell
   python Bridge\bridge.py
   ```

2. 운영자 페이지를 엽니다.

   ```text
   http://127.0.0.1:8765
   ```

3. Unity 2022.3에서 프로젝트를 열고 Play를 누릅니다.

   빌드 폴더를 사용하는 경우:

   ```text
   Build/BossRaidOverlay/BossRaidOverlay.exe
   ```

Unity 오버레이는 Play 시 자동으로 생성됩니다. 별도로 씬에 오브젝트를 배치하지 않아도 됩니다.

## Setting.Json 입력 위치

팀 정보와 맵 정보는 프로젝트 루트의 [Setting.Json](Setting.Json)에 입력합니다.

팀 예시:

```json
{
  "id": "team-1",
  "name": "Team A",
  "color": "#f24033",
  "players": ["player1", "player2"]
}
```

맵 예시:

```json
{
  "id": "aim-1",
  "mode": "Aim",
  "title": "Map Title",
  "artist": "Artist",
  "mapper": "Mapper",
  "difficultyName": "Expert",
  "link": "https://osu.ppy.sh/beatmapsets/000001#osu/000001"
}
```

주요 규칙:

- `maps`에는 최대 24개 맵을 넣습니다.
- 같은 `mode` 값을 가진 맵끼리 룰렛에서 묶입니다.
- `id`는 맵마다 고유해야 합니다.
- `link`에 osu! beatmap 링크를 넣으면 Bridge 관리자 페이지의 맵 목록에서 열 수 있습니다.
- Bridge 실행 중 `Setting.Json`을 수정했다면 관리자 페이지에서 `Reload Setting.Json`을 누르면 다시 불러옵니다.
- `Reset Event`도 `Setting.Json` 기준으로 초기화됩니다.

## 모드 이름 바꾸기

현재 `Aim`, `Speed`, `Tech`, `Stamina`으로 보이는 이유는 [Setting.Json](Setting.Json)의 각 맵에 있는 `mode` 값이 그렇게 들어가 있기 때문입니다.

예를 들어 `Aim` 대신 `Reading`으로 쓰고 싶다면 해당 맵들의 `mode`를 바꾸면 됩니다.

```json
{
  "id": "reading-1",
  "mode": "Reading",
  "title": "Reading Raid #1",
  "artist": "Artist",
  "mapper": "Mapper",
  "difficultyName": "Expert",
  "link": "https://osu.ppy.sh/beatmapsets/000001#osu/000001"
}
```

룰렛은 별도의 모드 목록을 보지 않고, `maps` 안에 들어 있는 `mode` 값들을 자동으로 모아서 표시합니다. 4개 모드가 아니어도 동작하지만, 화면 구성은 현재 4개 모드 기준으로 가장 보기 좋게 맞춰져 있습니다.

## 운영자 페이지에서 입력하는 것

- 현재 팀 선택: `Current Team`
- 난이도 선택: `Difficulty`
- 방송용 난이도 선택 화면 표시: `Difficulty Select`
- 팀 점수 입력: `Scores`
- 임시 맵풀 수정: `Map Pool JSON`

`Map Pool JSON`에서 수정한 내용은 실행 중인 Bridge 메모리에만 반영됩니다. 영구적으로 남길 내용은 `Setting.Json`에 적어두는 쪽이 좋습니다.

## Unity 조작 키

방송 화면에서 빠르게 이동할 때 사용합니다.
Bridge가 실행 중이면 아래 키 입력은 Bridge 상태에도 같이 반영됩니다. Bridge를 끈 상태에서는 Unity 로컬 미리보기로만 동작합니다.

- `Ctrl+1`: 최초 대기화면
- `Ctrl+9`: 24개 맵 중 버거 이모티콘이 붙은 맵 공개 화면
- `Ctrl+2`: 난이도 선택 화면부터 시작
- `Ctrl+3`: 맵 대기 화면과 게임 진행 화면 토글
- `Ctrl+4`: 난이도 선택 화면
- `Space`: 현재 화면의 랜덤 선택 실행

룰렛 동작:

- 버거맵 공개 화면에서 `Space`를 누르면 8개의 버거 후보가 동시에 빠르게 오가다가 각각 랜덤한 타이밍에 멈추며 최종 8개 버거맵으로 잠깁니다.
- 난이도 선택 화면에서는 `1/2/3` 또는 `E/N/H`로 쉬움/보통/어려움을 고르고, `Space`나 `Enter`로 모드 룰렛 화면으로 넘어갑니다.
- 모드 룰렛 화면에서 `Space`를 누르면 여러 모드를 빠르게 오가다가 점점 느려지고 하나가 선택됩니다.
- 모드가 선택되면 3초 대기 후 자동으로 맵 룰렛 화면으로 넘어갑니다.
- 맵 룰렛 화면에서 `Space`를 누르면 같은 방식으로 맵 하나가 선택됩니다.
- 맵이 선택되면 3초 대기 후 자동으로 맵 대기 화면으로 넘어갑니다.

개발 확인용 보조 키:

- `F1`: 대기화면
- `F2`: 버거맵 8개 랜덤 부착
- `F3`: 모드 즉시 선택
- `F4`: 맵 즉시 선택
- `F5`: 맵 대기
- `F6`: 게임 진행
- `F7`: 클리어 결과
- `F8`: 실패 결과
- `F9`: 난이도 선택 화면

## 기본 진행 흐름

1. `Ctrl+1`로 최초 대기화면을 띄웁니다.
2. Unity에서 `Ctrl+9`로 버거맵 공개 화면에 들어간 뒤 `Space`로 버거맵 8개를 랜덤 지정합니다. 관리자 페이지에서는 `Assign Burgers` 버튼으로 즉시 지정할 수 있습니다.
3. 필요하면 관리자 페이지에서 `Pick 8 Maps`로 당일 진행할 8개 맵을 뽑습니다.
4. `Ctrl+2`로 난이도 선택 화면에 들어갑니다.
5. 팀 회의 후 난이도를 고르고 `Space` 또는 `Enter`로 확정합니다.
6. 모드 룰렛 화면으로 넘어가면 `Space`로 모드 룰렛을 돌립니다.
7. 자동으로 맵 룰렛으로 넘어가면 다시 `Space`로 맵을 뽑습니다.
8. 맵 대기 화면에 들어가면 팀 점수를 관리자 페이지에서 입력합니다.
9. `Ctrl+3` 또는 관리자 페이지의 `Start Map`으로 게임 진행 화면으로 전환합니다.
10. 게임 종료 후 관리자 페이지에서 `Finish`를 누르면 클리어/실패, 상금, 버거 카운트가 계산됩니다.

## 기본 수치

- 쉬움: HP 1,000,000 / 클리어 상금 3,000원
- 보통: HP 1,400,000 / 클리어 상금 5,000원
- 어려움: HP 2,000,000 / 클리어 상금 15,000원

이 값은 `Setting.Json`의 `difficulties`에서 바꿀 수 있습니다.

## 연결 정보

- Unity WebSocket 주소: `ws://127.0.0.1:8765/ws`
- 운영자 페이지: `http://127.0.0.1:8765`

현재 점수 입력은 수동 입력 기준입니다. osu! API 연동은 이후 Bridge 쪽에 같은 상태 구조로 추가하면 됩니다.
