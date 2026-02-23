using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

/// <summary>
/// 编辑器测试脚本：自动进入Play模式、点击开始按钮、等待后检查报错
/// 通过 EditorApplication.update 在运行时轮询
/// </summary>
[InitializeOnLoad]
public class TestNotificationUI
{
    private static bool _testing = false;
    private static float _startTime;
    private static int _phase = 0; // 0=等待play, 1=等待UI, 2=点击按钮, 3=等待测试, 4=报告
    private static double _phaseStartTime;

    public static void Execute()
    {
        // 第一步：检查编译错误
        if (EditorUtility.scriptCompilationFailed)
        {
            Debug.LogError("[TestNotif] ❌ 有编译错误！无法测试");
            return;
        }
        Debug.Log("[TestNotif] ✅ 无编译错误");

        // 清除Console
        var logEntries = System.Type.GetType("UnityEditor.LogEntries, UnityEditor");
        if (logEntries != null)
        {
            var clearMethod = logEntries.GetMethod("Clear", BindingFlags.Static | BindingFlags.Public);
            if (clearMethod != null) clearMethod.Invoke(null, null);
        }

        // 进入Play模式
        _testing = true;
        _phase = 0;
        _phaseStartTime = EditorApplication.timeSinceStartup;
        EditorApplication.update += OnUpdate;
        EditorApplication.isPlaying = true;
        Debug.Log("[TestNotif] 进入Play模式...");
    }

    static void OnUpdate()
    {
        if (!_testing) { EditorApplication.update -= OnUpdate; return; }

        double elapsed = EditorApplication.timeSinceStartup - _phaseStartTime;

        switch (_phase)
        {
            case 0: // 等待Play模式就绪
                if (EditorApplication.isPlaying && elapsed > 2.0)
                {
                    _phase = 1;
                    _phaseStartTime = EditorApplication.timeSinceStartup;
                    Debug.Log("[TestNotif] Play模式已就绪，等待UI加载...");
                }
                break;

            case 1: // 等待UI加载
                if (elapsed > 1.5)
                {
                    _phase = 2;
                    _phaseStartTime = EditorApplication.timeSinceStartup;
                    // 点击开始按钮
                    var btnGo = GameObject.Find("Canvas/MainMenuPanel/ButtonGroup/BtnStartGame");
                    if (btnGo != null)
                    {
                        var btn = btnGo.GetComponent<Button>();
                        if (btn != null && btn.interactable)
                        {
                            btn.onClick.Invoke();
                            Debug.Log("[TestNotif] ✅ 已点击BtnStartGame，等待玩家加入...");
                        }
                        else
                        {
                            Debug.LogWarning("[TestNotif] BtnStartGame找到但不可点击");
                        }
                    }
                    else
                    {
                        Debug.LogWarning("[TestNotif] 未找到BtnStartGame，可能已在战斗中");
                        // 直接进入等待阶段
                    }
                }
                break;

            case 2: // 等待15秒让模拟玩家加入
                if (elapsed > 15.0)
                {
                    _phase = 3;
                    _phaseStartTime = EditorApplication.timeSinceStartup;
                    Debug.Log("[TestNotif] 15秒已过，开始检查结果...");

                    // 检查PlayerJoinNotificationUI是否存在
                    var joinUI = Object.FindObjectOfType<CapybaraDuel.UI.PlayerJoinNotificationUI>();
                    if (joinUI != null)
                        Debug.Log($"[TestNotif] ✅ PlayerJoinNotificationUI 已挂载，active={joinUI.gameObject.activeInHierarchy}");
                    else
                        Debug.LogError("[TestNotif] ❌ PlayerJoinNotificationUI 未找到！");

                    // 检查UpgradeNotificationUI是否存在
                    var upgradeUI = Object.FindObjectOfType<CapybaraDuel.UI.UpgradeNotificationUI>();
                    if (upgradeUI != null)
                        Debug.Log($"[TestNotif] ✅ UpgradeNotificationUI 已挂载，active={upgradeUI.gameObject.activeInHierarchy}");
                    else
                        Debug.LogError("[TestNotif] ❌ UpgradeNotificationUI 未找到！");

                    // 检查CampSystem事件
                    var campSys = Object.FindObjectOfType<CapybaraDuel.Systems.CampSystem>();
                    if (campSys != null)
                        Debug.Log($"[TestNotif] ✅ CampSystem存在, Left={campSys.LeftCount}, Right={campSys.RightCount}");
                    else
                        Debug.LogError("[TestNotif] ❌ CampSystem 未找到！");

                    // 检查NetworkManager
                    var netMgr = Object.FindObjectOfType<CapybaraDuel.Core.NetworkManager>();
                    if (netMgr != null)
                        Debug.Log($"[TestNotif] NetworkManager存在, connected={netMgr.IsConnected}");
                    else
                        Debug.LogWarning("[TestNotif] NetworkManager 未找到");

                    Debug.Log("[TestNotif] === 测试完成 === 请检查上方日志是否有NullReferenceException");
                }
                break;

            case 3: // 完成，停止
                if (elapsed > 2.0)
                {
                    _testing = false;
                    EditorApplication.update -= OnUpdate;
                    EditorApplication.isPlaying = false;
                    Debug.Log("[TestNotif] 测试结束，已停止Play模式");
                }
                break;
        }
    }
}
