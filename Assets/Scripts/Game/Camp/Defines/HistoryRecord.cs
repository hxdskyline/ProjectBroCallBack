using System;
using System.Collections.Generic;

namespace Camp
{
    /// <summary>
    /// 统一历史记录 — 记录每回合各系统的操作
    /// 设计参考：正式文档/104、105、106 中的历史记录部分
    /// </summary>
    [Serializable]
    public class HistoryRecord
    {
        public int round;           // 发生回合（层号）
        public string systemType;   // "recruitment" / "shop" / "ritual" / "choice"
        public string action;       // "browse" / "refresh" / "select" / "skip"
        public List<string> appeared;  // 展示的选项/商品列表
        public string chosen;       // 玩家选中的内容（可能为空）
        public long timestamp;      // 执行顺序

        public HistoryRecord()
        {
            appeared = new List<string>();
        }
    }

    /// <summary>
    /// 历史记录管理器 — 存储和查询本局所有系统的操作记录
    /// </summary>
    [Serializable]
    public class HistoryLog
    {
        public List<HistoryRecord> records = new List<HistoryRecord>();

        /// <summary>
        /// 添加一条记录
        /// </summary>
        public void Add(HistoryRecord record)
        {
            record.timestamp = records.Count;
            records.Add(record);
        }

        /// <summary>
        /// 获取指定回合的所有记录
        /// </summary>
        public List<HistoryRecord> GetByRound(int round)
        {
            var result = new List<HistoryRecord>();
            foreach (var r in records)
            {
                if (r.round == round)
                    result.Add(r);
            }
            return result;
        }

        /// <summary>
        /// 获取指定系统的所有记录
        /// </summary>
        public List<HistoryRecord> GetBySystem(string systemType)
        {
            var result = new List<HistoryRecord>();
            foreach (var r in records)
            {
                if (r.systemType == systemType)
                    result.Add(r);
            }
            return result;
        }

        /// <summary>
        /// 检查指定回合是否已完成某系统
        /// </summary>
        public bool IsSystemCompleted(int round, string systemType)
        {
            foreach (var r in records)
            {
                if (r.round == round && r.systemType == systemType && r.action == "select")
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 清空所有记录
        /// </summary>
        public void Clear()
        {
            records.Clear();
        }
    }
}
