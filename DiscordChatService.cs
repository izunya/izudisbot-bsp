using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using CP_SDK.Chat;
using CP_SDK.Chat.Interfaces;
using CP_SDK.Chat.Services;
using IPALogger = IPA.Logging.Logger;
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

        public DiscordChatService(Config config, IPALogger log)
        {
            _config = config;
            _log = log;
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
        // IChatService — web UI
        //   BSP 웹 설정 페이지에 폼을 띄워, 게임 재시작 없이 실시간으로
        //   Url/Token 등을 수정 → 저장 즉시 재접속.
        // ================================================================

        public string WebPageHTMLForm()
        {
            return
                "<div class='form-group'>" +
                "  <label>WebSocket URL</label>" +
                "  <input type='text' class='form-control' name='Url' value='" + Escape(_config.Url) + "' placeholder='wss://bsp.izunya.dev/bsp'>" +
                "</div>" +
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
            // 봇 측에서 인바운드 send 처리 추가되면 여기서 _ws.Send(...) 호출.
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
    }
}