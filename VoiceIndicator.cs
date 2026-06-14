using System;
using BeatSaberMarkupLanguage.FloatingScreen;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IzudisbotBSP
{
    /// <summary>
    /// 디스코드 음성채널 인원수를 표시하는 별도 floating UI.
    /// BSP_Chat 의 기존 사람 아이콘 + 시청자 수 표시는 그대로 두고,
    /// 그 옆/위에 디스코드 색 점 + 숫자 형태로 추가 표시.
    ///
    /// BSML 의 FloatingScreen 으로 만들어 게임 메뉴 / 게임플레이 모두에서 노출되며,
    /// DontDestroyOnLoad 로 씬 전환 시에도 유지. VR 컨트롤러로 잡고 위치 이동 가능.
    ///
    /// ⚠ 스레드 주의: Push() 는 WebSocket 수신 스레드에서 호출되므로 Unity 객체를
    /// 직접 만들면 "can only be called from the main thread" 예외가 난다.
    /// 따라서 Push() 는 값만 저장하고, 실제 FloatingScreen 생성/텍스트 갱신은
    /// Unity 메인 스레드의 Update() 에서 수행한다.
    /// </summary>
    public class VoiceIndicator : MonoBehaviour
    {
        private static VoiceIndicator _instance;

        private FloatingScreen _screen;
        private TextMeshProUGUI _text;

        // Push() 는 임의 스레드에서 호출됨 → lock 으로 보호, Update() 가 메인 스레드에서 소비.
        private static readonly object StateLock = new object();
        private static int _pendingCount;
        private static string _pendingChannel;
        private static bool _dirty;

        // 위치/크기 — 사용자가 원하는 정확한 anchor 는 게임에서 잡고 조정 필요.
        // 1차 디폴트: 정면 약간 왼쪽 위 (Chat 화면이 보통 정면 왼쪽이라 그 위쪽).
        private static readonly Vector2 ScreenSize = new Vector2(22f, 12f);
        private static readonly Vector3 ScreenPos = new Vector3(-2.4f, 3.2f, 2.0f);
        private static readonly Quaternion ScreenRot = Quaternion.Euler(0, -30, 0);

        /// <summary>메인 스레드(Plugin.OnEnable)에서 호출 — 영속 GameObject 생성.</summary>
        public static void Init()
        {
            if (_instance != null) return;
            try
            {
                var go = new GameObject("izudisbot-VoiceIndicator");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<VoiceIndicator>();
            }
            catch (Exception err)
            {
                Plugin.Log?.Warn("VoiceIndicator init 실패: " + err.Message);
            }
        }

        /// <summary>어떤 스레드에서든 안전. 값만 저장하고 Update() 가 메인 스레드에서 반영.</summary>
        public static void Push(int count, string voiceChannelName)
        {
            if (count < 0) count = 0;
            lock (StateLock)
            {
                _pendingCount = count;
                _pendingChannel = voiceChannelName;
                _dirty = true;
            }
        }

        /// <summary>메인 스레드(Plugin.OnDisable)에서 호출 — UI 제거.</summary>
        public static void Shutdown()
        {
            var inst = _instance;
            _instance = null;
            if (inst == null) return;
            try { if (inst.gameObject != null) Destroy(inst.gameObject); }
            catch { }
        }

        private void Update()
        {
            int count;
            string channel;
            lock (StateLock)
            {
                if (!_dirty) return;
                _dirty = false;
                count = _pendingCount;
                channel = _pendingChannel;
            }

            EnsureCreated();
            if (_text == null) return;

            // Discord blurple #5865F2 색 점 + 숫자
            if (count > 0 && !string.IsNullOrEmpty(channel))
                _text.text = "<color=#5865F2>●</color> " + count;
            else
                _text.text = "<color=#5865F2>●</color> <color=#888>—</color>";
        }

        private void EnsureCreated()
        {
            if (_screen != null) return;
            try
            {
                _screen = FloatingScreen.CreateFloatingScreen(ScreenSize, false, ScreenPos, ScreenRot);
                DontDestroyOnLoad(_screen.gameObject);

                // 반투명 배경
                var bg = _screen.gameObject.GetComponentInChildren<Image>();
                if (bg != null) bg.color = new Color(0f, 0f, 0f, 0.5f);

                var textGo = new GameObject("VoiceCountText");
                textGo.transform.SetParent(_screen.transform, false);
                _text = textGo.AddComponent<TextMeshProUGUI>();
                _text.fontSize = 6.5f;
                _text.alignment = TextAlignmentOptions.Center;
                _text.richText = true;
                _text.text = "<color=#5865F2>●</color> <color=#888>—</color>";
                var rt = _text.rectTransform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }
            catch (Exception err)
            {
                Plugin.Log?.Warn("VoiceIndicator create 실패: " + err.Message);
                _screen = null;
                _text = null;
            }
        }

        private void OnDestroy()
        {
            try { if (_screen != null) Destroy(_screen.gameObject); }
            catch { }
            _screen = null;
            _text = null;
        }
    }
}
