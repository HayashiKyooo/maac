using System.Threading;
using Cysharp.Threading.Tasks;

/// <summary>
/// コマの演出（文字送りなど OnActivatePanelTask）が全て終わったら満たされる条件。
/// 例: 吹き出しのセリフが全て表示し終わったら次のコマへ。
/// </summary>
public class ActivateCompletedCondition : PanelProceedConditionBase
{
    public override UniTask WaitAsync(PanelView panel, CancellationToken cancellationToken)
    {
        return panel.ActivateCompletedTask.AttachExternalCancellation(cancellationToken);
    }
}
