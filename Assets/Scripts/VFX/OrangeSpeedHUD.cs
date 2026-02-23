using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CapybaraDuel.Systems;

namespace CapybaraDuel.VFX
{
    /// <summary>
    /// 橘子头顶速度+方向HUD
    /// 场景中预创建的 World Space Canvas，运行时只负责更新文本和位置
    /// 所有UI对象在编辑器中生成，用户可直接在Inspector调整大小位置
    /// </summary>
    public class OrangeSpeedHUD : MonoBehaviour
    {
        [Header("场景引用（编辑器自动生成，可手动调整）")]
        [SerializeField] public GameObject hudRoot;       // OrangeSpeedHUD_Root
        [SerializeField] public TextMeshProUGUI mainText; // 速度文字
        [SerializeField] public Image bgImage;            // 背景底板

        [Header("跟随配置")]
        [SerializeField] private float heightOffset = 0.6f;

        [Header("显示阈值")]
        [SerializeField] private float arrowHideThreshold = 0.02f;
        [SerializeField] private float speedDisplayThreshold = 0.01f;

        [Header("箭头颜色")]
        [SerializeField] private Color arrowRightColor = new Color(1f, 0.45f, 0f, 1f);
        [SerializeField] private Color arrowLeftColor = new Color(0.4f, 0.9f, 0.15f, 1f);
        [SerializeField] private Color speedTextColor = Color.white;
        [SerializeField] private Color bgColor = new Color(0.08f, 0.04f, 0f, 0.6f);

        [Header("数字滚动")]
        [SerializeField] private float rollSpeed = 8f;
        [SerializeField] private float rollThreshold = 0.03f;

        private Transform _cameraTransform;
        private OrangeController _orangeCtrl;
        private float _displayedSpeed = 0f;
        private float _targetSpeed = 0f;
        private Vector3 _initialOffset; // 记录场景中编辑的初始偏移量

        private void Start()
        {
            _orangeCtrl = GetComponent<OrangeController>();
            _cameraTransform = Camera.main?.transform;

            // 记录场景中你手动设置的HUD相对橘子的偏移量
            if (hudRoot != null)
                _initialOffset = hudRoot.transform.position - transform.position;

            // 运行时字体 fallback 设置
            if (mainText != null)
            {
                var cjkFont = Resources.Load<TMP_FontAsset>("Fonts/ChineseFont SDF");
                if (mainText.font != null && cjkFont != null)
                {
                    if (mainText.font.fallbackFontAssetTable == null)
                        mainText.font.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset>();
                    if (!mainText.font.fallbackFontAssetTable.Contains(cjkFont))
                        mainText.font.fallbackFontAssetTable.Add(cjkFont);
                }
            }
        }

        private void LateUpdate()
        {
            if (_orangeCtrl == null || hudRoot == null || mainText == null) return;

            // === 同步位置（跟随橘子，保持场景中设定的偏移量）===
            hudRoot.transform.position = transform.position + _initialOffset;

            // === Billboard 面向摄像机 ===
            if (_cameraTransform == null) _cameraTransform = Camera.main?.transform;
            if (_cameraTransform != null)
                hudRoot.transform.rotation = _cameraTransform.rotation;

            // === 速度数据 ===
            float velocity = _orangeCtrl.GetVelocity();
            float absSpeed = Mathf.Abs(velocity);
            _targetSpeed = absSpeed;

            // === 数字滚动动画 ===
            float diff = _targetSpeed - _displayedSpeed;
            if (Mathf.Abs(diff) < rollThreshold)
                _displayedSpeed = _targetSpeed;
            else
            {
                _displayedSpeed += diff * rollSpeed * Time.deltaTime;
                if (Mathf.Sign(diff) != Mathf.Sign(_targetSpeed - _displayedSpeed))
                    _displayedSpeed = _targetSpeed;
            }

            // === 构建显示文本 ===
            bool moving = absSpeed > arrowHideThreshold;
            bool movingRight = velocity > 0;
            string speedStr = _displayedSpeed < speedDisplayThreshold ? "0.00" : _displayedSpeed.ToString("F2");

            if (!moving)
            {
                mainText.text = $"{speedStr} <color=#FFE6A0><size=70%>米/秒</size></color>";
                mainText.color = new Color(1f, 1f, 1f, 0.5f);
                mainText.transform.localScale = Vector3.one;
                if (bgImage != null)
                    bgImage.color = new Color(bgColor.r, bgColor.g, bgColor.b, bgColor.a * 0.5f);
            }
            else
            {
                string arrows;
                if (movingRight)
                {
                    string c = ColorUtility.ToHtmlStringRGB(arrowRightColor);
                    arrows = absSpeed > 0.8f ? $" <color=#{c}>>>></color>"
                           : absSpeed > 0.2f ? $" <color=#{c}>></color>"
                           : $" <color=#{c}>></color>";
                }
                else
                {
                    string c = ColorUtility.ToHtmlStringRGB(arrowLeftColor);
                    arrows = absSpeed > 0.8f ? $"<color=#{c}><<<</color> "
                           : absSpeed > 0.2f ? $"<color=#{c}><<</color> "
                           : $"<color=#{c}><</color> ";
                }

                mainText.text = movingRight
                    ? $"{speedStr} <color=#FFE6A0><size=70%>米/秒</size></color>{arrows}"
                    : $"{arrows}{speedStr} <color=#FFE6A0><size=70%>米/秒</size></color>";

                mainText.color = speedTextColor;

                float scale = 1f + Mathf.Clamp01(absSpeed / 1.5f) * 0.12f;
                mainText.transform.localScale = Vector3.Lerp(
                    mainText.transform.localScale, Vector3.one * scale, 8f * Time.deltaTime);

                if (bgImage != null)
                    bgImage.color = new Color(bgColor.r, bgColor.g, bgColor.b,
                        Mathf.Lerp(bgImage.color.a, bgColor.a, 4f * Time.deltaTime));
            }

            // === 呼吸脉冲 ===
            if (moving)
            {
                float pulse = 0.92f + Mathf.Sin(Time.time * 4f) * 0.08f;
                mainText.alpha = pulse;
            }
        }

        private void OnDestroy()
        {
            // 不再销毁场景对象，场景对象由编辑器管理
        }
    }
}
