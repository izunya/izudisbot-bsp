# izudisbot-bsp — Discord → BeatSaberPlus 채팅 브리지

izudisbot 봇이 보내주는 **디스코드 채널 메시지를 게임 안 BeatSaberPlus 채팅 오버레이로 그대로 띄워주는** BSIPA 플러그인입니다.
디스코드에서 누가 채팅을 치면 Beat Saber 화면의 BSP Chat 창에 실시간으로 나타나고, `!bsr <코드>` 를 치면 ChatRequest 큐에 곡이 자동으로 들어갑니다.

```
[디스코드 채널]  ──(izudisbot 봇)──▶  [WebSocket]  ──▶  izudisbot-bsp  ──▶  BeatSaberPlus Chat / ChatRequest
```

---

## 무슨 일을 하나요?

- 디스코드 채널의 메시지를 게임 내 **BeatSaberPlus_Chat** 오버레이에 표시
- 메시지에 포함된 **이모지**도 함께 표시
- `!bsr <맵코드>` 입력 시 **BeatSaberPlus_ChatRequest** 큐에 자동 추가
- 연결이 끊기면 **자동 재접속**

> 현재는 **단방향(디스코드 → 게임)** 입니다. 게임 채팅을 디스코드로 보내는 기능은 아직 없습니다.

---

## 작동에 필요한 것

| 필요한 것 | 설명 |
|---|---|
| **Beat Saber 1.40.8** | 이 버전 기준으로 빌드됨 |
| **BSIPA 4.3.0 이상** | 모드 로더 |
| **BeatSaberPlus** | `BeatSaberPlus_Chat` (필수), 곡 신청 쓰려면 `BeatSaberPlus_ChatRequest` 도 필요 |
| **izudisbot 봇 + 브리지 토큰** | 봇이 디스코드 메시지를 WebSocket 으로 보내줘야 함. [대시보드](https://izudisbot.izunya.dev/dashboard/me/bsp-bridge)에서 `bsp_xxx` 토큰 발급 |

> BSIPA / BeatSaberPlus 가 안 깔려 있으면 이 플러그인은 동작하지 않습니다. 모드 매니저(BSManager 등)로 의존성을 먼저 설치하세요.

---

## 설치 방법

1. [**Releases**](../../releases) 에서 최신 `izudisbot-bsp-x.y.z.zip` 을 받습니다.
2. zip 을 **Beat Saber 설치 폴더 루트**에 압축을 풉니다 → `izudisbot-bsp.dll` 이 자동으로 `Plugins\` 안으로 들어갑니다.
   예: `...\Beat Saber\Plugins\izudisbot-bsp.dll`
3. 게임을 한 번 실행했다 종료합니다 → 설정 파일이 자동 생성됩니다.

---

## 설정

게임을 한 번 켰다 끄면 **`UserData\izudisbot-bsp.json`** 파일이 생성됩니다. 게임을 **종료한 상태**에서 메모장으로 열어 수정하세요.

```json
{
    "Url": "wss://bsp.izunya.dev/bsp",
    "Token": "bsp_xxxxxxxxxxxxxxxxxxxxx",
    "AutoReconnect": true,
    "ReconnectIntervalSec": 10
}
```

| 항목 | 설명 |
|---|---|
| **Url** | 봇 WebSocket 주소. 기본값: `wss://bsp.izunya.dev/bsp` (로컬 테스트 시 `ws://localhost:3411/bsp`) |
| **Token** | [대시보드](https://izudisbot.izunya.dev/dashboard/me/bsp-bridge)에서 발급받은 `bsp_xxx` 토큰 |
| **AutoReconnect** | 끊겼을 때 자동 재접속 여부 |
| **ReconnectIntervalSec** | 재접속 시도 간격(초) |

저장 후 게임을 다시 실행하면 적용됩니다.

---

## 사용 방법

1. [봇 대시보드](https://izudisbot.izunya.dev/dashboard/me/bsp-bridge)에서 **토큰 발급** + 게임으로 전달할 **디스코드 채널 선택**
2. 위 설정 파일에 `Url` 과 `Token` 입력 후 게임 실행
3. 등록한 디스코드 채널에 누가 메시지를 보내면 → 게임 내 BSP Chat 오버레이에 표시됩니다
4. 디스코드에서 `!bsr <맵코드>` 입력 → 곡 신청 큐에 자동 추가됩니다

### 잘 연결됐는지 확인

게임 로그(`Logs\_latest.log`)에서 아래 메시지가 보이면 정상입니다:

```
[izudisbot-bsp] DiscordChatService registered → wss://bsp.izunya.dev/bsp
[izudisbot-bsp] Connected: wss://bsp.izunya.dev/bsp
```

---

## 문제 해결

| 증상 | 원인 / 해결 |
|---|---|
| `Discord: URL/Token 미설정` | `UserData\izudisbot-bsp.json` 의 Url/Token 입력 후 재시작 |
| `disconnected (1008)` | 토큰이 무효이거나 만료/회수됨 → 새 토큰 발급 |
| `disconnected (1006)` | URL 이 잘못됐거나 봇 서버가 꺼져 있음 |
| 게임에 채팅이 안 보임 | BeatSaberPlus 의 **Chat** 모듈이 켜져 있는지 확인 |
| `!bsr` 가 안 먹힘 | BeatSaberPlus 의 **ChatRequest** 모듈도 켜져 있어야 함 |
| 플러그인이 아예 안 뜸 | BSIPA / BeatSaberPlus 설치 여부, 게임 버전(1.40.8) 확인 |

---

## 개발자용 (직접 빌드)

- Windows + .NET SDK 8 + .NET Framework 4.7.2 + Beat Saber(BSIPA·BeatSaberPlus 포함) 설치 환경 필요
- `Directory.Build.props` 의 `BeatSaberDir` 를 본인 설치 경로로 맞추거나 환경변수 `BeatSaberDir` 설정

```cmd
dotnet build -c Release
:: 빌드 후 Plugins 폴더로 자동 복사
dotnet build -c Release /p:CopyToPlugins=true
```

GitHub 에서 `v*` 형식의 태그(예: `v0.1.0`)를 push 하면 GitHub Actions 가 자동으로 빌드 → zip 생성 → Releases 업로드까지 처리합니다. (러너 빌드에는 게임 참조 DLL 을 담은 `BS_REFS_B64` 시크릿이 필요합니다.)