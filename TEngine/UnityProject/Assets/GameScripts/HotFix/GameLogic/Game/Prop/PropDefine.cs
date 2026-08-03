using System;
using GameLogic.Data;
using TEngine;

namespace GameLogic
{
    public enum PropType
    {
        None,
        Coin,
        Tip,
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
                    cache.gamePropData = new GamePropData { tipCount = 3, coinCount = 0 };
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
        public static bool UseTip()
        {
            if (TipCount <= 0) return false;
            TipCount--;
            GameEvent.Send(EventDefine.Event_PropCountChanged, PropType.Tip, TipCount);
            return true;
        }

        /// <summary>增加提示道具</summary>
        public static void AddTip(int count)
        {
            TipCount += count;
            GameEvent.Send(EventDefine.Event_PropCountChanged, PropType.Tip, TipCount);
        }

        /// <summary>增加金币</summary>
        public static void AddCoin(int count)
        {
            CoinCount += count;
            GameEvent.Send(EventDefine.Event_PropCountChanged, PropType.Coin, CoinCount);
        }

        /// <summary>使用金币，返回是否成功</summary>
        public static bool UseCoin(int count)
        {
            if (CoinCount < count) return false;
            CoinCount -= count;
            GameEvent.Send(EventDefine.Event_PropCountChanged, PropType.Coin, CoinCount);
            return true;
        }

        /// <summary>设置初始道具数量（首次使用或重置时调用）</summary>
        public static void InitPropCounts(int tipCount, int coinCount)
        {
            var d = Data;
            if (d == null) return;
            d.tipCount = tipCount;
            d.coinCount = coinCount;
            Save();
            GameEvent.Send(EventDefine.Event_PropCountChanged, PropType.Tip, tipCount);
            GameEvent.Send(EventDefine.Event_PropCountChanged, PropType.Coin, coinCount);
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