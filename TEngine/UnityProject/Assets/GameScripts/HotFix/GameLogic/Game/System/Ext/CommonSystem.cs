using System;

namespace GameLogic
{
    public class CommonGameSystem : BaseGameSystem
    {
        public DateTime Now => NetTimeSystem.Instance.GetNow();
        
        protected override void OnInit()
        {
            base.OnInit();
            //检查是否是新的一天
        }

        protected override void OnUpdate(float dt)
        {
            base.OnUpdate(dt);
            //检查是不是新的一天
        }
    }
}