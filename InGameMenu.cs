using System;
using System.Collections;
using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.MenuButtons;
using HMUI;
using UnityEngine;
using IPALogger = IPA.Logging.Logger;

namespace IzudisbotBSP
{
    /// <summary>
    /// 인게임 좌측 모드(Mods) 탭에 버튼 하나 등록 → 클릭하면 로컬 웹 UI 를 브라우저로 연다.
    ///
    /// BSML 의 MenuButtons 는 메뉴 씬(Zenject)이 로드된 뒤에야 접근 가능하다.
    /// OnEnable 시점(게임 시작)에는 "too early" 예외가 나므로,
    /// 영속 GameObject + 코루틴으로 준비될 때까지 1초 간격 재시도한다.
    /// 실패해도 웹 UI/브리지 기능엔 영향 없음. (인게임 UI 텍스트는 영어 고정)
    /// </summary>
    public static class InGameMenu
    {
        private static MenuButton _button;
        private static GameObject _helperGo;
        private static SettingsFlowCoordinator _flow;
        private static WebServer _web;
        private static IPALogger _log;

        public static void Register(WebServer webServer, IPALogger log)
        {
            _web = webServer;
            _log = log;
            try
            {
                if (_helperGo == null)
                {
                    _helperGo = new GameObject("izudisbot-MenuButtonHelper");
                    UnityEngine.Object.DontDestroyOnLoad(_helperGo);
                    _helperGo.AddComponent<MenuButtonHelper>();
                }
            }
            catch (Exception err)
            {
                _log?.Warn("Menu helper 생성 실패: " + err.Message);
            }
        }

        /// <summary>메뉴가 준비됐으면 등록 성공(true). 아직이면 false → 재시도.</summary>
        internal static bool TryRegisterButton()
        {
            try
            {
                if (_button == null)
                {
                    _button = new MenuButton(
                        "izudisbot",
                        "Open the izudisbot Discord bridge settings",
                        OpenSettings);
                }
                MenuButtons.Instance.RegisterButton(_button);
                _log?.Info("In-game menu button registered.");
                return true;
            }
            catch (Exception)
            {
                return false; // MenuButtons 아직 준비 안 됨 → 재시도
            }
        }

        /// <summary>모드탭 버튼 클릭 → 정면에 설정 FlowCoordinator 표시.</summary>
        private static void OpenSettings()
        {
            try
            {
                if (_flow == null) _flow = BeatSaberUI.CreateFlowCoordinator<SettingsFlowCoordinator>();
                FlowCoordinator main = BeatSaberUI.MainFlowCoordinator;
                main.PresentFlowCoordinator(_flow);
            }
            catch (Exception err)
            {
                _log?.Warn("OpenSettings failed: " + err.Message);
            }
        }

        /// <summary>설정 패널의 'Web Open' 버튼 → 브라우저로 웹 UI 열기.</summary>
        internal static void OpenWeb()
        {
            try { _web?.OpenInBrowser(); }
            catch (Exception err) { _log?.Warn("OpenWeb failed: " + err.Message); }
        }

        public static void Unregister(IPALogger log)
        {
            try { if (_button != null) MenuButtons.Instance.UnregisterButton(_button); }
            catch (Exception err) { log?.Warn("Menu button unregister 실패: " + err.Message); }

            try { if (_helperGo != null) UnityEngine.Object.Destroy(_helperGo); }
            catch { }

            _button = null;
            _helperGo = null;
        }
    }

    /// <summary>MenuButtons 가 준비될 때까지 재시도하는 헬퍼 MonoBehaviour.</summary>
    internal class MenuButtonHelper : MonoBehaviour
    {
        private void Start() { StartCoroutine(Loop()); }

        private IEnumerator Loop()
        {
            for (int i = 0; i < 60; i++)
            {
                if (InGameMenu.TryRegisterButton()) yield break;
                yield return new WaitForSeconds(1f);
            }
        }
    }
}
