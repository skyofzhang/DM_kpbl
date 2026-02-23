using UnityEngine;
using UnityEditor;
using System.Threading.Tasks;

public class AutoStartBattle
{
    /// <summary>
    /// Play模式下：自动GM登录 → 等连接 → 开启模拟 → 显示战斗UI
    /// 必须在Play模式下调用
    /// </summary>
    public static async Task<string> Execute()
    {
        if (!EditorApplication.isPlaying)
            return "ERROR: 必须在Play模式下调用";

        // 1. 找到 GameManager 并触发连接
        var gm = Object.FindObjectOfType<CapybaraDuel.Core.GameManager>();
        if (gm == null) return "ERROR: GameManager not found";

        gm.ConnectToServer();

        // 2. 等待连接完成
        var net = CapybaraDuel.Core.NetworkManager.Instance;
        float timeout = 5f;
        while (timeout > 0 && (net == null || !net.IsConnected))
        {
            await Task.Delay(500);
            timeout -= 0.5f;
            net = CapybaraDuel.Core.NetworkManager.Instance;
        }

        if (net == null || !net.IsConnected)
            return "ERROR: 连接超时";

        // 3. 开启模拟弹幕
        gm.RequestToggleSim(true);

        // 4. 显示战斗UI
        var uiMgr = CapybaraDuel.UI.UIManager.Instance;
        if (uiMgr != null)
        {
            uiMgr.ShowGameUI();
        }

        await Task.Delay(1000);

        return "OK: GM已连接 + 模拟已开启 + 战斗UI已显示";
    }
}
