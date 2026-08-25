using System;
using GameLogic.Data;
using TEngine;

namespace GameLogic
{
    public class CommonGameSystem : BaseGameSystem
    {
        private DateTime Now => NetTimeSystem.Instance.GetNow();
        private GameCommonData CommonData => GameManager.Instance.CacheManager.CacheData.commonData;
        
        protected override void OnInit()
        {
            base.OnInit();
            //检查是否是新的一天
            CheckNewDay();
        }

        protected override void OnUpdate(float dt)
        {
            base.OnUpdate(dt);
            //检查是不是新的一天
            CheckNewDay();
        }

        private void CheckNewDay()
        {
            if (CommonData.lastDayTime != Now.Day)
            {
                //新的一天
                CommonData.lastDayTime = Now.Day;
                CommonData.isClaimEveryDayCoin = false;
                CommonData.shareCountByGetCoin = GameDefine.ShareCountByDay;
                GameEvent.Send(EventDefine.Event_NewDay);
            }
        }
        
        public bool CanClaimNewDayCoin()
        {
            return !CommonData.isClaimEveryDayCoin;
        }

        public void ClaimNewDayCoin()
        {
            CommonData.isClaimEveryDayCoin = true;
        }

        public int GetShareNewDayCount()
        {
            return CommonData.shareCountByGetCoin;
        }

        public void ShareNewDay()
        {
            CommonData.shareCountByGetCoin--;
        }
    }
}