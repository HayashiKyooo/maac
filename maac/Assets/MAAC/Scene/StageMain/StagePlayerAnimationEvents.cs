using UnityEngine;

/// <summary>
/// Starter Assets のアニメーション（歩行/着地）に埋め込まれたイベント OnFootstep / OnLand の受け口。
/// ThirdPersonController を外したため、Animator と同じ GameObject に置いて足音を鳴らす。
/// </summary>
[RequireComponent(typeof(Animator))]
public class StagePlayerAnimationEvents : MonoBehaviour
{
    [SerializeField] private CharacterController _controller;
    [SerializeField] private AudioClip _landingAudioClip;
    [SerializeField] private AudioClip[] _footstepAudioClips;
    [Range(0, 1)] [SerializeField] private float _footstepAudioVolume = 0.3f;

    private Vector3 SoundPosition =>
        _controller != null ? transform.TransformPoint(_controller.center) : transform.position;

    private void OnFootstep(AnimationEvent animationEvent)
    {
        if (animationEvent.animatorClipInfo.weight > 0.5f && _footstepAudioClips != null && _footstepAudioClips.Length > 0)
        {
            var index = Random.Range(0, _footstepAudioClips.Length);
            AudioSource.PlayClipAtPoint(_footstepAudioClips[index], SoundPosition, _footstepAudioVolume);
        }
    }

    private void OnLand(AnimationEvent animationEvent)
    {
        if (animationEvent.animatorClipInfo.weight > 0.5f && _landingAudioClip != null)
            AudioSource.PlayClipAtPoint(_landingAudioClip, SoundPosition, _footstepAudioVolume);
    }
}
