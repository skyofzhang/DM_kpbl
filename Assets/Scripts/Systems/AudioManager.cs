using UnityEngine;
using System.Collections.Generic;

namespace CapybaraDuel.Systems
{
    /// <summary>
    /// 音效管理器 - BGM(分阶段)+SFX(按名称+并发限制)
    /// BGM: battleStart / normalBattle / nearVictory
    /// SFX: player_join / vip_join / game_start / victory / pushback /
    ///      ui_click / unit_spawn / pushing / push_force / upgrade
    ///
    /// SFX并发限制: 同时播放不超过 maxConcurrentSFX (默认5)
    /// 音量通过 SetBGMVolume/SetSFXVolume 控制，数值保存到PlayerPrefs
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Audio Sources")]
        public AudioSource bgmSource;
        public AudioSource sfxSource;

        [Header("BGM Clips")]
        public AudioClip bgmBattleStart;
        public AudioClip bgmNormalBattle;
        public AudioClip bgmNearVictory;

        [Header("SFX Clips")]
        public AudioClip sfxPlayerJoin;
        public AudioClip sfxVipJoin;
        public AudioClip sfxGameStart;
        public AudioClip sfxVictory;
        public AudioClip sfxPushback;
        public AudioClip sfxUIClick;
        public AudioClip sfxUnitSpawn;
        public AudioClip sfxPushing;
        public AudioClip sfxPushForce;
        public AudioClip sfxUpgrade;
        public AudioClip sfxCountdown;

        [Header("Settings")]
        public int maxConcurrentSFX = 5;

        // 当前活跃SFX数量追踪
        private int _activeSFXCount = 0;
        private Dictionary<string, AudioClip> _sfxMap;
        private Dictionary<string, AudioClip> _bgmMap;

        // 音量 (0~1)
        private float _bgmVolume = 0.6f;
        private float _sfxVolume = 0.8f;
        private bool _bgmEnabled = true;
        private bool _sfxEnabled = true;

        // BGM状态
        private string _currentBGMName = "";

        // PlayerPrefs keys
        private const string KEY_BGM_VOL = "AudioBGMVolume";
        private const string KEY_SFX_VOL = "AudioSFXVolume";
        private const string KEY_BGM_ON = "AudioBGMEnabled";
        private const string KEY_SFX_ON = "AudioSFXEnabled";

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // 确保有AudioSource
            if (bgmSource == null)
            {
                bgmSource = gameObject.AddComponent<AudioSource>();
                bgmSource.playOnAwake = false;
                bgmSource.loop = true;
                bgmSource.spatialBlend = 0;
            }
            if (sfxSource == null)
            {
                sfxSource = gameObject.AddComponent<AudioSource>();
                sfxSource.playOnAwake = false;
                sfxSource.loop = false;
                sfxSource.spatialBlend = 0;
            }

            // 加载保存的音量设置
            _bgmVolume = PlayerPrefs.GetFloat(KEY_BGM_VOL, 0.6f);
            _sfxVolume = PlayerPrefs.GetFloat(KEY_SFX_VOL, 0.8f);
            _bgmEnabled = PlayerPrefs.GetInt(KEY_BGM_ON, 1) == 1;
            _sfxEnabled = PlayerPrefs.GetInt(KEY_SFX_ON, 1) == 1;

            bgmSource.volume = _bgmEnabled ? _bgmVolume : 0;
            sfxSource.volume = _sfxEnabled ? _sfxVolume : 0;

            BuildMaps();
        }

        private void BuildMaps()
        {
            // 自动从 Resources/Audio/ 加载（如果场景引用为空则fallback到Resources）
            LoadFromResources();

            _sfxMap = new Dictionary<string, AudioClip>
            {
                { "player_join", sfxPlayerJoin },
                { "vip_join", sfxVipJoin },
                { "game_start", sfxGameStart },
                { "victory", sfxVictory },
                { "pushback", sfxPushback },
                { "ui_click", sfxUIClick },
                { "unit_spawn", sfxUnitSpawn },
                { "pushing", sfxPushing },
                { "push_force", sfxPushForce },
                { "upgrade", sfxUpgrade },
                { "countdown", sfxCountdown }
            };

            _bgmMap = new Dictionary<string, AudioClip>
            {
                { "battle_start", bgmBattleStart },
                { "normal_battle", bgmNormalBattle },
                { "near_victory", bgmNearVictory }
            };

            // 统计加载结果
            int bgmLoaded = 0, sfxLoaded = 0;
            foreach (var kv in _bgmMap) if (kv.Value != null) bgmLoaded++;
            foreach (var kv in _sfxMap) if (kv.Value != null) sfxLoaded++;
            Debug.Log($"[AudioManager] Loaded {bgmLoaded}/{_bgmMap.Count} BGM, {sfxLoaded}/{_sfxMap.Count} SFX");
        }

        /// <summary>从 Resources/Audio/BGM 和 Resources/Audio/SFX 自动加载音频文件</summary>
        private void LoadFromResources()
        {
            // BGM: Resources/Audio/BGM/{name}
            if (bgmBattleStart == null) bgmBattleStart = Resources.Load<AudioClip>("Audio/BGM/battle_start");
            if (bgmNormalBattle == null) bgmNormalBattle = Resources.Load<AudioClip>("Audio/BGM/normal_battle");
            if (bgmNearVictory == null) bgmNearVictory = Resources.Load<AudioClip>("Audio/BGM/near_victory");

            // SFX: Resources/Audio/SFX/{name}
            if (sfxPlayerJoin == null) sfxPlayerJoin = Resources.Load<AudioClip>("Audio/SFX/player_join");
            if (sfxVipJoin == null) sfxVipJoin = Resources.Load<AudioClip>("Audio/SFX/vip_join");
            if (sfxGameStart == null) sfxGameStart = Resources.Load<AudioClip>("Audio/SFX/game_start");
            if (sfxVictory == null) sfxVictory = Resources.Load<AudioClip>("Audio/SFX/victory");
            if (sfxPushback == null) sfxPushback = Resources.Load<AudioClip>("Audio/SFX/pushback");
            if (sfxUIClick == null) sfxUIClick = Resources.Load<AudioClip>("Audio/SFX/ui_click");
            if (sfxUnitSpawn == null) sfxUnitSpawn = Resources.Load<AudioClip>("Audio/SFX/unit_spawn");
            if (sfxPushing == null) sfxPushing = Resources.Load<AudioClip>("Audio/SFX/pushing");
            if (sfxPushForce == null) sfxPushForce = Resources.Load<AudioClip>("Audio/SFX/push_force");
            if (sfxUpgrade == null) sfxUpgrade = Resources.Load<AudioClip>("Audio/SFX/upgrade");
            if (sfxCountdown == null) sfxCountdown = Resources.Load<AudioClip>("Audio/SFX/countdown");
        }

        // ==================== SFX ====================

        /// <summary>按名称播放SFX（受并发限制）</summary>
        public void PlaySFX(string sfxName)
        {
            if (!_sfxEnabled) return;
            if (_activeSFXCount >= maxConcurrentSFX) return;
            if (sfxSource == null) return;

            if (_sfxMap == null) BuildMaps();

            AudioClip clip = null;
            if (_sfxMap.TryGetValue(sfxName, out clip) && clip != null)
            {
                sfxSource.PlayOneShot(clip, _sfxVolume);
                _activeSFXCount++;
                StartCoroutine(DecrementSFXCount(clip.length));
            }
        }

        /// <summary>直接播放AudioClip（受并发限制）</summary>
        public void PlaySFX(AudioClip clip)
        {
            if (!_sfxEnabled || clip == null || sfxSource == null) return;
            if (_activeSFXCount >= maxConcurrentSFX) return;

            sfxSource.PlayOneShot(clip, _sfxVolume);
            _activeSFXCount++;
            StartCoroutine(DecrementSFXCount(clip.length));
        }

        private System.Collections.IEnumerator DecrementSFXCount(float delay)
        {
            yield return new WaitForSeconds(delay);
            _activeSFXCount = Mathf.Max(0, _activeSFXCount - 1);
        }

        // ==================== BGM ====================

        /// <summary>按名称播放BGM（battle_start / normal_battle / near_victory）</summary>
        public void PlayBGM(string bgmName)
        {
            if (bgmSource == null) return;
            if (_currentBGMName == bgmName && bgmSource.isPlaying) return;

            if (_bgmMap == null) BuildMaps();

            AudioClip clip = null;
            if (_bgmMap.TryGetValue(bgmName, out clip) && clip != null)
            {
                bgmSource.clip = clip;
                bgmSource.loop = true;
                bgmSource.volume = _bgmEnabled ? _bgmVolume : 0;
                bgmSource.Play();
                _currentBGMName = bgmName;
            }
        }

        /// <summary>直接播放AudioClip作为BGM</summary>
        public void PlayBGM(AudioClip clip)
        {
            if (bgmSource == null || clip == null) return;
            bgmSource.clip = clip;
            bgmSource.loop = true;
            bgmSource.volume = _bgmEnabled ? _bgmVolume : 0;
            bgmSource.Play();
            _currentBGMName = clip.name;
        }

        public void StopBGM()
        {
            if (bgmSource != null)
            {
                bgmSource.Stop();
                _currentBGMName = "";
            }
        }

        /// <summary>BGM淡入淡出切换</summary>
        public void CrossfadeBGM(string bgmName, float fadeDuration = 1f)
        {
            if (_currentBGMName == bgmName && bgmSource.isPlaying) return;
            StartCoroutine(CrossfadeCoroutine(bgmName, fadeDuration));
        }

        private System.Collections.IEnumerator CrossfadeCoroutine(string bgmName, float duration)
        {
            float half = duration * 0.5f;
            float startVol = bgmSource.volume;

            // Fade out
            float elapsed = 0;
            while (elapsed < half)
            {
                elapsed += Time.deltaTime;
                bgmSource.volume = Mathf.Lerp(startVol, 0, elapsed / half);
                yield return null;
            }

            // Switch
            PlayBGM(bgmName);

            // Fade in
            float targetVol = _bgmEnabled ? _bgmVolume : 0;
            elapsed = 0;
            while (elapsed < half)
            {
                elapsed += Time.deltaTime;
                bgmSource.volume = Mathf.Lerp(0, targetVol, elapsed / half);
                yield return null;
            }
            bgmSource.volume = targetVol;
        }

        // ==================== 音量控制 ====================

        public float BGMVolume
        {
            get => _bgmVolume;
            set
            {
                _bgmVolume = Mathf.Clamp01(value);
                if (bgmSource != null && _bgmEnabled)
                    bgmSource.volume = _bgmVolume;
                PlayerPrefs.SetFloat(KEY_BGM_VOL, _bgmVolume);
            }
        }

        public float SFXVolume
        {
            get => _sfxVolume;
            set
            {
                _sfxVolume = Mathf.Clamp01(value);
                if (sfxSource != null && _sfxEnabled)
                    sfxSource.volume = _sfxVolume;
                PlayerPrefs.SetFloat(KEY_SFX_VOL, _sfxVolume);
            }
        }

        public bool BGMEnabled
        {
            get => _bgmEnabled;
            set
            {
                _bgmEnabled = value;
                if (bgmSource != null)
                    bgmSource.volume = _bgmEnabled ? _bgmVolume : 0;
                PlayerPrefs.SetInt(KEY_BGM_ON, _bgmEnabled ? 1 : 0);
            }
        }

        public bool SFXEnabled
        {
            get => _sfxEnabled;
            set
            {
                _sfxEnabled = value;
                if (sfxSource != null)
                    sfxSource.volume = _sfxEnabled ? _sfxVolume : 0;
                PlayerPrefs.SetInt(KEY_SFX_ON, _sfxEnabled ? 1 : 0);
            }
        }

        public void SetBGMVolume(float volume)
        {
            BGMVolume = volume;
        }

        public void SetSFXVolume(float volume)
        {
            SFXVolume = volume;
        }
    }
}
