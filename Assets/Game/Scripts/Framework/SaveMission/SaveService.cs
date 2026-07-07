using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace BF.Framework.SaveMission
{
    /// <summary>
    /// 存档服务。
    /// 管理全局进度与战前存档的持久化（JSON 文件）。
    /// 第一版使用 Application.persistentDataPath 文件存储，
    /// 支持主存档 + 备份槽位。
    /// 不负责：存档数据模型定义、UI 展示。
    /// </summary>
    public class SaveService
    {
        private readonly string _saveDirectory;

        /// <summary>
        /// 创建存档服务。
        /// </summary>
        public SaveService()
        {
            _saveDirectory = Application.persistentDataPath;
        }

        /// <summary>
        /// 保存存档数据。
        /// </summary>
        /// <param name="data">存档数据。</param>
        /// <param name="slotName">槽位名称，默认 "default"。</param>
        /// <returns>是否保存成功。</returns>
        public bool Save(SaveData data, string slotName = "default")
        {
            if (data == null)
            {
                Debug.LogError("[SaveService] Save: data 为 null。");
                return false;
            }

            data.SavedAtTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            try
            {
                string filePath = GetSlotPath(slotName);
                string json = data.ToJson();

                // 备份旧存档（如果存在）
                if (File.Exists(filePath))
                {
                    string backupPath = GetBackupPath(slotName);
                    File.Copy(filePath, backupPath, true);
                }

                File.WriteAllText(filePath, json);
                Debug.Log($"[SaveService] 存档已保存: {filePath}");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveService] 保存存档失败: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 加载存档数据。
        /// </summary>
        /// <param name="slotName">槽位名称。</param>
        /// <returns>存档数据，不存在则返回 null。</returns>
        public SaveData Load(string slotName = "default")
        {
            string filePath = GetSlotPath(slotName);

            try
            {
                if (!File.Exists(filePath))
                {
                    // 尝试从备份恢复
                    string backupPath = GetBackupPath(slotName);
                    if (File.Exists(backupPath))
                    {
                        Debug.LogWarning($"[SaveService] 主存档不存在，从备份恢复: {backupPath}");
                        string backupJson = File.ReadAllText(backupPath);
                        File.WriteAllText(filePath, backupJson);
                        return SaveData.FromJson(backupJson);
                    }

                    Debug.Log($"[SaveService] 没有找到存档: {filePath}");
                    return null;
                }

                string json = File.ReadAllText(filePath);
                SaveData data = SaveData.FromJson(json);

                if (data == null)
                {
                    Debug.LogWarning("[SaveService] 存档反序列化失败，尝试从备份恢复。");
                    return LoadFromBackup(slotName);
                }

                Debug.Log($"[SaveService] 存档已加载: {filePath} (版本: {data.Version})");
                return data;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveService] 加载存档失败: {e.Message}");
                return LoadFromBackup(slotName);
            }
        }

        /// <summary>
        /// 删除存档。
        /// </summary>
        public void Delete(string slotName = "default")
        {
            try
            {
                string filePath = GetSlotPath(slotName);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    Debug.Log($"[SaveService] 存档已删除: {filePath}");
                }

                string backupPath = GetBackupPath(slotName);
                if (File.Exists(backupPath))
                {
                    File.Delete(backupPath);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveService] 删除存档失败: {e.Message}");
            }
        }

        /// <summary>
        /// 检查存档是否存在。
        /// </summary>
        public bool HasSave(string slotName = "default")
        {
            return File.Exists(GetSlotPath(slotName)) || File.Exists(GetBackupPath(slotName));
        }

        /// <summary>
        /// 列出所有存档槽位。
        /// </summary>
        public List<string> ListSlots()
        {
            var slots = new List<string>();
            try
            {
                if (!Directory.Exists(_saveDirectory)) return slots;

                var files = Directory.GetFiles(_saveDirectory, "*.sav");
                foreach (var file in files)
                {
                    string name = Path.GetFileNameWithoutExtension(file);
                    if (name.EndsWith("_backup")) continue;
                    slots.Add(name);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveService] 列出槽位失败: {e.Message}");
            }
            return slots;
        }

        private string GetSlotPath(string slotName)
        {
            return Path.Combine(_saveDirectory, $"{slotName}.sav");
        }

        private string GetBackupPath(string slotName)
        {
            return Path.Combine(_saveDirectory, $"{slotName}_backup.sav");
        }

        private SaveData LoadFromBackup(string slotName)
        {
            string backupPath = GetBackupPath(slotName);
            if (!File.Exists(backupPath)) return null;

            try
            {
                string json = File.ReadAllText(backupPath);
                return SaveData.FromJson(json);
            }
            catch
            {
                return null;
            }
        }
    }
}
