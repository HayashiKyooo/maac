using UnityEngine;

namespace GlowDust
{
    /// <summary>Goal: a faintly glowing crystal. Reaching the zone clears the stage.</summary>
    public class GoalZone : MonoBehaviour
    {
        public GlowDustGame game;
        public Transform player;
        public Vector3 zoneSize = new Vector3(3f, 3f, 3f);
        public Transform crystal;
        public Material moteMaterial;
        public float spin = 40f;
        [Header("Beacon (faint periodic reveal so the goal can be spotted)")]
        public float beaconInterval = 3f;
        public float beaconRadius = 2.2f;
        public float beaconStrength = 1.2f;
        float nextBeacon;

        Vector3 crystalBase;

        void Start()
        {
            if (crystal != null) crystalBase = crystal.localPosition;
            if (moteMaterial != null) CreateMotes();
        }

        void CreateMotes()
        {
            var go = new GameObject("GoalMotes");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0, 1.2f, 0);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true; main.duration = 2f;
            main.startLifetime = 3f;
            main.startSpeed = 0.25f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
            main.startColor = new Color(1f, 0.8f, 0.35f, 0.6f);
            main.maxParticles = 100;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.03f;
            var em = ps.emission; em.rateOverTime = 10f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.6f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                      new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.3f), new GradientAlphaKey(0, 1) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = moteMaterial;
            ps.Play();
        }

        void Update()
        {
            if (crystal != null)
            {
                crystal.Rotate(0f, spin * Time.deltaTime, 0f, Space.World);
                crystal.localPosition = crystalBase + Vector3.up * (Mathf.Sin(Time.time * 1.5f) * 0.12f);
            }
            if (beaconInterval > 0f && Time.time >= nextBeacon && PowderVolume.Instance != null)
            {
                nextBeacon = Time.time + beaconInterval;
                Vector3 c = crystal != null ? crystal.position : transform.position + Vector3.up;
                PowderVolume.Instance.AddSource(c, beaconRadius, beaconStrength, beaconInterval * 0.9f, 0.2f);
            }
            if (player == null || game == null || game.Finished) return;
            Vector3 local = transform.InverseTransformPoint(player.position);
            if (Mathf.Abs(local.x) < zoneSize.x * 0.5f && local.y > -0.5f && local.y < zoneSize.y &&
                Mathf.Abs(local.z) < zoneSize.z * 0.5f)
                game.OnGoalReached();
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.6f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(new Vector3(0, zoneSize.y * 0.5f, 0), zoneSize);
        }
    }
}
