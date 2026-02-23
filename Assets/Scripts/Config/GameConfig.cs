using UnityEngine;

namespace CapybaraDuel.Config
{
    /// <summary>
    /// 游戏配置 ScriptableObject
    /// </summary>
    [CreateAssetMenu(fileName = "GameConfig", menuName = "CapybaraDuel/GameConfig")]
    public class GameConfig : ScriptableObject
    {
        [Header("Server")]
        public string serverUrl = "wss://kpbl.jiuqian2025.com";
        public string httpUrl = "https://kpbl.jiuqian2025.com";
        public float heartbeatInterval = 30f;
        public float reconnectDelay = 3f;
        public int maxReconnectAttempts = 5;

        [Header("Room")]
        [Tooltip("房间ID，对应抖音直播间ID。留空则使用 'default'（测试用）")]
        public string roomId = "";

        [Header("Game")]
        public float gameDuration = 1800f; // 30分钟
        public float countdownWarningTime = 10f;

        [Header("Camps")]
        public string leftCampName = "香橙温泉";
        public Color leftCampColor = new Color(1f, 0.55f, 0f); // #FF8C00
        public string rightCampName = "柚子温泉";
        public Color rightCampColor = new Color(0.68f, 1f, 0.18f); // #ADFF2F

        [Header("Orange")]
        public float orangeMoveSmooth = 5f;
        public float orangeRotationSpeed = 100f;
        public bool enableOrangeRotation = true;

        [Header("Performance")]
        public int targetFrameRate = 60;
        public int maxCapybaraUnits = 100;
        [Range(0, 2)] public int effectQuality = 2;
    }
}
