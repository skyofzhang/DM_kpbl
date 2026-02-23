using UnityEngine;
using UnityEngine.UI;

public class ClickStart
{
    public static string Execute()
    {
        var go = GameObject.Find("Canvas/MainMenuPanel/ButtonGroup/BtnStartGame");
        if (go == null) return "BtnStartGame not found";
        var btn = go.GetComponent<Button>();
        if (btn == null) return "No Button component";
        if (!btn.interactable) return "Button not interactable";
        btn.onClick.Invoke();
        return "Clicked BtnStartGame OK";
    }
}
