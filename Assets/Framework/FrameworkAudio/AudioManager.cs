using System.Collections;
using System.Collections.Generic;
using Framework.FrameworkSingleton;
using UnityEngine;

namespace Framework.FrameworkAudio
{
    public class AudioManager : MonoSingleton<AudioManager>
    {
        private Dictionary<EAudioType, AudioSource> _audioSources;
        public IReadOnlyDictionary<EAudioType,AudioSource> AudioSources => _audioSources;
        
        // BGM 切换专用变量
        private AudioSource _bgmSourceA;
        private AudioSource _bgmSourceB;
        private AudioSource _bgmOverrideSource;
        private bool _isUsingSourceA = true;
        private Coroutine _fadeCoroutine;
        private float _musicMixVolume = 1f;
        private float _bgmLevelA = 1f;
        private float _bgmLevelB = 1f;
        private float _bgmOverrideBlend;
        private float BaseMusicVolume => _musicMixVolume * (1f - _bgmOverrideBlend);

        protected override void OnInit()
        {
            SetDontDestroyOnLoad(true);
            InitializeAudioSources();
        }

        private void InitializeAudioSources()
        {
            _audioSources = new Dictionary<EAudioType, AudioSource>();

            foreach (EAudioType audioType in System.Enum.GetValues(typeof(EAudioType)))
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                
                // 默认设置
                if (audioType == EAudioType.Bgm)
                {
                    source.loop = true;
                    _bgmSourceA = source; // 初始 BGM 通道
                }
                
                _audioSources[audioType] = source;
            }

            // 额外创建一个 BGM 通道用于交叉淡化
            _bgmSourceB = gameObject.AddComponent<AudioSource>();
            _bgmSourceB.loop = true;
            _bgmSourceB.playOnAwake = false;

            _bgmOverrideSource = gameObject.AddComponent<AudioSource>();
            _bgmOverrideSource.loop = true;
            _bgmOverrideSource.playOnAwake = false;
        }

        /// <summary>
        /// 普通播放（音效或语音）
        /// </summary>
        public void PlayAudio(EAudioType audioType, AudioClip clip, float volume = 1.0f)
        {
            if (clip == null) return;

            if (_audioSources.TryGetValue(audioType, out var source))
            {
                // 如果是 SFX，建议使用 PlayOneShot 避免切断正在播放的同类音效
                if (audioType == EAudioType.Sfx)
                {
                    source.PlayOneShot(clip, volume);
                }
                else
                {
                    source.clip = clip;
                    if (audioType == EAudioType.Bgm) SetBgmLevel(source, volume);
                    else source.volume = volume;
                    source.Play();
                }
            }
        }

        /// <summary>
        /// 动态切换背景音乐
        /// </summary>
        /// <param name="clip">新片段</param>
        /// <param name="fadeTime">淡入淡出时长</param>
        /// <param name="startTime">新音乐开始的时间点</param>
        public void ChangeBgm(AudioClip clip, float fadeTime = 1.5f, float startTime = 0f)
        {
            if (clip == null) return;
            
            // 如果新老音频相同且正在播放，则跳过
            AudioSource current = _isUsingSourceA ? _bgmSourceA : _bgmSourceB;
            if (current.clip == clip && current.isPlaying) return;

            if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = StartCoroutine(CrossFadeBgmCoroutine(clip, fadeTime, startTime));
        }

        private IEnumerator CrossFadeBgmCoroutine(AudioClip clip, float duration, float startTime)
        {
            // 识别当前活跃通道和待切通道
            AudioSource activeSource = _isUsingSourceA ? _bgmSourceA : _bgmSourceB;
            AudioSource nextSource = _isUsingSourceA ? _bgmSourceB : _bgmSourceA;

            // 初始化新通道
            nextSource.clip = clip;
            nextSource.time = startTime;
            SetBgmLevel(nextSource, 0f);
            nextSource.Play();

            float elapsed = 0;
            float initialActiveVol = _isUsingSourceA ? _bgmLevelA : _bgmLevelB;

            // 交叉淡化
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / duration;

                SetBgmLevel(activeSource, Mathf.Lerp(initialActiveVol, 0, t));
                SetBgmLevel(nextSource, Mathf.Lerp(0, 1f, t));

                yield return null;
            }

            // 状态清理
            activeSource.Stop();
            SetBgmLevel(activeSource, 0f);
            SetBgmLevel(nextSource, 1f);

            // 关键：更新状态和字典引用，确保 AudioSettings 依然有效
            _isUsingSourceA = !_isUsingSourceA;
            _audioSources[EAudioType.Bgm] = nextSource;
            _fadeCoroutine = null;
        }

        public void AudioSettings(EAudioType audioType, float volume)
        {
            if (!_audioSources.TryGetValue(audioType, out var source)) return;
            if (audioType == EAudioType.Bgm) SetBgmLevel(source, volume);
            else source.volume = volume;
        }

        /// <summary>Blend in temporary music while the underlying BGM keeps its playback position.</summary>
        public void SetBgmOverride(AudioClip clip, float blend = 1f)
        {
            blend = clip == null ? 0f : Mathf.Clamp01(blend);
            bool clipChanged = _bgmOverrideSource.clip != clip;
            if (!clipChanged && _bgmOverrideBlend == blend) return;

            if (clipChanged)
            {
                _bgmOverrideSource.Stop();
                _bgmOverrideSource.clip = clip;
            }

            _bgmOverrideBlend = blend;
            ApplyMusicMix();
            if (clipChanged && clip != null) _bgmOverrideSource.Play();
        }

        public void SetMusicMixVolume(float volume)
        {
            _musicMixVolume = Mathf.Clamp01(volume);
            ApplyMusicMix();
        }

        private void ApplyMusicMix()
        {
            _bgmSourceA.volume = _bgmLevelA * BaseMusicVolume;
            _bgmSourceB.volume = _bgmLevelB * BaseMusicVolume;
            _bgmOverrideSource.volume = _bgmOverrideBlend * _musicMixVolume;
        }

        private void SetBgmLevel(AudioSource source, float level)
        {
            level = Mathf.Clamp01(level);
            if (source == _bgmSourceA) _bgmLevelA = level;
            else _bgmLevelB = level;
            source.volume = level * BaseMusicVolume;
        }

        public bool IsAudioPlaying(EAudioType audioType)
        {
            return _audioSources.ContainsKey(audioType) && _audioSources[audioType].isPlaying;
        }
        
        public void StopAudio(EAudioType audioType)
        {
            if (audioType == EAudioType.Bgm)
            {
                if (_fadeCoroutine != null)
                {
                    StopCoroutine(_fadeCoroutine);
                    _fadeCoroutine = null;
                }
                _bgmSourceA.Stop();
                _bgmSourceB.Stop();
                SetBgmOverride(null);
                return;
            }

            if (_audioSources.TryGetValue(audioType, out var source))
            {
                source.Stop();
            }
        }

        
    }

    public enum EAudioType
    {
        Bgm,
        Sfx,
        Voice,
    }
}
