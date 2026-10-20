using System;
using UnityEngine;

namespace BlackoutProtocol.Player
{
    /// <summary>
    /// Supplies pooled first-person muzzle and impact effects, camera recoil,
    /// and original procedural weapon sounds for PlayerWeapon.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerWeapon), typeof(PlayerLook))]
    public sealed class PlayerCombatFeedback : MonoBehaviour
    {
        private const int ImpactPoolSize = 8;

        [Header("Feedback")]
        [SerializeField, Min(0f)] private float recoilPitch = 1.55f;
        [SerializeField, Min(0f)] private float recoilRoll = 0.22f;
        [SerializeField, Min(0f)] private float muzzleLightIntensity = 2.6f;
        [SerializeField, Min(0f)] private float muzzleLightRange = 2.8f;
        [SerializeField, Min(0f)] private float muzzleLightDuration = 0.055f;
        [SerializeField, Min(0)] private int muzzleParticleCount = 10;
        [SerializeField, Min(0)] private int impactParticleCount = 12;
        [SerializeField] private Color muzzleCoreColor = new Color32(106, 239, 255, 255);
        [SerializeField] private Color muzzleEdgeColor = new Color32(255, 190, 90, 255);
        [SerializeField] private Color impactCoreColor = new Color32(65, 211, 255, 255);
        [SerializeField] private Color impactEdgeColor = new Color32(255, 191, 86, 255);

        [Header("Optional Audio Overrides")]
        [Tooltip("Optional imported shot clip. Leave empty to use the built-in synthesized sound.")]
        [SerializeField] private AudioClip shotSound;
        [Tooltip("Optional imported reload clip. Leave empty to use the built-in synthesized sound.")]
        [SerializeField] private AudioClip reloadSound;
        [Tooltip("Optional imported empty-magazine clip. Leave empty to use the built-in synthesized sound.")]
        [SerializeField] private AudioClip emptySound;

        private PlayerWeapon weapon;
        private PlayerLook playerLook;
        private Camera playerCamera;
        private AudioSource audioSource;
        private AudioClip shotClip;
        private AudioClip reloadClip;
        private AudioClip emptyClip;
        private bool ownsShotClip;
        private bool ownsReloadClip;
        private bool ownsEmptyClip;
        private Transform muzzleAnchor;
        private ParticleSystem muzzleParticles;
        private Light muzzleLight;
        private float muzzleLightOffAt;
        private ParticleSystem[] impactPool;
        private int nextImpactIndex;
        private Material particleMaterial;

        private void Awake()
        {
            weapon = GetComponent<PlayerWeapon>();
            playerLook = GetComponent<PlayerLook>();
            playerCamera = GetComponentInChildren<Camera>();

            if (playerCamera == null)
            {
                Debug.LogError("PlayerCombatFeedback needs a child player camera.", this);
                enabled = false;
                return;
            }

            CreateAudio();
            CreatePooledEffects();
        }

        private void OnEnable()
        {
            if (weapon == null)
            {
                weapon = GetComponent<PlayerWeapon>();
            }

            weapon.ShotFired += OnShotFired;
            weapon.ShotHit += OnShotHit;
            weapon.EmptyShot += OnEmptyShot;
            weapon.ReloadStarted += OnReloadStarted;
        }

        private void OnDisable()
        {
            if (weapon != null)
            {
                weapon.ShotFired -= OnShotFired;
                weapon.ShotHit -= OnShotHit;
                weapon.EmptyShot -= OnEmptyShot;
                weapon.ReloadStarted -= OnReloadStarted;
            }

            if (muzzleLight != null)
            {
                muzzleLight.enabled = false;
            }
        }

        private void OnDestroy()
        {
            if (ownsShotClip) Destroy(shotClip);
            if (ownsReloadClip) Destroy(reloadClip);
            if (ownsEmptyClip) Destroy(emptyClip);
            Destroy(particleMaterial);
        }

        private void Update()
        {
            if (muzzleLight != null && muzzleLight.enabled && Time.time >= muzzleLightOffAt)
            {
                muzzleLight.enabled = false;
            }
        }

        private void CreateAudio()
        {
            audioSource = playerCamera.gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 0f;
            audioSource.dopplerLevel = 0f;
            audioSource.volume = 0.82f;
            audioSource.priority = 16;

            shotClip = shotSound != null ? shotSound : CreateShotClip();
            reloadClip = reloadSound != null ? reloadSound : CreateReloadClip();
            emptyClip = emptySound != null ? emptySound : CreateEmptyClip();
            ownsShotClip = shotSound == null;
            ownsReloadClip = reloadSound == null;
            ownsEmptyClip = emptySound == null;
        }

        private void CreatePooledEffects()
        {
            particleMaterial = CreateParticleMaterial();
            muzzleAnchor = FindMuzzleAnchor();
            if (muzzleAnchor == null)
            {
                GameObject anchor = new GameObject("Combat Feedback Muzzle Anchor");
                muzzleAnchor = anchor.transform;
                muzzleAnchor.SetParent(playerCamera.transform, false);
                // The sandbox has no weapon mesh. This camera-relative point keeps
                // its flash visible until an equipped weapon provides a Muzzle child.
                muzzleAnchor.localPosition = new Vector3(0.31f, -0.23f, 0.58f);
            }

            muzzleParticles = CreateBurst("Pooled Muzzle Flash", muzzleAnchor,
                ParticleSystemSimulationSpace.Local, muzzleCoreColor,
                muzzleEdgeColor, 0.035f, 0.085f,
                0.07f, 0.19f, 0.05f, 0.12f, 0.05f, 0.65f, 10);

            GameObject lightObject = new GameObject("Pooled Muzzle Light");
            lightObject.transform.SetParent(muzzleAnchor, false);
            muzzleLight = lightObject.AddComponent<Light>();
            muzzleLight.type = LightType.Point;
            muzzleLight.color = muzzleCoreColor;
            muzzleLight.intensity = muzzleLightIntensity;
            muzzleLight.range = muzzleLightRange;
            muzzleLight.shadows = LightShadows.None;
            muzzleLight.enabled = false;

            impactPool = new ParticleSystem[ImpactPoolSize];
            for (int i = 0; i < impactPool.Length; i++)
            {
                impactPool[i] = CreateBurst($"Pooled Impact Sparks {i + 1}", transform,
                    ParticleSystemSimulationSpace.World, impactCoreColor,
                    impactEdgeColor, 0.08f, 0.2f,
                    0.025f, 0.065f, 1.2f, 3.6f, 18f, 0.06f, 12);
            }
        }

        private Transform FindMuzzleAnchor()
        {
            Transform[] descendants = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < descendants.Length; i++)
            {
                if (descendants[i] != transform && descendants[i].name == "Muzzle")
                {
                    return descendants[i];
                }
            }

            return null;
        }

        private ParticleSystem CreateBurst(string objectName, Transform parent,
            ParticleSystemSimulationSpace simulationSpace, Color primary, Color secondary,
            float minLifetime, float maxLifetime, float minSize, float maxSize,
            float minSpeed, float maxSpeed, float coneAngle, float coneRadius, int maxParticles)
        {
            GameObject effectObject = new GameObject(objectName);
            effectObject.transform.SetParent(parent, false);
            ParticleSystem particles = effectObject.AddComponent<ParticleSystem>();
            // Unity starts a newly added ParticleSystem immediately in some editor states.
            // Stop it before configuring MainModule values such as duration.
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = particles.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.22f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(minLifetime, maxLifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(minSpeed, maxSpeed);
            main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
            main.startColor = new ParticleSystem.MinMaxGradient(primary, secondary);
            main.simulationSpace = simulationSpace;
            main.maxParticles = maxParticles;
            main.gravityModifier = simulationSpace == ParticleSystemSimulationSpace.World ? 0.15f : 0f;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = false;

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = coneAngle;
            shape.radius = coneRadius;

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient fade = new Gradient();
            fade.SetKeys(
                new[]
                {
                    new GradientColorKey(primary, 0f),
                    new GradientColorKey(secondary, 0.45f),
                    new GradientColorKey(secondary, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.8f, 0.28f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(fade);

            ParticleSystemRenderer particleRenderer = effectObject.GetComponent<ParticleSystemRenderer>();
            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            particleRenderer.sortMode = ParticleSystemSortMode.Distance;
            if (particleMaterial != null)
            {
                particleRenderer.sharedMaterial = particleMaterial;
            }

            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return particles;
        }

        private Material CreateParticleMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Particles/Standard Unlit");
            }

            if (shader == null)
            {
                return null;
            }

            Material material = new Material(shader)
            {
                name = "Runtime Combat Feedback Particles",
                hideFlags = HideFlags.HideAndDontSave
            };
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", Color.white);
            }
            else if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", Color.white);
            }

            return material;
        }

        private void OnShotFired()
        {
            audioSource.PlayOneShot(shotClip, 0.78f);
            muzzleParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            muzzleParticles.Play(true);
            muzzleParticles.Emit(muzzleParticleCount);
            muzzleLight.intensity = muzzleLightIntensity;
            muzzleLight.range = muzzleLightRange;
            muzzleLight.enabled = true;
            muzzleLightOffAt = Time.time + muzzleLightDuration;

            float roll = UnityEngine.Random.Range(-recoilRoll, recoilRoll);
            playerLook.AddRecoil(recoilPitch, roll);
        }

        private void OnShotHit(Vector3 point, Vector3 normal)
        {
            ParticleSystem sparks = impactPool[nextImpactIndex];
            nextImpactIndex = (nextImpactIndex + 1) % impactPool.Length;
            sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            sparks.transform.SetPositionAndRotation(
                point + normal * 0.015f,
                normal.sqrMagnitude > 0.001f ? Quaternion.LookRotation(normal) : Quaternion.identity);
            sparks.Play(true);
            sparks.Emit(impactParticleCount);
        }

        private void OnEmptyShot()
        {
            audioSource.PlayOneShot(emptyClip, 0.62f);
        }

        private void OnReloadStarted()
        {
            audioSource.PlayOneShot(reloadClip, 0.72f);
        }

        private static AudioClip CreateShotClip()
        {
            const int sampleRate = 22050;
            const float duration = 0.24f;
            float[] samples = new float[Mathf.CeilToInt(sampleRate * duration)];
            System.Random noise = new System.Random(2701);
            double phase = 0.0;
            double subPhase = 0.0;

            for (int i = 0; i < samples.Length; i++)
            {
                float time = i / (float)sampleRate;
                float envelope = Mathf.Exp(-time * 14f);
                float frequency = 84f + 88f * Mathf.Exp(-time * 24f);
                phase += 2.0 * Math.PI * frequency / sampleRate;
                subPhase += 2.0 * Math.PI * (frequency * 0.51f) / sampleRate;
                float transient = time < 0.022f
                    ? (float)(noise.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-time * 190f)
                    : 0f;
                samples[i] = Mathf.Clamp(
                    (Mathf.Sin((float)phase) * 0.48f + Mathf.Sin((float)subPhase) * 0.26f) * envelope
                    + transient * 0.48f,
                    -1f, 1f);
            }

            return CreateClip("Original Plasma Shot", samples, sampleRate);
        }

        private static AudioClip CreateReloadClip()
        {
            const int sampleRate = 22050;
            const float duration = 0.74f;
            float[] samples = new float[Mathf.CeilToInt(sampleRate * duration)];
            System.Random noise = new System.Random(1609);
            double servoPhase = 0.0;
            double ringPhase = 0.0;

            for (int i = 0; i < samples.Length; i++)
            {
                float time = i / (float)sampleRate;
                float servoEnvelope = Mathf.Clamp01((time - 0.12f) * 14f)
                    * Mathf.Clamp01((0.58f - time) * 10f);
                float servoFrequency = 190f + 75f * Mathf.Sin(time * 7f);
                servoPhase += 2.0 * Math.PI * servoFrequency / sampleRate;
                float servo = Mathf.Sin((float)servoPhase) * servoEnvelope * 0.13f;
                float clack = 0f;

                clack += Click(time - 0.035f, 470f, noise, ref ringPhase);
                clack += Click(time - 0.265f, 760f, noise, ref ringPhase) * 0.9f;
                clack += Click(time - 0.565f, 580f, noise, ref ringPhase) * 0.8f;
                samples[i] = Mathf.Clamp(servo + clack, -1f, 1f);
            }

            return CreateClip("Original Plasma Reload", samples, sampleRate);
        }

        private static float Click(float time, float frequency, System.Random noise,
            ref double phase)
        {
            if (time < 0f || time > 0.065f)
            {
                return 0f;
            }

            phase += 2.0 * Math.PI * frequency / 22050.0;
            float envelope = Mathf.Exp(-time * 82f);
            float tickNoise = (float)(noise.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-time * 145f);
            return (Mathf.Sin((float)phase) * 0.34f + tickNoise * 0.35f) * envelope;
        }

        private static AudioClip CreateEmptyClip()
        {
            const int sampleRate = 22050;
            const float duration = 0.105f;
            float[] samples = new float[Mathf.CeilToInt(sampleRate * duration)];
            System.Random noise = new System.Random(8843);
            double phase = 0.0;

            for (int i = 0; i < samples.Length; i++)
            {
                float time = i / (float)sampleRate;
                phase += 2.0 * Math.PI * 1120f / sampleRate;
                float envelope = Mathf.Exp(-time * 48f);
                float tickNoise = (float)(noise.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-time * 230f);
                samples[i] = Mathf.Clamp(
                    Mathf.Sin((float)phase) * envelope * 0.3f + tickNoise * 0.22f,
                    -1f, 1f);
            }

            return CreateClip("Original Empty Magazine Click", samples, sampleRate);
        }

        private static AudioClip CreateClip(string clipName, float[] samples, int sampleRate)
        {
            AudioClip clip = AudioClip.Create(clipName, samples.Length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void OnValidate()
        {
            recoilPitch = Mathf.Max(0f, recoilPitch);
            recoilRoll = Mathf.Max(0f, recoilRoll);
            muzzleLightIntensity = Mathf.Max(0f, muzzleLightIntensity);
            muzzleLightRange = Mathf.Max(0f, muzzleLightRange);
        }
    }
}
