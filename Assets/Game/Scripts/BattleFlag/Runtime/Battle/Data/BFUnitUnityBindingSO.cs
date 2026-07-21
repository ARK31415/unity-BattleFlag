using UnityEngine;

namespace BF.Game.Runtime.Battle.Data
{
    /// <summary>
    /// 单位 Unity 资产绑定层，手动维护且不由外部策划表覆盖。
    /// </summary>
    [CreateAssetMenu(fileName = "BFUnitUnityBinding", menuName = "BF/Battle/Units/Unity Binding")]
    public class BFUnitUnityBindingSO : ScriptableObject
    {
        [Header("Prefab")]
        [SerializeField] private GameObject _overrideUnitPrefab;

        [Header("UI")]
        [SerializeField] private Sprite _icon;
        [SerializeField] private Sprite _portrait;

        [Header("Animation")]
        [SerializeField] private RuntimeAnimatorController _animatorController;
        [SerializeField] private string _idleAnimationKey = "Idle";
        [SerializeField] private string _moveAnimationKey = "Move";
        [SerializeField] private string _attackAnimationKey = "Attack";
        [SerializeField] private string _hurtAnimationKey = "Hurt";
        [SerializeField] private string _deathAnimationKey = "Death";

        [Header("Effects")]
        [SerializeField] private GameObject _hitVfxPrefab;
        [SerializeField] private AudioClip _hitSfx;

        public GameObject OverrideUnitPrefab => _overrideUnitPrefab;
        public Sprite Icon => _icon;
        public Sprite Portrait => _portrait;
        public RuntimeAnimatorController AnimatorController => _animatorController;
        public string IdleAnimationKey => _idleAnimationKey;
        public string MoveAnimationKey => _moveAnimationKey;
        public string AttackAnimationKey => _attackAnimationKey;
        public string HurtAnimationKey => _hurtAnimationKey;
        public string DeathAnimationKey => _deathAnimationKey;
        public GameObject HitVfxPrefab => _hitVfxPrefab;
        public AudioClip HitSfx => _hitSfx;
    }
}
