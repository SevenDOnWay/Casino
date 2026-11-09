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

            Vector3 spawnPos = new Vector3(worldPosition.x, worldPosition.y, -1f);

            if ( customWinParticlePrefab != null ) {
                var ps = Instantiate(customWinParticlePrefab, spawnPos, Quaternion.identity);
                ps.transform.SetParent(null);
                ps.transform.localScale = Vector3.one;
                ps.Play();
                currentEffectInstance = ps.gameObject;
            }
            else {
                currentEffectInstance = CreateProceduralParticles(spawnPos);
            }

            if ( autoDestroyAfterSeconds > 0f && currentEffectInstance != null ) {
                if ( Application.isPlaying ) {
                    Destroy(currentEffectInstance, autoDestroyAfterSeconds);
                }
            }
        }

        public void StopWinEffect() {
            if ( currentEffectInstance != null ) {
                if ( Application.isPlaying ) {
                    Destroy(currentEffectInstance);
                }
                else {
                    DestroyImmediate(currentEffectInstance);
                }
                currentEffectInstance = null;
            }
        }

        private GameObject CreateProceduralParticles( Vector3 position ) {
            GameObject go = new GameObject("WinCelebrationParticles");
            go.transform.position = new Vector3(position.x, position.y, -1f);
            go.transform.SetParent(null);
            go.transform.localScale = Vector3.one;

            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            // Immediately stop system before setting duration/curves to avoid Unity runtime errors
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 4.0f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 2.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(6f, 12f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
            main.gravityModifier = 0.9f;
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
            emission.rateOverTime = 100;
            emission.SetBursts(new ParticleSystem.Burst[] {
                new ParticleSystem.Burst(0.0f, 60),
                new ParticleSystem.Burst(0.4f, 50),
                new ParticleSystem.Burst(0.9f, 40)
            });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 40f;
            shape.radius = 0.8f;
            shape.rotation = new Vector3(-90f, 0f, 0f); // Upward burst in 2D space

            var velocityOverLifetime = ps.velocityOverLifetime;
            velocityOverLifetime.enabled = true;
            velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(-3f, 3f);
            velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(1f, 4f);
            velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(0f, 0f);

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
            curve.AddKey(0.0f, 0.6f);
            curve.AddKey(0.2f, 1.0f);
            curve.AddKey(1.0f, 0.0f);
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1.0f, curve);

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sortingLayerName = "Default";
            renderer.sortingOrder = 500;

            Shader particleShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")
                ?? Shader.Find("Sprites/Default")
                ?? Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if ( particleShader != null ) {
                Material mat = new Material(particleShader);
                mat.mainTexture = Texture2D.whiteTexture;
                if ( mat.HasProperty("_BaseMap") ) {
                    mat.SetTexture("_BaseMap", Texture2D.whiteTexture);
                }
                renderer.material = mat;
            }

            ps.Play();
            return go;
        }

        private void OnDisable() {
            StopWinEffect();
        }

        private void OnDestroy() {
            StopWinEffect();
        }
    }
}
