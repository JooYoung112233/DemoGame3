using UnityEngine;

namespace EastTrain
{
    public sealed class EastTrainAudio : MonoBehaviour
    {
        AudioSource wind, motor, effects, radio;
        AudioClip tap, miss, signal;
        public void Initialize()
        {
            wind = Source(.13f, true); motor = Source(.07f, true);
            effects = Source(.24f, false); radio = Source(.8f, false);
            radio.gameObject.AddComponent<AudioHighPassFilter>().cutoffFrequency = 450;
            radio.gameObject.AddComponent<AudioLowPassFilter>().cutoffFrequency = 2800;
            wind.clip = Noise(); wind.Play();
            motor.clip = Tone(48, 2, false); motor.Play();
            tap = Tone(630, .12f, true); miss = Tone(95, .3f, true); signal = Tone(1050, .09f, true);
        }
        AudioSource Source(float volume, bool loop)
        {
            var child = new GameObject("Mechanical audio"); child.transform.SetParent(transform, false);
            var source = child.AddComponent<AudioSource>();
            source.volume = volume; source.loop = loop; source.spatialBlend = 0; return source;
        }
        AudioClip Tone(float hz, float seconds, bool fade)
        {
            int length = (int)(22050 * seconds); var data = new float[length];
            for (int i = 0; i < length; i++)
            {
                float t = i / 22050f;
                data[i] = (Mathf.Sin(t * hz * Mathf.PI * 2) * .65f + Mathf.Sin(t * hz * Mathf.PI * 4) * .18f) * (fade ? 1 - i / (float)length : 1);
            }
            var clip = AudioClip.Create("Mechanical tone", length, 1, 22050, false); clip.SetData(data, 0); return clip;
        }
        AudioClip Noise()
        {
            var data = new float[22050 * 4]; var random = new System.Random(7); float low = 0;
            for (int i = 0; i < data.Length; i++) { low = low * .97f + ((float)random.NextDouble() * 2 - 1) * .03f; data[i] = low * 4; }
            var clip = AudioClip.Create("Snow wind", data.Length, 1, 22050, false); clip.SetData(data, 0); return clip;
        }
        public void UpdateEngine(TrainState state)
        {
            motor.pitch = .65f + state.Throttle * .45f + state.Speed * .12f;
            motor.volume = state.EngineRunning ? .065f + state.Throttle * .12f : 0;
        }
        public void Hit(bool good) => effects.PlayOneShot(good ? tap : miss);
        public void Cue() => effects.PlayOneShot(signal);
        public void Radio()
        {
            var clip = Resources.Load<AudioClip>("RadioEast");
            if (clip != null) { radio.clip = clip; radio.Play(); } else Cue();
        }
    }
}
