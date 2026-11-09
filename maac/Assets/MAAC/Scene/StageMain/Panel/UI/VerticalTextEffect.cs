using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// uGUI Text を縦書き（右の列から左へ、上から下へ）に並べ替える MeshEffect。
/// ・"\n" で改列、列の高さを超えたら自動で改列
/// ・句読点（、。）は右上へ寄せ、長音・三点リーダー・括弧などは 90° 回転
/// ・小書き文字（っゃゅょ など）は少し右上へ寄せる
/// Text は「Rich Text OFF / Horizontal・Vertical Overflow = Overflow」で使う（OnValidate で自動設定）。
/// 文字送り（Text.text を 1 文字ずつ伸ばす）とそのまま併用できる。
/// </summary>
[RequireComponent(typeof(Text))]
public class VerticalTextEffect : BaseMeshEffect
{
    [Tooltip("文字送り方向の間隔（フォントサイズ倍率）")]
    [Min(0.5f)]
    [SerializeField] private float _characterSpacing = 1.05f;

    [Tooltip("列の間隔（フォントサイズ倍率）")]
    [Min(0.5f)]
    [SerializeField] private float _columnSpacing = 1.5f;

    [Tooltip("右上に寄せる文字")]
    [SerializeField] private string _punctuationCharacters = "、。，．";

    [Tooltip("90° 回転させる文字")]
    [SerializeField] private string _rotateCharacters = "ー－−—―～〜…‥「」『』（）()［］[]【】〈〉《》｛｝{}＝=";

    [Tooltip("少し右上に寄せる小書き文字")]
    [SerializeField] private string _smallCharacters = "ぁぃぅぇぉっゃゅょゎァィゥェォッャュョヮヵヶ";

    private Text _text;
    private readonly List<UIVertex> _vertices = new();

    private Text Text => _text != null ? _text : (_text = GetComponent<Text>());

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive() || Text == null) return;

        var str = Text.text;
        if (string.IsNullOrEmpty(str)) return;

        _vertices.Clear();
        vh.GetUIVertexStream(_vertices);   // 1 文字 = 6 頂点（三角形 2 枚）
        var quadCount = _vertices.Count / 6;
        if (quadCount == 0) return;

        var fontSize = Text.fontSize;
        var cell = fontSize * _characterSpacing;
        var column = fontSize * _columnSpacing;
        var rect = Text.rectTransform.rect;
        var maxRows = Mathf.Max(1, Mathf.FloorToInt(rect.height / cell));

        var startX = rect.xMax - column * 0.5f;
        var startY = rect.yMax - cell * 0.5f;

        int col = 0, row = 0, quad = 0;
        foreach (var c in str)
        {
            if (c == '\n')
            {
                col++;
                row = 0;
                continue;
            }
            if (row >= maxRows)
            {
                col++;
                row = 0;
            }
            if (char.IsWhiteSpace(c) || char.IsControl(c))
            {
                row++;   // 空白は 1 マス空ける（メッシュは生成されない）
                continue;
            }
            if (quad >= quadCount) break;   // 想定外（リッチテキスト等）の場合は途中まで

            var target = new Vector2(startX - col * column, startY - row * cell);
            PlaceQuad(quad, c, target, fontSize);

            quad++;
            row++;
        }

        vh.Clear();
        vh.AddUIVertexTriangleStream(_vertices);
    }

    private void PlaceQuad(int quad, char c, Vector2 target, float fontSize)
    {
        var start = quad * 6;

        // 現在の文字の中心
        var min = new Vector2(float.MaxValue, float.MaxValue);
        var max = new Vector2(float.MinValue, float.MinValue);
        for (var i = 0; i < 6; i++)
        {
            var p = (Vector2)_vertices[start + i].position;
            min = Vector2.Min(min, p);
            max = Vector2.Max(max, p);
        }
        var center = (min + max) * 0.5f;

        var rotate = _rotateCharacters.IndexOf(c) >= 0;
        var offset = Vector2.zero;
        if (_punctuationCharacters.IndexOf(c) >= 0)
            offset = new Vector2(fontSize * 0.3f, fontSize * 0.3f);   // マスの右上へ
        else if (_smallCharacters.IndexOf(c) >= 0)
            offset = new Vector2(fontSize * 0.1f, fontSize * 0.1f);

        var anchor = center;
        var dest = target + offset;

        for (var i = 0; i < 6; i++)
        {
            var v = _vertices[start + i];
            var p = (Vector2)v.position - anchor;
            if (rotate) p = new Vector2(p.y, -p.x);   // 時計回りに 90°
            v.position = new Vector3(dest.x + p.x, dest.y + p.y, v.position.z);
            _vertices[start + i] = v;
        }
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        if (Text != null)
        {
            Text.supportRichText = false;
            Text.horizontalOverflow = HorizontalWrapMode.Overflow;
            Text.verticalOverflow = VerticalWrapMode.Overflow;
        }
    }
#endif
}
