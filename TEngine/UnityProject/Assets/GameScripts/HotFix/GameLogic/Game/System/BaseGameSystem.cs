namespace GameLogic
{
    public abstract class BaseGameSystem
    {
        public void Init()
        {
            OnInit();
        }

        protected virtual void OnInit()
        {
            
        }


        public void Destroy()
        {
            OnDestroy();
        }

        protected virtual void OnDestroy()
        {
            
        }

        public void Update(float dt)
        {
            OnUpdate(dt);
        }
        
        protected virtual void OnUpdate(float dt)
        {
            
        }
    }
}