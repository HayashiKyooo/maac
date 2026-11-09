using System.Collections.Generic;
using UnityEngine;

/// <summary>波動拳（直線移動する弾）。相手に当たるか、相手の弾と相殺するか、画面外に出ると消える</summary>
public class HadoukenProjectile : MonoBehaviour
{
    [SerializeField] private float _radius = 0.25f;
    [SerializeField] private Transform _visual;
    [SerializeField] private float _spinSpeed = 540f;
    [Tooltip("ゆらゆら脈打つ見た目の強さ")]
    [SerializeField] private float _pulse = 0.12f;

    private static readonly List<HadoukenProjectile> s_active = new();

    private Fighter _owner;
    private int _direction;
    private float _speed;
    private FighterAttackData _attack;
    private BattleArena _arena;
    private bool _dead;
    private float _time;
    private Vector3 _visualScale = Vector3.one;

    public Fighter Owner => _owner;
    public int Direction => _direction;

    public void Launch(Fighter owner, int direction, float speed, FighterAttackData attack, BattleArena arena)
    {
        _owner = owner;
        _direction = direction;
        _speed = speed;
        _attack = attack;
        _arena = arena;
        if (_visual != null) _visualScale = _visual.localScale;
        s_active.Add(this);
    }

    private void OnDestroy()
    {
        s_active.Remove(this);
        if (_owner != null) _owner.NotifyProjectileGone(this);
    }

    private void Update()
    {
        if (_dead || _owner == null) return;
        if (_owner.IsInHitStop) return;

        var dt = Time.deltaTime;
        _time += dt;
        transform.position += new Vector3(_direction * _speed * dt, 0f, 0f);

        if (_visual != null)
        {
            _visual.Rotate(Vector3.right, _spinSpeed * dt, Space.World);
            _visual.localScale = _visualScale * (1f + Mathf.Sin(_time * 40f) * _pulse);
        }

        var pos = transform.position;

        // 相手の弾と相殺
        for (var i = 0; i < s_active.Count; i++)
        {
            var other = s_active[i];
            if (other == this || other._dead || other._owner == _owner) continue;
            if (Mathf.Abs(other.transform.position.x - pos.x) < _radius + other._radius)
            {
                other.Kill();
                Kill();
                return;
            }
        }

        // 相手に命中
        var opp = _owner.Opponent;
        if (opp != null && !opp.IsKO && opp.HurtOverlaps(pos, _radius))
        {
            opp.ReceiveAttack(_attack, _direction, pos, true);
            opp.ApplyHitStop(0.08f);
            Kill();
            return;
        }

        // 画面外
        if (_arena != null && !_arena.IsInsideView(pos.x, 1f))
            Kill();
    }

    private void Kill()
    {
        if (_dead) return;
        _dead = true;
        Destroy(gameObject);
    }
}
