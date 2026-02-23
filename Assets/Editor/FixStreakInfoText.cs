using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

public class FixStreakInfoText
{
    public static string Execute()
    {
        // Find StreakInfoText object
        var settlementPanel = GameObject.Find("Canvas/SettlementPanel");
        if (settlementPanel == null) return "ERROR: SettlementPanel not found";

        var streakGo = settlementPanel.transform.Find("StreakInfoText");
        if (streakGo == null) return "ERROR: StreakInfoText not found";

        // Remove old UnityEngine.UI.Text if present
        var oldText = streakGo.GetComponent<Text>();
        if (oldText != null)
        {
            Object.DestroyImmediate(oldText);
        }

        // Add TMP if not present
        var tmp = streakGo.GetComponent<TextMeshProUGUI>();
        if (tmp == null)
        {
            tmp = streakGo.gameObject.AddComponent<TextMeshProUGUI>();
        }

        // Load Chinese font
        var font = Resources.Load<TMP_FontAsset>("Fonts/ChineseFont SDF");
        if (font != null) tmp.font = font;

        tmp.text = "";
        tmp.fontSize = 36;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = new Color(1f, 0.84f, 0f, 1f); // Gold
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.richText = true;

        // Start inactive (code will activate when there's streak data)
        streakGo.gameObject.SetActive(false);

        // Wire to SettlementUI
        var settlementUI = settlementPanel.GetComponent<CapybaraDuel.UI.SettlementUI>();
        if (settlementUI == null) return "ERROR: SettlementUI component not found";

        // Use SerializedObject to set the field
        var so = new SerializedObject(settlementUI);
        var prop = so.FindProperty("streakInfoText");
        if (prop != null)
        {
            prop.objectReferenceValue = tmp;
            so.ApplyModifiedProperties();
        }
        else
        {
            return "ERROR: streakInfoText property not found on SettlementUI";
        }

        EditorUtility.SetDirty(settlementUI);
        EditorUtility.SetDirty(streakGo.gameObject);

        return $"SUCCESS: StreakInfoText converted to TMP and wired to SettlementUI. Font={font?.name ?? "NULL"}";
    }
}
