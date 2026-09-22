using System;
using WeChatWASM;

namespace GameLogic
{
    public class WXSDK : ISdk
    {
        private const string PlayerPrefsOpenIdKey = "WX_openid";

        private const string GameCenterOpenLink =
            "-SSEykJvFV3pORt5kTNpS-gRdTx2iixOqRRPWz1BBnl69Ttni0ZLetaZXdSifeYATwC6iKRakfy-6prIB-gkjUfjCAtcHiTCxM1bcnJbYR0-ySyYid8eeaXsY-nGOaapr01q8G1TZQMpj0HxwhGL1mQuWkh3AVgpBr3iLWw6rvKmYoovB9BGVAoipFp0WhSRL2E10raoOsWP_BlylMxH3ezmOzqgTGZguMmYcRV-LVPuHlxg9GZ6ZHK6w8Y12MNsWKRoFruMCFyeKCA4uKTqSZUtSDaFPIvXxZfPJ7t-YtwNVoEgolltTSJhZwLbMzoO-dzYquOABWuOgoo6A6YU-A";
        
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

        public void OpenGameClub()
        {
            WXPageManager pageManager = WX.CreatePageManager();
            pageManager.Load(new LoadOption()
            {
                openlink = GameCenterOpenLink,
                success = (result) =>
                {
                    pageManager.Show(new ShowOption()
                    {
                        openlink = GameCenterOpenLink,
                    });
                },
            });
        }
    }
}