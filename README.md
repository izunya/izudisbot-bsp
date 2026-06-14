# izudisbot-bsp — Discord → BeatSaberPlus Chat Bridge

> [English](#english) · [한국어](#한국어)

A BSIPA plugin that forwards **Discord channel messages into the in-game BeatSaberPlus chat overlay** (and `!bsr` song requests). When someone types in your Discord channel, it appears live on the Beat Saber BSP Chat overlay; `!bsr <code>` is queued into ChatRequest automatically.

```
[Discord channel] ──(izudisbot bot)──▶ [WebSocket] ──▶ izudisbot-bsp ──▶ BeatSaberPlus Chat / ChatRequest
```

---

## English

### What it does
- Shows Discord channel messages on the **BeatSaberPlus_Chat** overlay (with emotes)
- Queues songs into **BeatSaberPlus_ChatRequest** when a message is `!bsr <code>`
- Auto-reconnects if the connection drops
- **Local web UI** (English/Korean toggle) to configure everything live — no restart
- **In-game button** in the left Mods tab to open the web UI
- Optionally **opens the web UI in your browser every time Beat Saber launches**

### Requirements
| Need | Notes |
|---|---|
| **Beat Saber 1.40.8** | Built against this version |
| **BSIPA 4.3.0+** | Mod loader |
| **BeatSaberPlus** | `Chat` (required); `ChatRequest` for song requests |
| **izudisbot bot token** | Issue a `bsp_xxx` token from the [dashboard](https://izudisbot.izunya.dev/dashboard/me/bsp-bridge) |

### Install
1. Download the latest `izudisbot-bsp-x.y.z.zip` from [**Releases**](../../releases).
2. Extract it into your **Beat Saber install folder root** → `izudisbot-bsp.dll` lands in `Plugins\`.
3. Launch the game once, then quit (creates the config file).

### Configure
There are three ways; the web UI is recommended.

**1. Local web UI (recommended)** — open **http://localhost:9001/** in your browser.
Enter your `Token`, toggle auto-reconnect, manage channels (forward on/off), watch the live message log. The bridge URL is preset (`wss://bsp.izunya.dev/bsp`), so you only need the token. Saving applies instantly (no restart). Top-right button toggles English/Korean.

**2. In-game** — in the main menu's left **Mods** tab, click the **izudisbot** button to open the web UI.

**3. JSON file** — edit `UserData\izudisbot-bsp.json` while the game is closed:
```json
{
    "Url": "wss://bsp.izunya.dev/bsp",
    "Token": "bsp_xxxxxxxxxxxxxxxxxxxxx",
    "AutoReconnect": true,
    "ReconnectIntervalSec": 10,
    "WebUIEnabled": true,
    "WebUIPort": 9001,
    "OpenWebOnLaunch": true,
    "ForwardOnlyCommands": false,
    "DisabledChannels": []
}
```
- **Token** — your `bsp_xxx` token (sent via a cookie header). This is the only thing you normally set.
- **Url** — bridge endpoint, preset to `wss://bsp.izunya.dev/bsp`; advanced/optional
- **WebUIPort** — local web UI port (changing it needs a game restart)
- **OpenWebOnLaunch** — auto-open the web UI in your browser on launch
- **ForwardOnlyCommands** — only forward `!`-commands to the game
- **DisabledChannels** — channel IDs muted from forwarding

### Usage
1. Get a token on the [dashboard](https://izudisbot.izunya.dev/dashboard/me/bsp-bridge) and pick the Discord channel(s) to forward.
2. Enter your `Token` (web UI or JSON). The URL is already preset.
3. Messages in the Discord channel show up on the BSP Chat overlay.
4. `!bsr <map code>` is added to the song-request queue.

Connected correctly when the game log (`Logs\_latest.log`) shows:
```
[izudisbot-bsp] Connected: wss://bsp.izunya.dev/bsp
[izudisbot-bsp] Local web UI: http://localhost:9001/
```

### Troubleshooting
| Symptom | Fix |
|---|---|
| `Discord: URL/Token not set` | Enter Url/Token, then reconnect |
| `disconnected (1008)` | Token invalid/revoked → issue a new one |
| `disconnected (1006)` | Wrong URL or the bot server is down |
| Web UI won't open | Check `WebUIEnabled`, the port, and that the game is running |
| In-game button missing | BSML/Zenject timing — non-fatal; use the web UI instead |
| No chat in game | Make sure the BeatSaberPlus **Chat** module is enabled |
| `!bsr` ignored | The **ChatRequest** module must also be enabled |

### Build it yourself
Needs Windows + .NET SDK 8 + .NET Framework 4.7.2 + Beat Saber (with BSIPA, BeatSaberPlus, BSML). Set `BeatSaberDir` in `Directory.Build.props` (or the env var) to your install.
```cmd
dotnet build -c Release
:: build + copy into the game's Plugins folder
dotnet build -c Release /p:CopyToPlugins=true
```
Pushing a `v*` tag (e.g. `v0.1.1`) triggers GitHub Actions to build, zip, and upload a Release (CI build needs the `BS_REFS_B64` secret containing the game reference DLLs).

---

## 한국어

### 무슨 일을 하나요
- 디스코드 채널 메시지를 **BeatSaberPlus_Chat** 오버레이에 표시 (이모지 포함)
- 메시지가 `!bsr <코드>` 면 **BeatSaberPlus_ChatRequest** 큐에 자동 추가
- 연결이 끊기면 자동 재접속
- **로컬 웹 UI**(영어/한국어 전환)로 모든 설정을 재시작 없이 실시간 변경
- 메인 메뉴 좌측 **Mods 탭의 인게임 버튼**으로 웹 UI 열기
- 옵션으로 **비트세이버 실행 때마다 웹 UI 자동 열기**

### 작동에 필요한 것
| 필요 | 비고 |
|---|---|
| **Beat Saber 1.40.8** | 이 버전 기준 빌드 |
| **BSIPA 4.3.0+** | 모드 로더 |
| **BeatSaberPlus** | `Chat`(필수), 곡 신청은 `ChatRequest` |
| **izudisbot 봇 토큰** | [대시보드](https://izudisbot.izunya.dev/dashboard/me/bsp-bridge)에서 `bsp_xxx` 발급 |

### 설치
1. [**Releases**](../../releases)에서 최신 `izudisbot-bsp-x.y.z.zip` 다운로드.
2. **Beat Saber 설치 폴더 루트**에 압축 해제 → `izudisbot-bsp.dll` 이 `Plugins\` 로 들어감.
3. 게임을 한 번 실행했다 종료 (설정 파일 생성).

### 설정
세 가지 방법이 있으며 웹 UI를 권장합니다.

**1. 로컬 웹 UI (권장)** — 브라우저에서 **http://localhost:9001/** 접속.
`Token` 입력, 자동 재접속 토글, 채널 전달 on/off, 실시간 메시지 로그 확인. 브리지 URL은 `wss://bsp.izunya.dev/bsp` 로 고정이라 토큰만 넣으면 됩니다. 저장 시 재시작 없이 즉시 적용. 우측 상단 버튼으로 영어/한국어 전환.

**2. 인게임** — 메인 메뉴 좌측 **Mods 탭**의 **izudisbot** 버튼 클릭 → 웹 UI 열림.

**3. JSON 파일** — 게임 종료 상태에서 `UserData\izudisbot-bsp.json` 편집:
```json
{
    "Url": "wss://bsp.izunya.dev/bsp",
    "Token": "bsp_xxxxxxxxxxxxxxxxxxxxx",
    "AutoReconnect": true,
    "ReconnectIntervalSec": 10,
    "WebUIEnabled": true,
    "WebUIPort": 9001,
    "OpenWebOnLaunch": true,
    "ForwardOnlyCommands": false,
    "DisabledChannels": []
}
```
- **Token** — `bsp_xxx` 토큰 (쿠키 헤더로 전송). 보통 이것만 설정하면 됩니다.
- **Url** — 브리지 주소, `wss://bsp.izunya.dev/bsp` 로 고정 (고급/선택)
- **WebUIPort** — 로컬 웹 UI 포트 (변경 시 게임 재시작 필요)
- **OpenWebOnLaunch** — 실행 시 웹 UI 자동 열기
- **ForwardOnlyCommands** — `!` 명령어만 게임으로 전달
- **DisabledChannels** — 전달 음소거할 채널 ID 목록

### 사용 방법
1. [대시보드](https://izudisbot.izunya.dev/dashboard/me/bsp-bridge)에서 토큰 발급 + 전달할 디스코드 채널 선택.
2. `Token` 입력 (웹 UI 또는 JSON). URL은 이미 고정돼 있습니다.
3. 디스코드 채널 메시지가 BSP Chat 오버레이에 표시됩니다.
4. `!bsr <맵코드>` 는 곡 신청 큐에 자동 추가됩니다.

게임 로그(`Logs\_latest.log`)에 아래가 보이면 정상:
```
[izudisbot-bsp] Connected: wss://bsp.izunya.dev/bsp
[izudisbot-bsp] Local web UI: http://localhost:9001/
```

### 문제 해결
| 증상 | 해결 |
|---|---|
| `Discord: URL/Token 미설정` | Url/Token 입력 후 재접속 |
| `disconnected (1008)` | 토큰 무효/회수됨 → 새 토큰 발급 |
| `disconnected (1006)` | URL 오류 또는 봇 서버 꺼짐 |
| 웹 UI 안 열림 | `WebUIEnabled`/포트 확인, 게임 실행 중인지 확인 |
| 인게임 버튼 없음 | BSML/Zenject 타이밍 문제 — 치명적 아님, 웹 UI 사용 |
| 게임에 채팅 안 보임 | BeatSaberPlus **Chat** 모듈 켜짐 확인 |
| `!bsr` 안 먹힘 | **ChatRequest** 모듈도 켜져 있어야 함 |

### 직접 빌드
Windows + .NET SDK 8 + .NET Framework 4.7.2 + Beat Saber(BSIPA·BeatSaberPlus·BSML 포함) 필요. `Directory.Build.props` 의 `BeatSaberDir`(또는 환경변수)를 설치 경로로 설정.
```cmd
dotnet build -c Release
:: 빌드 후 게임 Plugins 폴더로 복사
dotnet build -c Release /p:CopyToPlugins=true
```
`v*` 태그(예: `v0.1.1`)를 push 하면 GitHub Actions 가 빌드 → zip → Release 업로드까지 자동 처리합니다 (CI 빌드에는 게임 참조 DLL 을 담은 `BS_REFS_B64` 시크릿 필요).
