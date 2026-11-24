using UnityEngine;

namespace BlackoutProtocol.Audio
{
    /// <summary>Small synthesized effects shared by world objects and robots.</summary>
    public static class ProceduralSfx
    {
        private const int SampleRate = 22050;
        private static AudioClip step, attack, hit, death, alert, hum, scrape, door, click;

        public static AudioClip Step => step != null ? step : (step = Make("Robot step", 0.15f, 72f, 0.08f, 0));
        public static AudioClip Attack => attack != null ? attack : (attack = Make("Robot attack", 0.23f, 520f, 0.2f, 1));
        public static AudioClip Hit => hit != null ? hit : (hit = Make("Robot hit", 0.18f, 190f, 0.2f, 2));
        public static AudioClip Death => death != null ? death : (death = Make("Robot shutdown", 0.7f, 260f, 0.22f, 3));
        public static AudioClip Alert => alert != null ? alert : (alert = Make("Robot alert", 0.34f, 660f, 0.15f, 4));
        public static AudioClip DroneHum => hum != null ? hum : (hum = Make("Drone rotor loop", 0.6f, 95f, 0.12f, 5));
        public static AudioClip Scrape => scrape != null ? scrape : (scrape = Make("Crate scrape loop", 0.5f, 115f, 0.16f, 6));
        public static AudioClip Door => door != null ? door : (door = Make("Door motor", 0.65f, 135f, 0.15f, 7));
        public static AudioClip Click => click != null ? click : (click = Make("Switch click", 0.09f, 410f, 0.1f, 8));

        private static AudioClip Make(string name, float duration, float frequency, float gain, int kind)
        {
            int count = Mathf.CeilToInt(duration * SampleRate);
            var samples = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / SampleRate;
                float phase = t / duration;
                float envelope = kind == 5 || kind == 6 ? 1f : Mathf.Sin(Mathf.PI * phase);
                float pitch = kind == 3 ? frequency * (1f - 0.75f * phase) :
                    kind == 1 ? frequency * (1f + 0.9f * phase) :
                    kind == 5 || kind == 6 ? Mathf.Round(frequency * duration) / duration : frequency;
                float fundamental = Mathf.Sin(2f * Mathf.PI * pitch * t);
                float overtone = Mathf.Sin(2f * Mathf.PI * pitch * 2f * t);
                float grit = Mathf.Sin(2f * Mathf.PI * (pitch * 7f) * t) *
                    Mathf.Sin(2f * Mathf.PI * 40f * t);
                float pulse = kind == 5 ? 0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * 30f * t) : 1f;
                samples[i] = gain * envelope * pulse *
                    (0.62f * fundamental + 0.23f * overtone + 0.15f * grit);
            }
            AudioClip clip = AudioClip.Create(name, count, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
