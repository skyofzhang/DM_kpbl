using UnityEngine;
using UnityEditor;

public class GetSiblingOrder
{
    public static void Execute()
    {
        var panel = GameObject.Find("Canvas/GameUIPanel");
        if (panel == null) { Debug.LogError("GameUIPanel not found"); return; }

        var rt = panel.transform;
        Debug.Log($"[SiblingOrder] GameUIPanel has {rt.childCount} direct children:");
        for (int i = 0; i < rt.childCount; i++)
        {
            var child = rt.GetChild(i);
            Debug.Log($"[SiblingOrder]   [{i}] {child.name} (active={child.gameObject.activeSelf})");
        }
    }
}
