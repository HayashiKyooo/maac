using System;
using UnityEngine;

/// <summary>攻撃の当たる高さ。ガードの可否に使う</summary>
public enum AttackHeight
{
    /// <summary>立ち・しゃがみどちらでもガード可能</summary>
    Mid,
    /// <summary>しゃがみガードのみ可能（下段）</summary>
    Low,
    /// <summary>立ちガードのみ可能（ジャンプ攻撃）</summary>
    Overhead,
}

/// <summary>ファイターの行動状態</summary>
public enum FighterAction
{
    None,
    Attack,
    Special,
    HitStun,
    BlockStun,
    KO,
}

public enum FighterAttackKind
{
    Stand,
    Crouch,
    Air,
}

/// <summary>1 フレーム分の操作（キーボード / AI 共通）</summary>
public struct FighterCommand
{
    /// <summary>左右入力（ワールド X 方向。-1: 左, 0, 1: 右）</summary>
    public int Move;
    public bool Crouch;
    /// <summary>ジャンプを押した瞬間</summary>
    public bool Jump;
    /// <summary>攻撃を押した瞬間</summary>
    public bool Attack;
    /// <summary>必殺技（波動拳）コマンドが成立した瞬間</summary>
    public bool Special;
}

/// <summary>攻撃 1 種類分のパラメータ</summary>
[Serializable]
public class FighterAttackData
{
    public int Damage = 8;
    [Tooltip("発生までの時間 (秒)")]
    public float Startup = 0.08f;
    [Tooltip("攻撃判定が出ている時間 (秒)")]
    public float Active = 0.1f;
    [Tooltip("硬直 (秒)")]
    public float Recovery = 0.2f;
    [Tooltip("体の中心から前方へのリーチ (m)")]
    public float Reach = 0.95f;
    [Tooltip("攻撃判定の高さ（足元からの相対, m）")]
    public float HitMinHeight = 0.8f;
    public float HitMaxHeight = 1.6f;
    public AttackHeight Height = AttackHeight.Mid;
    public float HitStun = 0.35f;
    public float BlockStun = 0.2f;
    [Tooltip("ヒット時に相手を押し出す速度 (m/s)")]
    public float Knockback = 2.5f;

    public float TotalDuration => Startup + Active + Recovery;
}

/// <summary>見た目（ボーンの手続き的ポーズ）用の重み。Fighter が毎フレーム計算する</summary>
public struct FighterPose
{
    public float Crouch;
    public float Guard;
    public float Punch;
    public float AirKick;
    public float SpecialCharge;
    public float SpecialRelease;
    public float Hit;
}

/// <summary>ファイターの入力ソース（キーボード / AI）</summary>
public abstract class FighterInputBase : MonoBehaviour
{
    public abstract FighterCommand ReadCommand(Fighter self, float deltaTime);
}
