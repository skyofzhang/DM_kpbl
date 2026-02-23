using UnityEngine;

namespace CapybaraDuel.VFX
{
    /// <summary>
    /// 橘子移动特效 - 粒子从橘子移动方向的反面喷出
    /// 粒子带拖尾效果，大小随速度变化
    /// 视觉：像橘子在跑步，身后扬起沙尘粒子
    /// </summary>
    public class OrangeDustTrail : MonoBehaviour
    {
        [Header("Emission Control")]
        [Tooltip("速度低于此值时不发射")]
        [SerializeField] private float minVelocityThreshold = 0.05f;
        [Tooltip("最大发射速率（个/秒）")]
        [SerializeField] private float maxEmissionRate = 20f;
        [Tooltip("速度到发射速率的转换系数")]
        [SerializeField] private float velocityToEmission = 12f;

        [Header("Particle Config")]
        [SerializeField] private float particleLifetime = 1.0f;
        [Tooltip("基础粒子大小（速度=0时）")]
        [SerializeField] private float baseParticleSize = 0.8f;
        [Tooltip("速度对粒子大小的增益系数")]
        [SerializeField] private float speedSizeBonus = 0.6f;
        [Tooltip("粒子最大大小上限")]
        [SerializeField] private float maxParticleSize = 2.5f;
        [SerializeField] private float particleSpeed = 1.5f;
        [Tooltip("烟尘偏移距离（橘子后方）")]
        [SerializeField] private float trailOffsetDistance = 2.0f;

        [Header("Trail (拖尾)")]
        [Tooltip("拖尾长度（粒子寿命比例）")]
        [SerializeField] private float trailRatio = 0.3f;
        [Tooltip("拖尾宽度（粒子大小比例）")]
        [SerializeField] private float trailWidthRatio = 0.4f;

        [Header("Colors")]
        [SerializeField] private Color dustColorStart = new Color(0.82f, 0.72f, 0.55f, 0.55f);
        [SerializeField] private Color dustColorEnd = new Color(0.65f, 0.55f, 0.4f, 0f);

        [Header("Material")]
        [Tooltip("URP粒子材质，由SceneUpdater自动wire")]
        [SerializeField] public Material particleMaterial;

        private ParticleSystem _ps;
        private Systems.OrangeController _orangeController;
        private bool _initialized;

        private void Awake()
        {
            _orangeController = GetComponent<Systems.OrangeController>();
            if (_orangeController == null)
            {
                enabled = false;
                return;
            }

            CreateParticleSystem();
            _initialized = (_ps != null);
        }

        private void CreateParticleSystem()
        {
            var psGo = new GameObject("DustTrail");
            psGo.transform.SetParent(transform, false);
            psGo.transform.localPosition = new Vector3(0, -0.2f, 0);

            _ps = psGo.AddComponent<ParticleSystem>();

            // 主模块
            var main = _ps.main;
            main.startLifetime = particleLifetime;
            main.startSpeed = new ParticleSystem.MinMaxCurve(particleSpeed * 0.3f, particleSpeed);
            main.startSize = new ParticleSystem.MinMaxCurve(baseParticleSize * 0.6f, baseParticleSize);
            main.startColor = dustColorStart;
            main.maxParticles = 60;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.1f; // 轻微上飘

            // 发射模块
            var emission = _ps.emission;
            emission.rateOverTime = 0f;

            // 形状：小球形发射区域
            var shape = _ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.3f;

            // 颜色渐变
            var col = _ps.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] {
                    new GradientColorKey(dustColorStart, 0f),
                    new GradientColorKey(dustColorEnd, 1f)
                },
                new[] {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(dustColorStart.a, 0.1f),
                    new GradientAlphaKey(dustColorStart.a * 0.6f, 0.4f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            col.color = gradient;

            // 大小渐变
            var sol = _ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f,
                new AnimationCurve(
                    new Keyframe(0f, 0.5f),
                    new Keyframe(0.2f, 1f),
                    new Keyframe(0.7f, 0.8f),
                    new Keyframe(1f, 0.2f)));

            // 拖尾模块（粒子自带小尾巴）
            var trails = _ps.trails;
            trails.enabled = true;
            trails.ratio = 1f; // 所有粒子都有拖尾
            trails.lifetime = trailRatio;
            trails.minVertexDistance = 0.1f;
            trails.worldSpace = true;
            trails.dieWithParticles = true;
            trails.widthOverTrail = new ParticleSystem.MinMaxCurve(1f,
                new AnimationCurve(
                    new Keyframe(0f, 1f),
                    new Keyframe(1f, 0f)));
            // 拖尾颜色渐变
            var trailGradient = new Gradient();
            trailGradient.SetKeys(
                new[] {
                    new GradientColorKey(dustColorStart, 0f),
                    new GradientColorKey(dustColorEnd, 1f)
                },
                new[] {
                    new GradientAlphaKey(dustColorStart.a * 0.4f, 0f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            trails.colorOverLifetime = trailGradient;

            // 渲染器
            var renderer = psGo.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingOrder = -1;

            // 拖尾渲染
            renderer.trailMaterial = GetParticleMaterial();
            renderer.material = GetParticleMaterial();
        }

        private Material GetParticleMaterial()
        {
            if (particleMaterial != null)
            {
                var matInst = new Material(particleMaterial);
                matInst.SetTexture("_BaseMap", GetOrCreateSmokeTexture());
                return matInst;
            }
            return GetDefaultParticleMaterial();
        }

        private static Texture2D _sharedSmokeTexture;
        private static Texture2D GetOrCreateSmokeTexture()
        {
            if (_sharedSmokeTexture != null) return _sharedSmokeTexture;

            int size = 64;
            _sharedSmokeTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            _sharedSmokeTexture.filterMode = FilterMode.Bilinear;
            _sharedSmokeTexture.wrapMode = TextureWrapMode.Clamp;

            float center = size * 0.5f;
            float maxDist = center;
            var pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center + 0.5f;
                    float dy = y - center + 0.5f;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy) / maxDist;
                    float alpha = Mathf.Clamp01(1f - dist * dist);
                    alpha *= alpha;
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            _sharedSmokeTexture.SetPixels(pixels);
            _sharedSmokeTexture.Apply(false, true);
            return _sharedSmokeTexture;
        }

        private Material GetDefaultParticleMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader != null)
            {
                var mat = new Material(shader);
                mat.SetFloat("_Surface", 1);
                mat.SetFloat("_Blend", 0);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = 3000;
                mat.SetTexture("_BaseMap", GetOrCreateSmokeTexture());
                return mat;
            }

            shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");
            if (shader != null)
            {
                var mat = new Material(shader);
                mat.SetTexture("_MainTex", GetOrCreateSmokeTexture());
                return mat;
            }
            return null;
        }

        private void Update()
        {
            if (!_initialized || _orangeController == null || _ps == null) return;

            float velocity = _orangeController.GetVelocity();
            float absVel = Mathf.Abs(velocity);

            var emission = _ps.emission;
            var shape = _ps.shape;
            var main = _ps.main;

            if (absVel < minVelocityThreshold)
            {
                emission.rateOverTime = 0f;
                return;
            }

            // 发射速率随速度增加
            float rate = Mathf.Clamp(absVel * velocityToEmission, 0, maxEmissionRate);
            emission.rateOverTime = rate;

            // 粒子大小随速度增加（但有上限）
            float dynamicSize = Mathf.Min(baseParticleSize + absVel * speedSizeBonus, maxParticleSize);
            main.startSize = new ParticleSystem.MinMaxCurve(dynamicSize * 0.5f, dynamicSize);

            // 烟尘在橘子移动方向的反面发射
            float behindX = -Mathf.Sign(velocity) * trailOffsetDistance;
            shape.position = new Vector3(behindX, 0, 0);
        }
    }
}
