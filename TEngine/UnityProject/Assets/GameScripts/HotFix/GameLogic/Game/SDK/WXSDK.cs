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
    }
}