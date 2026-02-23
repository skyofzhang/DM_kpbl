using UnityEngine;

namespace CapybaraDuel.Systems
{
    /// <summary>
    /// 魔法橘子效果控制器 - 运行时动态调整Shader参数
    /// 可以根据游戏状态（推力、速度）动态增强效果
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public class MagicOrangeEffect : MonoBehaviour
    {
        [Header("Dynamic Effect (根据推力/速度动态增强)")]
        [Tooltip("启用推力驱动的动态效果增强")]
        [SerializeField] private bool enableDynamicEffect = true;
        [Tooltip("推力越大，发光越强（倍数）")]
        [SerializeField] private float forceGlowMultiplier = 0.002f;
        [Tooltip("速度越快，流光越亮（倍数）")]
        [SerializeField] private float velocityFlowMultiplier = 0.25f;
        [Tooltip("动态效果最大增幅")]
        [SerializeField] private float maxDynamicBoost = 2f;

        private Renderer _renderer;
        private MaterialPropertyBlock _mpb;
        private OrangeController _orangeController;

        // Shader property IDs (缓存避免字符串查找)
        private static readonly int _GlowIntensityID = Shader.PropertyToID("_GlowIntensity");
        private static readonly int _FresnelIntensityID = Shader.PropertyToID("_FresnelIntensity");
        private static readonly int _FlowIntensityID = Shader.PropertyToID("_FlowIntensity");
        private static readonly int _EmissionIntensityID = Shader.PropertyToID("_EmissionIntensity");
        private static readonly int _PulseSpeedID = Shader.PropertyToID("_PulseSpeed");

        // 基础值（从材质初始设置读取）
        private float _baseGlowIntensity;
        private float _baseFresnelIntensity;
        private float _baseFlowIntensity;
        private float _baseEmissionIntensity;

        private void Start()
        {
            _renderer = GetComponent<Renderer>();
            _mpb = new MaterialPropertyBlock();
            _orangeController = GetComponent<OrangeController>();

            // 读取材质初始值作为基础
            if (_renderer != null && _renderer.sharedMaterial != null)
            {
                var mat = _renderer.sharedMaterial;
                _baseGlowIntensity = mat.HasProperty("_GlowIntensity") ? mat.GetFloat(_GlowIntensityID) : 2f;
                _baseFresnelIntensity = mat.HasProperty("_FresnelIntensity") ? mat.GetFloat(_FresnelIntensityID) : 1.5f;
                _baseFlowIntensity = mat.HasProperty("_FlowIntensity") ? mat.GetFloat(_FlowIntensityID) : 0.8f;
                _baseEmissionIntensity = mat.HasProperty("_EmissionIntensity") ? mat.GetFloat(_EmissionIntensityID) : 0.5f;
            }
        }

        private void Update()
        {
            if (!enableDynamicEffect || _orangeController == null || _renderer == null || _mpb == null)
                return;

            try
            {
                float velocity = Mathf.Abs(_orangeController.GetVelocity());
                float dynamicBoost = Mathf.Clamp(velocity * velocityFlowMultiplier, 0, maxDynamicBoost);

                _renderer.GetPropertyBlock(_mpb);

                // 速度越快 → 发光/流光越强
                _mpb.SetFloat(_GlowIntensityID, _baseGlowIntensity + dynamicBoost * 0.8f);
                _mpb.SetFloat(_FresnelIntensityID, _baseFresnelIntensity + dynamicBoost * 0.5f);
                _mpb.SetFloat(_FlowIntensityID, _baseFlowIntensity + dynamicBoost);
                _mpb.SetFloat(_EmissionIntensityID, _baseEmissionIntensity + dynamicBoost * 0.3f);

                _renderer.SetPropertyBlock(_mpb);
            }
            catch (System.Exception)
            {
                // 材质或Renderer状态异常时静默跳过，不阻断游戏流程
                enableDynamicEffect = false;
            }
        }

        /// <summary>
        /// 外部设置推力强度（可由GameManager调用）
        /// </summary>
        public void SetForceIntensity(float forceAbs)
        {
            if (!enableDynamicEffect || _renderer == null || _mpb == null) return;

            try
            {
                float boost = Mathf.Clamp(forceAbs * forceGlowMultiplier, 0, maxDynamicBoost);

                _renderer.GetPropertyBlock(_mpb);
                _mpb.SetFloat(_GlowIntensityID, _baseGlowIntensity + boost);
                _renderer.SetPropertyBlock(_mpb);
            }
            catch (System.Exception)
            {
                enableDynamicEffect = false;
            }
        }
    }
}
