using System;
using GameLogic.Data;
using TEngine;

namespace GameLogic
{
    public enum PropType
    {
        None,
        Coin,
        Tip,  //答案提示
        Reset, //重置答案
        Next, //直接完成当前关卡
    }

    /// <summary>
    /// 道具管理器 —— 直接访问 GameCacheData.gamePropData 字段，数量修改后立即存档。
    /// </summary>
    public static class PropDefine
    {
        /// <summary>获取道具数据（懒加载）</summary>
        private static GamePropData Data
        {
            get
            {
                var cache = GameManager.Instance?.CacheManager?.CacheData;
                if (cache != null && cache.gamePropData == null)
                {
                    cache.gamePropData = new GamePropData { tipCount = 3, coinCount = 0, resetCount = 1, nextCount = 1 };
                }
                return cache?.gamePropData;
            }
        }

        /// <summary>提示道具数量</summary>
        public static int TipCount
        {
            get => Data?.tipCount ?? 0;
            private set
            {
                var d = Data;
                if (d != null) d.tipCount = value;
                Save();
            }
        }

        /// <summary>金币数量</summary>
        public static int CoinCount
        {
            get => Data?.coinCount ?? 0;
            private set
            {
                var d = Data;
                if (d != null) d.coinCount = value;
                Save();
            }
        }

        /// <summary>提示道具是否可用（数量 > 0）</summary>
        public static bool IsTipAvailable => TipCount > 0;

        /// <summary>使用提示道具（数量减1），返回是否成功</summary>
        public static bool UseTip(string source = "tip")
        {
            int before = TipCount;
            if (TipCount <= 0) return false;
            TipCount--;
            BiMgr.ResourceChanged("tip", before, TipCount, source);
            GameEvent.Send(EventDefine.Event_PropCountChanged, PropType.Tip, TipCount);
            return true;
        }

        /// <summary>增加提示道具</summary>
        public static void AddTip(int count, string source = "unknown")
        {
            int before = TipCount;
            TipCount += count;
            BiMgr.ResourceChanged("tip", before, TipCount, source);
            GameEvent.Send(EventDefine.Event_PropCountChanged, PropType.Tip, TipCount);
        }

        /// <summary>增加金币</summary>
        public static void AddCoin(int count, string source = "unknown")
        {
            int before = CoinCount;
            CoinCount += count;
            BiMgr.ResourceChanged("coin", before, CoinCount, source);
            GameEvent.Send(EventDefine.Event_PropCountChanged, PropType.Coin, CoinCount);
        }

        /// <summary>使用金币，返回是否成功</summary>
        public static bool UseCoin(int count, string source = "unknown")
        {
            int before = CoinCount;
            if (CoinCount < count) return false;
            CoinCount -= count;
            BiMgr.ResourceChanged("coin", before, CoinCount, source);
            GameEvent.Send(EventDefine.Event_PropCountChanged, PropType.Coin, CoinCount);
            return true;
        }

        /// <summary>
        /// 数量不足时，按指定金币消耗量扣金币以替代使用道具（当场扣金币直接使用）。
        /// 由 Widget 在「道具数量为 0 且金币足够」分支调用。
        /// </summary>
        /// <param name="coinCost">该道具的单次金币消耗量（见 GameDefine.PropXxxCoinCost）</param>
        /// <returns>是否扣款成功</returns>
        public static bool UsePropByCoin(int coinCost, string source = "prop")
        {
            return UseCoin(coinCost, source);
        }

        /// <summary>指定道具数量是否大于 0（统一查询入口）</summary>
        public static bool IsPropAvailable(PropType propType)
        {
            return propType switch
            {
                PropType.Tip => TipCount > 0,
                PropType.Reset => ResetCount > 0,
                PropType.Next => NextCount > 0,
                _ => false,
            };
        }

        /// <summary>查询指定道具当前数量</summary>
        public static int GetPropCount(PropType propType)
        {
            return propType switch
            {
                PropType.Tip => TipCount,
                PropType.Reset => ResetCount,
                PropType.Next => NextCount,
                _ => 0,
            };
        }

        /// <summary>重置道具数量</summary>
        public static int ResetCount
        {
            get => Data?.resetCount ?? 0;
            private set
            {
                var d = Data;
                if (d != null) d.resetCount = value;
                Save();
            }
        }

        /// <summary>重置道具是否可用（数量 > 0）</summary>
        public static bool IsResetAvailable => ResetCount > 0;

        /// <summary>使用重置道具（数量减1），返回是否成功</summary>
        public static bool UseReset(string source = "reset")
        {
            int before = ResetCount;
            if (ResetCount <= 0) return false;
            ResetCount--;
            BiMgr.ResourceChanged("reset", before, ResetCount, source);
            GameEvent.Send(EventDefine.Event_PropCountChanged, PropType.Reset, ResetCount);
            return true;
        }

        /// <summary>增加重置道具</summary>
        public static void AddReset(int count, string source = "unknown")
        {
            int before = ResetCount;
            ResetCount += count;
            BiMgr.ResourceChanged("reset", before, ResetCount, source);
            GameEvent.Send(EventDefine.Event_PropCountChanged, PropType.Reset, ResetCount);
        }

        /// <summary>下一关道具数量</summary>
        public static int NextCount
        {
            get => Data?.nextCount ?? 0;
            private set
            {
                var d = Data;
                if (d != null) d.nextCount = value;
                Save();
            }
        }

        /// <summary>使用下一关道具（数量减1），返回是否成功</summary>
        public static bool UseNext(string source = "next")
        {
            int before = NextCount;
            if (NextCount <= 0) return false;
            NextCount--;
            BiMgr.ResourceChanged("next", before, NextCount, source);
            GameEvent.Send(EventDefine.Event_PropCountChanged, PropType.Next, NextCount);
            return true;
        }

        /// <summary>增加下一关道具</summary>
        public static void AddNext(int count, string source = "unknown")
        {
            int before = NextCount;
            NextCount += count;
            BiMgr.ResourceChanged("next", before, NextCount, source);
            GameEvent.Send(EventDefine.Event_PropCountChanged, PropType.Next, NextCount);
        }

        /// <summary>设置初始道具数量（首次使用或重置时调用）</summary>
        public static void InitPropCounts(int tipCount, int coinCount, int resetCount)
        {
            InitPropCounts(tipCount, coinCount, resetCount, NextCount);
        }

        /// <summary>设置全部道具数量（GM 等批量修改场景使用）</summary>
        public static void InitPropCounts(int tipCount, int coinCount, int resetCount, int nextCount)
        {
            var d = Data;
            if (d == null) return;
            int oldTip = d.tipCount, oldCoin = d.coinCount, oldReset = d.resetCount, oldNext = d.nextCount;
            d.tipCount = tipCount;
            d.coinCount = coinCount;
            d.resetCount = resetCount;
            d.nextCount = nextCount;
            Save();
            BiMgr.ResourceChanged("tip", oldTip, tipCount, "set_counts");
            BiMgr.ResourceChanged("coin", oldCoin, coinCount, "set_counts");
            BiMgr.ResourceChanged("reset", oldReset, resetCount, "set_counts");
            BiMgr.ResourceChanged("next", oldNext, nextCount, "set_counts");
            GameEvent.Send(EventDefine.Event_PropCountChanged, PropType.Tip, tipCount);
            GameEvent.Send(EventDefine.Event_PropCountChanged, PropType.Coin, coinCount);
            GameEvent.Send(EventDefine.Event_PropCountChanged, PropType.Reset, resetCount);
            GameEvent.Send(EventDefine.Event_PropCountChanged, PropType.Next, nextCount);
        }

        /// <summary>立即持久化存档</summary>
        private static void Save()
        {
            GameManager.Instance?.CacheManager?.Save();
        }
    }
    
    /// <summary>
    /// 道具ID常量（与 Item 表 Id 对应）
    /// </summary>
    public static class ItemId
    {
        public const int Coin = 10000;
        public const int TipProp = 10001;
    }
}
