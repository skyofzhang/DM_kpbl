using UnityEngine;

namespace CapybaraDuel.VFX
{
    /// <summary>
    /// 单位视觉效果管理器（母材质系统）
    /// 根据礼物等级(tier 0-6)为单位添加差异化视觉效果
    ///
    /// 设计原则：
    /// - 不改变 _BaseColor，保留原始贴图完整性
    /// - Emission 只做微弱边缘光，不做全覆盖
    /// - 小礼物几乎无变化，大礼物才酷炫
    /// - 所有参数暴露为 SerializeField，可在 Inspector 手调
    /// - 使用 material instance (.material) 替代 SetPropertyBlock
    /// </summary>
    public class UnitVisualEffect : MonoBehaviour
    {
        // ==================== 可调参数（Inspector 暴露） ====================

        [Header("=== Tier 1: 仙女棒 (0.1币) — 几乎无变化 ===")]
        [SerializeField] private float tier1Scale = 1.0f;
        [SerializeField] private Color tier1EmissionColor = new Color(1f, 0.95f, 0.85f);
        [SerializeField] private float tier1EmissionIntensity = 0.05f;
        [SerializeField] private float tier1PulseSpeed = 0f;
        [SerializeField] private float tier1PulseAmount = 0f;

        [Header("=== Tier 2: 能力药丸 (10币) — 微弱蓝色边缘光 ===")]
        [SerializeField] private float tier2Scale = 1.1f;
        [SerializeField] private Color tier2EmissionColor = new Color(0.4f, 0.7f, 1f);
        [SerializeField] private float tier2EmissionIntensity = 0.15f;
        [SerializeField] private float tier2PulseSpeed = 1f;
        [SerializeField] private float tier2PulseAmount = 0.05f;

        [Header("=== Tier 3: 甜甜圈 (52币) — 紫色光晕 ===")]
        [SerializeField] private float tier3Scale = 1.2f;
        [SerializeField] private Color tier3EmissionColor = new Color(0.6f, 0.3f, 1f);
        [SerializeField] private float tier3EmissionIntensity = 0.3f;
        [SerializeField] private float tier3PulseSpeed = 1.5f;
        [SerializeField] private float tier3PulseAmount = 0.1f;

        [Header("=== Tier 4: 能量电池 (99币) — 金色闪耀 ===")]
        [SerializeField] private float tier4Scale = 1.35f;
        [SerializeField] private Color tier4EmissionColor = new Color(1f, 0.8f, 0.2f);
        [SerializeField] private float tier4EmissionIntensity = 0.5f;
        [SerializeField] private float tier4PulseSpeed = 2f;
        [SerializeField] private float tier4PulseAmount = 0.15f;

        [Header("=== Tier 5: 爱的爆炸 (199币) — 红色烈焰 ===")]
        [SerializeField] private float tier5Scale = 1.5f;
        [SerializeField] private Color tier5EmissionColor = new Color(1f, 0.25f, 0.15f);
        [SerializeField] private float tier5EmissionIntensity = 0.8f;
        [SerializeField] private float tier5PulseSpeed = 2.5f;
        [SerializeField] private float tier5PulseAmount = 0.2f;

        [Header("=== Tier 6: 神秘空投 (520币) — 彩虹流光 ===")]
        [SerializeField] private float tier6Scale = 1.7f;
        [SerializeField] private float tier6EmissionIntensity = 1.2f;
        [SerializeField] private float tier6PulseSpeed = 3f;
        [SerializeField] private float tier6PulseAmount = 0.25f;
        [SerializeField] private float tier6RainbowSpeed = 0.3f;
        [SerializeField] private float tier6RainbowSaturation = 0.5f;

        [Header("=== BaseColor Tint (按tier区分角色颜色) ===")]
        [Tooltip("Tint混合强度（0=纯原色, 1=纯tint色）。tier>=2有独立模型用低值")]
        [SerializeField] private float tintStrength = 0.35f;
        [Tooltip("tier>=2独立模型的tint强度（它们有自己的贴图，不需要强着色）")]
        [SerializeField] private float giftModelTintStrength = 0.02f;
        [SerializeField] private Color tier1Tint = new Color(0.85f, 0.85f, 0.9f);   // 白银偏冷
        [SerializeField] private Color tier2Tint = new Color(0.6f, 0.8f, 1f);         // 蓝色
        [SerializeField] private Color tier3Tint = new Color(0.8f, 0.6f, 1f);         // 紫色
        [SerializeField] private Color tier4Tint = new Color(1f, 0.85f, 0.3f);        // 金色
        [SerializeField] private Color tier5Tint = new Color(1f, 0.5f, 0.3f);         // 红橙
        [SerializeField] private Color tier6Tint = new Color(0.9f, 0.7f, 1f);         // 紫金

        [Header("=== 升级等级材质球 (Lv.1~10) ===")]
        [Tooltip("10个预制材质球，按等级1~10对应。为null时fallback到代码生成")]
        [SerializeField] private Material[] upgradeMaterials = new Material[10];

        [Header("=== 通用设置 ===")]
        [Tooltip("缩放呼吸动画幅度（所有tier共用的微弱呼吸）")]
        [SerializeField] private float breathAmplitude = 0.02f;
        [Tooltip("缩放呼吸频率")]
        [SerializeField] private float breathSpeed = 0.8f;

        // ==================== 运行时状态 ====================
        private int _tier = 0;
        private Renderer[] _renderers;
        private Material[] _instanceMaterials;
        private Material[] _originalSharedMaterials; // 存储原始共享材质引用
        private float _baseScale = 1f;
        private bool _initialized = false;

        // 动画
        private float _pulsePhase;
        private float _breathPhase;

        // 当前tier的参数缓存
        private Color _emissionColor;
        private float _emissionIntensity;
        private float _pulseSpeed;
        private float _pulseAmount;
        private float _tierScale;

        // BaseColor tint缓存
        private Color _tintColor = Color.white;
        private Color[] _originalBaseColors; // 存储每个材质的原始BaseColor

        // Shader property IDs
        private static readonly int _EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly int _BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int _RimColorId = Shader.PropertyToID("_RimColor");
        private static readonly int _RimPowerId = Shader.PropertyToID("_RimPower");
        private static readonly int _RimSmoothnessId = Shader.PropertyToID("_RimSmoothness");
        private static readonly int _FuzzyEdgeColorId = Shader.PropertyToID("_FuzzyEdgeColor");
        private static readonly int _FuzzyIntensityId = Shader.PropertyToID("_FuzzyIntensity");
        private static readonly int _FuzzyPowerId = Shader.PropertyToID("_FuzzyPower");
        private static readonly int _SSSColorId = Shader.PropertyToID("_SSSColor");
        private static readonly int _SSSScaleId = Shader.PropertyToID("_SSSScale");
        private static readonly int _ShadowColorId = Shader.PropertyToID("_ShadowColor");

        /// <summary>
        /// 应用 tier 视觉效果
        /// </summary>
        public void ApplyTier(int tier, float baseScale = 1f)
        {
            _tier = Mathf.Clamp(tier, 0, 6);
            _baseScale = baseScale;
            _pulsePhase = Random.Range(0f, Mathf.PI * 2f);
            _breathPhase = Random.Range(0f, Mathf.PI * 2f);

            // 只获取 MeshRenderer 和 SkinnedMeshRenderer
            if (_renderers == null)
            {
                var allRenderers = GetComponentsInChildren<Renderer>();
                var filtered = new System.Collections.Generic.List<Renderer>();
                foreach (var r in allRenderers)
                {
                    if (r is MeshRenderer || r is SkinnedMeshRenderer)
                        filtered.Add(r);
                }
                _renderers = filtered.ToArray();
            }

            if (_renderers.Length == 0) return;

            // 读取当前tier参数
            LoadTierParams();

            // 设置缩放
            transform.localScale = Vector3.one * (_baseScale * _tierScale);

            if (_tier >= 1)
            {
                CreateInstanceMaterials();
                ApplyEmission();
                _initialized = true;
            }
            else
            {
                RestoreSharedMaterials();
                _initialized = false;
            }
        }

        /// <summary>从 SerializeField 读取当前 tier 的参数</summary>
        private void LoadTierParams()
        {
            switch (_tier)
            {
                case 1:
                    _tierScale = tier1Scale;
                    _emissionColor = tier1EmissionColor;
                    _emissionIntensity = tier1EmissionIntensity;
                    _pulseSpeed = tier1PulseSpeed;
                    _pulseAmount = tier1PulseAmount;
                    _tintColor = tier1Tint;
                    break;
                case 2:
                    _tierScale = tier2Scale;
                    _emissionColor = tier2EmissionColor;
                    _emissionIntensity = tier2EmissionIntensity;
                    _pulseSpeed = tier2PulseSpeed;
                    _pulseAmount = tier2PulseAmount;
                    _tintColor = tier2Tint;
                    break;
                case 3:
                    _tierScale = tier3Scale;
                    _emissionColor = tier3EmissionColor;
                    _emissionIntensity = tier3EmissionIntensity;
                    _pulseSpeed = tier3PulseSpeed;
                    _pulseAmount = tier3PulseAmount;
                    _tintColor = tier3Tint;
                    break;
                case 4:
                    _tierScale = tier4Scale;
                    _emissionColor = tier4EmissionColor;
                    _emissionIntensity = tier4EmissionIntensity;
                    _pulseSpeed = tier4PulseSpeed;
                    _pulseAmount = tier4PulseAmount;
                    _tintColor = tier4Tint;
                    break;
                case 5:
                    _tierScale = tier5Scale;
                    _emissionColor = tier5EmissionColor;
                    _emissionIntensity = tier5EmissionIntensity;
                    _pulseSpeed = tier5PulseSpeed;
                    _pulseAmount = tier5PulseAmount;
                    _tintColor = tier5Tint;
                    break;
                case 6:
                    _tierScale = tier6Scale;
                    _emissionColor = Color.white; // 彩虹模式，运行时计算
                    _emissionIntensity = tier6EmissionIntensity;
                    _pulseSpeed = tier6PulseSpeed;
                    _pulseAmount = tier6PulseAmount;
                    _tintColor = tier6Tint;
                    break;
                default:
                    _tierScale = 1f;
                    _emissionColor = Color.black;
                    _emissionIntensity = 0f;
                    _pulseSpeed = 0f;
                    _pulseAmount = 0f;
                    _tintColor = Color.white;
                    break;
            }
        }

        /// <summary>创建独立材质实例（不污染共享材质）</summary>
        private void CreateInstanceMaterials()
        {
            if (_instanceMaterials != null) return;

            _instanceMaterials = new Material[_renderers.Length];
            _originalSharedMaterials = new Material[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null) continue;
                _originalSharedMaterials[i] = _renderers[i].sharedMaterial;
                _instanceMaterials[i] = _renderers[i].material; // 创建实例
            }
        }

        /// <summary>恢复共享材质（对象池回收时）</summary>
        private void RestoreSharedMaterials()
        {
            if (_instanceMaterials == null || _renderers == null) return;
            int count = Mathf.Min(_renderers.Length, _instanceMaterials.Length);
            for (int i = 0; i < count; i++)
            {
                if (_renderers[i] == null) continue;
                if (_instanceMaterials[i] != null)
                    Destroy(_instanceMaterials[i]);
                if (_originalSharedMaterials != null && i < _originalSharedMaterials.Length
                    && _originalSharedMaterials[i] != null)
                    _renderers[i].sharedMaterial = _originalSharedMaterials[i];
            }
            _instanceMaterials = null;
            _originalSharedMaterials = null;
            _originalBaseColors = null;
        }

        /// <summary>
        /// 应用 Emission + BaseColor Tint
        /// Emission: 微弱发光边缘效果
        /// BaseColor Tint: 与原色混合，让不同tier角色有颜色区分
        ///
        /// 注意：tier>=2 的gift单位有独立高质量模型和贴图，
        /// 需要大幅降低tint和emission强度，避免覆盖贴图细节
        /// </summary>
        private void ApplyEmission()
        {
            if (_instanceMaterials == null) return;

            // 保存原始BaseColor（首次调用时）
            if (_originalBaseColors == null)
            {
                _originalBaseColors = new Color[_instanceMaterials.Length];
                for (int i = 0; i < _instanceMaterials.Length; i++)
                {
                    if (_instanceMaterials[i] != null && _instanceMaterials[i].HasProperty(_BaseColorId))
                        _originalBaseColors[i] = _instanceMaterials[i].GetColor(_BaseColorId);
                    else
                        _originalBaseColors[i] = Color.white;
                }
            }

            // gift单位(tier>=2)有独立模型贴图，大幅降低效果强度
            bool isGiftModel = _tier >= 2;
            float emissionMult = isGiftModel ? 0.1f : 1f;  // gift单位emission降到10%
            float actualTintStrength = isGiftModel ? giftModelTintStrength : tintStrength;

            for (int i = 0; i < _instanceMaterials.Length; i++)
            {
                var mat = _instanceMaterials[i];
                if (mat == null) continue;

                // Emission（gift单位只用30%强度）
                mat.EnableKeyword("_EMISSION");
                mat.SetColor(_EmissionColorId, _emissionColor * (_emissionIntensity * emissionMult));

                // BaseColor Tint: gift单位极轻tint(8%)，基础单位正常(35%)
                if (_tier >= 1 && actualTintStrength > 0f && mat.HasProperty(_BaseColorId))
                {
                    Color original = _originalBaseColors[i];
                    Color tinted = Color.Lerp(original, _tintColor, actualTintStrength);
                    tinted.a = original.a; // 保留原始alpha
                    mat.SetColor(_BaseColorId, tinted);
                }
            }
        }

        private void Update()
        {
            if (!_initialized || _instanceMaterials == null) return;

            _pulsePhase += Time.deltaTime;
            _breathPhase += breathSpeed * Time.deltaTime;

            bool isUpgradeMode = _upgradeInitialized && _upgradeLevel >= 2;

            // === Emission 脉动 ===
            if (_pulseSpeed > 0f && _emissionIntensity > 0f)
            {
                float pulse = 1f + Mathf.Sin(_pulsePhase * _pulseSpeed * Mathf.PI * 2f) * _pulseAmount;

                Color emColor;
                bool isRainbow = (_tier == 6) || (_upgradeLevel == 10);
                if (isRainbow)
                {
                    float hue = Mathf.Repeat(_pulsePhase * tier6RainbowSpeed, 1f);
                    emColor = Color.HSVToRGB(hue, tier6RainbowSaturation, 1f);
                }
                else
                {
                    emColor = _emissionColor;
                }

                // gift单位(tier>=2且非升级模式)有独立贴图，emission降到10%
                float emMult = (!isUpgradeMode && _tier >= 2) ? 0.1f : 1f;
                Color finalEmission = emColor * (_emissionIntensity * pulse * emMult);
                foreach (var mat in _instanceMaterials)
                {
                    if (mat == null) continue;
                    mat.SetColor(_EmissionColorId, finalEmission);
                }
            }

            // === 升级Rim脉动（呼吸光环效果） ===
            if (isUpgradeMode && _upgradeRimPulseSpeed > 0f)
            {
                float rimPulse = 1f + Mathf.Sin(_pulsePhase * _upgradeRimPulseSpeed * Mathf.PI * 2f) * _upgradeRimPulseAmount;

                Color rimColor;
                if (_upgradeLevel == 10)
                {
                    // 彩虹Rim
                    float hue = Mathf.Repeat(_pulsePhase * tier6RainbowSpeed * 0.7f, 1f);
                    rimColor = Color.HSVToRGB(hue, 0.6f, 1f);
                }
                else
                {
                    rimColor = _upgradeRimColor;
                }

                Color finalRim = rimColor * rimPulse;
                foreach (var mat in _instanceMaterials)
                {
                    if (mat != null && mat.HasProperty(_RimColorId))
                        mat.SetColor(_RimColorId, finalRim);
                }
            }

            // === 缩放呼吸（微弱） ===
            bool shouldBreathe = (breathAmplitude > 0f) && (_tier >= 2 || _upgradeLevel >= 4);
            if (shouldBreathe)
            {
                float breathAmp = isUpgradeMode ? breathAmplitude * (1f + (_upgradeLevel - 4) * 0.15f) : breathAmplitude;
                float breath = 1f + Mathf.Sin(_breathPhase * Mathf.PI * 2f) * breathAmp;
                transform.localScale = Vector3.one * (_baseScale * _tierScale * breath);
            }

            // === 升级光圈动画 ===
            if (_upgradeInitialized && _upgradeLevel >= 2)
            {
                UpdateHaloAnimation();
            }
        }

        /// <summary>清理（对象池回收时调用）</summary>
        public void Cleanup()
        {
            DestroyHalo();
            RestoreSharedMaterials();
            ClearPropertyBlocks();
            _tier = 0;
            _upgradeLevel = 1;
            _upgradeInitialized = false;
            _initialized = false;
            _renderers = null;
        }

        private void OnDestroy()
        {
            DestroyHalo();
            RestoreSharedMaterials();
        }

        /// <summary>清除残留 PropertyBlock</summary>
        private void ClearPropertyBlocks()
        {
            if (_renderers == null) return;
            foreach (var r in _renderers)
            {
                if (r == null) continue;
                try { r.SetPropertyBlock(null); }
                catch (System.Exception) { }
            }
        }

        // ==================== 升级系统视觉 ====================
        //
        // 设计理念：10级渐进色彩体系，充分利用CuteCapybara shader的多通道
        //
        //   Lv.1  原始态 — 无任何修改，最朴素的卡皮巴拉
        //   Lv.2  初醒 — 暖白Rim轮廓光微亮，身体微暖
        //   Lv.3  觉醒 — 浅金Rim + 暖色FuzzyEdge + 体色偏暖奶油
        //   Lv.4  蓄力 — 天蓝Rim + 蓝色Emission微光 + SSS偏蓝
        //   Lv.5  凝聚 — 钴蓝Rim加强 + Emission加深 + 阴影偏靛
        //   Lv.6  炽热 — 金色Rim + 金色Emission + FuzzyEdge金 + 体色偏暖金
        //   Lv.7  辉煌 — 琥珀Rim + Emission加强 + SSS橙金 + 脉动
        //   Lv.8  炎皇 — 红橙Rim + 强Emission + SSS火红 + 阴影深赭
        //   Lv.9  神威 — 品红Rim + 紫红Emission + SSS桃粉 + 快脉动
        //   Lv.10 传说 — 彩虹Rim循环 + 彩虹Emission + 最大脉动 + 呼吸缩放
        //
        // 层次感来源：
        //   - RimColor/Power: 轮廓光 = 最显眼的"光环"，低级暖白→高级彩色
        //   - FuzzyEdgeColor: 毛绒边缘 = 温暖柔和的"氛围"
        //   - EmissionColor: 自发光 = 内在的"能量感"
        //   - SSSColor: 次表面散射 = 皮肤通透的"生命力"
        //   - ShadowColor: 阴影色调 = 整体色调氛围的"底色"
        //   - BaseColor tint: 体色微调 = 最底层的"血统"
        //   - Scale + Pulse + Breath: 动态感 = "力量"

        // 升级等级运行时状态
        private int _upgradeLevel = 1;
        private bool _upgradeInitialized = false;

        // 升级Rim动画缓存
        private Color _upgradeRimColor;
        private float _upgradeRimPulseSpeed;
        private float _upgradeRimPulseAmount;

        // ==================== 升级光圈系统 ====================
        // 头顶透明光圈：匹配升级等级配色，温暖生动
        // 使用SpriteRenderer + 程序生成环形纹理，Billboard朝向相机

        [Header("=== 升级光圈 (Halo Ring) ===")]
        [Tooltip("光圈高度偏移（相对于单位顶部）")]
        [SerializeField] private float haloHeightOffset = 0.35f;
        [Tooltip("光圈基础半径")]
        [SerializeField] private float haloBaseRadius = 0.45f;
        [Tooltip("光圈旋转速度（度/秒）")]
        [SerializeField] private float haloRotateSpeed = 45f;
        [Tooltip("光圈脉动速度")]
        [SerializeField] private float haloPulseSpeed = 1.5f;
        [Tooltip("光圈脉动幅度（缩放±比例）")]
        [SerializeField] private float haloPulseAmount = 0.1f;
        [Tooltip("光圈透明度")]
        [SerializeField] private float haloAlpha = 0.85f;
        [Tooltip("光圈上下浮动幅度")]
        [SerializeField] private float haloFloatAmount = 0.03f;
        [Tooltip("光圈上下浮动速度")]
        [SerializeField] private float haloFloatSpeed = 1.2f;

        private GameObject _haloObject;
        private SpriteRenderer _haloRenderer;
        private float _haloPhase;       // 动画相位
        private Color _haloColor;       // 光圈颜色
        private float _haloYBase;       // 光圈基础Y位置

        // 程序生成的环形纹理缓存（所有实例共享）
        private static Texture2D _sharedRingTexture;
        private static Sprite _sharedRingSprite;
        private static Material _sharedHaloMaterial;

        /// <summary>
        /// 升级等级视觉参数结构体
        /// </summary>
        private struct UpgradeVisualParams
        {
            public float scale;
            // Rim Light
            public Color rimColor;
            public float rimPower;
            public float rimSmoothness;
            // Fuzzy Edge
            public Color fuzzyColor;
            public float fuzzyIntensity;
            // Emission
            public Color emissionColor;
            public float emissionIntensity;
            // SSS
            public Color sssColor;
            public float sssScale;
            // Shadow tint
            public Color shadowColor;
            // BaseColor tint
            public Color baseTint;
            public float baseTintStrength;
            // Animation
            public float pulseSpeed;
            public float pulseAmount;
            public float rimPulseSpeed;    // Rim单独脉动（呼吸光环）
            public float rimPulseAmount;
        }

        // ==================== 光圈方法 ====================

        /// <summary>创建或获取共享的环形纹理</summary>
        private static void EnsureSharedRingTexture()
        {
            if (_sharedRingTexture != null) return;

            // 程序生成环形纹理 128x128（更清晰）
            int size = 128;
            _sharedRingTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            _sharedRingTexture.filterMode = FilterMode.Bilinear;
            _sharedRingTexture.wrapMode = TextureWrapMode.Clamp;

            float center = size * 0.5f;
            float outerR = size * 0.48f;  // 外半径
            float innerR = size * 0.28f;  // 内半径（更粗的环）
            float edgeSoftness = 4f;      // 边缘柔和度

            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    // 环形alpha：内外边缘都柔化
                    float alphaOuter = 1f - Mathf.Clamp01((dist - outerR) / edgeSoftness);
                    float alphaInner = Mathf.Clamp01((dist - innerR) / edgeSoftness);
                    float alpha = alphaOuter * alphaInner;

                    // 添加微妙的亮度渐变（靠近外缘稍亮）
                    float brightGrad = 0.8f + 0.2f * Mathf.Clamp01((dist - innerR) / (outerR - innerR));

                    byte b = (byte)(brightGrad * 255);
                    byte a = (byte)(alpha * 255);
                    pixels[y * size + x] = new Color32(b, b, b, a);
                }
            }

            _sharedRingTexture.SetPixels32(pixels);
            _sharedRingTexture.Apply();

            // 创建Sprite
            _sharedRingSprite = Sprite.Create(
                _sharedRingTexture,
                new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f),
                128f  // pixels per unit
            );

            // 创建共享材质（使用Additive混合）
            var shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                _sharedHaloMaterial = new Material(shader);
                _sharedHaloMaterial.SetFloat("_Mode", 1); // Additive
                _sharedHaloMaterial.renderQueue = 3100; // 在透明物体之后渲染
            }
        }

        /// <summary>创建光圈对象</summary>
        private void CreateHaloObject(Color color)
        {
            EnsureSharedRingTexture();

            if (_haloObject != null)
            {
                // 已存在，只更新颜色
                _haloColor = color;
                UpdateHaloColor();
                return;
            }

            _haloObject = new GameObject("UpgradeHalo");
            _haloObject.transform.SetParent(transform, false);

            // 定位到头顶上方
            // 获取单位高度（通过renderer bounds或固定值）
            float unitHeight = GetUnitHeight();
            _haloYBase = unitHeight + haloHeightOffset;
            _haloObject.transform.localPosition = new Vector3(0f, _haloYBase, 0f);

            // 添加SpriteRenderer
            _haloRenderer = _haloObject.AddComponent<SpriteRenderer>();
            _haloRenderer.sprite = _sharedRingSprite;
            if (_sharedHaloMaterial != null)
                _haloRenderer.sharedMaterial = _sharedHaloMaterial;
            _haloRenderer.sortingOrder = 5; // 在单位之上

            // 设置光圈大小
            _haloObject.transform.localScale = Vector3.one * haloBaseRadius * 2f;

            // 初始化
            _haloColor = color;
            _haloPhase = Random.Range(0f, Mathf.PI * 2f);
            UpdateHaloColor();

            // 初始旋转为水平（X轴倾斜90度让环形水平躺着）
            _haloObject.transform.localRotation = Quaternion.Euler(70f, 0f, 0f);
        }

        /// <summary>更新光圈颜色</summary>
        private void UpdateHaloColor()
        {
            if (_haloRenderer == null) return;
            Color c = _haloColor;
            c.a = haloAlpha;
            _haloRenderer.color = c;
        }

        /// <summary>获取单位高度</summary>
        private float GetUnitHeight()
        {
            if (_renderers != null && _renderers.Length > 0)
            {
                float maxY = 0f;
                foreach (var r in _renderers)
                {
                    if (r == null) continue;
                    float top = r.bounds.max.y - transform.position.y;
                    if (top > maxY) maxY = top;
                }
                if (maxY > 0.01f) return maxY;
            }
            return 0.4f; // 默认值
        }

        /// <summary>更新光圈动画（在Update中调用）</summary>
        private void UpdateHaloAnimation()
        {
            if (_haloObject == null || !_haloObject.activeSelf) return;

            _haloPhase += Time.deltaTime;

            // 1. Y轴旋转（生动自然的持续旋转）
            float yRot = _haloPhase * haloRotateSpeed;

            // 2. 缩放脉动
            float pulse = 1f + Mathf.Sin(_haloPhase * haloPulseSpeed * Mathf.PI * 2f) * haloPulseAmount;
            _haloObject.transform.localScale = Vector3.one * haloBaseRadius * 2f * pulse;

            // 3. 上下浮动
            float floatOffset = Mathf.Sin(_haloPhase * haloFloatSpeed * Mathf.PI * 2f) * haloFloatAmount;
            _haloObject.transform.localPosition = new Vector3(0f, _haloYBase + floatOffset, 0f);

            // 4. 组合旋转（保持水平倾斜 + Y旋转）
            _haloObject.transform.localRotation = Quaternion.Euler(70f, yRot, 0f);

            // 5. 彩虹模式 (Lv.10)
            if (_upgradeLevel == 10)
            {
                float hue = Mathf.Repeat(_haloPhase * 0.3f, 1f);
                _haloColor = Color.HSVToRGB(hue, 0.5f, 1f);
                UpdateHaloColor();
            }

            // 6. Alpha呼吸（微妙的透明度变化）
            float alphaBreath = haloAlpha + Mathf.Sin(_haloPhase * 0.8f * Mathf.PI * 2f) * 0.12f;
            Color c = _haloRenderer.color;
            c.a = alphaBreath;
            _haloRenderer.color = c;
        }

        /// <summary>销毁光圈对象</summary>
        private void DestroyHalo()
        {
            if (_haloObject != null)
            {
                Destroy(_haloObject);
                _haloObject = null;
                _haloRenderer = null;
            }
        }

        /// <summary>根据升级等级获取光圈颜色</summary>
        private Color GetHaloColorForLevel(int level)
        {
            switch (level)
            {
                case 2:  return new Color(1f, 0.95f, 0.85f);     // 暖白
                case 3:  return new Color(1f, 0.9f, 0.6f);       // 浅金
                case 4:  return new Color(0.6f, 0.85f, 1f);      // 天蓝
                case 5:  return new Color(0.35f, 0.65f, 1f);     // 钴蓝
                case 6:  return new Color(1f, 0.82f, 0.25f);     // 金色
                case 7:  return new Color(1f, 0.7f, 0.15f);      // 琥珀
                case 8:  return new Color(1f, 0.45f, 0.1f);      // 红橙
                case 9:  return new Color(1f, 0.3f, 0.6f);       // 品红
                case 10: return Color.white;                       // 彩虹（运行时计算）
                default: return Color.clear;
            }
        }

        /// <summary>获取指定等级的完整视觉参数</summary>
        private UpgradeVisualParams GetUpgradeParams(int level)
        {
            var p = new UpgradeVisualParams();

            // 默认值（shader原始值，修改时才覆盖）
            p.scale = 1f;
            p.rimColor = new Color(1f, 0.85f, 0.65f);  // shader默认
            p.rimPower = 3f;
            p.rimSmoothness = 0.4f;
            p.fuzzyColor = new Color(0.95f, 0.9f, 0.8f);
            p.fuzzyIntensity = 0.25f;
            p.emissionColor = Color.black;
            p.emissionIntensity = 0f;
            p.sssColor = new Color(1f, 0.4f, 0.3f);
            p.sssScale = 0.4f;
            p.shadowColor = new Color(0.75f, 0.65f, 0.85f);
            p.baseTint = Color.white;
            p.baseTintStrength = 0f;
            p.pulseSpeed = 0f;
            p.pulseAmount = 0f;
            p.rimPulseSpeed = 0f;
            p.rimPulseAmount = 0f;

            switch (level)
            {
                case 1: // 原始态 — 不做任何修改
                    break;

                case 2: // 初醒 — 暖白轮廓光微亮
                    p.scale = 1.02f;
                    p.rimColor = new Color(1f, 0.95f, 0.85f);  // 暖白
                    p.rimPower = 2.5f;  // 稍宽的Rim
                    p.rimSmoothness = 0.5f;
                    p.rimPulseSpeed = 0.6f;
                    p.rimPulseAmount = 0.15f;
                    p.baseTint = new Color(1f, 0.97f, 0.92f);  // 极微暖
                    p.baseTintStrength = 0.08f;
                    break;

                case 3: // 觉醒 — 浅金Rim + 暖毛绒边缘
                    p.scale = 1.04f;
                    p.rimColor = new Color(1f, 0.9f, 0.6f);    // 浅金
                    p.rimPower = 2.2f;
                    p.rimSmoothness = 0.55f;
                    p.fuzzyColor = new Color(1f, 0.92f, 0.75f); // 暖奶油毛绒
                    p.fuzzyIntensity = 0.35f;
                    p.emissionColor = new Color(1f, 0.95f, 0.8f);
                    p.emissionIntensity = 0.06f;  // 极微弱暖光
                    p.rimPulseSpeed = 0.8f;
                    p.rimPulseAmount = 0.2f;
                    p.baseTint = new Color(1f, 0.95f, 0.85f);
                    p.baseTintStrength = 0.12f;
                    break;

                case 4: // 蓄力 — 天蓝系
                    p.scale = 1.06f;
                    p.rimColor = new Color(0.6f, 0.85f, 1f);   // 天蓝
                    p.rimPower = 2.0f;
                    p.rimSmoothness = 0.55f;
                    p.fuzzyColor = new Color(0.8f, 0.9f, 1f);
                    p.fuzzyIntensity = 0.3f;
                    p.emissionColor = new Color(0.5f, 0.8f, 1f);
                    p.emissionIntensity = 0.12f;
                    p.sssColor = new Color(0.6f, 0.8f, 1f);     // SSS偏蓝
                    p.sssScale = 0.5f;
                    p.pulseSpeed = 0.8f;
                    p.pulseAmount = 0.03f;
                    p.rimPulseSpeed = 1.0f;
                    p.rimPulseAmount = 0.25f;
                    p.baseTint = new Color(0.9f, 0.95f, 1f);
                    p.baseTintStrength = 0.1f;
                    break;

                case 5: // 凝聚 — 钴蓝加深
                    p.scale = 1.08f;
                    p.rimColor = new Color(0.35f, 0.65f, 1f);  // 钴蓝
                    p.rimPower = 1.8f;
                    p.rimSmoothness = 0.6f;
                    p.fuzzyColor = new Color(0.7f, 0.82f, 1f);
                    p.fuzzyIntensity = 0.35f;
                    p.emissionColor = new Color(0.3f, 0.6f, 1f);
                    p.emissionIntensity = 0.2f;
                    p.sssColor = new Color(0.4f, 0.65f, 1f);
                    p.sssScale = 0.55f;
                    p.shadowColor = new Color(0.55f, 0.55f, 0.8f);  // 阴影偏靛
                    p.pulseSpeed = 1.0f;
                    p.pulseAmount = 0.05f;
                    p.rimPulseSpeed = 1.2f;
                    p.rimPulseAmount = 0.3f;
                    p.baseTint = new Color(0.85f, 0.9f, 1f);
                    p.baseTintStrength = 0.12f;
                    break;

                case 6: // 炽热 — 金色系
                    p.scale = 1.10f;
                    p.rimColor = new Color(1f, 0.82f, 0.25f);  // 金色
                    p.rimPower = 1.6f;
                    p.rimSmoothness = 0.6f;
                    p.fuzzyColor = new Color(1f, 0.88f, 0.5f);  // 金色毛绒
                    p.fuzzyIntensity = 0.4f;
                    p.emissionColor = new Color(1f, 0.85f, 0.3f);
                    p.emissionIntensity = 0.3f;
                    p.sssColor = new Color(1f, 0.75f, 0.35f);   // SSS金
                    p.sssScale = 0.6f;
                    p.shadowColor = new Color(0.8f, 0.65f, 0.5f);
                    p.pulseSpeed = 1.2f;
                    p.pulseAmount = 0.06f;
                    p.rimPulseSpeed = 1.5f;
                    p.rimPulseAmount = 0.3f;
                    p.baseTint = new Color(1f, 0.93f, 0.78f);
                    p.baseTintStrength = 0.15f;
                    break;

                case 7: // 辉煌 — 琥珀深金
                    p.scale = 1.13f;
                    p.rimColor = new Color(1f, 0.7f, 0.15f);   // 琥珀
                    p.rimPower = 1.5f;
                    p.rimSmoothness = 0.65f;
                    p.fuzzyColor = new Color(1f, 0.82f, 0.4f);
                    p.fuzzyIntensity = 0.45f;
                    p.emissionColor = new Color(1f, 0.75f, 0.2f);
                    p.emissionIntensity = 0.45f;
                    p.sssColor = new Color(1f, 0.65f, 0.25f);   // SSS橙金
                    p.sssScale = 0.65f;
                    p.shadowColor = new Color(0.75f, 0.58f, 0.45f);
                    p.pulseSpeed = 1.5f;
                    p.pulseAmount = 0.08f;
                    p.rimPulseSpeed = 1.8f;
                    p.rimPulseAmount = 0.35f;
                    p.baseTint = new Color(1f, 0.9f, 0.72f);
                    p.baseTintStrength = 0.18f;
                    break;

                case 8: // 炎皇 — 红橙火焰
                    p.scale = 1.16f;
                    p.rimColor = new Color(1f, 0.45f, 0.1f);   // 红橙
                    p.rimPower = 1.3f;
                    p.rimSmoothness = 0.7f;
                    p.fuzzyColor = new Color(1f, 0.7f, 0.4f);
                    p.fuzzyIntensity = 0.5f;
                    p.emissionColor = new Color(1f, 0.5f, 0.15f);
                    p.emissionIntensity = 0.6f;
                    p.sssColor = new Color(1f, 0.4f, 0.15f);    // SSS火红
                    p.sssScale = 0.7f;
                    p.shadowColor = new Color(0.7f, 0.45f, 0.35f); // 深赭
                    p.pulseSpeed = 2.0f;
                    p.pulseAmount = 0.1f;
                    p.rimPulseSpeed = 2.2f;
                    p.rimPulseAmount = 0.4f;
                    p.baseTint = new Color(1f, 0.85f, 0.7f);
                    p.baseTintStrength = 0.2f;
                    break;

                case 9: // 神威 — 品红紫焰
                    p.scale = 1.20f;
                    p.rimColor = new Color(1f, 0.3f, 0.6f);    // 品红
                    p.rimPower = 1.2f;
                    p.rimSmoothness = 0.75f;
                    p.fuzzyColor = new Color(1f, 0.6f, 0.8f);
                    p.fuzzyIntensity = 0.55f;
                    p.emissionColor = new Color(0.9f, 0.3f, 0.6f);
                    p.emissionIntensity = 0.75f;
                    p.sssColor = new Color(1f, 0.5f, 0.7f);     // SSS桃粉
                    p.sssScale = 0.75f;
                    p.shadowColor = new Color(0.65f, 0.4f, 0.6f);
                    p.pulseSpeed = 2.5f;
                    p.pulseAmount = 0.12f;
                    p.rimPulseSpeed = 2.5f;
                    p.rimPulseAmount = 0.45f;
                    p.baseTint = new Color(0.95f, 0.82f, 0.9f);
                    p.baseTintStrength = 0.2f;
                    break;

                case 10: // 传说 — 彩虹流转
                    p.scale = 1.25f;
                    p.rimColor = Color.white; // 运行时彩虹计算
                    p.rimPower = 1.0f;
                    p.rimSmoothness = 0.8f;
                    p.fuzzyColor = Color.white;
                    p.fuzzyIntensity = 0.6f;
                    p.emissionColor = Color.white; // 运行时彩虹计算
                    p.emissionIntensity = 0.9f;
                    p.sssColor = new Color(1f, 0.7f, 0.9f);
                    p.sssScale = 0.8f;
                    p.shadowColor = new Color(0.6f, 0.5f, 0.7f);
                    p.pulseSpeed = 3f;
                    p.pulseAmount = 0.15f;
                    p.rimPulseSpeed = 3f;
                    p.rimPulseAmount = 0.5f;
                    break;
            }

            return p;
        }

        /// <summary>
        /// 应用仙女棒升级等级视觉效果（Lv.1~10，每局独立）
        /// 多通道材质调色：Rim + Fuzzy + Emission + SSS + Shadow + BaseTint
        /// 仅应用于 tier0/1 的基础卡皮巴拉
        /// </summary>
        public void ApplyUpgradeLevel(int level, float baseScale = 1f)
        {
            _upgradeLevel = Mathf.Clamp(level, 1, 10);

            // 只获取 Renderer（如果还没初始化的话）
            if (_renderers == null)
            {
                var allRenderers = GetComponentsInChildren<Renderer>();
                var filtered = new System.Collections.Generic.List<Renderer>();
                foreach (var r in allRenderers)
                {
                    if (r is MeshRenderer || r is SkinnedMeshRenderer)
                        filtered.Add(r);
                }
                _renderers = filtered.ToArray();
            }
            if (_renderers.Length == 0) return;

            var p = GetUpgradeParams(_upgradeLevel);

            // 应用缩放
            _baseScale = baseScale;
            _tierScale = p.scale;
            transform.localScale = Vector3.one * (_baseScale * _tierScale);

            if (_upgradeLevel >= 2)
            {
                // 缓存动画参数（脉动/呼吸仍由代码驱动）
                _emissionColor = p.emissionColor;
                _emissionIntensity = p.emissionIntensity;
                _pulseSpeed = p.pulseSpeed;
                _pulseAmount = p.pulseAmount;
                _upgradeRimColor = p.rimColor;
                _upgradeRimPulseSpeed = p.rimPulseSpeed;
                _upgradeRimPulseAmount = p.rimPulseAmount;
                _pulsePhase = Random.Range(0f, Mathf.PI * 2f);
                _breathPhase = Random.Range(0f, Mathf.PI * 2f);

                // Lv.10 使用彩虹模式（复用 tier 6 标记）
                if (_upgradeLevel == 10) _tier = 6;

                // === 优先使用预制材质球 ===
                int matIndex = _upgradeLevel - 1; // 0~9
                bool usedPreMat = false;
                if (upgradeMaterials != null && matIndex < upgradeMaterials.Length
                    && upgradeMaterials[matIndex] != null)
                {
                    // 使用预制材质球（创建实例避免污染asset）
                    _ApplyPreMadeMaterial(upgradeMaterials[matIndex]);
                    usedPreMat = true;
                }

                if (!usedPreMat)
                {
                    // Fallback: 代码驱动材质调色
                    CreateInstanceMaterials();

                    // 保存原始BaseColor
                    if (_originalBaseColors == null)
                    {
                        _originalBaseColors = new Color[_instanceMaterials.Length];
                        for (int i = 0; i < _instanceMaterials.Length; i++)
                        {
                            if (_instanceMaterials[i] != null && _instanceMaterials[i].HasProperty(_BaseColorId))
                                _originalBaseColors[i] = _instanceMaterials[i].GetColor(_BaseColorId);
                            else
                                _originalBaseColors[i] = Color.white;
                        }
                    }

                    // 多通道材质调色
                    for (int i = 0; i < _instanceMaterials.Length; i++)
                    {
                        var mat = _instanceMaterials[i];
                        if (mat == null) continue;
                        mat.EnableKeyword("_EMISSION");
                        mat.SetColor(_EmissionColorId, p.emissionColor * p.emissionIntensity);
                        if (mat.HasProperty(_RimColorId))
                        {
                            mat.SetColor(_RimColorId, p.rimColor);
                            mat.SetFloat(_RimPowerId, p.rimPower);
                            mat.SetFloat(_RimSmoothnessId, p.rimSmoothness);
                        }
                        if (mat.HasProperty(_FuzzyEdgeColorId))
                        {
                            mat.SetColor(_FuzzyEdgeColorId, p.fuzzyColor);
                            mat.SetFloat(_FuzzyIntensityId, p.fuzzyIntensity);
                        }
                        if (mat.HasProperty(_SSSColorId))
                        {
                            mat.SetColor(_SSSColorId, p.sssColor);
                            mat.SetFloat(_SSSScaleId, p.sssScale);
                        }
                        if (mat.HasProperty(_ShadowColorId))
                            mat.SetColor(_ShadowColorId, p.shadowColor);
                        if (p.baseTintStrength > 0f && mat.HasProperty(_BaseColorId))
                        {
                            Color original = _originalBaseColors[i];
                            Color tinted = Color.Lerp(original, p.baseTint, p.baseTintStrength);
                            tinted.a = original.a;
                            mat.SetColor(_BaseColorId, tinted);
                        }
                    }
                }

                _initialized = true;
                _upgradeInitialized = true;

                // 创建/更新头顶光圈
                Color haloColor = GetHaloColorForLevel(_upgradeLevel);
                CreateHaloObject(haloColor);

                // 升级金光特效（脚底→头顶，比角色大2~3倍）
                SpawnUpgradeFlash(haloColor);
            }
            else
            {
                // Lv.1: 清理之前的升级效果
                DestroyHalo();
                if (_upgradeInitialized)
                {
                    RestoreSharedMaterials();
                    _initialized = false;
                    _upgradeInitialized = false;
                }
            }
        }

        /// <summary>应用预制材质球（从asset创建实例，替换所有renderer的材质）</summary>
        private void _ApplyPreMadeMaterial(Material preMadeMat)
        {
            if (_renderers == null) return;

            // 先保存原始共享材质（用于回收时恢复）
            if (_originalSharedMaterials == null)
            {
                _originalSharedMaterials = new Material[_renderers.Length];
                for (int i = 0; i < _renderers.Length; i++)
                {
                    if (_renderers[i] != null)
                        _originalSharedMaterials[i] = _renderers[i].sharedMaterial;
                }
            }

            // 清理之前的实例材质
            if (_instanceMaterials != null)
            {
                for (int i = 0; i < _instanceMaterials.Length; i++)
                {
                    if (_instanceMaterials[i] != null)
                        Destroy(_instanceMaterials[i]);
                }
            }

            // 创建预制材质的实例并赋给所有renderer
            _instanceMaterials = new Material[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null) continue;
                _instanceMaterials[i] = new Material(preMadeMat);
                _renderers[i].material = _instanceMaterials[i];
            }

            // 运行时加强低等级的视觉辨识度（预制材质lv02~04 Emission太弱）
            if (_upgradeLevel >= 2 && _upgradeLevel <= 4)
            {
                foreach (var mat in _instanceMaterials)
                {
                    if (mat == null) continue;
                    // 加强Emission（乘以2.5倍）
                    if (mat.HasProperty("_EmissionColor"))
                    {
                        Color em = mat.GetColor("_EmissionColor");
                        mat.SetColor("_EmissionColor", em * 2.5f);
                    }
                    // 加强Rim（降低RimPower使边缘光更宽，提高亮度）
                    if (mat.HasProperty("_RimPower"))
                    {
                        float rp = mat.GetFloat("_RimPower");
                        mat.SetFloat("_RimPower", Mathf.Max(rp * 0.6f, 1.0f));
                    }
                    if (mat.HasProperty("_RimColor"))
                    {
                        Color rc = mat.GetColor("_RimColor");
                        mat.SetColor("_RimColor", rc * 1.3f);
                    }
                }
            }
        }

        // ==================== 升级金光特效 ====================
        //
        // 升级瞬间播放一道从脚底到头顶的金色光柱，比角色大2~3倍
        // 使用SpriteRenderer + 程序化纹理，1秒后自动销毁
        // 光柱从底部向上扩展，然后渐隐消失

        [Header("=== 升级金光 (Upgrade Flash) ===")]
        [Tooltip("金光宽度（相对于角色）")]
        [SerializeField] private float flashWidth = 0.8f;
        [Tooltip("金光高度倍数（相对于角色高度）")]
        [SerializeField] private float flashHeightMultiplier = 3f;
        [Tooltip("金光持续时间")]
        [SerializeField] private float flashDuration = 1.2f;

        private static Texture2D _sharedFlashTexture;
        private static Sprite _sharedFlashSprite;

        /// <summary>生成共享的渐变光柱纹理（所有实例共享）</summary>
        private static void EnsureSharedFlashTexture()
        {
            if (_sharedFlashTexture != null) return;

            // 16x64 垂直渐变纹理（底部亮，向上渐暗，中间最亮）
            int w = 16, h = 64;
            _sharedFlashTexture = new Texture2D(w, h, TextureFormat.RGBA32, false);
            _sharedFlashTexture.filterMode = FilterMode.Bilinear;
            _sharedFlashTexture.wrapMode = TextureWrapMode.Clamp;

            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                float t = y / (float)(h - 1); // 0=底部, 1=顶部

                // 亮度曲线：底部20%快速上升，中间50%最亮，顶部30%渐暗消散
                float brightness;
                if (t < 0.2f) brightness = t / 0.2f;          // 底部上升
                else if (t < 0.7f) brightness = 1f;            // 中段最亮
                else brightness = 1f - (t - 0.7f) / 0.3f;     // 顶部消散

                // 水平渐变：中间亮，边缘淡
                for (int x = 0; x < w; x++)
                {
                    float xNorm = (x - w * 0.5f) / (w * 0.5f); // -1 ~ +1
                    float edgeFade = 1f - Mathf.Abs(xNorm);
                    edgeFade = Mathf.Pow(edgeFade, 0.5f); // 柔和边缘

                    float alpha = brightness * edgeFade;
                    byte a = (byte)(Mathf.Clamp01(alpha) * 255);
                    byte b = (byte)(Mathf.Clamp01(brightness * 0.95f + 0.05f) * 255);
                    pixels[y * w + x] = new Color32(b, b, b, a);
                }
            }

            _sharedFlashTexture.SetPixels32(pixels);
            _sharedFlashTexture.Apply();

            _sharedFlashSprite = Sprite.Create(
                _sharedFlashTexture,
                new Rect(0, 0, w, h),
                new Vector2(0.5f, 0f), // pivot在底部中心
                16f
            );
        }

        /// <summary>播放升级金光特效</summary>
        private void SpawnUpgradeFlash(Color baseColor)
        {
            EnsureSharedFlashTexture();

            float unitHeight = GetUnitHeight();
            float flashHeight = unitHeight * flashHeightMultiplier;

            var flashGo = new GameObject("UpgradeFlash");
            flashGo.transform.SetParent(transform, false);
            flashGo.transform.localPosition = Vector3.zero; // 从脚底开始

            var sr = flashGo.AddComponent<SpriteRenderer>();
            sr.sprite = _sharedFlashSprite;
            sr.sortingOrder = 10;

            // 使用Additive材质
            var flashMat = new Material(Shader.Find("Sprites/Default"));
            if (flashMat != null)
            {
                flashMat.renderQueue = 3200;
            }
            sr.material = flashMat;

            // 金光颜色 = 升级颜色 + 加强亮度
            Color flashColor = Color.Lerp(baseColor, new Color(1f, 0.9f, 0.5f), 0.5f); // 偏金色
            flashColor.a = 0.9f;
            sr.color = flashColor;

            // 初始缩放：宽flashWidth，高从0开始
            flashGo.transform.localScale = new Vector3(flashWidth, 0f, 1f);

            // 启动动画协程
            StartCoroutine(AnimateUpgradeFlash(flashGo, sr, flashMat, flashHeight));
        }

        private System.Collections.IEnumerator AnimateUpgradeFlash(
            GameObject flashGo, SpriteRenderer sr, Material flashMat, float targetHeight)
        {
            if (flashGo == null) yield break;

            float elapsed = 0f;
            float riseTime = flashDuration * 0.4f;    // 40% 时间上升
            float holdTime = flashDuration * 0.2f;    // 20% 时间保持
            float fadeTime = flashDuration * 0.4f;    // 40% 时间消散
            Color baseColor = sr.color;

            // 阶段1：光柱从底部快速上升到顶部
            while (elapsed < riseTime)
            {
                if (flashGo == null) yield break;
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / riseTime);
                float easeT = 1f - Mathf.Pow(1f - t, 3f); // ease-out cubic

                float curHeight = targetHeight * easeT;
                flashGo.transform.localScale = new Vector3(flashWidth, curHeight * 0.25f, 1f);

                // 微弱闪烁
                float flicker = 0.85f + 0.15f * Mathf.Sin(elapsed * 20f);
                Color c = baseColor;
                c.a = baseColor.a * flicker;
                sr.color = c;

                yield return null;
            }

            // 阶段2：短暂保持
            float holdElapsed = 0f;
            while (holdElapsed < holdTime)
            {
                if (flashGo == null) yield break;
                holdElapsed += Time.deltaTime;
                float flicker = 0.9f + 0.1f * Mathf.Sin((elapsed + holdElapsed) * 15f);
                Color c = baseColor;
                c.a = baseColor.a * flicker;
                sr.color = c;
                yield return null;
            }

            // 阶段3：渐隐消散（从底部向上消失 + alpha降低）
            float fadeElapsed = 0f;
            while (fadeElapsed < fadeTime)
            {
                if (flashGo == null) yield break;
                fadeElapsed += Time.deltaTime;
                float t = Mathf.Clamp01(fadeElapsed / fadeTime);

                // alpha渐隐
                Color c = baseColor;
                c.a = baseColor.a * (1f - t);
                sr.color = c;

                // 宽度微扩散
                float widthExpand = flashWidth * (1f + t * 0.5f);
                flashGo.transform.localScale = new Vector3(widthExpand,
                    flashGo.transform.localScale.y * (1f - t * 0.3f), 1f);

                yield return null;
            }

            // 清理
            if (flashMat != null) Destroy(flashMat);
            if (flashGo != null) Destroy(flashGo);
        }

        // ==================== 静态工具 ====================

        /// <summary>从 tier 字符串转换为 int</summary>
        public static int ParseTier(string tierStr)
        {
            if (string.IsNullOrEmpty(tierStr)) return 0;
            if (int.TryParse(tierStr, out int tier)) return tier;
            switch (tierStr.ToLower())
            {
                case "basic": return 0;
                case "common": return 2;
                case "rare": return 3;
                case "epic": return 4;
                case "legendary": return 5;
                default: return 0;
            }
        }
    }
}
