using System;
using UnityEngine;

namespace BF.Framework.SaveMission
{
    /// <summary>
    /// 任务运行时数据。
    /// 记录单个任务的当前进度与完成状态，可序列化以支持存档。
    /// 不负责：任务条件校验、奖励发放。
    /// </summary>
    [Serializable]
    public class MissionRuntimeData
    {
        /// <summary>
        /// 关联的任务定义 ID。
        /// </summary>
        [field: SerializeField] public string MissionId { get; set; }

        /// <summary>
        /// 当前进度。
        /// </summary>
        [field: SerializeField] public int CurrentProgress { get; set; }

        /// <summary>
        /// 目标数量。
        /// </summary>
        [field: SerializeField] public int TargetCount { get; set; }

        /// <summary>
        /// 是否已完成。
        /// </summary>
        [field: SerializeField] public bool IsCompleted { get; set; }

        /// <summary>
        /// 是否已领取奖励。
        /// </summary>
        [field: SerializeField] public bool IsClaimed { get; set; }

        /// <summary>
        /// 是否处于激活状态。
        /// </summary>
        [field: SerializeField] public bool IsActive { get; set; } = true;

        /// <summary>
        /// 更新进度并检查完成状态。
        /// </summary>
        /// <param name="delta">进度增量。</param>
        /// <returns>是否在本次更新后达到完成条件。</returns>
        public bool UpdateProgress(int delta)
        {
            if (IsCompleted || !IsActive) return false;

            CurrentProgress = Mathf.Min(CurrentProgress + delta, TargetCount);

            if (CurrentProgress >= TargetCount)
            {
                IsCompleted = true;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 是否可以领取奖励。
        /// </summary>
        public bool CanClaim() => IsCompleted && !IsClaimed && IsActive;

        /// <summary>
        /// 标记为已领取。
        /// </summary>
        public void MarkClaimed()
        {
            IsClaimed = true;
        }

        /// <summary>
        /// 进度百分比（0.0 ~ 1.0）。
        /// </summary>
        public float ProgressRatio => TargetCount > 0 ? (float)CurrentProgress / TargetCount : 0f;
    }
}
