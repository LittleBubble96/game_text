namespace GameLogic
{
    [Window(UILayer.System)]
    class UIShowGM : UIWindow
    {
        private XYButton _showGmBtn;

        protected override void OnCreate()
        {
            base.OnCreate();
            _showGmBtn = CreateWidget<XYButton>("GM");
            _showGmBtn.OnAddListener(OnClickShowGM);
        }

        private void OnClickShowGM()
        {
            GameModule.UI.ShowUI<UIGM>();
        }
    }
}
