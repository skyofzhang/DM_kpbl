using UnityEngine;
using UnityEditor;

public class ToggleGameUIPanel
{
    public static string Execute()
    {
        var panel = GameObject.Find("Canvas")?.transform.Find("GameUIPanel");
        if (panel == null)
        {
            // 尝试查找非激活的
            var canvas = GameObject.Find("Canvas");
            if (canvas != null)
            {
                for (int i = 0; i < canvas.transform.childCount; i++)
                {
                    var child = canvas.transform.GetChild(i);
                    if (child.name == "GameUIPanel")
                    {
                        panel = child;
                        break;
                    }
                }
            }
        }

        if (panel == null) return "ERROR: GameUIPanel not found";

        bool newState = !panel.gameObject.activeSelf;
        panel.gameObject.SetActive(newState);
        return $"GameUIPanel active: {newState}";
    }
}
