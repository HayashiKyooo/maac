using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Player を指定位置まで自動で歩かせる演出タスク。
/// StagePlayerMover の入力を上書きして移動させるので、歩行アニメーション・重力・加減速は通常操作と同じ。
/// 到着（または タイムアウト）すると完了する。
/// ユーザー操作を止めたい場合は PanelView の Block Player Input を ON にする。
/// </summary>
public class MovePlayerToTargetTask : OnActivatePanelTaskBase
{
    [SerializeField] private StagePlayerMover _mover;
    [SerializeField] private Transform _destination;

    [Tooltip("この距離（水平）まで近づいたら到着とみなす")]
    [Min(0.01f)]
    [SerializeField] private float _arriveDistance = 0.2f;

    [Tooltip("ダッシュで移動する")]
    [SerializeField] private bool _sprint;

    [Tooltip("到着できなかった場合に打ち切る秒数（0 で無制限）")]
    [Min(0f)]
    [SerializeField] private float _timeout = 15f;

    public override async UniTask OnActivateAsync(CancellationToken cancellationToken)
    {
        if (_mover == null || _destination == null)
        {
            Debug.LogError($"[MovePlayerToTargetTask] {name}: Mover / Destination が未設定です", this);
            return;
        }

        var elapsed = 0f;
        try
        {
            while (true)
            {
                var diff = _destination.position - _mover.transform.position;
                diff.y = 0f;
                if (diff.sqrMagnitude <= _arriveDistance * _arriveDistance)
                    break;
                if (_timeout > 0f && elapsed >= _timeout)
                {
                    Debug.LogWarning($"[MovePlayerToTargetTask] {name}: {_timeout} 秒以内に目的地へ到着できませんでした", this);
                    break;
                }

                // 目的地の方向をワールドのヨー角で指定し、前入力として渡す
                var yaw = Mathf.Atan2(diff.x, diff.z) * Mathf.Rad2Deg;
                _mover.SetOverrideInput(new StagePlayerMover.MoveInput
                {
                    Move = Vector2.up,
                    Sprint = _sprint,
                    ReferenceYaw = yaw,
                });

                elapsed += Time.deltaTime;
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }
        }
        finally
        {
            if (_mover != null)
                _mover.ClearOverrideInput();
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (_destination == null) return;
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.9f);
        Gizmos.DrawWireSphere(_destination.position, _arriveDistance);
    }
}
