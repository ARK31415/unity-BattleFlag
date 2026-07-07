using System;
using System.Collections.Generic;
using UnityEngine;

namespace BF.Framework.SaveMission
{
    /// <summary>
    /// 任务进度快照，用于存档序列化。
    /// </summary>
    [Serializable]
    public class MissionProgressSnapshot
    {
        public List<MissionRuntimeData> Missions = new();
    }

    /// <summary>
    /// 任务服务。
    /// 管理任务激活、进度推进（统一 ReportEvent 入口）、完成判定与奖励领取。
    /// 不负责：UI 展示、存档 IO、任务定义加载。
    /// </summary>
    public class MissionService
    {
        private readonly Dictionary<string, MissionRuntimeData> _activeMissions = new();
        private readonly Dictionary<string, MissionDefinition> _missionCatalog = new();

        /// <summary>
        /// 任务完成事件（missionId）。
        /// </summary>
        public event Action<string> OnMissionCompleted;

        /// <summary>
        /// 任务进度更新事件（missionId, currentProgress, targetCount）。
        /// </summary>
        public event Action<string, int, int> OnMissionProgressUpdated;

        /// <summary>
        /// 从 ScriptableObject 列表加载任务目录。
        /// </summary>
        public void LoadCatalog(List<MissionDefinition> definitions)
        {
            _missionCatalog.Clear();
            foreach (var def in definitions)
            {
                if (def != null && !string.IsNullOrEmpty(def.MissionId))
                    _missionCatalog[def.MissionId] = def;
            }
            Debug.Log($"[MissionService] 加载任务目录: {_missionCatalog.Count} 个任务定义。");
        }

        /// <summary>
        /// 激活一个任务，开始追踪进度。
        /// </summary>
        public void ActivateMission(string missionId)
        {
            if (!_missionCatalog.TryGetValue(missionId, out var def))
            {
                Debug.LogWarning($"[MissionService] ActivateMission: 任务 '{missionId}' 不在目录中。");
                return;
            }

            if (_activeMissions.ContainsKey(missionId))
            {
                Debug.LogWarning($"[MissionService] ActivateMission: 任务 '{missionId}' 已激活。");
                return;
            }

            var runtime = new MissionRuntimeData
            {
                MissionId = missionId,
                TargetCount = def.TargetCount,
                CurrentProgress = 0,
                IsCompleted = false,
                IsClaimed = false,
                IsActive = true
            };

            _activeMissions[missionId] = runtime;
            Debug.Log($"[MissionService] 激活任务: {def.Title} ({missionId})");
        }

        /// <summary>
        /// 统一任务推进入口。
        /// 外部系统（战斗、交互等）通过此方法推送事件，
        /// 由服务内部匹配活跃任务并更新进度。
        /// </summary>
        /// <param name="type">目标类型。</param>
        /// <param name="targetId">目标对象 ID。</param>
        /// <param name="count">增量数量。</param>
        public void ReportEvent(ObjectiveType type, string targetId, int count = 1)
        {
            if (count <= 0) return;

            foreach (var kvp in _activeMissions)
            {
                var runtime = kvp.Value;
                if (runtime == null || !runtime.IsActive || runtime.IsCompleted) continue;

                if (!_missionCatalog.TryGetValue(kvp.Key, out var def)) continue;
                if (!def.MatchesEvent(type, targetId)) continue;

                bool justCompleted = runtime.UpdateProgress(count);
                OnMissionProgressUpdated?.Invoke(runtime.MissionId, runtime.CurrentProgress, runtime.TargetCount);

                if (justCompleted)
                {
                    Debug.Log($"[MissionService] 任务完成: {def.Title} ({runtime.MissionId})");
                    OnMissionCompleted?.Invoke(runtime.MissionId);
                }
            }
        }

        /// <summary>
        /// 获取所有活跃任务。
        /// </summary>
        public List<MissionRuntimeData> GetActiveMissions()
        {
            var result = new List<MissionRuntimeData>();
            foreach (var kvp in _activeMissions)
            {
                if (kvp.Value?.IsActive == true)
                    result.Add(kvp.Value);
            }
            return result;
        }

        /// <summary>
        /// 获取所有已完成但未领取奖励的任务。
        /// </summary>
        public List<MissionRuntimeData> GetCompletedUnclaimedMissions()
        {
            var result = new List<MissionRuntimeData>();
            foreach (var kvp in _activeMissions)
            {
                if (kvp.Value?.CanClaim() == true)
                    result.Add(kvp.Value);
            }
            return result;
        }

        /// <summary>
        /// 检查任务是否已完成。
        /// </summary>
        public bool IsMissionCompleted(string missionId)
        {
            return _activeMissions.TryGetValue(missionId, out var runtime)
                   && runtime.IsCompleted;
        }

        /// <summary>
        /// 检查任务是否已领取奖励。
        /// </summary>
        public bool IsMissionClaimed(string missionId)
        {
            return _activeMissions.TryGetValue(missionId, out var runtime)
                   && runtime.IsClaimed;
        }

        /// <summary>
        /// 领取任务奖励。返回奖励数据，由调用方执行实际的物品/金币发放。
        /// </summary>
        public ClaimResult ClaimReward(string missionId, ref int gold)
        {
            if (!_activeMissions.TryGetValue(missionId, out var runtime))
                return ClaimResult.Fail($"任务 '{missionId}' 未激活。");

            if (!runtime.CanClaim())
            {
                if (runtime.IsClaimed)
                    return ClaimResult.Fail($"任务 '{missionId}' 奖励已领取。");
                if (!runtime.IsCompleted)
                    return ClaimResult.Fail($"任务 '{missionId}' 尚未完成。");
            }

            if (!_missionCatalog.TryGetValue(missionId, out var def))
                return ClaimResult.Fail($"任务 '{missionId}' 定义不存在。");

            // 发放金币
            gold += def.RewardGold;

            // 发放物品
            var rewardedItems = new List<MissionReward>();
            foreach (var reward in def.RewardItems)
            {
                if (string.IsNullOrEmpty(reward.ItemId)) continue;
                // 注意：此处假设 ItemDefinition 由上层提供
                rewardedItems.Add(new MissionReward { ItemId = reward.ItemId, Quantity = reward.Quantity });
            }

            runtime.MarkClaimed();
            Debug.Log($"[MissionService] 奖励已领取: {def.Title}，金币 +{def.RewardGold}，物品 {def.RewardItems.Count} 种。");
            return ClaimResult.Ok(def.RewardGold, rewardedItems);
        }

        /// <summary>
        /// 获取任务定义。
        /// </summary>
        public MissionDefinition GetDefinition(string missionId)
        {
            _missionCatalog.TryGetValue(missionId, out var def);
            return def;
        }

        /// <summary>
        /// 序列化所有活跃任务状态为 JSON。
        /// </summary>
        public string SerializeState()
        {
            var snapshot = new MissionProgressSnapshot();
            foreach (var kvp in _activeMissions)
            {
                if (kvp.Value != null)
                    snapshot.Missions.Add(kvp.Value);
            }
            return JsonUtility.ToJson(snapshot);
        }

        /// <summary>
        /// 从 JSON 恢复活跃任务状态。
        /// </summary>
        public void RestoreState(string json)
        {
            if (string.IsNullOrEmpty(json)) return;

            var snapshot = JsonUtility.FromJson<MissionProgressSnapshot>(json);
            if (snapshot?.Missions == null) return;

            _activeMissions.Clear();
            foreach (var data in snapshot.Missions)
            {
                if (data != null && !string.IsNullOrEmpty(data.MissionId))
                    _activeMissions[data.MissionId] = data;
            }

            Debug.Log($"[MissionService] 已从存档恢复: {_activeMissions.Count} 个任务。");
        }
    }
}
