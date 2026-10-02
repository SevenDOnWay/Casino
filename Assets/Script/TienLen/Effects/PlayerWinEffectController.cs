using UnityEngine;

namespace Assets.Script.TienLen.Effects {
    /// <summary>
    /// Plays celebratory particle effects when a player wins a match.
    /// Modular & extensible: supports custom ParticleSystem prefabs,
    /// and provides an automatic procedural particle fallback if no prefab is assigned.
    /// </summary>
    public class PlayerWinEffectController : MonoBehaviour, IPlayerWinEffect {
        [Header("Prefab Configuration (Optional)")]
        [Tooltip("Optional custom particle system prefab. If null, a vibrant procedural celebration is spawned.")]
        [SerializeField] private ParticleSystem customWinParticlePrefab;

        [Header("Celebration Settings")]
        [SerializeField] private float autoDestroyAfterSeconds = 6.0f;

        private GameObject currentEffectInstance;

        public void PlayWinEffect( int winnerSeatIndex, Vector3 worldPosition ) {
            StopWinEffect();

            if ( customWinParticlePrefab != null ) {
                var ps = Instantiate(customWinParticlePrefab, worldPosition, Quaternion.identity, transform);
                ps.Play();
                currentEffectInstance = ps.gameObject;
            }
            else {
                currentEffectInstance = CreateProceduralParticles(worldPosition);
            }

            if ( autoDestroyAfterSeconds > 0f && currentEffectInstance != null ) {
                Destroy(currentEffectInstance, autoDestroyAfterSeconds);
            }
        }

        public void StopWinEffect() {
            if ( currentEffectInstance != null ) {
                Destroy(currentEffectInstance);
                currentEffectInstance = null;
            }
        }

        private GameObject CreateProceduralParticles( Vector3 position ) {
            GameObject go = new GameObject("WinCelebrationParticles");
            go.transform.position = position;
            go.transform.SetParent(transform, true);

            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            // Immediately stop system before setting duration/curves to avoid Unity runtime errors
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 3.5f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.35f);
            main.gravityModifier = 0.6f;
            main.playOnAwake = false;

            // Vibrant multi-color confetti / sparkles
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(new Color(1f, 0.84f, 0f), 0.0f),    // Gold
                    new GradientColorKey(new Color(1f, 0.22f, 0.45f), 0.25f), // Crimson Pink
                    new GradientColorKey(new Color(0.1f, 0.85f, 1f), 0.5f),   // Sky Blue
                    new GradientColorKey(new Color(0.3f, 1f, 0.3f), 0.75f),   // Emerald Green
                    new GradientColorKey(new Color(1f, 0.5f, 0.0f), 1.0f)     // Orange
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 0.8f),
                    new GradientAlphaKey(0f, 1.0f)
                }
            );
            main.startColor = new ParticleSystem.MinMaxGradient(gradient);

            var emission = ps.emission;
            emission.rateOverTime = 80;
            emission.SetBursts(new ParticleSystem.Burst[] {
                new ParticleSystem.Burst(0.0f, 50),
                new ParticleSystem.Burst(0.5f, 40),
                new ParticleSystem.Burst(1.0f, 30)
            });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 35f;
            shape.radius = 0.6f;
            shape.rotation = new Vector3(-90f, 0f, 0f); // Upward burst

            // All velocity axes must share the same curve mode (TwoConstants)
            var velocityOverLifetime = ps.velocityOverLifetime;
            velocityOverLifetime.enabled = true;
            velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f);
            velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient fadeGradient = new Gradient();
            fadeGradient.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLifetime.color = fadeGradient;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve curve = new AnimationCurve();
            curve.AddKey(0.0f, 0.5f);
            curve.AddKey(0.2f, 1.0f);
            curve.AddKey(1.0f, 0.0f);
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1.0f, curve);

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sortingOrder = 300;
            if ( renderer.sharedMaterial == null ) {
                Shader particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                    ?? Shader.Find("Particles/Standard Unlit")
                    ?? Shader.Find("Sprites/Default");
                if ( particleShader != null ) {
                    renderer.material = new Material(particleShader);
                }
            }

            ps.Play();
            return go;
        }

        private void OnDestroy() {
            StopWinEffect();
        }
    }
}
