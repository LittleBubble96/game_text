using System;
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
    /// 道具数量数据（用于序列化缓存）
    /// </summary>
    [Serializable]
    public class PropCountData
    {
        public int coinCount;
        public int tipCount;
    }

    /// <summary>
    /// 道具管理器 —— 统一管理道具数量的增删改查与持久化
    /// </summary>
    public static class PropDefine
    {
        private const string CacheKey = "PropCountData";

        private static int _tipCount = -1;
        private static int _coinCount = -1;

        /// <summary>提示道具数量</summary>
        public static int TipCount
        {
            get
            {
                if (_tipCount < 0) LoadFromCache();
                return _tipCount;
            }
            private set
            {
                _tipCount = value;
                SaveToCache();
            }
        }

        /// <summary>金币数量</summary>
        public static int CoinCount
        {
            get
            {
                if (_coinCount < 0) LoadFromCache();
                return _coinCount;
            }
            private set
            {
                _coinCount = value;
                SaveToCache();
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
            _tipCount = tipCount;
            _coinCount = coinCount;
            SaveToCache();
            GameEvent.Send(EventDefine.Event_PropCountChanged, PropType.Tip, TipCount);
            GameEvent.Send(EventDefine.Event_PropCountChanged, PropType.Coin, CoinCount);
        }

        private static void LoadFromCache()
        {
            var cacheManager = GameManager.Instance?.CacheManager;
            if (cacheManager != null)
            {
                var data = cacheManager.CacheData.GetCustomData<PropCountData>(CacheKey);
                if (data != null)
                {
                    _tipCount = data.tipCount;
                    _coinCount = data.coinCount;
                    return;
                }
            }
            // 默认值
            _tipCount = 3;
            _coinCount = 0;
        }

        private static void SaveToCache()
        {
            var cacheManager = GameManager.Instance?.CacheManager;
            if (cacheManager != null)
            {
                var data = new PropCountData { tipCount = _tipCount, coinCount = _coinCount };
                cacheManager.CacheData.SetCustomData(CacheKey, data);
            }
        }
    }
}