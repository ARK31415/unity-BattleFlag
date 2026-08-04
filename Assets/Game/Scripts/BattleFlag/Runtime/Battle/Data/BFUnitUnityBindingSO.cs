using UnityEngine;

namespace BF.Game.Runtime.Battle.Data
{
    /// <summary>
    /// 单位 Unity 资产绑定层（ScriptableObject），手动维护且不由外部策划表覆盖。
    /// 保存表现资源和可选特殊 Prefab，不负责基础数值、成长、关卡布阵或运行时状态。
    /// </summary>
    [CreateAssetMenu(fileName = "BFUnitUnityBinding", menuName = "BF/Battle/Units/Unity Binding")]
    public class BFUnitUnityBindingSO : ScriptableObject
    {
        [Header("Prefab")]
        /// <summary>可选特殊实体模板，为空时使用默认 Prefab。</summary>
        [SerializeField] private GameObject _overrideUnitPrefab;

        [Header("UI")]
        /// <summary>单位图标（UI 展示用）。</summary>
        [SerializeField] private Sprite _icon;
        /// <summary>单位立绘（UI 展示用）。</summary>
        [SerializeField] private Sprite _portrait;

        [Header("Animation")]
        /// <summary>单位动画控制器。</summary>
        [SerializeField] private RuntimeAnimatorController _animatorController;
        /// <summary>Idle 动画键。</summary>
        [SerializeField] private string _idleAnimationKey = "Idle";
        /// <summary>Move 动画键。</summary>
        [SerializeField] private string _moveAnimationKey = "Move";
        /// <summary>Attack 动画键。</summary>
        [SerializeField] private string _attackAnimationKey = "Attack";
        /// <summary>Hurt 动画键。</summary>
        [SerializeField] private string _hurtAnimationKey = "Hurt";
        /// <summary>Death 动画键。</summary>
        [SerializeField] private string _deathAnimationKey = "Death";

        [Header("Effects")]
        /// <summary>命中特效预制体。</summary>
        [SerializeField] private GameObject _hitVfxPrefab;
        /// <summary>命中音效。</summary>
        [SerializeField] private AudioClip _hitSfx;

        /// <summary>可选特殊实体模板，为空时使用默认 Prefab。</summary>
        public GameObject OverrideUnitPrefab => _overrideUnitPrefab;
        /// <summary>单位图标。</summary>
        public Sprite Icon => _icon;
        /// <summary>单位立绘。</summary>
        public Sprite Portrait => _portrait;
        /// <summary>单位动画控制器。</summary>
        public RuntimeAnimatorController AnimatorController => _animatorController;
        /// <summary>Idle 动画键。</summary>
        public string IdleAnimationKey => _idleAnimationKey;
        /// <summary>Move 动画键。</summary>
        public string MoveAnimationKey => _moveAnimationKey;
        /// <summary>Attack 动画键。</summary>
        public string AttackAnimationKey => _attackAnimationKey;
        /// <summary>Hurt 动画键。</summary>
        public string HurtAnimationKey => _hurtAnimationKey;
        /// <summary>Death 动画键。</summary>
        public string DeathAnimationKey => _deathAnimationKey;
        /// <summary>命中特效预制体。</summary>
        public GameObject HitVfxPrefab => _hitVfxPrefab;
        /// <summary>命中音效。</summary>
        public AudioClip HitSfx => _hitSfx;
    }
}
