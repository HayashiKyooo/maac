using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

/// <summary>ヒット位置に漫画の擬音（バシッ / ガッ / ドォン）を一瞬表示する</summary>
public class BattleHitEffect : MonoBehaviour
{
    [SerializeField] private RectTransform _container;
    [SerializeField] private Camera _camera;
    [SerializeField] private Font _font;
    [SerializeField] private int _fontSize = 64;
    [SerializeField] private float _popDuration = 0.08f;
    [SerializeField] private float _holdDuration = 0.18f;
    [SerializeField] private float _fadeDuration = 0.2f;

    public void Spawn(Vector3 worldPosition, string text, float scale)
    {
        if (_container == null || _camera == null) return;
        SpawnAsync(worldPosition, text, scale, destroyCancellationToken).Forget();
    }

    private async UniTaskVoid SpawnAsync(Vector3 worldPosition, string text, float scale, CancellationToken ct)
    {
        var go = new GameObject("HitText", typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(_container, false);
        rt.sizeDelta = new Vector2(400f, 160f);

        var label = go.AddComponent<Text>();
        label.font = _font != null ? _font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = _fontSize;
        label.fontStyle = FontStyle.Normal; // フォント自体が Bold
        label.alignment = TextAnchor.MiddleCenter;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.color = Color.black;
        label.raycastTarget = false;
        label.text = text;
        var outline = go.AddComponent<Outline>();
        outline.effectColor = Color.white;
        outline.effectDistance = new Vector2(4f, -4f);

        var screen = RectTransformUtility.WorldToScreenPoint(_camera, worldPosition);
        var canvas = _container.GetComponentInParent<Canvas>();
        var uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_container, screen, uiCamera, out var local);
        rt.anchoredPosition = local + new Vector2(Random.Range(-20f, 20f), Random.Range(10f, 40f));
        rt.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-18f, 18f));

        try
        {
            var t = 0f;
            while (t < _popDuration)
            {
                rt.localScale = Vector3.one * (scale * Mathf.Lerp(1.8f, 1f, t / _popDuration));
                await UniTask.Yield(ct);
                t += Time.unscaledDeltaTime;
            }
            rt.localScale = Vector3.one * scale;
            await UniTask.Delay(System.TimeSpan.FromSeconds(_holdDuration), DelayType.UnscaledDeltaTime, cancellationToken: ct);
            t = 0f;
            while (t < _fadeDuration)
            {
                var a = 1f - t / _fadeDuration;
                label.color = new Color(0f, 0f, 0f, a);
                outline.effectColor = new Color(1f, 1f, 1f, a);
                await UniTask.Yield(ct);
                t += Time.unscaledDeltaTime;
            }
        }
        finally
        {
            if (go != null) Destroy(go);
        }
    }
}
