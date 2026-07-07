using System.Collections.Generic;
using UnityEngine;

namespace BF.Framework.SaveMission
{
    /// <summary>
    /// 任务定义 ScriptableObject。
    /// 描述任务的静态属性（目标、条件、奖励），不携带运行时可变状态。
    /// 不负责：任务进度追踪、奖励发放逻辑。
    /// </summary>
    [CreateAssetMenu(fileName = "BFMissionDefinition", menuName = "BF/Mission/Mission Definition")]
    public class MissionDefinition : ScriptableObject
    {
        [SerializeField] private string _missionId;
        [SerializeField] private string _title;
        [TextArea(2, 4)]
        [SerializeField] private string _description;
        [SerializeField] private ObjectiveType _objectiveType;
        [SerializeField] private string _targetId;
        [SerializeField] private int _targetCount = 1;
        [SerializeField] private int _rewardGold;
        [SerializeField] private List<MissionReward> _rewardItems = new();

        /// <summary>
        /// 任务唯一标识。
        /// </summary>
        public string MissionId => _missionId;

        /// <summary>
        /// 任务标题。
        /// </summary>
        public string Title => _title;

        /// <summary>
        /// 任务描述。
        /// </summary>
        public string Description => _description;

        /// <summary>
        /// 目标类型。
        /// </summary>
        public ObjectiveType ObjectiveType => _objectiveType;

        /// <summary>
        /// 目标对象 ID（单位 ID、物品 ID 等）。
        /// </summary>
        public string TargetId => _targetId;

        /// <summary>
        /// 目标完成数量。
        /// </summary>
        public int TargetCount => _targetCount;

        /// <summary>
        /// 金币奖励。
        /// </summary>
        public int RewardGold => _rewardGold;

        /// <summary>
        /// 物品奖励列表。
        /// </summary>
        public IReadOnlyList<MissionReward> RewardItems => _rewardItems;

        /// <summary>
        /// 匹配此任务的事件是否相关。
        /// </summary>
        public bool MatchesEvent(ObjectiveType type, string targetId)
        {
            if (_objectiveType != type) return false;
            if (string.IsNullOrEmpty(_targetId) || _targetId == "Any") return true;
            return _targetId == targetId;
        }
    }
}
