using IPA;
using IPALogger = IPA.Logging.Logger;

namespace IzudisbotBSP
{
    /// <summary>
    /// BSIPA 진입점.
    /// CP_SDK 의 Chat.Service 에 DiscordChatService 를 external service 로 등록 →
    /// BSP Chat / ChatRequest 모듈의 ChatServiceMultiplexer 가 자동으로 픽업.
    ///
    /// 등록 타이밍:
    ///   manifest.json 의 loadBefore 로 BSP Chat 모듈보다 먼저 로드되도록 보장.
    ///   OnEnable 시점에 RegisterExternalService 호출 → 이후 BSP 모듈이 Acquire 할 때 포함됨.
    /// </summary>
    [Plugin(RuntimeOptions.SingleStartInit)]
    public class Plugin
    {
        public static IPALogger Log { get; private set; }
        private DiscordChatService _service;

        [Init]
        public Plugin(IPALogger logger)
        {
            Log = logger;
            Log.Info("izudisbot-bsp loaded");
        }

        [OnEnable]
        public void OnEnable()
        {
            Config.Load();
            _service = new DiscordChatService(Config.Current, Log);
            CP_SDK.Chat.Service.RegisterExternalService(_service);
            _service.Start();
            Log.Info("DiscordChatService registered → " + (Config.Current.Url ?? "(unconfigured)"));
        }

        [OnDisable]
        public void OnDisable()
        {
            _service?.Stop();
            _service = null;
            Log?.Info("izudisbot-bsp disabled");
        }
    }
}
