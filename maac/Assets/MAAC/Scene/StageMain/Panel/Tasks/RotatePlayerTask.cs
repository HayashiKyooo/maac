using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 対象（Player）を、現在の正面を基準に Y 軸回りへ指定角度だけ回転させる演出タスク。
/// 角度は正で右回り（上から見て時計回り）、負で左回り。
/// </summary>
public class RotatePlayerTask : OnActivatePanelTaskBase
{
    [SerializeField] private Transform _target;

    [Tooltip("回転角度（度）。正 = 右, 負 = 左")]
    [SerializeField] private float _angle = 90f;

    [Tooltip("回転にかける秒数（0 で即時）")]
    [Min(0f)]
    [SerializeField] private float _duration = 0.4f;

    [SerializeField] private AnimationCurve _curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    public override async UniTask OnActivateAsync(CancellationToken cancellationToken)
    {
        if (_target == null)
        {
            Debug.LogError($"[RotatePlayerTask] {name}: Target が未設定です", this);
            return;
        }

        var startYaw = _target.eulerAngles.y;
        var endYaw = startYaw + _angle;

        if (_duration > 0f)
        {
            var elapsed = 0f;
            while (elapsed < _duration)
            {
                elapsed += Time.deltaTime;
                var t = _curve.Evaluate(Mathf.Clamp01(elapsed / _duration));
                _target.rotation = Quaternion.Euler(0f, Mathf.LerpUnclamped(startYaw, endYaw, t), 0f);
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }
        }

        _target.rotation = Quaternion.Euler(0f, endYaw, 0f);
    }
}
