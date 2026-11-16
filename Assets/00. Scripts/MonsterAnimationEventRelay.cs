using UnityEngine;

/// <summary>
/// 애니메이션 이벤트는 Animator가 붙은 오브젝트에만 전달된다.
/// Animator가 몬스터 루트의 하위 오브젝트에 있을 때 이벤트를 루트의 Monster로 넘겨준다.
/// Monster.Awake에서 필요할 때 자동으로 붙는다.
/// </summary>
[DisallowMultipleComponent]
public class MonsterAnimationEventRelay : MonoBehaviour
{
    private Monster owner;

    private void Awake()
    {
        owner = GetComponentInParent<Monster>();
    }

    // Attack 클립의 Animation Event
    public void OnAttackHit()
    {
        if (owner == null)
            owner = GetComponentInParent<Monster>();
        if (owner != null)
            owner.OnAttackHit();
    }
}
