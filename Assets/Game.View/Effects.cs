using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace AdventurerCity.View
{
    public static class Effects
    {
        static Material particleMaterial;

        static Material ParticleMaterial
        {
            get
            {
                if (particleMaterial != null) return particleMaterial;
                particleMaterial = new Material(PixelArt.LitMaterial) { mainTexture = PixelArt.Dot().texture };
                return particleMaterial;
            }
        }

        public static Light2D Light(string name, Transform parent, Light2D.LightType type)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var light = go.AddComponent<Light2D>();
            light.lightType = type;
            return light;
        }

        public static Light2D PointLight(string name, Transform parent, Color color, float radius, float intensity)
        {
            var light = Light(name, parent, Light2D.LightType.Point);
            light.color = color;
            light.intensity = intensity;
            light.pointLightOuterRadius = radius;
            light.pointLightInnerRadius = radius * 0.15f;
            light.falloffIntensity = 0.6f;
            return light;
        }

        public static ParticleSystem Smoke(Transform parent, int order)
        {
            var ps = Create("Smoke", parent, order, 40);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 3.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.22f, 0.4f);
            main.startColor = new Color(0.82f, 0.82f, 0.86f, 0.5f);
            var emission = ps.emission;
            emission.rateOverTime = 3f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 10f;
            shape.radius = 0.05f;
            Fade(ps, 0.5f, 0.6f, 2f);
            Wind(ps, 0.12f);
            ps.Play();
            return ps;
        }

        public static ParticleSystem Sparks(Transform parent, int order)
        {
            var ps = Create("Sparks", parent, order, 30);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.1f);
            main.startColor = new Color(1f, 0.7f, 0.25f, 1f);
            main.gravityModifier = 0.25f;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 4, 8, -1, 0.9f) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 35f;
            shape.radius = 0.05f;
            Fade(ps, 1f, 1f, 0.4f);
            ps.Play();
            return ps;
        }

        public static ParticleSystem Burst(Transform parent, int order, float speed, float size)
        {
            var ps = Create("Burst", parent, order, 256);
            ps.transform.localRotation = Quaternion.identity;
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.5f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
            main.gravityModifier = 0.4f;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.15f;
            Fade(ps, 1f, 1f, 0.3f);
            ps.Play();
            return ps;
        }

        static ParticleSystem Create(string name, Transform parent, int order, int max)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = max;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = ParticleMaterial;
            renderer.sortingOrder = order;
            return ps;
        }

        static void Fade(ParticleSystem ps, float startAlpha, float startSize, float endSize)
        {
            var color = ps.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(startAlpha, 0f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, startSize, 1f, endSize));
        }

        static void Wind(ParticleSystem ps, float strength)
        {
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(strength);
            velocity.y = new ParticleSystem.MinMaxCurve(0f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f);
        }
    }
}
