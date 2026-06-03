
using TEngine;
using UnityEngine;

namespace GameLogic
{
    [Window(UILayer.UI,location:"UIHome")]
    class UIHome : UIWindow
    {
        #region 脚本工具生成的代码
        private XYButton  _playBtn;
       
        protected override void ScriptGenerator()
        {
            _playBtn = CreateWidget<XYButton>("m_btnStartLevel");
            _playBtn.OnAddListener(OnStartLevel);
        }
        #endregion

        #region 事件

        private void OnStartLevel()
        {
            GameManager.Instance.StartGame();
        }

        #endregion

    }
}