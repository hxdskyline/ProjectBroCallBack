using System;
using System.Collections.Generic;
using UnityEngine;

namespace Camp
{
    /// <summary>
    /// 回合事件类型
    /// </summary>
    public enum RoundEventType
    {
        Battle = 0,
        EliteBattle = 1,
        Boss = 2,
        Shop = 3,
        Event = 4,
        HotSpring = 5,
        Recruitment = 6,
        Fate = 7,
        Choice = 8
    }

    /// <summary>
    /// 回合管理器 — 追踪当前回合和回合内事件
    /// </summary>
    public class RoundManager
    {
        public int CurrentRound { get; private set; } = 1;
        public int MaxRounds { get; private set; } = 45;
        public bool IsGameOver => CurrentRound > MaxRounds;

        private List<RoundEventType> _roundEvents = new List<RoundEventType>();

        public void SetRound(int round)
        {
            CurrentRound = Mathf.Max(1, round);
        }

        public void EndRound()
        {
            CurrentRound++;
            _roundEvents.Clear();
        }

        public void Reset()
        {
            CurrentRound = 1;
            _roundEvents.Clear();
        }

        public void AddEvent(RoundEventType eventType)
        {
            _roundEvents.Add(eventType);
        }

        public List<RoundEventType> GetRoundEvents()
        {
            return new List<RoundEventType>(_roundEvents);
        }

        public string GetRoundDescription()
        {
            return $"第 {CurrentRound} 关 / 共 {MaxRounds} 关";
        }
    }
}
