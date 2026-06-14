using System;
using System.IO;
using Newtonsoft.Json;

namespace IzudisbotBSP
{
    /// <summary>
    /// 브리지 설정 — UserData/izudisbot-bsp.json 에 보관.
    /// 첫 실행 시 기본값으로 자동 생성. 게임 종료 후 메모장으로 직접 수정.
    /// </summary>
    public class Config
    {
        /// <summary>WebSocket URL. 예: ws://localhost:3411/bsp 또는 wss://your-host/bsp</summary>
        public string Url { get; set; } = "ws://localhost:3411/bsp";

        /// <summary>대시보드에서 발급받은 bsp_xxx 토큰</summary>
        public string Token { get; set; } = "";

        /// <summary>끊겼을 때 자동 재접속</summary>
        public bool AutoReconnect { get; set; } = true;

        /// <summary>재접속 간격 (초)</summary>
        public int ReconnectIntervalSec { get; set; } = 10;

        public static Config Current { get; private set; } = new Config();

        private static string FilePath => Path.Combine(
            Environment.CurrentDirectory, "UserData", "izudisbot-bsp.json"
        );

        public static void Load()
        {
            try
            {
                var dir = Path.GetDirectoryName(FilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                if (File.Exists(FilePath))
                {
                    var json = File.ReadAllText(FilePath);
                    Current = JsonConvert.DeserializeObject<Config>(json) ?? new Config();
                }
                else
                {
                    Save();  // 디폴트 파일 생성 → 사용자가 편집할 수 있게
                }
            }
            catch (Exception err)
            {
                Plugin.Log?.Warn("Config load failed: " + err.Message);
                Current = new Config();
            }
        }

        public static void Save()
        {
            try
            {
                File.WriteAllText(FilePath, JsonConvert.SerializeObject(Current, Formatting.Indented));
            }
            catch (Exception err)
            {
                Plugin.Log?.Warn("Config save failed: " + err.Message);
            }
        }
    }
}
