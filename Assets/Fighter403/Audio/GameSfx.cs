using System;
using System.Collections;
using System.Collections.Generic;
using Fighter403.Player;
using Framework.FrameworkAudio;
using UnityEngine;

namespace Fighter403.Audio
{
    internal enum Sfx
    {
        BadLanding,
        BadTarget,
        BlackHoleCollapse,
        BlackHoleHit,
        BlackHoleOpen,
        BlackHolePull,
        BossDeploy,
        BossDown,
        BossEnter,
        BossMeteorLand,
        BossMeteorWarn,
        BossPhase,
        BossPortal,
        BossRainFire,
        BossRainHit,
        BossRainPass,
        BossRainWarn,
        BossSealRelease,
        BossSealWarn,
        BossVortexEnd,
        BossVortexOpen,
        BossVortexPull,
        CardCancel,
        CardDraw,
        CardGuaranteed,
        CardRecover,
        CardSelect,
        CardShuffle,
        CardSubmit,
        CastBurn,
        ClusterBreak,
        ClusterHit,
        ClusterLink,
        ClusterSurge,
        DashHit,
        DashPass,
        DashStart,
        DashStop,
        DebuggerAcquire,
        DualEnd,
        DualPair,
        DualStart,
        DummyDown,
        DummyHit,
        EnemyDown,
        EnemyEngage,
        EnemyHit,
        EnemyShield,
        EnemyShieldBreak,
        EnemySlash,
        EnemySplit,
        EnemyWindup,
        EnergyDenied,
        EnergyReady,
        FlurryFinishHit,
        FlurryFinishSlash,
        FlurryHit,
        FlurrySlash,
        FlurryStart,
        Fracture,
        FragmentSpawn,
        GuardUp,
        HandOpen,
        LightSlash,
        MirrorAttack,
        MirrorCopy,
        MirrorWarn,
        PileClose,
        PileOpen,
        PlanConfirm,
        PlanUndo,
        PlayerBlock,
        PlayerBlockBreak,
        PlayerDown,
        PlayerHurt,
        PlayerMove,
        ProjectileHit,
        ProjectilePass,
        ProjectileWall,
        ProtocolHeal,
        QueueClear,
        QueueMove,
        Resonance,
        RiftSlash,
        SandevistanEnd,
        SandevistanLoop,
        SandevistanStart,
        ShooterAim,
        ShooterFire,
        SlashPrep,
        Slow,
        SpinSlash,
        StarfallHit,
        StarfallLand,
        StarfallWarn,
        Stun,
        SwarmAttack,
        SwarmRush,
        TargetClear,
        TargetLock,
        TargetSwitch,
        TrialReset,
        TrialSwitch,
        BigBangBurst,
        BigBangCharge,
        DungeonBreach,
        DungeonCardRemoved,
        DungeonComplete,
        DungeonDenied,
        DungeonDescent,
        DungeonMerchant,
        DungeonPurchase,
        DungeonReward,
        DungeonRoomCleared,
        DungeonTraverse,
        DungeonTreasureOpen,
        PlayerHeal,
        WhiteHoleRelease,
        MenuFocus,
        MenuConfirm,
        MenuBack,
        MenuStart,
    }

    internal static class GameSfx
    {
        private const string ResourcePath = "Fighter403/Sfx";
        private const float SfxGain = 0.72f;
        private const float LoopFadeInSeconds = 0.09f;
        private const float LoopFadeOutSeconds = 0.13f;
        private const float VoiceFadeOutSeconds = 0.018f;
        private const float OneShotAttackSeconds = 0.0008f;
        private const float OneShotReleaseSeconds = 0.012f;
        private const float MinimumPlaybackRate = 0.38f;
        private const float PlaybackRateCurve = 0.38f;
        private const float PlaybackRateSmoothingSeconds = 0.028f;
        private const int MaximumOneShots = 24;
        private const int FadeOutVoiceReserve = 4;
        private const float LoopImpactDuckSeconds = 0.085f;
        private const float LoopImpactGain = 0.66f;
        private static readonly Dictionary<Sfx, List<AudioClip>> clips = new();
        private static readonly Dictionary<AudioClip, AudioClip> oneShots = new();
        private static readonly Dictionary<Sfx, int> previousTake = new();
        private static readonly Dictionary<Sfx, double> lastPlayed = new();
        private static readonly System.Random random = new();
        private static bool loaded;
        private static AudioManager playbackManager;
        private static AudioSource sfxChannel;
        private static readonly List<Voice> voices = new(MaximumOneShots + FadeOutVoiceReserve);
        private static readonly List<Loop> loops = new();
        private static float combatRate = 1f;
        private static float enemyRate = 1f;
        private static double duckLoopsUntil;
        private static double dspBufferGraceSeconds;

        private enum Clock { Interface, Combat, Enemy }

        private sealed class Voice
        {
            internal AudioSource Source;
            internal SfxEnvelope Envelope;
            internal Clock Clock;
            internal float Gain;
            internal double StartedAt;
            internal bool Releasing;
            private double releaseDeadline;

            internal bool ReleaseFinished => Releasing && (Envelope.Released || AudioSettings.dspTime >= releaseDeadline);

            internal void Release(float seconds)
            {
                if (Releasing) return;
                Releasing = true;
                Envelope.Release(seconds);
                // Virtual audio sources may no longer receive filter callbacks.
                releaseDeadline = AudioSettings.dspTime + seconds + dspBufferGraceSeconds;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            foreach (AudioClip clip in oneShots.Values)
                if (clip != null) UnityEngine.Object.Destroy(clip);
            oneShots.Clear();
            clips.Clear();
            previousTake.Clear();
            lastPlayed.Clear();
            voices.Clear();
            loops.Clear();
            playbackManager = null;
            sfxChannel = null;
            combatRate = enemyRate = 1f;
            duckLoopsUntil = 0d;
            loaded = false;
        }

        internal static void WarmUp()
        {
            if (loaded) return;
            loaded = true;
            foreach (var clip in Resources.LoadAll<AudioClip>(ResourcePath))
            {
                string name = clip.name;
                int separator = name.IndexOf('_');
                if (separator >= 0) name = name.Substring(0, separator);
                if (!Enum.TryParse(name, out Sfx cue)) continue;
                if (clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
                if (cue is not (Sfx.BlackHolePull or Sfx.BossVortexPull or Sfx.SandevistanLoop))
                    PrepareOneShot(clip);
                if (!clips.TryGetValue(cue, out var takes))
                {
                    takes = new List<AudioClip>();
                    clips.Add(cue, takes);
                }
                takes.Add(clip);
            }
        }

        internal static void Play(Sfx cue, float volume = 1f, float minimumInterval = 0.045f)
        {
            if (!Application.isPlaying) return;
            double now = Time.unscaledTimeAsDouble;
            if (lastPlayed.TryGetValue(cue, out double last) && now - last < minimumInterval) return;
            var clip = Take(cue);
            if (clip == null) return;
            EnsurePlayback();
            Voice voice = TakeVoice();
            if (voice == null) return;
            lastPlayed[cue] = now;
            voice.Clock = SoundClock(cue);
            voice.Gain = SfxGain * volume;
            voice.StartedAt = AudioSettings.dspTime;
            voice.Releasing = false;
            voice.Source.clip = PrepareOneShot(clip);
            voice.Envelope.Begin(0f);
            UpdateVoice(voice);
            if (EmphasizesImpact(cue))
            {
                duckLoopsUntil = voice.StartedAt + LoopImpactDuckSeconds;
                foreach (Loop loop in loops) loop.SetGain(LoopImpactGain);
            }
            voice.Source.Play();
        }

        internal sealed class Loop
        {
            private readonly Voice voice;
            private readonly GameObject owner;

            private Loop(Voice voice, GameObject owner)
            {
                this.voice = voice;
                this.owner = owner;
            }

            internal static Loop Create(Sfx cue, AudioClip clip, GameObject owner)
            {
                Voice voice = CreateVoice(clip.name);
                voice.Clock = SoundClock(cue);
                voice.Gain = SfxGain;
                voice.Source.loop = true;
                voice.Source.clip = clip;
                voice.Envelope.Begin(LoopFadeInSeconds);
                var loop = new Loop(voice, owner);
                loop.SetGain(AudioSettings.dspTime < duckLoopsUntil ? LoopImpactGain : 1f);
                UpdateVoice(voice);
                voice.Source.Play();
                return loop;
            }

            internal void Stop() => voice.Release(LoopFadeOutSeconds);

            internal void SetGain(float gain) => voice.Envelope.SetGain(gain);

            internal bool Update()
            {
                if (owner == null || !owner.activeInHierarchy) Stop();
                if (voice.ReleaseFinished || !voice.Source.isPlaying)
                {
                    voice.Source.Stop();
                    UnityEngine.Object.Destroy(voice.Source.gameObject);
                    return false;
                }
                UpdateVoice(voice);
                return true;
            }
        }

        internal static Loop StartLoop(Sfx cue, GameObject owner)
        {
            if (!Application.isPlaying || owner == null || !owner.activeInHierarchy) return null;
            var clip = Take(cue);
            if (clip == null) return null;
            EnsurePlayback();
            var loop = Loop.Create(cue, clip, owner);
            loops.Add(loop);
            return loop;
        }

        internal static void StopLoop(ref Loop loop)
        {
            loop?.Stop();
            loop = null;
        }

        private static AudioClip PrepareOneShot(AudioClip clip)
        {
            if (oneShots.TryGetValue(clip, out AudioClip prepared)) return prepared;
            if (clip.loadState != AudioDataLoadState.Loaded) return clip;
            var samples = new float[clip.samples * clip.channels];
            if (!clip.GetData(samples, 0)) return clip;

            // Apply the natural envelope in clip time so it follows playback speed.
            ApplyOneShotFade(samples, clip, OneShotAttackSeconds, false);
            ApplyOneShotFade(samples, clip, OneShotReleaseSeconds, true);
            prepared = AudioClip.Create(clip.name, clip.samples, clip.channels, clip.frequency, false);
            if (!prepared.SetData(samples, 0))
            {
                UnityEngine.Object.Destroy(prepared);
                return clip;
            }
            oneShots.Add(clip, prepared);
            return prepared;
        }

        private static void ApplyOneShotFade(float[] samples, AudioClip clip, float seconds, bool fromEnd)
        {
            int length = Mathf.Min(clip.samples, Mathf.RoundToInt(clip.frequency * seconds));
            for (int i = 0; i < length; i++)
            {
                float level = (float)SfxEnvelope.Smooth((double)i / length);
                int position = (fromEnd ? clip.samples - 1 - i : i) * clip.channels;
                for (int channel = 0; channel < clip.channels; channel++) samples[position + channel] *= level;
            }
        }

        private static void EnsurePlayback()
        {
            if (playbackManager != null) return;
            playbackManager = AudioManager.Instance;
            sfxChannel = playbackManager.AudioSources[EAudioType.Sfx];
            voices.Clear();
            loops.Clear();
            combatRate = PlaybackRate(Time.timeScale);
            enemyRate = PlaybackRate(EnemyTimeScale);
            AudioSettings.GetDSPBufferSize(out int bufferFrames, out _);
            dspBufferGraceSeconds = 2d * bufferFrames / Mathf.Max(1, AudioSettings.outputSampleRate);
            playbackManager.StartCoroutine(FollowGameTime());
        }

        private static float EnemyTimeScale => Time.timeScale *
            (CombatTarget.BattleVitals?.SandevistanActive == true ? CombatVitals.SandevistanEnemyScale : 1f);

        // Compress very low gameplay rates so the hit transient stays audible through hitstop.
        private static float PlaybackRate(float gameScale) =>
            Mathf.Max(MinimumPlaybackRate, Mathf.Pow(Mathf.Max(0f, gameScale), PlaybackRateCurve));

        private static float Rate(Clock clock) => clock switch
        {
            Clock.Interface => 1f,
            Clock.Enemy => enemyRate,
            _ => combatRate,
        };

        private static IEnumerator FollowGameTime()
        {
            while (true)
            {
                float blend = 1f - Mathf.Exp(-Time.unscaledDeltaTime / PlaybackRateSmoothingSeconds);
                combatRate = Mathf.Lerp(combatRate, PlaybackRate(Time.timeScale), blend);
                enemyRate = Mathf.Lerp(enemyRate, PlaybackRate(EnemyTimeScale), blend);
                foreach (Voice voice in voices)
                {
                    if (!voice.Source.isPlaying) continue;
                    if (voice.ReleaseFinished) voice.Source.Stop();
                    else UpdateVoice(voice);
                }
                float loopGain = AudioSettings.dspTime < duckLoopsUntil ? LoopImpactGain : 1f;
                for (int i = loops.Count - 1; i >= 0; i--)
                {
                    if (loops[i].Update()) loops[i].SetGain(loopGain);
                    else loops.RemoveAt(i);
                }
                yield return null;
            }
        }

        private static void UpdateVoice(Voice voice)
        {
            voice.Source.pitch = Rate(voice.Clock);
            voice.Source.volume = voice.Gain * sfxChannel.volume;
        }

        private static Voice CreateVoice(string name)
        {
            // A filter belongs to one audio source, so keep each voice on its own object.
            var voiceObject = new GameObject(name);
            voiceObject.transform.SetParent(playbackManager.transform, false);
            var source = voiceObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.outputAudioMixerGroup = sfxChannel.outputAudioMixerGroup;
            return new Voice { Source = source, Envelope = voiceObject.AddComponent<SfxEnvelope>() };
        }

        private static Voice TakeVoice()
        {
            int activeVoices = 0;
            Voice available = null;
            Voice oldest = null;
            foreach (Voice voice in voices)
            {
                if (!voice.Source.isPlaying)
                {
                    available ??= voice;
                    continue;
                }
                if (voice.Releasing) continue;
                activeVoices++;
                if (oldest == null || voice.StartedAt < oldest.StartedAt) oldest = voice;
            }
            if (available == null)
            {
                if (voices.Count >= MaximumOneShots + FadeOutVoiceReserve) return null;
                available = CreateVoice("SFX voice");
                voices.Add(available);
            }
            if (activeVoices >= MaximumOneShots) oldest.Release(VoiceFadeOutSeconds);
            return available;
        }

        private static bool EmphasizesImpact(Sfx cue) => cue is Sfx.PlayerBlockBreak or Sfx.EnemyShieldBreak
            or Sfx.FlurryFinishHit or Sfx.StarfallLand or Sfx.BlackHoleCollapse or Sfx.BossMeteorLand
            or Sfx.BossSealRelease or Sfx.BossVortexEnd or Sfx.BigBangBurst or Sfx.WhiteHoleRelease;

        private static Clock SoundClock(Sfx cue) => cue switch
        {
            Sfx.CardCancel or Sfx.CardDraw or Sfx.CardGuaranteed or Sfx.CardRecover or Sfx.CardSelect
                or Sfx.CardShuffle or Sfx.CardSubmit or Sfx.DebuggerAcquire or Sfx.HandOpen
                or Sfx.EnergyDenied or Sfx.EnergyReady or Sfx.BadLanding or Sfx.BadTarget
                or Sfx.PileClose or Sfx.PileOpen or Sfx.PlanConfirm or Sfx.PlanUndo or Sfx.QueueClear
                or Sfx.QueueMove or Sfx.TargetClear or Sfx.TargetLock or Sfx.TargetSwitch
                or Sfx.BigBangCharge or Sfx.DungeonCardRemoved or Sfx.DungeonComplete or Sfx.DungeonDenied or Sfx.DungeonDescent
                or Sfx.DungeonMerchant or Sfx.DungeonPurchase or Sfx.DungeonReward or Sfx.DungeonRoomCleared
                or Sfx.DungeonTraverse or Sfx.DungeonTreasureOpen or Sfx.PlayerHeal
                or Sfx.TrialReset or Sfx.TrialSwitch or Sfx.MenuFocus or Sfx.MenuConfirm or Sfx.MenuBack
                or Sfx.MenuStart => Clock.Interface,
            Sfx.BossDeploy or Sfx.BossDown or Sfx.BossEnter or Sfx.BossMeteorLand or Sfx.BossMeteorWarn
                or Sfx.BossPhase or Sfx.BossPortal or Sfx.BossRainFire or Sfx.BossRainHit
                or Sfx.BossRainPass or Sfx.BossRainWarn or Sfx.BossSealRelease or Sfx.BossSealWarn
                or Sfx.BossVortexEnd or Sfx.BossVortexOpen or Sfx.BossVortexPull or Sfx.ClusterBreak
                or Sfx.ClusterLink or Sfx.ClusterSurge or Sfx.EnemyEngage or Sfx.EnemySlash
                or Sfx.EnemySplit or Sfx.EnemyWindup or Sfx.FragmentSpawn or Sfx.MirrorAttack
                or Sfx.MirrorCopy or Sfx.MirrorWarn or Sfx.ProjectilePass or Sfx.ProtocolHeal
                or Sfx.Resonance or Sfx.ShooterAim or Sfx.ShooterFire or Sfx.SwarmAttack
                or Sfx.SwarmRush => Clock.Enemy,
            _ => Clock.Combat,
        };

        private static AudioClip Take(Sfx cue)
        {
            WarmUp();
            if (!clips.TryGetValue(cue, out var takes)) return null;
            if (takes.Count == 1) return takes[0];
            int index = random.Next(takes.Count);
            if (previousTake.TryGetValue(cue, out int previous) && index == previous)
                index = (index + 1 + random.Next(takes.Count - 1)) % takes.Count;
            previousTake[cue] = index;
            return takes[index];
        }
    }
}
