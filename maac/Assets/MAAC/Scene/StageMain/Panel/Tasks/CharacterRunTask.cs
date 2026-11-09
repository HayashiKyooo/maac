using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// CharacterController を持たないキャラクター（NPC など）を、走りアニメーション付きで移動させる演出タスク。
/// ・Destination モード: 指定位置まで走る
/// ・StopNearTarget モード: 動いている相手（Player など）に指定距離まで近づいたら止まる（ぶつかる演出用）
/// 移動はスクリプトで行う（Animator の Apply Root Motion は OFF 推奨）。
/// </summary>
public class CharacterRunTask : OnActivatePanelTaskBase
{
    public enum TargetMode
    {
        Destination,
        StopNearTarget,
    }

    [SerializeField] private Transform _character;
    [SerializeField] private Animator _animator;
    [Tooltip("走りアニメーションを再生する Animator の bool パラメータ名")]
    [SerializeField] private string _runParameter = "Run";

    [SerializeField] private TargetMode _mode = TargetMode.Destination;
    [SerializeField] private Transform _destination;
    [Tooltip("Destination: 到着とみなす距離 / StopNearTarget: この距離まで近づいたら止まる")]
    [Min(0.01f)]
    [SerializeField] private float _stopDistance = 0.2f;

    [Tooltip("移動速度 (m/s)")]
    [Min(0f)]
    [SerializeField] private float _speed = 5f;
    [Tooltip("向き直る速さ (度/秒)")]
    [Min(0f)]
    [SerializeField] private float _turnSpeed = 720f;
    [Tooltip("終了時に走りアニメーションを止める（次の演出でも走り続けるなら OFF）")]
    [SerializeField] private bool _stopRunAnimationAtEnd = true;
    [Tooltip("打ち切り秒数（0 で無制限）")]
    [Min(0f)]
    [SerializeField] private float _timeout = 10f;

    public override async UniTask OnActivateAsync(CancellationToken cancellationToken)
    {
        if (_character == null || _destination == null)
        {
            Debug.LogError($"[CharacterRunTask] {name}: Character / Destination が未設定です", this);
            return;
        }

        var hasRunParam = _animator != null && HasBool(_animator, _runParameter);
        var elapsed = 0f;
        try
        {
            if (hasRunParam) _animator.SetBool(_runParameter, true);

            while (true)
            {
                var pos = _character.position;
                var diff = _destination.position - pos;
                diff.y = 0f;
                var distance = diff.magnitude;
                if (distance <= _stopDistance + 0.001f) break;
                if (_timeout > 0f && elapsed >= _timeout) break;

                var dir = diff / Mathf.Max(distance, 1e-5f);
                var targetRot = Quaternion.LookRotation(dir, Vector3.up);
                _character.rotation = Quaternion.RotateTowards(_character.rotation, targetRot, _turnSpeed * Time.deltaTime);

                var step = _speed * Time.deltaTime;
                if (step >= distance - _stopDistance)
                {
                    // 残りを一気に詰めて到着
                    _character.position = pos + dir * Mathf.Max(0f, distance - _stopDistance);
                    break;
                }
                _character.position = pos + dir * step;

                elapsed += Time.deltaTime;
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }
        }
        finally
        {
            if (hasRunParam && _stopRunAnimationAtEnd && _animator != null)
                _animator.SetBool(_runParameter, false);
        }
    }

    private static bool HasBool(Animator animator, string parameter)
    {
        if (animator.runtimeAnimatorController == null) return false;
        foreach (var p in animator.parameters)
            if (p.type == AnimatorControllerParameterType.Bool && p.name == parameter) return true;
        return false;
    }
}
