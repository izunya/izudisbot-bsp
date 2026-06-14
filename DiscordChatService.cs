using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using CP_SDK.Chat;
using CP_SDK.Chat.Interfaces;
using CP_SDK.Chat.Services;
using IPALogger = IPA.Logging.Logger;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using WebSocketSharp;

namespace IzudisbotBSP
{
    /// <summary>
    /// Discord → BeatSaberPlus 채팅 브리지.
    ///
    /// 흐름:
    ///   1) Start() 호출 시 봇 WS 서버에 접속 (Config.Url + ?token=Config.Token)
    ///   2) 봇이 푸시하는 JSON 메시지 파싱 → IChatMessage 생성
    ///   3) m_OnTextMessageReceivedCallbacks.InvokeAll → ChatServiceMultiplexer 가
    ///      이 이벤트를 모든 구독자(BSP_Chat UI, BSP_ChatRequest 등) 에 전달
    ///
    /// 봇 측 페이로드 (utils/bspBridge.js 와 1:1):
    ///   { "type": "message",
    ///     "channel": { "id", "name", "guildId", "guildName" },
    ///     "user":    { "id", "name", "color", "avatar", "bot" },
    ///     "content": "!bsr abc",
    ///     "timestamp": "ISO8601" }
    /// </summary>
    public class DiscordChatService : ChatServiceBase, IChatService
    {
        public string DisplayName => "Discord";
        public Color AccentColor => new Color(0.345f, 0.396f, 0.949f);  // Discord blurple

        private readonly Config _config;
        private readonly IPALogger _log;
        private readonly object _lock = new object();
        private readonly Dictionary<string, DiscordChatChannel> _channels = new Dictionary<string, DiscordChatChannel>();

        private WebSocket _ws;
        private bool _running;
        private bool _connected;
        private Timer _reconnectTimer;

        // ---- 웹 UI 용 상태/로그/채널 통계 ----
        private readonly object _logLock = new object();
        private readonly LinkedList<LogEntry> _recentLog = new LinkedList<LogEntry>();
        private readonly Dictionary<string, ChannelStat> _channelStats = new Dictionary<string, ChannelStat>();
        private DateTime? _lastMessageUtc;
        private const int MaxLog = 200;

        // ---- !bsr → 디스코드 알림 추적 ----
        private static readonly Regex BsrRegex = new Regex(@"^!bsr\s+([0-9a-fA-F]{1,8})\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private readonly object _pendingLock = new object();
        private readonly Dictionary<string, PendingBsr> _pendingBsr = new Dictionary<string, PendingBsr>(StringComparer.OrdinalIgnoreCase);
        private Timer _pendingGcTimer;

        private class PendingBsr
        {
            public string Code;
            public string ChannelId;
            public string UserName;
            public DateTime ExpiresUtc;
        }

        public DiscordChatService(Config config, IPALogger log)
        {
            _config = config;
            _log = log;
        }

        // 웹 UI 로 넘기는 직렬화용 DTO (public 필드 → Newtonsoft 가 그대로 직렬화)
        public class ChannelInfo
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public long Count { get; set; }
            public bool Enabled { get; set; }
        }

        public class LogEntry
        {
            public string Time { get; set; }
            public string Channel { get; set; }
            public string User { get; set; }
            public string Content { get; set; }
            public bool Forwarded { get; set; }
        }

        private class ChannelStat
        {
            public string Id;
            public string Name;
            public long Count;
        }

        public ReadOnlyCollection<(IChatService, IChatChannel)> Channels
        {
            get
            {
                lock (_lock)
                {
                    return _channels.Values
                        .Select(c => ((IChatService)this, (IChatChannel)c))
                        .ToList()
                        .AsReadOnly();
                }
            }
        }

        // ================================================================
        // IChatService — lifecycle
        // ================================================================

        public void Start()
        {
            _running = true;
            Connect();
        }

        public void Stop()
        {
            _running = false;
            DisposeSocket();
        }

        public bool IsConnectedAndLive() => _connected;
        public string PrimaryChannelName() => "Discord";
        public void RecacheEmotes() { }

        // ================================================================
        // 로컬 웹 UI 지원 — 상태 조회 / 로그 / 채널·필터 제어
        // ================================================================

        public bool Connected => _connected;
        public string CurrentUrl => _config.Url;
        public bool TokenSet => !string.IsNullOrEmpty(_config.Token);

        public DateTime? LastMessageUtc
        {
            get { lock (_logLock) { return _lastMessageUtc; } }
        }

        /// <summary>채널 목록 + 누적 수신 수 + 음소거 여부 (수신 많은 순).</summary>
        public List<ChannelInfo> GetChannels()
        {
            lock (_lock)
            {
                var muted = _config.DisabledChannels ?? new List<string>();
                return _channelStats.Values
                    .Select(s => new ChannelInfo
                    {
                        Id = s.Id,
                        Name = s.Name,
                        Count = s.Count,
                        Enabled = !muted.Contains(s.Id)
                    })
                    .OrderByDescending(c => c.Count)
                    .ToList();
            }
        }

        /// <summary>최근 수신 메시지 (신규순).</summary>
        public List<LogEntry> GetRecentLog(int max = 100)
        {
            lock (_logLock)
            {
                return _recentLog.Take(Math.Max(1, max)).ToList();
            }
        }

        public void ClearLog()
        {
            lock (_logLock) { _recentLog.Clear(); }
        }

        /// <summary>채널 음소거/해제 후 저장.</summary>
        public void SetChannelEnabled(string channelId, bool enabled)
        {
            if (string.IsNullOrEmpty(channelId)) return;
            if (_config.DisabledChannels == null) _config.DisabledChannels = new List<string>();
            if (enabled) _config.DisabledChannels.RemoveAll(x => x == channelId);
            else if (!_config.DisabledChannels.Contains(channelId)) _config.DisabledChannels.Add(channelId);
            Config.Save();
        }

        /// <summary>웹에서 설정 변경 후 호출 — 저장 + 즉시 재접속.</summary>
        public void SaveAndReconnect()
        {
            Config.Save();
            _log?.Info("Config updated via local web → reconnecting: " + _config.Url);
            DisposeSocket();
            Connect();
        }

        // ================================================================
        // IChatService — web UI
        //   BSP 웹 설정 페이지에 폼을 띄워, 게임 재시작 없이 실시간으로
        //   Url/Token 등을 수정 → 저장 즉시 재접속.
        // ================================================================

        public string WebPageHTMLForm()
        {
            return
                "<div class='form-group'>" +
                "  <label>Token</label>" +
                "  <input type='text' class='form-control' name='Token' value='" + Escape(_config.Token) + "' placeholder='bsp_...'>" +
                "</div>" +
                "<div class='form-group'>" +
                "  <label>Reconnect interval (sec)</label>" +
                "  <input type='number' min='1' class='form-control' name='ReconnectIntervalSec' value='" + _config.ReconnectIntervalSec + "'>" +
                "</div>" +
                "<div class='form-check'>" +
                "  <input type='checkbox' class='form-check-input' id='izudisbot-autoreconnect' name='AutoReconnect' " + (_config.AutoReconnect ? "checked" : "") + ">" +
                "  <label class='form-check-label' for='izudisbot-autoreconnect'>Auto reconnect</label>" +
                "</div>";
        }

        public string WebPageHTML() => "";
        public string WebPageJS() => "";
        public string WebPageJSValidate() => "";

        public void WebPageOnPost(Dictionary<string, string> postData)
        {
            if (postData == null) return;

            if (postData.TryGetValue("Url", out var url))
                _config.Url = (url ?? "").Trim();
            if (postData.TryGetValue("Token", out var token))
                _config.Token = (token ?? "").Trim();

            // 체크박스: 값이 false/0/off 가 아니면 켜진 것으로 본다. 키 자체가 없으면 꺼짐.
            _config.AutoReconnect = postData.TryGetValue("AutoReconnect", out var ar)
                && (ar == null || (ar != "false" && ar != "0" && ar != "off"));

            if (postData.TryGetValue("ReconnectIntervalSec", out var ivStr) && int.TryParse(ivStr, out var iv))
                _config.ReconnectIntervalSec = Math.Max(1, iv);

            Config.Save();
            _log?.Info("Config updated via web → reconnecting: " + _config.Url);

            // 재시작 없이 새 Url/Token 으로 즉시 재접속
            DisposeSocket();
            Connect();
        }

        private static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("&", "&amp;")
                    .Replace("\"", "&quot;")
                    .Replace("'", "&#39;")
                    .Replace("<", "&lt;")
                    .Replace(">", "&gt;");
        }

        // ================================================================
        // IChatService — outbound (BSP → Discord) — 향후 확장
        // ================================================================

        public void SendTextMessage(IChatChannel channel, string message)
        {
            // BSP_ChatRequest 등 다른 모듈이 응답을 우리 service 의 채널로 보내면 여기로 들어옴.
            // 직전 !bsr 명령에 대한 응답인지 매칭 → 봇에 bsr_request 이벤트 전달.
            TryMatchBsrResponse(message);
        }

        // ================================================================
        // IChatService — temp channels (디스코드에선 의미 없음 → no-op)
        // ================================================================

        public void JoinTempChannel(string groupIdentifier, string channelName, string prefix, bool canSendMessage) { }
        public void LeaveTempChannel(string channelName) { }
        public bool IsInTempChannel(string channelName) => false;
        public void LeaveAllTempChannel(string groupIdentifier) { }

        // ================================================================
        // WebSocket
        // ================================================================

        private void Connect()
        {
            if (!_running) return;
            if (string.IsNullOrEmpty(_config.Url) || string.IsNullOrEmpty(_config.Token))
            {
                m_OnSystemMessageCallbacks?.InvokeAll(
                    (IChatService)this,
                    "Discord: URL/Token 미설정. UserData\\izudisbot-bsp.json 편집 후 게임 재시작."
                );
                _log?.Warn("Url 또는 Token 비어있음");
                return;
            }

            try
            {
                DisposeSocket();
                // 토큰을 Cookie 헤더로 전송 (URL query 대신 — Cloudflare access log 노출 회피)
                _ws = new WebSocket(_config.Url);
                _ws.SetCookie(new WebSocketSharp.Net.Cookie("bsp_token", _config.Token));
                _ws.OnOpen += (s, e) =>
                {
                    _connected = true;
                    _log?.Info("Connected: " + _config.Url);
                    m_OnSystemMessageCallbacks?.InvokeAll((IChatService)this, "Discord: connected");
                    m_OnLoginCallbacks?.InvokeAll((IChatService)this);
                };
                _ws.OnMessage += (s, e) => HandleMessage(e.Data);
                _ws.OnError += (s, e) => _log?.Warn("WS error: " + e.Message);
                _ws.OnClose += (s, e) =>
                {
                    _connected = false;
                    _log?.Info("Closed: code=" + e.Code + " reason=" + e.Reason);
                    m_OnSystemMessageCallbacks?.InvokeAll(
                        (IChatService)this,
                        "Discord: disconnected (" + e.Code + ")"
                    );
                    ScheduleReconnect();
                };
                _ws.ConnectAsync();
            }
            catch (Exception err)
            {
                _log?.Error("Connect failed: " + err);
                ScheduleReconnect();
            }
        }

        private void ScheduleReconnect()
        {
            if (!_running || !_config.AutoReconnect) return;
            _reconnectTimer?.Dispose();
            var interval = Math.Max(1, _config.ReconnectIntervalSec) * 1000;
            _reconnectTimer = new Timer(_ =>
            {
                try { Connect(); } catch { }
            }, null, interval, Timeout.Infinite);
        }

        private void DisposeSocket()
        {
            try { _ws?.Close(); } catch { }
            _ws = null;
            _connected = false;
            _reconnectTimer?.Dispose();
            _reconnectTimer = null;
        }

        // ================================================================
        // 메시지 파싱
        // ================================================================

        private void HandleMessage(string raw)
        {
            try
            {
                var obj = JObject.Parse(raw);
                var type = obj["type"]?.ToString();

                if (type == "system")
                {
                    var msg = obj["message"]?.ToString() ?? "";
                    m_OnSystemMessageCallbacks?.InvokeAll((IChatService)this, "Discord: " + msg);
                    return;
                }

                if (type == "pong") return;
                if (type == "voice_count")
                {
                    var count = obj["count"]?.ToObject<int?>() ?? 0;
                    var vc = obj["voiceChannel"] as JObject;
                    var vcName = vc?["name"]?.ToString();
                    ApplyVoiceCount(count, vcName);
                    return;
                }
                if (type != "message") return;

                var channelObj = obj["channel"] as JObject;
                var userObj = obj["user"] as JObject;
                if (channelObj == null || userObj == null) return;

                // 봇 메시지는 봇 측에서 이미 필터하지만 한 번 더 방어
                var isBot = userObj["bot"]?.ToObject<bool?>() ?? false;
                if (isBot) return;

                var channel = GetOrCreateChannel(channelObj);
                var user = new DiscordChatUser(
                    id: userObj["id"]?.ToString() ?? "",
                    userName: userObj["name"]?.ToString() ?? "Unknown",
                    color: userObj["color"]?.ToString()
                );
                var content = obj["content"]?.ToString() ?? "";

                // ---- 필터 판단 ----
                var chId = channelObj["id"]?.ToString() ?? "unknown";
                var muted = _config.DisabledChannels?.Contains(chId) ?? false;
                var isCommand = content.StartsWith("!");
                var forward = !muted && (!_config.ForwardOnlyCommands || isCommand);

                // ---- 통계/로그 기록 (필터와 무관하게 전부 — 웹 미리보기용) ----
                RecordIncoming(chId, channel.Name, user.UserName, content, forward);

                if (!forward) return;

                // !bsr 명령 — BSP_ChatRequest 응답을 추적하기 위해 pending 등록
                var bsrMatch = BsrRegex.Match(content);
                if (bsrMatch.Success)
                {
                    TrackPendingBsr(user.UserName, bsrMatch.Groups[1].Value, chId);
                }

                var emotes = ParseEmotes(obj["emotes"] as JArray);
                var message = new DiscordChatMessage(
                    id: Guid.NewGuid().ToString(),
                    text: content,
                    sender: user,
                    channel: channel,
                    emotes: emotes
                );
                m_OnTextMessageReceivedCallbacks?.InvokeAll((IChatService)this, message);
            }
            catch (Exception err)
            {
                _log?.Warn("HandleMessage failed: " + err.Message);
            }
        }

        private static IChatEmote[] ParseEmotes(JArray arr)
        {
            if (arr == null || arr.Count == 0) return new IChatEmote[0];
            var list = new System.Collections.Generic.List<IChatEmote>(arr.Count);
            foreach (var item in arr)
            {
                if (!(item is JObject e)) continue;
                var id = e["id"]?.ToString() ?? "";
                var name = e["name"]?.ToString() ?? "";
                var uri = e["uri"]?.ToString() ?? "";
                if (string.IsNullOrEmpty(uri)) continue;
                var startIndex = e["startIndex"]?.ToObject<int?>() ?? 0;
                var endIndex = e["endIndex"]?.ToObject<int?>() ?? 0;
                var animated = (e["animation"]?.ToString() ?? "none").ToLower() == "gif";
                list.Add(new DiscordChatEmote(id, name, uri, startIndex, endIndex, animated));
            }
            return list.ToArray();
        }

        private void RecordIncoming(string chId, string chName, string user, string content, bool forwarded)
        {
            lock (_lock)
            {
                if (_channelStats.TryGetValue(chId, out var st)) { st.Count++; st.Name = chName; }
                else _channelStats[chId] = new ChannelStat { Id = chId, Name = chName, Count = 1 };
            }
            lock (_logLock)
            {
                _lastMessageUtc = DateTime.UtcNow;
                _recentLog.AddFirst(new LogEntry
                {
                    Time = DateTime.Now.ToString("HH:mm:ss"),
                    Channel = chName,
                    User = user,
                    Content = content,
                    Forwarded = forwarded
                });
                while (_recentLog.Count > MaxLog) _recentLog.RemoveLast();
            }
        }

        private DiscordChatChannel GetOrCreateChannel(JObject obj)
        {
            var id = obj["id"]?.ToString() ?? "unknown";
            lock (_lock)
            {
                if (_channels.TryGetValue(id, out var existing)) return existing;
                var rawName = obj["name"]?.ToString() ?? id;
                var ch = new DiscordChatChannel(id, "#" + rawName);
                _channels[id] = ch;
                m_OnJoinRoomCallbacks?.InvokeAll((IChatService)this, (IChatChannel)ch);
                m_OnRoomStateUpdatedCallbacks?.InvokeAll((IChatService)this, (IChatChannel)ch);
                m_OnLiveStatusUpdatedCallbacks?.InvokeAll((IChatService)this, (IChatChannel)ch, true, 0);
                return ch;
            }
        }

        // ================================================================
        // !bsr 신청 추적 — 신청 직후 BSP_ChatRequest 응답을 매칭해 봇에
        // bsr_request (queued / rejected / queue_closed) 이벤트를 보낸다.
        // 응답이 6초 안에 안 오면 queue_closed 로 간주 (모듈 비활성 / 큐 닫힘).
        // ================================================================

        private void TrackPendingBsr(string userName, string code, string channelId)
        {
            if (string.IsNullOrEmpty(userName)) return;
            lock (_pendingLock)
            {
                _pendingBsr[userName] = new PendingBsr
                {
                    Code = code,
                    ChannelId = channelId,
                    UserName = userName,
                    ExpiresUtc = DateTime.UtcNow.AddSeconds(6),
                };
                EnsurePendingGcTimer();
            }
        }

        private void EnsurePendingGcTimer()
        {
            if (_pendingGcTimer != null) return;
            _pendingGcTimer = new Timer(_ => GcPending(), null, 2000, Timeout.Infinite);
        }

        private void GcPending()
        {
            var now = DateTime.UtcNow;
            List<PendingBsr> expired = null;
            bool hasRemaining;
            lock (_pendingLock)
            {
                foreach (var key in _pendingBsr.Keys.ToList())
                {
                    var p = _pendingBsr[key];
                    if (p.ExpiresUtc <= now)
                    {
                        (expired ?? (expired = new List<PendingBsr>())).Add(p);
                        _pendingBsr.Remove(key);
                    }
                }
                hasRemaining = _pendingBsr.Count > 0;
                _pendingGcTimer?.Dispose();
                _pendingGcTimer = null;
                if (hasRemaining) EnsurePendingGcTimer();
            }
            if (expired == null) return;
            foreach (var p in expired)
            {
                SendBsrEvent(p, "queue_closed", null, null);
            }
        }

        private void TryMatchBsrResponse(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            PendingBsr matched = null;
            lock (_pendingLock)
            {
                foreach (var kv in _pendingBsr)
                {
                    var name = kv.Value.UserName;
                    if (!string.IsNullOrEmpty(name) && message.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        matched = kv.Value;
                        _pendingBsr.Remove(kv.Key);
                        break;
                    }
                }
            }
            if (matched == null) return;

            string status;
            string reason = null;
            string songName = null;
            if (IsClosedResponse(message))
            {
                status = "queue_closed";
            }
            else if (IsQueuedResponse(message))
            {
                status = "queued";
                songName = ExtractSongInfo(message);
            }
            else
            {
                status = "rejected";
                reason = message.Length > 256 ? message.Substring(0, 256) : message;
            }
            SendBsrEvent(matched, status, reason, songName);
        }

        private static bool IsQueuedResponse(string msg)
        {
            return msg.IndexOf("added to queue", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsClosedResponse(string msg)
        {
            return msg.IndexOf("queue is closed", StringComparison.OrdinalIgnoreCase) >= 0
                || msg.IndexOf("queue is currently closed", StringComparison.OrdinalIgnoreCase) >= 0
                || msg.IndexOf("ChatRequest is disabled", StringComparison.OrdinalIgnoreCase) >= 0
                || msg.IndexOf("requests are not", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string ExtractSongInfo(string msg)
        {
            // 응답 형식 예: "@user (key) Song Name by Mapper - Diff (key) was added to queue!"
            var idx = msg.IndexOf("was added to queue", StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return null;
            var prefix = msg.Substring(0, idx).Trim();
            var firstClose = prefix.IndexOf(')');
            var lastOpen = prefix.LastIndexOf('(');
            if (firstClose < 0 || lastOpen < 0 || lastOpen <= firstClose + 1) return null;
            return prefix.Substring(firstClose + 1, lastOpen - firstClose - 1).Trim();
        }

        private void SendBsrEvent(PendingBsr p, string status, string reason, string songName)
        {
            if (_ws == null || !_connected) return;
            var obj = new JObject
            {
                ["type"] = "bsr_request",
                ["code"] = p.Code,
                ["status"] = status,
                ["channelId"] = p.ChannelId,
                ["requestedBy"] = new JObject { ["name"] = p.UserName },
            };
            if (!string.IsNullOrEmpty(songName)) obj["songName"] = songName;
            if (!string.IsNullOrEmpty(reason)) obj["reason"] = reason;
            try { _ws.Send(obj.ToString(Formatting.None)); }
            catch (Exception err) { _log?.Warn("bsr_request send 실패: " + err.Message); }
        }

        // ================================================================
        // 음성채널 인원수 — 봇이 voice_count 페이로드를 보낼 때마다
        // VoiceIndicator (별도 floating UI) 로 전달.
        // BSP_Chat 의 기존 사람 아이콘 + 시청자 수 표시는 건드리지 않음.
        // ================================================================

        private void ApplyVoiceCount(int count, string voiceChannelName)
        {
            // Push 는 값만 저장 → 실제 UI 갱신은 VoiceIndicator.Update (Unity 메인 스레드).
            try { VoiceIndicator.Push(count, voiceChannelName); }
            catch (Exception err) { _log?.Warn("VoiceIndicator update 실패: " + err.Message); }
        }
    }
}
