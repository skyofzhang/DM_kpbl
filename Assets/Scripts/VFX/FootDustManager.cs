using System.Collections.Generic;
using UnityEngine;
using CapybaraDuel.Entity;

namespace CapybaraDuel.VFX
{
    /// <summary>
    /// 卡皮巴拉脚底推进烟雾 - 单例Manager，共享ParticleSystem
    /// 性能方案：只用一个 ParticleSystem，通过 Emit() 在指定位置发射
    /// 每0.3秒扫描一次，只给最前排20个单位（最可见的）发烟雾
    /// 动画片风格的小烟雾：卡通、短命、低透明度
    /// </summary>
    public class FootDustManager : MonoBehaviour
    {
        public static FootDustManager Instance { get; private set; }

        [Header("Scan Settings")]
        [Tooltip("扫描间隔（秒）")]
        [SerializeField] private float scanInterval = 0.2f;
        [Tooltip("每次最多发烟的单位数")]
        [SerializeField] private int maxUnitsPerScan = 25;
        [Tooltip("速度低于此值不发烟")]
        [SerializeField] private float minSpeedThreshold = 0.05f;

        [Header("Particle Config")]
        [SerializeField] private int particlesPerUnit = 3;
        [SerializeField] private float particleLifetime = 0.8f;
        [SerializeField] private float particleSize = 1.5f;
        [SerializeField] private float particleSpeed = 1.2f;

        [Header("Colors")]
        [SerializeField] private Color dustColor = new Color(0.92f, 0.9f, 0.85f, 0.4f); // 偏白色烟雾（地板上轻薄）

        [Header("Material (必须预设, Shader.Find在Build中不可用)")]
        [Tooltip("URP Particles/Unlit 透明材质，由SceneUpdater自动wire")]
        [SerializeField] public Material particleMaterial;

        private ParticleSystem _ps;
        private ParticleSystem.EmitParams _emitParams;
        private float _nextScanTime;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            CreateParticleSystem();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void CreateParticleSystem()
        {
            var psGo = new GameObject("FootDustPS");
            psGo.transform.SetParent(transform, false);

            _ps = psGo.AddComponent<ParticleSystem>();

            // 主模块
            var main = _ps.main;
            main.startLifetime = particleLifetime;
            main.startSpeed = new ParticleSystem.MinMaxCurve(particleSpeed * 0.3f, particleSpeed);
            main.startSize = new ParticleSystem.MinMaxCurve(particleSize * 0.5f, particleSize);
            main.startColor = dustColor;
            main.maxParticles = 300;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.1f; // 轻微上飘

            // 关闭默认发射
            var emission = _ps.emission;
            emission.rateOverTime = 0f;

            // 颜色渐变：快速淡出
            var col = _ps.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] {
                    new GradientColorKey(new Color(1f, 1f, 0.98f), 0f),      // 开始近白色
                    new GradientColorKey(new Color(0.95f, 0.88f, 0.75f), 0.4f), // 中段微暖黄
                    new GradientColorKey(new Color(0.85f, 0.82f, 0.75f), 1f)  // 结尾淡灰黄
                },
                new[] {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(dustColor.a, 0.1f),
                    new GradientAlphaKey(dustColor.a * 0.25f, 0.5f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            col.color = gradient;

            // 大小渐变：先膨胀后消散
            var sol = _ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f,
                new AnimationCurve(
                    new Keyframe(0f, 0.3f),
                    new Keyframe(0.2f, 1f),
                    new Keyframe(1f, 0.5f)));

            // 渲染器
            var renderer = psGo.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingOrder = -2; // 渲染在角色后面

            // 优先使用预设材质（Build中Shader.Find不可用）
            if (particleMaterial != null)
            {
                // 创建实例避免修改原始asset
                var matInst = new Material(particleMaterial);
                matInst.SetTexture("_BaseMap", GetOrCreateSmokeTexture());
                renderer.material = matInst;
            }
            else
            {
                renderer.material = GetDefaultParticleMaterial();
            }

            // 初始化发射参数
            _emitParams = new ParticleSystem.EmitParams();
        }

        /// <summary>运行时生成圆形渐变烟雾贴图（共享，64×64）</summary>
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
            if (_ps == null) return;
            if (Time.time < _nextScanTime) return;
            _nextScanTime = Time.time + scanInterval;

            ScanAndEmit();
        }

        private void ScanAndEmit()
        {
            // 收集左右阵营前排单位
            int emitted = 0;

            // 左阵营：按X坐标从大到小排（离橘子最近的排前面）
            EmitForCamp(Capybara.LeftAlive, true, ref emitted);

            // 右阵营：按X坐标从小到大排（离橘子最近的排前面）
            EmitForCamp(Capybara.RightAlive, false, ref emitted);
        }

        private void EmitForCamp(IReadOnlyList<Capybara> units, bool leftCamp, ref int emitted)
        {
            if (units == null || units.Count == 0) return;

            for (int i = 0; i < units.Count && emitted < maxUnitsPerScan; i++)
            {
                var unit = units[i];
                if (unit == null || !unit.gameObject.activeInHierarchy) continue;

                // 只有角色在前进（向橘子方向推进）时才发烟，倒退时不发
                if (!unit.IsAdvancing) continue;

                Vector3 pos = unit.transform.position;

                // 烟尘在角色身后发射（远离橘子方向），位置略偏后+偏低
                float backOffset = leftCamp ? -0.8f : 0.8f; // 左阵营后方是-X，右阵营后方是+X
                Vector3 behindPos = new Vector3(
                    pos.x + backOffset + Random.Range(-0.3f, 0.3f),
                    pos.y - 0.1f,
                    pos.z + Random.Range(-0.3f, 0.3f));

                _emitParams.position = behindPos;

                // 烟尘速度：向身后方向飘散 + 轻微上飘
                _emitParams.velocity = new Vector3(
                    backOffset * Random.Range(0.5f, particleSpeed),
                    Random.Range(0.1f, 0.4f),
                    Random.Range(-0.4f, 0.4f));

                _ps.Emit(_emitParams, particlesPerUnit);
                emitted++;
            }
        }
    }
}
