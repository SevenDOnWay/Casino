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
        private static Texture2D cachedCircleTexture;

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

        private static Texture2D GetOrCreateCircleTexture() {
            if ( cachedCircleTexture != null ) return cachedCircleTexture;
            int size = 32;
            cachedCircleTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float center = (size - 1) * 0.5f;
            float radius = size * 0.5f;
            for ( int y = 0; y < size; y++ ) {
                for ( int x = 0; x < size; x++ ) {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    float alpha = Mathf.Clamp01(1f - (dist / radius));
                    alpha = Mathf.SmoothStep(0f, 1f, alpha);
                    cachedCircleTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            cachedCircleTexture.Apply();
            return cachedCircleTexture;
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
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 5.0f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
            main.gravityModifier = 0.35f;
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
                new ParticleSystem.Burst(0.3f, 40),
                new ParticleSystem.Burst(0.7f, 30)
            });

            // Radial 360-degree blossom around winning player seat
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.6f;
            shape.rotation = Vector3.zero;
            shape.arc = 360f;

            var limitVelocityOverLifetime = ps.limitVelocityOverLifetime;
            limitVelocityOverLifetime.enabled = true;
            limitVelocityOverLifetime.limit = 1.2f;
            limitVelocityOverLifetime.dampen = 0.2f;

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

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            if ( renderer != null ) {
                renderer.sortingLayerName = "Default";
                renderer.sortingOrder = 500;

                Shader particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                    ?? Shader.Find("Particles/Standard Unlit")
                    ?? Shader.Find("Universal Render Pipeline/Unlit")
                    ?? Shader.Find("Sprites/Default");

                if ( particleShader != null ) {
                    Material mat = new Material(particleShader);
                    Texture2D circleTex = GetOrCreateCircleTexture();
                    mat.mainTexture = circleTex;
                    if ( mat.HasProperty("_BaseMap") ) {
                        mat.SetTexture("_BaseMap", circleTex);
                    }
                    renderer.material = mat;
                }
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
