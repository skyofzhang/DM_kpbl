using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// 调试用：游戏启动后自动点击开始按钮
/// 挂在场景任意活跃对象上，运行后自动点击BtnStartGame，然后自毁
/// </summary>
public class AutoClickStart : MonoBehaviour
{
    private IEnumerator Start()
    {
        // 等待1秒让UI初始化
        yield return new WaitForSeconds(1f);

        // 尝试点击主菜单开始按钮
        var btnGo = GameObject.Find("Canvas/MainMenuPanel/ButtonGroup/BtnStartGame");
        if (btnGo != null)
        {
            var btn = btnGo.GetComponent<Button>();
            if (btn != null && btn.interactable)
            {
                Debug.Log("[AutoClickStart] Auto-clicking BtnStartGame...");
                btn.onClick.Invoke();
            }
        }
        else
        {
            Debug.LogWarning("[AutoClickStart] BtnStartGame not found");
        }

        // 自毁
        Destroy(this);
    }
}
