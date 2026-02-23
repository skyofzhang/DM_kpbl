using UnityEngine;
using UnityEditor;

public class ShowBattleUI
{
    public static string Execute()
    {
        if (!EditorApplication.isPlaying)
            return "ERROR: Must be in Play mode";

        // 直接显示战斗UI
        var uiMgr = Object.FindObjectOfType<CapybaraDuel.UI.UIManager>();
        if (uiMgr == null) return "ERROR: UIManager not found";

        uiMgr.ShowGameUI();
        return "OK: ShowGameUI called";
    }
}
