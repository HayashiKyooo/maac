using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>全てのコマの再生が終わったら、フェードして指定シーンへ遷移する</summary>
public class PanelCompletedSceneLoader : MonoBehaviour
{
    [SerializeField] private PanelManager _panelManager;
    [SerializeField] private string _sceneName = "Battle";
    [Tooltip("最後のコマが終わってから遷移を始めるまでの時間 (秒)")]
    [SerializeField] private float _delay = 1.5f;
    [Tooltip("遷移前にフェードさせる全画面の Graphic（任意）")]
    [SerializeField] private Graphic _fadeGraphic;
    [SerializeField] private float _fadeDuration = 0.6f;

    private bool _loading;

    private void Awake()
    {
        if (_fadeGraphic != null)
        {
            _fadeGraphic.raycastTarget = false;
            var c = _fadeGraphic.color;
            c.a = 0f;
            _fadeGraphic.color = c;
        }
    }

    private void OnEnable()
    {
        if (_panelManager != null) _panelManager.Completed += OnCompleted;
    }

    private void OnDisable()
    {
        if (_panelManager != null) _panelManager.Completed -= OnCompleted;
    }

    private void OnCompleted()
    {
        if (_loading) return;
        _loading = true;
        LoadAsync().Forget();
    }

    private async UniTaskVoid LoadAsync()
    {
        var ct = destroyCancellationToken;
        await UniTask.Delay(System.TimeSpan.FromSeconds(_delay), cancellationToken: ct);

        if (_fadeGraphic != null)
        {
            var c = _fadeGraphic.color;
            var t = 0f;
            while (t < _fadeDuration)
            {
                c.a = t / _fadeDuration;
                _fadeGraphic.color = c;
                await UniTask.Yield(ct);
                t += Time.unscaledDeltaTime;
            }
            c.a = 1f;
            _fadeGraphic.color = c;
        }

        await SceneManager.LoadSceneAsync(_sceneName);
    }
}
