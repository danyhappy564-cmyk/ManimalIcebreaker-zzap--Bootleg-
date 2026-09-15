using System;
using UnityEngine;

namespace Manimal.Icebreaker.Blowtorch
{
    // the bundle ships no flame, only an empty muzzleflash stub, so the burner jet is built
    // here from a game particle shader, parented to the torch mesh. placement is config-driven;
    // the defaults were tuned in game.
    internal sealed class BlowtorchFlame
    {
        private const string MeshName = "item_key_labyrinth_torch_LOD0";
        private const string HostName = "manimal_torch_flame";

        private static Material _material;
        private static bool _materialFailed;

        private readonly Transform _host;
        private readonly ParticleSystem _core;
        private readonly ParticleSystem _plume;
        private readonly Light _light;
        private const float LightIntensity = 0.8f;

        private BlowtorchFlame(Transform host, ParticleSystem core, ParticleSystem plume, Light light)
        {
            _host = host;
            _core = core;
            _plume = plume;
            _light = light;
        }

        internal static BlowtorchFlame Attach(Transform prefabRoot)
        {
            try
            {
                var mount = TransformTools.FindTransformRecursive(prefabRoot, MeshName, false)
                            ?? TransformTools.FindTransformRecursive(prefabRoot, "fireport", false);
                if (mount == null) return null;
                var material = Material();
                if (material == null) return null;

                // pooled prefab: reuse the rig a previous draw already built
                var existing = mount.Find(HostName);
                var host = existing != null ? existing : new GameObject(HostName).transform;
                if (existing == null)
                {
                    host.SetParent(mount, false);
                    host.gameObject.layer = mount.gameObject.layer;
                    Build(host, material);
                }
                var flame = new BlowtorchFlame(host,
                    host.Find("core").GetComponent<ParticleSystem>(),
                    host.Find("plume").GetComponent<ParticleSystem>(),
                    host.GetComponentInChildren<Light>(true));
                flame.Set(false);
                return flame;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"[Blowtorch] flame setup failed: {e.Message}");
                return null;
            }
        }

        internal void Set(bool on)
        {
            if (_host == null) return;
            ApplyTuning();
            if (on)
            {
                _core.Play(true);
                _plume.Play(true);
            }
            else
            {
                _core.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                _plume.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
            if (_light != null) _light.enabled = on;
        }

        // live: config edits show while the trigger is held
        private void ApplyTuning()
        {
            _host.localPosition = Plugin.TorchFlameOffset.Value;
            _host.localRotation = Quaternion.Euler(Plugin.TorchFlameRotation.Value);
            _host.localScale = Vector3.one * Plugin.TorchFlameScale.Value;
        }

        internal void Tick()
        {
            if (_host == null) return;
            ApplyTuning();
            if (_light == null || !_light.enabled) return;
            _light.intensity = LightIntensity * (0.85f + 0.3f * Mathf.PerlinNoise(Time.time * 18f, 0.37f));
        }

        private static void Build(Transform host, Material material)
        {
            // hot inner cone: tight, fast, blue-white
            Emitter(host, "core", material, lifetime: 0.07f, speed: 1.6f, size: 0.018f, rate: 220f, angle: 2.5f,
                new GradientColorKey[] { new GradientColorKey(new Color(0.85f, 0.95f, 1f), 0f), new GradientColorKey(new Color(0.3f, 0.55f, 1f), 0.5f), new GradientColorKey(new Color(0.2f, 0.35f, 1f), 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.7f, 0.5f), new GradientAlphaKey(0f, 1f) });
            // outer plume: wider, slower, blue fading to an orange tip
            Emitter(host, "plume", material, lifetime: 0.12f, speed: 1.2f, size: 0.032f, rate: 140f, angle: 6f,
                new GradientColorKey[] { new GradientColorKey(new Color(0.35f, 0.5f, 1f), 0f), new GradientColorKey(new Color(0.55f, 0.45f, 0.9f), 0.45f), new GradientColorKey(new Color(1f, 0.55f, 0.15f), 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0.45f, 0f), new GradientAlphaKey(0.35f, 0.6f), new GradientAlphaKey(0f, 1f) });

            var lightGo = new GameObject("glow");
            lightGo.layer = host.gameObject.layer;
            lightGo.transform.SetParent(host, false);
            lightGo.transform.localPosition = new Vector3(0f, 0f, 0.06f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.55f, 0.7f, 1f);
            light.intensity = 0.8f;
            light.range = 1.6f;
            light.shadows = LightShadows.None;
            light.enabled = false;
        }

        private static void Emitter(Transform host, string name, Material material, float lifetime, float speed, float size,
            float rate, float angle, GradientColorKey[] colors, GradientAlphaKey[] alphas)
        {
            var go = new GameObject(name);
            go.layer = host.gameObject.layer;
            go.transform.SetParent(host, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.8f, lifetime * 1.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.85f, speed * 1.1f);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.8f, size * 1.2f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            // local so the jet stays glued to the nozzle through sway and turns
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = 128;

            var emission = ps.emission;
            emission.rateOverTime = rate;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = angle;
            shape.radius = 0.002f;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(colors, alphas);
            col.color = gradient;

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.5f), new Keyframe(0.35f, 1f), new Keyframe(1f, 0.35f)));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static Material Material()
        {
            if (_material != null) return _material;
            if (_materialFailed) return null;
            var shader = ShadersFinder.Find("Legacy Shaders/Particles/Additive (Soft)")
                         ?? ShadersFinder.Find("Legacy Shaders/Particles/Additive");
            if (shader == null)
            {
                _materialFailed = true;
                Plugin.Log.LogWarning("[Blowtorch] no particle additive shader loaded, torch fires without a flame");
                return null;
            }
            _material = new Material(shader) { name = "manimal_torch_flame", mainTexture = SoftDot() };
            _material.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, 0.5f));
            return _material;
        }

        private static Texture2D SoftDot()
        {
            const int n = 32;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "manimal_torch_dot" };
            var pixels = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    byte v = (byte)(a * a * 255f);
                    pixels[y * n + x] = new Color32(v, v, v, v);
                }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return tex;
        }
    }
}
