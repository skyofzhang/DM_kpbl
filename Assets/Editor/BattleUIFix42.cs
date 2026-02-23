using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

/// <summary>
/// #42 战斗UI反馈5项修复
/// 1. 连接OrangeIcon跟随引用（posIndicatorRect, progressBarContainerRect）
/// 2. BtnEnd/BtnSettings位置下移到横幅高度
/// 3. 卡片间距→0 + 卡片背景图
/// 4. progress_bar_bg蒙版覆盖进度条
/// 5. StickerPanelUI组件挂载+引用连接
/// </summary>
public class BattleUIFix42 : MonoBehaviour
{
    [MenuItem("Tools/Battle UI/Fix42 - Feedback 5 Items")]
    public static void Execute()
    {
        int fixCount = 0;

        // ============ Fix1: 连接OrangeIcon跟随引用 ============
        fixCount += Fix1_ConnectOrangeIconRefs();

        // ============ Fix2: 按钮位置下移到横幅高度 ============
        fixCount += Fix2_ButtonPositions();

        // ============ Fix3: 卡片间距+背景图 ============
        fixCount += Fix3_CardSpacingAndBg();

        // ============ Fix4: progress_bar_bg蒙版 ============
        fixCount += Fix4_ProgressBarOverlay();

        // ============ Fix5: StickerPanelUI ============
        fixCount += Fix5_StickerPanel();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"=== BattleUIFix42 完成: {fixCount} 项修复 ===");
    }

    static int Fix1_ConnectOrangeIconRefs()
    {
        // 找到TopBarUI组件（通过类名匹配避免命名空间问题）
        var topBar = GameObject.Find("Canvas/GameUIPanel/TopBar");
        if (topBar == null) { Debug.LogWarning("[Fix1] TopBar not found"); return 0; }

        MonoBehaviour topBarUI = null;
        foreach (var mb in topBar.GetComponents<MonoBehaviour>())
        {
            if (mb.GetType().Name == "TopBarUI")
            {
                topBarUI = mb;
                break;
            }
        }
        if (topBarUI == null) { Debug.LogWarning("[Fix1] TopBarUI component not found"); return 0; }

        var so = new SerializedObject(topBarUI);

        // 连接 posIndicatorRect
        var posIndicator = GameObject.Find("Canvas/GameUIPanel/TopBar/TopBarBg/PosIndicator");
        if (posIndicator != null)
        {
            var prop = so.FindProperty("posIndicatorRect");
            if (prop != null)
            {
                prop.objectReferenceValue = posIndicator.GetComponent<RectTransform>();
                Debug.Log("[Fix1] posIndicatorRect → PosIndicator.RectTransform");
            }
        }

        // 连接 progressBarContainerRect
        var progressBarContainer = GameObject.Find("Canvas/GameUIPanel/TopBar/TopBarBg/ProgressBarContainer");
        if (progressBarContainer != null)
        {
            var prop = so.FindProperty("progressBarContainerRect");
            if (prop != null)
            {
                prop.objectReferenceValue = progressBarContainer.GetComponent<RectTransform>();
                Debug.Log("[Fix1] progressBarContainerRect → ProgressBarContainer.RectTransform");
            }
        }

        so.ApplyModifiedProperties();
        return 1;
    }

    static int Fix2_ButtonPositions()
    {
        int count = 0;

        // BtnEnd: y=-15 → y=-50
        var btnEnd = GameObject.Find("Canvas/GameUIPanel/BtnEnd");
        if (btnEnd != null)
        {
            var rt = btnEnd.GetComponent<RectTransform>();
            Undo.RecordObject(rt, "Fix2 BtnEnd pos");
            rt.anchoredPosition = new Vector2(15f, -50f);
            Debug.Log("[Fix2] BtnEnd: y=-15 → y=-50");
            count++;
        }

        // BtnSettings: y=-15 → y=-50
        var btnSettings = GameObject.Find("Canvas/GameUIPanel/BtnSettings");
        if (btnSettings != null)
        {
            var rt = btnSettings.GetComponent<RectTransform>();
            Undo.RecordObject(rt, "Fix2 BtnSettings pos");
            rt.anchoredPosition = new Vector2(-15f, -50f);
            Debug.Log("[Fix2] BtnSettings: y=-15 → y=-50");
            count++;
        }

        return count > 0 ? 1 : 0;
    }

    static int Fix3_CardSpacingAndBg()
    {
        int count = 0;
        string bgSpritePath = "Assets/Art/BattleUI/player_rank_item_bg.png";
        var bgSprite = AssetDatabase.LoadAssetAtPath<Sprite>(bgSpritePath);

        string[] listPaths = {
            "Canvas/GameUIPanel/LeftPlayerList",
            "Canvas/GameUIPanel/RightPlayerList"
        };

        foreach (var path in listPaths)
        {
            var listGo = GameObject.Find(path);
            if (listGo == null) continue;

            // 间距 → 0
            var hlg = listGo.GetComponent<HorizontalLayoutGroup>();
            if (hlg != null)
            {
                Undo.RecordObject(hlg, "Fix3 spacing");
                hlg.spacing = 0f;
                hlg.padding = new RectOffset(0, 0, 0, 0);
                Debug.Log($"[Fix3] {listGo.name}: spacing→0, padding→0");
                count++;
            }

            // 给每张卡片添加背景图
            if (bgSprite != null)
            {
                for (int i = 0; i < 3; i++)
                {
                    var cardGo = listGo.transform.Find($"PlayerRow_{i}");
                    if (cardGo == null) continue;

                    // 检查是否已有Image组件
                    var img = cardGo.GetComponent<Image>();
                    if (img == null)
                    {
                        img = cardGo.gameObject.AddComponent<Image>();
                    }
                    Undo.RecordObject(img, "Fix3 card bg");
                    img.sprite = bgSprite;
                    img.type = Image.Type.Sliced;
                    img.preserveAspect = false;
                    img.raycastTarget = false;
                    img.color = Color.white;
                    Debug.Log($"[Fix3] {cardGo.name}: 添加背景图 player_rank_item_bg.png");
                }
            }
            else
            {
                Debug.LogWarning($"[Fix3] 未找到背景sprite: {bgSpritePath}");
            }
        }

        return count > 0 ? 1 : 0;
    }

    static int Fix4_ProgressBarOverlay()
    {
        var container = GameObject.Find("Canvas/GameUIPanel/TopBar/TopBarBg/ProgressBarContainer");
        if (container == null) { Debug.LogWarning("[Fix4] ProgressBarContainer not found"); return 0; }

        // 检查是否已存在BarOverlay
        var existing = container.transform.Find("BarOverlay");
        if (existing != null)
        {
            Debug.Log("[Fix4] BarOverlay already exists, skipping");
            return 0;
        }

        // 加载sprite
        string spritePath = "Assets/Art/BattleUI/progress_bar_bg.png";
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        if (sprite == null)
        {
            Debug.LogWarning($"[Fix4] 未找到sprite: {spritePath}");
            return 0;
        }

        // 创建 BarOverlay
        var overlayGo = new GameObject("BarOverlay");
        overlayGo.transform.SetParent(container.transform, false);

        // RectTransform: 铺满容器
        var rt = overlayGo.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // Image: preserveAspect不拉伸
        var img = overlayGo.AddComponent<Image>();
        img.sprite = sprite;
        img.type = Image.Type.Simple;
        img.preserveAspect = true;
        img.raycastTarget = false;
        img.color = Color.white;

        // sibling index 设为最高（渲染在最上层）
        overlayGo.transform.SetAsLastSibling();

        // Undo支持
        Undo.RegisterCreatedObjectUndo(overlayGo, "Fix4 BarOverlay");

        Debug.Log("[Fix4] BarOverlay created with progress_bar_bg.png, sibling=last");
        return 1;
    }

    static int Fix5_StickerPanel()
    {
        var giftPanel = GameObject.Find("Canvas/GameUIPanel/GiftInfoPanel");
        if (giftPanel == null) { Debug.LogWarning("[Fix5] GiftInfoPanel not found"); return 0; }

        // 检查是否已有StickerPanelUI组件
        MonoBehaviour existingComp = null;
        foreach (var mb in giftPanel.GetComponents<MonoBehaviour>())
        {
            if (mb.GetType().Name == "StickerPanelUI")
            {
                existingComp = mb;
                break;
            }
        }

        if (existingComp == null)
        {
            // 通过类型名查找并添加组件
            var scriptType = System.Type.GetType("CapybaraDuel.UI.StickerPanelUI, Assembly-CSharp");
            if (scriptType == null)
            {
                // 备用查找方式
                foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    scriptType = asm.GetType("CapybaraDuel.UI.StickerPanelUI");
                    if (scriptType != null) break;
                }
            }

            if (scriptType != null)
            {
                existingComp = (MonoBehaviour)giftPanel.AddComponent(scriptType);
                Undo.RegisterCreatedObjectUndo(existingComp, "Fix5 StickerPanelUI");
                Debug.Log("[Fix5] StickerPanelUI added to GiftInfoPanel");
            }
            else
            {
                Debug.LogWarning("[Fix5] StickerPanelUI type not found. Make sure StickerPanelUI.cs compiled.");
                return 0;
            }
        }

        // 连接引用
        var so = new SerializedObject(existingComp);

        var btnSticker = GameObject.Find("Canvas/GameUIPanel/BtnSticker");
        if (btnSticker != null)
        {
            var prop = so.FindProperty("btnSticker");
            if (prop != null)
            {
                prop.objectReferenceValue = btnSticker.GetComponent<Button>();
                Debug.Log("[Fix5] btnSticker connected");
            }
        }

        var giftPanelProp = so.FindProperty("giftInfoPanel");
        if (giftPanelProp != null)
        {
            giftPanelProp.objectReferenceValue = giftPanel;
            Debug.Log("[Fix5] giftInfoPanel connected");
        }

        so.ApplyModifiedProperties();
        return 1;
    }
}
