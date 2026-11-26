using UnityEngine;

namespace GlowDust
{
    /// <summary>Fits a box renderer (GlowDust/PowderFog material) to the PowderVolume bounds so the smoke is ray-marched there.</summary>
    [RequireComponent(typeof(MeshRenderer))]
    public class PowderFogVolume : MonoBehaviour
    {
        public PowderVolume volume;

        void Start()
        {
            if (volume == null) volume = PowderVolume.Instance;
            if (volume == null) return;
            Vector3 size = new Vector3(volume.SizeX, volume.SizeY, volume.SizeZ) * volume.cellSize;
            transform.SetPositionAndRotation(volume.origin + size * 0.5f, Quaternion.identity);
            transform.localScale = size;
            var r = GetComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
    }
}
