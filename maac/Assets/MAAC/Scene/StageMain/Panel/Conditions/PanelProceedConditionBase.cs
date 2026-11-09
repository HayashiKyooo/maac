using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 「次のコマへ進んでよい」条件の基底クラス。
/// PanelView の _proceedConditions に設定し、条件を満たしたら WaitAsync を完了させる。
/// 新しい条件はこのクラスを継承して WaitAsync を実装するだけで追加できる。
/// </summary>
public abstract class PanelProceedConditionBase : MonoBehaviour
{
    /// <param name="panel">この条件を持つコマ</param>
    public abstract UniTask WaitAsync(PanelView panel, CancellationToken cancellationToken);
}
