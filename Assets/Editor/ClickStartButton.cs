using UnityEngine;
using UnityEngine.UI;

public class ClickStartButton
{
    public static void Execute()
    {
        // 先尝试主菜单的开始按钮
        var mainMenuBtn = GameObject.Find("Canvas/MainMenuPanel/ButtonGroup/BtnStartGame");
        if (mainMenuBtn != null && mainMenuBtn.activeInHierarchy)
        {
            var btn = mainMenuBtn.GetComponent<Button>();
            if (btn != null && btn.interactable)
            {
                btn.onClick.Invoke();
                Debug.Log("[ClickStart] Clicked BtnStartGame on MainMenuPanel");
                return;
            }
        }

        // 再尝试底部栏的开始按钮
        var bottomBtn = GameObject.Find("Canvas/BottomBar/BtnStart");
        if (bottomBtn != null && bottomBtn.activeInHierarchy)
        {
            var btn = bottomBtn.GetComponent<Button>();
            if (btn != null && btn.interactable)
            {
                btn.onClick.Invoke();
                Debug.Log("[ClickStart] Clicked BtnStart on BottomBar");
                return;
            }
        }

        Debug.LogWarning("[ClickStart] No start button found or none are active/interactable");
    }
}
