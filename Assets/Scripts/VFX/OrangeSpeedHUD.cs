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
        [Tooltip("运行时额外Y偏移（正=上移，负=下移）")]
        [SerializeField] private float extraYOffset = -0.15f;

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

        // === 性能优化：预缓存箭头字符串，消除每帧GC分配 ===
        private const string SUFFIX = " <color=#FFE6A0><size=85%>m/s</size></color>";
        private string _arrowR1, _arrowR2, _arrowR3; // 右箭头 >, >>, >>>
        private string _arrowL1, _arrowL2, _arrowL3; // 左箭头 <, <<, <<<
        private string _lastSpeedStr;
        private float _lastDisplayedRounded = -1f;

        private void Start()
        {
            _orangeCtrl = GetComponent<OrangeController>();
            _cameraTransform = Camera.main?.transform;

            // 记录场景中你手动设置的HUD相对橘子的偏移量
            if (hudRoot != null)
                _initialOffset = hudRoot.transform.position - transform.position;

            // 隐藏黑底背景，改用文字描边+投影保证清晰度
            if (bgImage != null)
                bgImage.gameObject.SetActive(false);

            // 运行时字体 fallback 设置 + 描边投影
            if (mainText != null)
            {
                // 描边（Outline）— 保证数字在任何背景下清晰
                mainText.outlineWidth = 0.35f;
                mainText.outlineColor = new Color32(0, 0, 0, 255);

                // 性能优化：预缓存箭头富文本字符串（颜色固定，无需每帧重建）
                string cR = ColorUtility.ToHtmlStringRGB(arrowRightColor);
                _arrowR1 = $" <color=#{cR}>></color>";
                _arrowR2 = $" <color=#{cR}>>></color>";
                _arrowR3 = $" <color=#{cR}>>>></color>";
                string cL = ColorUtility.ToHtmlStringRGB(arrowLeftColor);
                _arrowL1 = $"<color=#{cL}><</color> ";
                _arrowL2 = $"<color=#{cL}><<</color> ";
                _arrowL3 = $"<color=#{cL}><<<</color> ";

                // 投影（Underlay/Shadow）— 增加层次感
                mainText.ForceMeshUpdate();
                var mat = mainText.fontMaterial;
                if (mat != null)
                {
                    mat.EnableKeyword("UNDERLAY_ON");
                    mat.SetColor("_UnderlayColor", new Color(0, 0, 0, 0.8f));
                    mat.SetFloat("_UnderlayOffsetX", 1.0f);
                    mat.SetFloat("_UnderlayOffsetY", -1.0f);
                    mat.SetFloat("_UnderlayDilate", 0.3f);
                    mat.SetFloat("_UnderlaySoftness", 0.4f);
                }

                // v118: 速度文字放大1.5倍，更清晰醒目
                mainText.fontSize *= 1.5f;
            }
        }

        private void LateUpdate()
        {
            if (_orangeCtrl == null || hudRoot == null || mainText == null) return;

            // === 同步位置（跟随橘子，保持场景中设定的偏移量 + 额外微调）===
            hudRoot.transform.position = transform.position + _initialOffset + new Vector3(0, extraYOffset, 0);

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

            // === 构建显示文本（性能优化：缓存speedStr，预缓存箭头，减少每帧GC 4→0次）===
            bool moving = absSpeed > arrowHideThreshold;
            bool movingRight = velocity > 0;

            // 只在数值变化时重新生成speedStr（精度0.01，避免每帧ToString）
            float rounded = Mathf.Round(_displayedSpeed * 100f) / 100f;
            if (rounded != _lastDisplayedRounded)
            {
                _lastDisplayedRounded = rounded;
                _lastSpeedStr = _displayedSpeed < speedDisplayThreshold ? "0.00" : _displayedSpeed.ToString("F2");
            }

            if (!moving)
            {
                mainText.text = string.Concat(_lastSpeedStr, SUFFIX);
                mainText.color = new Color(1f, 1f, 1f, 0.85f); // v118: 静止时也保持高可见度
                mainText.transform.localScale = Vector3.one;
            }
            else
            {
                // 使用Start()中预缓存的箭头字符串，零GC
                string arrows;
                if (movingRight)
                    arrows = absSpeed > 0.8f ? _arrowR3 : absSpeed > 0.2f ? _arrowR2 : _arrowR1;
                else
                    arrows = absSpeed > 0.8f ? _arrowL3 : absSpeed > 0.2f ? _arrowL2 : _arrowL1;

                mainText.text = movingRight
                    ? string.Concat(_lastSpeedStr, SUFFIX, arrows)
                    : string.Concat(arrows, _lastSpeedStr, SUFFIX);

                mainText.color = speedTextColor;

                float scale = 1f + Mathf.Clamp01(absSpeed / 1.5f) * 0.12f;
                mainText.transform.localScale = Vector3.Lerp(
                    mainText.transform.localScale, Vector3.one * scale, 8f * Time.deltaTime);
            }

            // === 呼吸脉冲 ===
            if (moving)
            {
                float pulse = 0.96f + Mathf.Sin(Time.time * 4f) * 0.04f; // v118: 最低0.92，不再明显透明
                mainText.alpha = pulse;
            }
        }

        private void OnDestroy()
        {
            // 不再销毁场景对象，场景对象由编辑器管理
        }
    }
}
