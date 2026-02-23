using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace CapybaraDuel.UI
{
    /// <summary>
    /// 贴纸面板控制器
    /// - BtnSticker 点击 → toggle GiftInfoPanel 显示/隐藏
    /// - GiftInfoPanel 上按住拖动 → 改变贴纸位置
    /// 挂载在 GiftInfoPanel 上
    /// </summary>
    public class StickerPanelUI : MonoBehaviour, IDragHandler, IBeginDragHandler
    {
        [Header("References")]
        [SerializeField] private Button btnSticker;
        [SerializeField] private GameObject giftInfoPanel;

        [SerializeField] private Button btnResetPosition;

        private RectTransform _panelRect;
        private Canvas _canvas;
        private bool _isDragging = false;
        private Vector2 _defaultPosition;

        private void Awake()
        {
            if (giftInfoPanel != null)
                _panelRect = giftInfoPanel.GetComponent<RectTransform>();

            // 查找根Canvas用于坐标转换
            _canvas = GetComponentInParent<Canvas>();
            if (_canvas != null)
            {
                // 获取最顶层的Canvas（ScreenSpace-Overlay模式）
                while (_canvas.transform.parent != null)
                {
                    var parentCanvas = _canvas.transform.parent.GetComponentInParent<Canvas>();
                    if (parentCanvas != null)
                        _canvas = parentCanvas;
                    else
                        break;
                }
            }
        }

        private void Start()
        {
            // 记录初始位置用于重置
            if (_panelRect != null)
                _defaultPosition = _panelRect.anchoredPosition;

            if (btnSticker != null)
            {
                btnSticker.onClick.RemoveAllListeners();
                btnSticker.onClick.AddListener(TogglePanel);
            }

            if (btnResetPosition != null)
            {
                btnResetPosition.onClick.RemoveAllListeners();
                btnResetPosition.onClick.AddListener(ResetPosition);
            }

            // 订阅状态变化事件，Running时自动重置位置
            var gm = CapybaraDuel.Core.GameManager.Instance;
            if (gm != null)
                gm.OnStateChanged += HandleStateChanged;
        }

        private void OnDestroy()
        {
            var gm = CapybaraDuel.Core.GameManager.Instance;
            if (gm != null)
                gm.OnStateChanged -= HandleStateChanged;
        }

        private void HandleStateChanged(CapybaraDuel.Core.GameManager.GameState oldState,
            CapybaraDuel.Core.GameManager.GameState newState)
        {
            // 进入Running（新局开始）时重置贴纸位置
            if (newState == CapybaraDuel.Core.GameManager.GameState.Running)
                ResetPosition();
        }

        /// <summary>重置贴纸到默认位置</summary>
        public void ResetPosition()
        {
            if (_panelRect != null)
            {
                _panelRect.anchoredPosition = _defaultPosition;
                Debug.Log("[StickerPanelUI] Position reset to default");
            }
        }

        private void TogglePanel()
        {
            if (giftInfoPanel != null)
            {
                bool isActive = giftInfoPanel.activeSelf;
                giftInfoPanel.SetActive(!isActive);
                Debug.Log($"[StickerPanelUI] GiftInfoPanel {(!isActive ? "显示" : "隐藏")}");
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _isDragging = true;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_panelRect == null) return;

            // 根据Canvas的渲染模式计算正确的偏移
            float scaleFactor = 1f;
            if (_canvas != null)
                scaleFactor = _canvas.scaleFactor;

            _panelRect.anchoredPosition += eventData.delta / scaleFactor;
        }
    }
}
