using System.Collections.Generic;
using UnityEngine;

namespace AdventurerCity.View
{
    public enum Sfx
    {
        Hit,
        Crit,
        Heal,
        Cast,
        Death,
        Boom,
        Click,
        Place,
        Built,
        Alarm,
        LevelUp,
        Fanfare,
        Sad
    }

    public sealed class GameAudio : MonoBehaviour
    {
        const int Rate = 22050;
        const float MinGap = 0.06f;
        const float MusicVolume = 0.32f;

        static GameAudio instance;

        readonly Dictionary<Sfx, AudioClip> clips = new Dictionary<Sfx, AudioClip>();
        readonly Dictionary<Sfx, float> lastPlayed = new Dictionary<Sfx, float>();
        readonly List<AudioSource> voices = new List<AudioSource>();
        AudioSource cityMusic;
        AudioSource battleMusic;
        System.Random random = new System.Random(11);
        int nextVoice;
        float blend;

        public static bool Muted
        {
            get => GameSettings.Muted;
            set => GameSettings.SetMuted(value);
        }

        public static bool Battle { get; set; }

        public static void Play(Sfx sfx, float volume = 1f)
        {
            if (instance != null) instance.PlayInternal(sfx, volume);
        }

        public void Init()
        {
            instance = this;
            if (FindAnyObjectByType<AudioListener>() == null) gameObject.AddComponent<AudioListener>();
            GameSettings.Load();
            for (var i = 0; i < 12; i++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                voices.Add(source);
            }

            clips[Sfx.Hit] = Make(0.12f, (t, i) => Noise() * Env(t, 0.002f, 0.08f) * 0.5f + Sine(Sweep(t, 140f, 60f, 0.1f), t) * Env(t, 0.002f, 0.1f) * 0.7f);
            clips[Sfx.Crit] = Make(0.2f, (t, i) => Noise() * Env(t, 0.001f, 0.12f) * 0.7f + Sine(Sweep(t, 220f, 50f, 0.18f), t) * Env(t, 0.001f, 0.18f) + Square(1600f, t) * Env(t, 0.001f, 0.03f) * 0.2f);
            clips[Sfx.Heal] = Make(0.6f, (t, i) => (Sine(880f, t) * Env(t, 0.01f, 0.35f) + Sine(1320f, t - 0.08f) * Env(t - 0.08f, 0.01f, 0.4f)) * 0.35f);
            clips[Sfx.Cast] = Make(0.45f, (t, i) => (Noise() * 0.4f + Sine(Sweep(t, 300f, 900f, 0.45f), t) * 0.4f) * Env(t, 0.15f, 0.3f) * 0.5f);
            clips[Sfx.Death] = Make(0.5f, (t, i) => Square(Sweep(t, 420f, 70f, 0.5f), t) * Env(t, 0.005f, 0.45f) * 0.22f);
            clips[Sfx.Boom] = Make(0.9f, (t, i) => (Noise() * 0.6f * Env(t, 0.005f, 0.5f) + Sine(Sweep(t, 90f, 35f, 0.8f), t) * Env(t, 0.005f, 0.85f)) * 0.8f);
            clips[Sfx.Click] = Make(0.05f, (t, i) => Square(1200f, t) * Env(t, 0.001f, 0.04f) * 0.15f);
            clips[Sfx.Place] = Make(0.18f, (t, i) => (Sine(Sweep(t, 180f, 90f, 0.15f), t) + Noise() * 0.3f) * Env(t, 0.002f, 0.15f) * 0.6f);
            clips[Sfx.Built] = Make(0.9f, (t, i) =>
            {
                var knock = 0f;
                for (var k = 0; k < 3; k++) knock += (Sine(160f, t - k * 0.14f) + Noise() * 0.4f) * Env(t - k * 0.14f, 0.002f, 0.08f);
                return knock * 0.45f + Sine(1046f, t - 0.45f) * Env(t - 0.45f, 0.01f, 0.4f) * 0.3f;
            });
            clips[Sfx.Alarm] = Make(2.4f, (t, i) =>
            {
                var bell = 0f;
                for (var k = 0; k < 2; k++)
                {
                    var b = t - k * 1.1f;
                    bell += (Sine(660f, b) + Sine(990f, b) * 0.5f + Sine(1650f, b) * 0.25f) * Env(b, 0.003f, 1.0f);
                }
                return bell * 0.3f;
            });
            clips[Sfx.LevelUp] = Make(0.6f, (t, i) => Arpeggio(t, new[] { 523f, 659f, 784f, 1046f }, 0.09f) * 0.25f);
            clips[Sfx.Fanfare] = Make(1.3f, (t, i) => Arpeggio(t, new[] { 392f, 523f, 659f, 784f, 659f, 1046f }, 0.14f) * 0.25f);
            clips[Sfx.Sad] = Make(1.2f, (t, i) => Arpeggio(t, new[] { 440f, 415f, 349f, 330f }, 0.25f) * 0.22f);

            cityMusic = MusicSource(CityTheme());
            battleMusic = MusicSource(BattleTheme());
            battleMusic.volume = 0f;
        }

        void Update()
        {
            if (cityMusic == null) return;
            var target = Battle ? 1f : 0f;
            blend = Mathf.MoveTowards(blend, target, Time.unscaledDeltaTime * 0.6f);
            var volume = MusicVolume * GameSettings.MusicVolume;
            battleMusic.volume = blend * volume;
            cityMusic.volume = (1f - blend) * volume;
        }

        void PlayInternal(Sfx sfx, float volume)
        {
            if (!clips.TryGetValue(sfx, out var clip)) return;
            if (lastPlayed.TryGetValue(sfx, out var last) && Time.unscaledTime - last < MinGap) return;
            lastPlayed[sfx] = Time.unscaledTime;
            var source = voices[nextVoice];
            nextVoice = (nextVoice + 1) % voices.Count;
            source.pitch = 0.92f + (float)random.NextDouble() * 0.16f;
            source.PlayOneShot(clip, volume * GameSettings.SfxVolume);
        }

        AudioSource MusicSource(AudioClip clip)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.volume = MusicVolume;
            source.Play();
            return source;
        }

        AudioClip CityTheme()
        {
            var chords = new[] { new[] { 261.6f, 329.6f, 392f }, new[] { 196f, 246.9f, 293.7f }, new[] { 220f, 261.6f, 329.6f }, new[] { 174.6f, 220f, 261.6f } };
            var scale = new[] { 523.3f, 587.3f, 659.3f, 784f, 880f, 1046.5f };
            const float chordLength = 3f;
            var notes = new List<(float time, float freq)>();
            var rng = new System.Random(3);
            for (var bar = 0; bar < 8; bar++)
            for (var beat = 0; beat < 4; beat++)
                if (rng.NextDouble() < 0.55)
                    notes.Add((bar * chordLength + beat * chordLength / 4f, scale[rng.Next(scale.Length)]));
            return Make(8 * chordLength, (t, i) =>
            {
                var chord = chords[(int)(t / chordLength) % chords.Length];
                var local = t % chordLength;
                var pad = 0f;
                foreach (var f in chord) pad += Sine(f, t) * 0.6f + Sine(f * 2f, t) * 0.1f;
                pad *= Mathf.Clamp01(local / 0.6f) * Mathf.Clamp01((chordLength - local) / 0.6f) * 0.12f;
                var pluck = 0f;
                foreach (var (time, freq) in notes)
                {
                    var d = t - time;
                    if (d < 0f || d > 0.8f) continue;
                    pluck += (Sine(freq, d) + Sine(freq * 2f, d) * 0.3f) * Env(d, 0.005f, 0.7f) * 0.12f;
                }
                var bass = Sine(chord[0] * 0.5f, t) * 0.12f;
                return pad + pluck + bass;
            });
        }

        AudioClip BattleTheme()
        {
            const float beat = 0.25f;
            var bassline = new[] { 110f, 110f, 130.8f, 110f, 98f, 98f, 87.3f, 98f };
            return Make(8f, (t, i) =>
            {
                var step = (int)(t / beat);
                var local = t - step * beat;
                var kick = step % 4 == 0 ? Sine(Sweep(local, 120f, 45f, 0.15f), local) * Env(local, 0.002f, 0.18f) * 0.6f : 0f;
                var snare = step % 4 == 2 ? Noise() * Env(local, 0.002f, 0.12f) * 0.3f : 0f;
                var hat = Noise() * Env(local, 0.001f, 0.03f) * 0.08f;
                var bass = Square(bassline[(step / 2) % bassline.Length], t) * Env(local % (beat * 2f), 0.005f, 0.4f) * 0.12f;
                var lead = Sine(bassline[(step / 4) % bassline.Length] * 4f, t) * 0.05f * (step % 8 < 6 ? 1f : 0f);
                return kick + snare + hat + bass + lead;
            });
        }

        delegate float Wave(float time, int index);

        AudioClip Make(float seconds, Wave wave)
        {
            var samples = new float[(int)(seconds * Rate)];
            for (var i = 0; i < samples.Length; i++)
                samples[i] = Mathf.Clamp(wave(i / (float)Rate, i), -1f, 1f);
            var clip = AudioClip.Create("sfx", samples.Length, 1, Rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        float Noise() => (float)random.NextDouble() * 2f - 1f;

        static float Sine(float freq, float t) => t < 0f ? 0f : Mathf.Sin(2f * Mathf.PI * freq * t);

        static float Square(float freq, float t) => t < 0f ? 0f : Mathf.Sin(2f * Mathf.PI * freq * t) >= 0f ? 1f : -1f;

        static float Sweep(float t, float from, float to, float length) => Mathf.Lerp(from, to, Mathf.Clamp01(t / length));

        static float Env(float t, float attack, float decay)
        {
            if (t < 0f) return 0f;
            if (t < attack) return t / attack;
            return Mathf.Exp(-(t - attack) / Mathf.Max(0.001f, decay) * 3f);
        }

        static float Arpeggio(float t, float[] notes, float step)
        {
            var sum = 0f;
            for (var k = 0; k < notes.Length; k++)
            {
                var d = t - k * step;
                sum += (Square(notes[k], d) * 0.4f + Sine(notes[k], d)) * Env(d, 0.005f, 0.35f);
            }
            return sum;
        }
    }
}
