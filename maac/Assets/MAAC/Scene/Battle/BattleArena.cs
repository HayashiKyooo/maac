using UnityEngine;

/// <summary>
/// 固定カメラの格闘ステージ。
/// ファイターが画面外に出ようとしたら画面内へ押し戻し、2 人の体が重ならないように押し合う。
/// </summary>
[DefaultExecutionOrder(50)]
public class BattleArena : MonoBehaviour
{
    [SerializeField] private Camera _camera;
    [Tooltip("ファイターが並ぶ奥行き（ワールド Z）")]
    [SerializeField] private float _depthZ = 67.9f;
    [SerializeField] private float _groundY;
    [Tooltip("画面端からの余白 (m)")]
    [SerializeField] private float _screenMargin = 0.1f;
    [Tooltip("この高さ以上離れていれば、押し合わずにすり抜けられる（飛び越え）")]
    [SerializeField] private float _pushVerticalThreshold = 1.2f;
    [SerializeField] private Fighter[] _fighters;

    public Camera Camera => _camera;
    public float DepthZ => _depthZ;
    public float GroundY => _groundY;

    /// <summary>ファイターの奥行きでのカメラの左右端（ワールド X）</summary>
    public void GetViewBounds(out float minX, out float maxX)
    {
        if (_camera == null)
        {
            minX = float.NegativeInfinity;
            maxX = float.PositiveInfinity;
            return;
        }
        minX = ViewportXToWorld(0f);
        maxX = ViewportXToWorld(1f);
        if (minX > maxX) (minX, maxX) = (maxX, minX);
    }

    private float ViewportXToWorld(float vx)
    {
        var ray = _camera.ViewportPointToRay(new Vector3(vx, 0.5f, 0f));
        if (Mathf.Abs(ray.direction.z) < 1e-5f) return ray.origin.x;
        var t = (_depthZ - ray.origin.z) / ray.direction.z;
        return ray.origin.x + ray.direction.x * t;
    }

    public bool IsInsideView(float x, float extra)
    {
        GetViewBounds(out var min, out var max);
        return x >= min - extra && x <= max + extra;
    }

    private void LateUpdate()
    {
        if (_fighters == null) return;
        GetViewBounds(out var min, out var max);

        foreach (var f in _fighters)
            if (f != null) Clamp(f, min, max);

        if (_fighters.Length >= 2 && _fighters[0] != null && _fighters[1] != null)
            Separate(_fighters[0], _fighters[1], min, max);
    }

    private void Clamp(Fighter f, float min, float max)
    {
        var x = f.transform.position.x;
        var lo = min + f.BodyHalfWidth + _screenMargin;
        var hi = max - f.BodyHalfWidth - _screenMargin;
        var clamped = Mathf.Clamp(x, lo, hi);
        if (!Mathf.Approximately(clamped, x)) f.SetPositionX(clamped);
    }

    private void Separate(Fighter a, Fighter b, float min, float max)
    {
        if (a.IsKO || b.IsKO) return;
        var pa = a.transform.position;
        var pb = b.transform.position;
        if (Mathf.Abs(pa.y - pb.y) >= _pushVerticalThreshold) return;

        var dx = pb.x - pa.x;
        var need = a.BodyHalfWidth + b.BodyHalfWidth;
        var overlap = need - Mathf.Abs(dx);
        if (overlap <= 0f) return;

        var sign = Mathf.Abs(dx) > 1e-4f ? Mathf.Sign(dx) : (float)a.FacingSign;
        a.SetPositionX(pa.x - sign * overlap * 0.5f);
        b.SetPositionX(pb.x + sign * overlap * 0.5f);
        Clamp(a, min, max);
        Clamp(b, min, max);

        // 片方が画面端で止まった場合は、もう片方を押し出す
        pa = a.transform.position;
        pb = b.transform.position;
        overlap = need - Mathf.Abs(pb.x - pa.x);
        if (overlap > 0.0001f)
        {
            var aAtEdge = Mathf.Abs(pa.x - (sign > 0 ? min + a.BodyHalfWidth + _screenMargin : max - a.BodyHalfWidth - _screenMargin)) < 0.001f;
            if (aAtEdge) b.SetPositionX(pa.x + sign * need);
            else a.SetPositionX(pb.x - sign * need);
            Clamp(a, min, max);
            Clamp(b, min, max);
        }
    }
}
