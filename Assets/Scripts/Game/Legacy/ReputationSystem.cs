using System;
using UnityEngine;

namespace Legacy
{
    /// <summary>
    /// 声望等级
    /// </summary>
    public enum ReputationLevel
    {
        Novice = 0,      // 新手
        Beginner = 1,    // 初学者
        Intermediate = 2, // 中级
        Advanced = 3,    // 高级
        Expert = 4,      // 专家
        Master = 5       // 大师
    }

    /// <summary>
    /// 声望系统 — 跨局元进度
    /// </summary>
    public class ReputationSystem
    {
        private const string REPUTATION_KEY = "Legacy_Reputation";
        private const string LEVEL_KEY = "Legacy_ReputationLevel";

        /// <summary>
        /// 获取当前声望值
        /// </summary>
        public int GetReputation()
        {
            return PlayerPrefs.GetInt(REPUTATION_KEY, 0);
        }

        /// <summary>
        /// 获取当前声望等级
        /// </summary>
        public ReputationLevel GetReputationLevel()
        {
            return (ReputationLevel)PlayerPrefs.GetInt(LEVEL_KEY, 0);
        }

        /// <summary>
        /// 添加声望（局结束时调用）
        /// </summary>
        public void AddReputation(int amount)
        {
            int current = GetReputation();
            int newTotal = current + amount;
            PlayerPrefs.SetInt(REPUTATION_KEY, newTotal);

            // 检查升级
            var newLevel = CalculateLevel(newTotal);
            if ((int)newLevel > (int)GetReputationLevel())
            {
                PlayerPrefs.SetInt(LEVEL_KEY, (int)newLevel);
                Debug.Log($"[ReputationSystem] 声望升级! {newLevel}");
            }

            PlayerPrefs.Save();
            Debug.Log($"[ReputationSystem] 声望 +{amount}，当前: {newTotal}");
        }

        /// <summary>
        /// 根据声望值计算等级
        /// </summary>
        private ReputationLevel CalculateLevel(int reputation)
        {
            if (reputation >= 10000) return ReputationLevel.Master;
            if (reputation >= 5000) return ReputationLevel.Expert;
            if (reputation >= 2500) return ReputationLevel.Advanced;
            if (reputation >= 1000) return ReputationLevel.Intermediate;
            if (reputation >= 300) return ReputationLevel.Beginner;
            return ReputationLevel.Novice;
        }

        /// <summary>
        /// 计算局结束时应获得的声望值
        /// </summary>
        public int CalculateRunReputation(bool victory, int completedLevels, bool bossDefeated)
        {
            int baseRep = completedLevels * 10;
            if (bossDefeated) baseRep += 100;
            if (victory) baseRep += 500;
            return baseRep;
        }
    }
}
