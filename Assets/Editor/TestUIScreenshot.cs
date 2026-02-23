using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using System.IO;
using System.Reflection;

/// <summary>
/// 自动化UI测试：进入Play → 点开始 → 等待 → 点公告 → 截图 → 点设置 → 截图 → 退出
/// </summary>
[InitializeOnLoad]
public class TestUIScreenshot
{
    private static bool _testing = false;
    private static int _phase = 0;
    private static double _phaseStartTime;
    private static string _screenshotDir = @"C:\Users\Administrator\Desktop\反馈\auto_test";

    public static void Execute()
    {
        if (EditorUtility.scriptCompilationFailed)
        {
            Debug.LogError("[UITest] 有编译错误，无法测试");
            return;
        }

        if (!Directory.Exists(_screenshotDir))
            Directory.CreateDirectory(_screenshotDir);

        // 清除Console
        var logEntries = System.Type.GetType("UnityEditor.LogEntries, UnityEditor");
        if (logEntries != null)
        {
            var clearMethod = logEntries.GetMethod("Clear", BindingFlags.Static | BindingFlags.Public);
            if (clearMethod != null) clearMethod.Invoke(null, null);
        }

        _testing = true;
        _phase = 0;
        _phaseStartTime = EditorApplication.timeSinceStartup;
        EditorApplication.update += OnUpdate;
        EditorApplication.isPlaying = true;
        Debug.Log("[UITest] 开始自动化UI测试...");
    }

    static void OnUpdate()
    {
        if (!_testing) { EditorApplication.update -= OnUpdate; return; }
        double elapsed = EditorApplication.timeSinceStartup - _phaseStartTime;

        switch (_phase)
        {
            case 0: // 等待Play就绪
                if (EditorApplication.isPlaying && elapsed > 2.5)
                {
                    NextPhase();
                    // 点击开始游戏
                    var btn = GameObject.Find("Canvas/MainMenuPanel/ButtonGroup/BtnStartGame");
                    if (btn != null)
                    {
                        var b = btn.GetComponent<Button>();
                        if (b != null && b.interactable) { b.onClick.Invoke(); Debug.Log("[UITest] ✅ 已点击开始游戏"); }
                    }
                    else Debug.LogWarning("[UITest] 未找到开始按钮");
                }
                break;

            case 1: // 等待20秒让玩家加入
                if (elapsed > 20.0)
                {
                    NextPhase();
                    Debug.Log("[UITest] 20秒到，截取战斗画面...");
                    TakeScreenshot("01_battle");
                }
                break;

            case 2: // 等1秒后打开公告
                if (elapsed > 1.5)
                {
                    NextPhase();
                    var announcementUI = Object.FindObjectOfType<CapybaraDuel.UI.AnnouncementPanelUI>();
                    if (announcementUI != null)
                    {
                        announcementUI.Open();
                        Debug.Log("[UITest] ✅ 已打开公告面板");
                    }
                    else Debug.LogWarning("[UITest] AnnouncementPanelUI未找到");
                }
                break;

            case 3: // 等1秒后截图公告
                if (elapsed > 1.5)
                {
                    NextPhase();
                    TakeScreenshot("02_announcement");
                    Debug.Log("[UITest] 截取公告面板");
                    // 关闭公告
                    var announcementUI = Object.FindObjectOfType<CapybaraDuel.UI.AnnouncementPanelUI>();
                    if (announcementUI != null) announcementUI.Close();
                }
                break;

            case 4: // 等1秒后打开设置
                if (elapsed > 1.5)
                {
                    NextPhase();
                    var settingsUI = Object.FindObjectOfType<CapybaraDuel.UI.SettingsPanelUI>();
                    if (settingsUI != null)
                    {
                        settingsUI.Open();
                        Debug.Log("[UITest] ✅ 已打开设置面板");
                    }
                    else Debug.LogWarning("[UITest] SettingsPanelUI未找到");
                }
                break;

            case 5: // 等1秒后截图设置
                if (elapsed > 1.5)
                {
                    NextPhase();
                    TakeScreenshot("03_settings");
                    Debug.Log("[UITest] 截取设置面板");
                }
                break;

            case 6: // 检查错误并结束
                if (elapsed > 2.0)
                {
                    // 检查组件状态
                    var joinUI = Object.FindObjectOfType<CapybaraDuel.UI.PlayerJoinNotificationUI>();
                    var upgradeUI = Object.FindObjectOfType<CapybaraDuel.UI.UpgradeNotificationUI>();
                    var campSys = Object.FindObjectOfType<CapybaraDuel.Systems.CampSystem>();

                    Debug.Log($"[UITest] PlayerJoinNotificationUI: {(joinUI != null ? "✅存在" : "❌缺失")}");
                    Debug.Log($"[UITest] UpgradeNotificationUI: {(upgradeUI != null ? "✅存在" : "❌缺失")}");
                    if (campSys != null)
                        Debug.Log($"[UITest] CampSystem: Left={campSys.LeftCount}, Right={campSys.RightCount}");

                    Debug.Log($"[UITest] === 测试完成 === 截图保存在: {_screenshotDir}");
                    _testing = false;
                    EditorApplication.update -= OnUpdate;
                    EditorApplication.isPlaying = false;
                }
                break;
        }
    }

    static void NextPhase()
    {
        _phase++;
        _phaseStartTime = EditorApplication.timeSinceStartup;
    }

    static void TakeScreenshot(string name)
    {
        string path = Path.Combine(_screenshotDir, $"{name}_{System.DateTime.Now:HHmmss}.png");
        ScreenCapture.CaptureScreenshot(path);
        Debug.Log($"[UITest] 截图已保存: {path}");
    }
}
