#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

namespace CapybaraDuel.Editor
{
    /// <summary>
    /// 更新 BottomBar GM面板 — 清理旧按钮，创建新的GM登录/模拟按钮
    /// 菜单: CapybaraDuel > Update GM Panel
    /// </summary>
    public static class GMPanelUpdater
    {
        [MenuItem("CapybaraDuel/Update GM Panel (Safe)")]
        public static void UpdateGMPanel()
        {
            // 查找 BottomBar
            var allCanvas = Object.FindObjectsOfType<Canvas>(true);
            Transform bottomBar = null;
            foreach (var c in allCanvas)
            {
                if (c.gameObject.name == "GameUIPanel")
                {
                    bottomBar = FindChildRecursive(c.transform, "BottomBar");
                    break;
                }
            }

            if (bottomBar == null)
            {
                EditorUtility.DisplayDialog("Error", "找不到 BottomBar!", "OK");
                return;
            }

            // === 清理旧按钮 ===
            string[] oldButtonNames = { "BtnConnect", "BtnStart", "BtnSimulate", "BtnReset", "BtnGMLeft", "BtnGMRight" };
            foreach (var name in oldButtonNames)
            {
                var child = bottomBar.Find(name);
                if (child != null)
                {
                    Undo.DestroyObjectImmediate(child.gameObject);
                    Debug.Log($"[GMPanel] 删除旧按钮: {name}");
                }
            }

            // === 设置 BottomBar 基础属性 ===
            var barRect = bottomBar.GetComponent<RectTransform>();
            Undo.RecordObject(barRect, "Update BottomBar layout");
            barRect.anchorMin = new Vector2(0, 0);
            barRect.anchorMax = new Vector2(1, 0);
            barRect.pivot = new Vector2(0.5f, 0);
            barRect.anchoredPosition = Vector2.zero;
            barRect.sizeDelta = new Vector2(0, 80);

            // 确保有背景
            var barBg = bottomBar.GetComponent<Image>();
            if (barBg == null) barBg = bottomBar.gameObject.AddComponent<Image>();
            Undo.RecordObject(barBg, "Set BottomBar bg");
            barBg.color = new Color(0.1f, 0.1f, 0.1f, 0.85f);
            barBg.raycastTarget = true;

            // 确保有 HorizontalLayoutGroup
            var hlg = bottomBar.GetComponent<HorizontalLayoutGroup>();
            if (hlg == null) hlg = Undo.AddComponent<HorizontalLayoutGroup>(bottomBar.gameObject);
            Undo.RecordObject(hlg, "Set layout");
            hlg.spacing = 15;
            hlg.padding = new RectOffset(20, 20, 10, 10);
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;

            // === 创建 GM登录按钮 ===
            var gmLoginBtn = CreateButton(bottomBar, "BtnGMLogin", "GM登录", new Color(0.2f, 0.7f, 0.3f));

            // === 创建 模拟按钮 ===
            var simBtn = CreateButton(bottomBar, "BtnSimulate", "模拟", new Color(0.9f, 0.6f, 0.1f));

            // === 创建状态文字 ===
            var statusGo = new GameObject("StatusText");
            Undo.RegisterCreatedObjectUndo(statusGo, "Create StatusText");
            statusGo.transform.SetParent(bottomBar, false);
            var statusRect = statusGo.AddComponent<RectTransform>();
            var statusTmp = statusGo.AddComponent<TextMeshProUGUI>();
            statusTmp.text = "GM工具就绪";
            statusTmp.fontSize = 20;
            statusTmp.color = Color.white;
            statusTmp.alignment = TextAlignmentOptions.Center;
            statusTmp.enableWordWrapping = false;

            // === 绑定 GameControlUI 组件引用 ===
            var controlUI = bottomBar.GetComponent<CapybaraDuel.UI.GameControlUI>();
            if (controlUI != null)
            {
                var so = new SerializedObject(controlUI);
                so.FindProperty("gmLoginButton").objectReferenceValue = gmLoginBtn.GetComponent<Button>();
                so.FindProperty("simulateButton").objectReferenceValue = simBtn.GetComponent<Button>();
                so.FindProperty("statusText").objectReferenceValue = statusTmp;
                so.ApplyModifiedProperties();
                Debug.Log("[GMPanel] ✅ GameControlUI 引用已绑定");
            }
            else
            {
                Debug.LogWarning("[GMPanel] ⚠️ BottomBar 上没有 GameControlUI 组件");
            }

            EditorUtility.SetDirty(bottomBar.gameObject);

            Debug.Log("[GMPanel] ✅ GM面板更新完成");
            EditorUtility.DisplayDialog("GM Panel Updated",
                "GM面板已更新:\n• GM登录按钮\n• 模拟按钮\n• 状态文字\n\n请检查场景并保存。",
                "OK");
        }

        static GameObject CreateButton(Transform parent, string name, string label, Color bgColor)
        {
            var btnGo = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(btnGo, $"Create {name}");
            btnGo.transform.SetParent(parent, false);

            var btnRect = btnGo.AddComponent<RectTransform>();
            btnRect.sizeDelta = new Vector2(150, 60);

            var btnImg = btnGo.AddComponent<Image>();
            btnImg.color = bgColor;

            var btn = btnGo.AddComponent<Button>();
            btn.targetGraphic = btnImg;

            // 文字子物体
            var textGo = new GameObject("Text");
            textGo.transform.SetParent(btnGo.transform, false);
            var textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 24;
            tmp.fontStyle = TMPro.FontStyles.Bold;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableWordWrapping = false;

            return btnGo;
        }

        static Transform FindChildRecursive(Transform parent, string name)
        {
            if (parent.name == name) return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                var result = FindChildRecursive(parent.GetChild(i), name);
                if (result != null) return result;
            }
            return null;
        }
    }
}
#endif
