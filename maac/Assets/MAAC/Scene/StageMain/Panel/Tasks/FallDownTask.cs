using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// キャラクターを後ろ（または前）に倒す演出タスク（転倒アニメーションのクリップが無くても使える手続き的な転倒）。
/// 足元を支点に体を倒し、少し後ろへ滑ってから軽く跳ね返る。
/// CharacterController を持つ場合は、倒れている間 CharacterController を無効にする
/// （CharacterController の当たり判定は傾けられないため。StagePlayerMover は無効中は動かない）。
/// </summary>
public class FallDownTask : OnActivatePanelTaskBase
{
    [SerializeField] private Transform _target;
    [Tooltip("倒れる瞬間のポーズで Animator を止める")]
    [SerializeField] private Animator _animatorToFreeze;

    [Tooltip("倒れる角度（度）。正 = 後ろ向き, 負 = 前向き")]
    [Range(-90f, 90f)]
    [SerializeField] private float _fallAngle = 82f;
    [Tooltip("倒れるまでの秒数")]
    [Min(0.01f)]
    [SerializeField] private float _duration = 0.45f;
    [Tooltip("倒れながら後ろへ滑る距離 (m)")]
    [SerializeField] private float _slideBack = 0.5f;
    [Tooltip("着地後に跳ね返る角度（度）")]
    [Min(0f)]
    [SerializeField] private float _bounceAngle = 7f;
    [Min(0f)]
    [SerializeField] private float _bounceDuration = 0.18f;
    [Tooltip("倒れた後も CharacterController を無効のままにする（起き上がらせる演出を別に作るまでは ON）")]
    [SerializeField] private bool _keepControllerDisabled = true;

    public override async UniTask OnActivateAsync(CancellationToken cancellationToken)
    {
        if (_target == null)
        {
            Debug.LogError($"[FallDownTask] {name}: Target が未設定です", this);
            return;
        }

        if (_animatorToFreeze != null)
            _animatorToFreeze.speed = 0f;

        _target.TryGetComponent(out CharacterController controller);
        var controllerWasEnabled = controller != null && controller.enabled;
        if (controllerWasEnabled) controller.enabled = false;

        var startRot = _target.rotation;
        var axis = _target.right;
        var back = -_target.forward;
        back.y = 0f;
        back.Normalize();
        if (_fallAngle < 0f) back = -back;

        var slid = 0f;
        void Apply(float angle, float slide)
        {
            _target.rotation = Quaternion.AngleAxis(-angle, axis) * startRot;
            var delta = back * (slide - slid);
            slid = slide;
            _target.position += delta;
        }

        // 倒れる（重力っぽく加速）
        var t = 0f;
        while (t < _duration)
        {
            t += Time.deltaTime;
            var k = Mathf.Clamp01(t / _duration);
            k *= k;
            Apply(_fallAngle * k, _slideBack * Mathf.Sqrt(k));
            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
        }
        Apply(_fallAngle, _slideBack);

        // 軽く跳ね返る
        if (_bounceAngle > 0f && _bounceDuration > 0f)
        {
            var sign = Mathf.Sign(_fallAngle);
            t = 0f;
            while (t < _bounceDuration)
            {
                t += Time.deltaTime;
                var k = Mathf.Sin(Mathf.Clamp01(t / _bounceDuration) * Mathf.PI);
                Apply(_fallAngle - sign * _bounceAngle * k, _slideBack);
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }
            Apply(_fallAngle, _slideBack);
        }

        if (controllerWasEnabled && !_keepControllerDisabled)
            controller.enabled = true;
    }
}
