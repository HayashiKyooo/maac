using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 対象（Player など）が指定位置の半径内に入ったら満たされる条件。
/// </summary>
public class PlayerReachedCondition : PanelProceedConditionBase
{
    [Tooltip("到達を判定する対象（Player）")]
    [SerializeField] private Transform _target;

    [Tooltip("目的地")]
    [SerializeField] private Transform _destination;

    [Min(0f)]
    [SerializeField] private float _radius = 1.0f;

    [Tooltip("高さ（Y）を無視して水平距離だけで判定する")]
    [SerializeField] private bool _ignoreHeight = true;

    public override UniTask WaitAsync(PanelView panel, CancellationToken cancellationToken)
    {
        if (_target == null || _destination == null)
        {
            Debug.LogError($"[PlayerReachedCondition] {name}: Target / Destination が未設定です", this);
            return UniTask.Never(cancellationToken);
        }
        return UniTask.WaitUntil(IsReached, PlayerLoopTiming.Update, cancellationToken);
    }

    private bool IsReached()
    {
        var diff = _target.position - _destination.position;
        if (_ignoreHeight) diff.y = 0f;
        return diff.sqrMagnitude <= _radius * _radius;
    }

    private void OnDrawGizmosSelected()
    {
        if (_destination == null) return;
        Gizmos.color = new Color(0f, 0.6f, 1f, 0.8f);
        Gizmos.DrawWireSphere(_destination.position, _radius);
    }
}
