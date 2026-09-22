using System;
using WeChatWASM;

namespace GameLogic
{
    public class WXSDK : ISdk
    {
        private const string PlayerPrefsOpenIdKey = "WX_openid";
        
        public string GetOpenId()
        {
            return PlayerPrefs.GetString(PlayerPrefsOpenIdKey , "");
        }

        public void ShareAppMessage(string title)
        {
            WX.ShareAppMessage(new ShareAppMessageOption {
                title=title,
                query=$"inviter={GetOpenId()}&sid={Guid.NewGuid():N}"
            });
        }

        public void CreateGameClubButton()
        {
            WXCreateGameClubButtonParam param = new WXCreateGameClubButtonParam();
            GameClubButtonStyle clubButtonStyle = new GameClubButtonStyle();
            clubButtonStyle.left = 10;
            clubButtonStyle.top = 76;
            clubButtonStyle.width = 40;
            clubButtonStyle.height = 40;
            param.style = clubButtonStyle;
            param.type = GameClubButtonType.text;
            WX.CreateGameClubButton(param);
        }
    }
}