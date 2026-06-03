namespace GameLogic
{
    public class UIFinish : UIWindow
    {
        private XYButton _btnNext;

        protected override void ScriptGenerator()
        {
            base.ScriptGenerator();
            _btnNext = CreateWidget<XYButton>("BtnNext");
        }
        
        private void OnBtnNextClick()
        {
            
        }
    }
}