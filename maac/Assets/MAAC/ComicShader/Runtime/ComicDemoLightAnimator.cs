using UnityEngine;

namespace MAAC.ComicShader
{
    /// <summary>
    /// Demo helper: rotates / pulses the attached light in Play Mode so the manga tone,
    /// halftone and hatching response to light direction and intensity can be checked.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public class ComicDemoLightAnimator : MonoBehaviour
    {
        public bool rotate = true;
        public float yawSpeed = 20f;
        public bool pulseIntensity = true;
        public float minIntensity = 0.4f;
        public float maxIntensity = 2.0f;
        public float pulsePeriod = 6f;

        Light m_Light;

        void Awake() => m_Light = GetComponent<Light>();

        void Update()
        {
            if (rotate)
                transform.Rotate(Vector3.up, yawSpeed * Time.deltaTime, Space.World);
            if (pulseIntensity && m_Light != null && pulsePeriod > 0f)
            {
                float t = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.PI * 2f / pulsePeriod);
                m_Light.intensity = Mathf.Lerp(minIntensity, maxIntensity, t);
            }
        }
    }
}
