using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// コマが有効になった瞬間に、対象を映すようにカメラを配置する演出タスク。
/// 対象が演出で動いた後の位置（ぶつかる位置・倒れた顔など）に合わせてカメラを置ける。
///  ・World     : 注視点 + Offset（ワールド座標）
///  ・SideOfLine: Targets[0] → Targets[1] を結ぶ線の横から見る（Targets[0] が画面左に映る）。
///                Offset = (線方向, 上, 横の距離)
///  ・Local     : 注視点 + SpaceReference のローカル座標での Offset
/// </summary>
public class FrameTargetsCameraTask : OnActivatePanelTaskBase
{
    public enum OffsetSpace
    {
        World,
        SideOfLine,
        Local,
    }

    [SerializeField] private Camera _camera;
    [Tooltip("注視点はこれらの平均位置")]
    [SerializeField] private Transform[] _targets;
    [Tooltip("注視点に足すオフセット（ワールド）")]
    [SerializeField] private Vector3 _lookOffset = new(0f, 0.8f, 0f);

    [SerializeField] private OffsetSpace _offsetSpace = OffsetSpace.SideOfLine;
    [SerializeField] private Vector3 _offset = new(0f, 0.6f, 5f);
    [Tooltip("Local の基準（未設定なら Targets[0]）")]
    [SerializeField] private Transform _spaceReference;

    [Tooltip("0 より大きければ Field of View を設定する")]
    [Min(0f)]
    [SerializeField] private float _fieldOfView;

    public override UniTask OnActivateAsync(CancellationToken cancellationToken)
    {
        Apply();
        return UniTask.CompletedTask;
    }

    [ContextMenu("Apply Now")]
    public void Apply()
    {
        if (_camera == null || _targets == null || _targets.Length == 0 || _targets[0] == null)
        {
            Debug.LogError($"[FrameTargetsCameraTask] {name}: Camera / Targets が未設定です", this);
            return;
        }

        var focus = Vector3.zero;
        var count = 0;
        foreach (var t in _targets)
        {
            if (t == null) continue;
            focus += t.position;
            count++;
        }
        focus = focus / count + _lookOffset;

        Vector3 position;
        var cameraUp = Vector3.up;
        switch (_offsetSpace)
        {
            case OffsetSpace.SideOfLine when _targets.Length >= 2 && _targets[1] != null:
            {
                var line = _targets[1].position - _targets[0].position;
                line.y = 0f;
                line = line.sqrMagnitude > 1e-6f ? line.normalized : Vector3.forward;
                var side = Vector3.Cross(Vector3.up, line);   // この側から見ると Targets[0] が左に映る
                position = focus + line * _offset.x + Vector3.up * _offset.y + side * _offset.z;
                break;
            }
            case OffsetSpace.Local:
            {
                var reference = _spaceReference != null ? _spaceReference : _targets[0];
                position = focus + reference.TransformDirection(_offset);
                cameraUp = reference.up;   // 寝転んだ対象を真上から映しても向きが安定するように
                break;
            }
            default:
                position = focus + _offset;
                break;
        }

        _camera.transform.position = position;
        _camera.transform.LookAt(focus, cameraUp);
        if (_fieldOfView > 0f)
            _camera.fieldOfView = _fieldOfView;
    }
}
