using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 漫画の吹き出し（四角 or 楕円、しっぽ付きも可）を描く uGUI Graphic。
/// テクスチャを使わずメッシュで描くので、サイズを変えても枠線の太さが一定。
/// RectTransform いっぱいに本体を描き、しっぽは本体の外へ伸びる。
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class SpeechBalloonGraphic : MaskableGraphic
{
    public enum BalloonShape
    {
        Rectangle,
        Ellipse,
    }

    [Header("吹き出し")]
    [SerializeField] private BalloonShape _shape = BalloonShape.Rectangle;
    [SerializeField] private Color _fillColor = Color.white;
    [SerializeField] private Color _outlineColor = Color.black;
    [Min(0f)]
    [SerializeField] private float _outlineWidth = 3f;
    [Range(16, 128)]
    [SerializeField] private int _segments = 64;

    [Header("しっぽ")]
    [SerializeField] private bool _showTail;
    [Tooltip("しっぽの付け根の位置（楕円上の角度。0=右, 90=上, 180=左, 270=下）")]
    [Range(0f, 360f)]
    [SerializeField] private float _tailAngle = 250f;
    [Tooltip("しっぽの付け根の幅 (px)")]
    [Min(1f)]
    [SerializeField] private float _tailBaseWidth = 36f;
    [Tooltip("しっぽの長さ (px)")]
    [Min(0f)]
    [SerializeField] private float _tailLength = 60f;
    [Tooltip("しっぽの曲がり（先端の向きを楕円の法線から回転させる角度）")]
    [Range(-80f, 80f)]
    [SerializeField] private float _tailBend = -20f;

    public override Texture mainTexture => s_WhiteTexture;

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        var rect = GetPixelAdjustedRect();
        var center = rect.center;
        var ow = Mathf.Min(_outlineWidth, Mathf.Min(rect.width, rect.height) * 0.25f);
        // 枠線の外側が Rect にちょうど収まるように内側（塗り）の半径を決める
        var fillRadius = new Vector2(Mathf.Max(rect.width * 0.5f - ow, 1f), Mathf.Max(rect.height * 0.5f - ow, 1f));
        var outlineRadius = fillRadius + new Vector2(ow, ow);

        var fill = _fillColor * color;
        var line = _outlineColor * color;

        // 枠線（大きい楕円＋しっぽ）を先に描き、その上に塗り（小さい楕円＋しっぽ）を重ねる。
        // こうすると楕円としっぽの枠線が継ぎ目なくつながる。
        if (ow > 0f)
        {
            AddBody(vh, center, outlineRadius, line);
            if (_showTail) AddTail(vh, center, fillRadius, ow, line, true);
        }
        AddBody(vh, center, fillRadius, fill);
        if (_showTail) AddTail(vh, center, fillRadius, ow, fill, false);
    }

    private void AddBody(VertexHelper vh, Vector2 center, Vector2 halfSize, Color32 c)
    {
        if (_shape == BalloonShape.Ellipse)
            AddEllipse(vh, center, halfSize, c);
        else
            AddRect(vh, center, halfSize, c);
    }

    private static void AddRect(VertexHelper vh, Vector2 center, Vector2 halfSize, Color32 c)
    {
        var start = vh.currentVertCount;
        vh.AddVert(center + new Vector2(-halfSize.x, -halfSize.y), c, Vector2.zero);
        vh.AddVert(center + new Vector2(-halfSize.x, halfSize.y), c, Vector2.zero);
        vh.AddVert(center + new Vector2(halfSize.x, halfSize.y), c, Vector2.zero);
        vh.AddVert(center + new Vector2(halfSize.x, -halfSize.y), c, Vector2.zero);
        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start + 2, start + 3, start);
    }

    private void AddEllipse(VertexHelper vh, Vector2 center, Vector2 radius, Color32 c)
    {
        var start = vh.currentVertCount;
        vh.AddVert(center, c, Vector2.zero);
        for (var i = 0; i <= _segments; i++)
        {
            var t = i / (float)_segments * Mathf.PI * 2f;
            vh.AddVert(center + new Vector2(Mathf.Cos(t) * radius.x, Mathf.Sin(t) * radius.y), c, Vector2.zero);
        }
        for (var i = 1; i <= _segments; i++)
            vh.AddTriangle(start, start + i, start + i + 1);
    }

    private void AddTail(VertexHelper vh, Vector2 center, Vector2 radius, float ow, Color32 c, bool isOutline)
    {
        var rad = _tailAngle * Mathf.Deg2Rad;
        var cos = Mathf.Cos(rad);
        var sin = Mathf.Sin(rad);
        var basePoint = center + new Vector2(cos * radius.x, sin * radius.y);

        // 楕円の法線（外向き）と接線
        var normal = new Vector2(cos / radius.x, sin / radius.y).normalized;
        var tangent = new Vector2(-normal.y, normal.x);
        var dir = (Vector2)(Quaternion.Euler(0f, 0f, _tailBend) * normal);

        var halfWidth = _tailBaseWidth * 0.5f;
        // 付け根は楕円の内側に食い込ませる（塗りの楕円で隠れる）
        var inset = ow * 2f + 4f;

        Vector2 a, b, tip;
        if (isOutline)
        {
            // 枠線の太さ分だけ一回り大きい三角形
            a = basePoint - tangent * (halfWidth + ow * 1.5f) - normal * inset;
            b = basePoint + tangent * (halfWidth + ow * 1.5f) - normal * inset;
            tip = basePoint + dir * (_tailLength + ow * 2.5f);
        }
        else
        {
            a = basePoint - tangent * halfWidth - normal * inset;
            b = basePoint + tangent * halfWidth - normal * inset;
            tip = basePoint + dir * _tailLength;
        }

        var start = vh.currentVertCount;
        vh.AddVert(a, c, Vector2.zero);
        vh.AddVert(b, c, Vector2.zero);
        vh.AddVert(tip, c, Vector2.zero);
        vh.AddTriangle(start, start + 1, start + 2);
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        SetVerticesDirty();
    }
#endif
}
