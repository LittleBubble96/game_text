using TEngine;

namespace GameLogic
{
    public class EditorSDK : ISdk
    {
        public string GetOpenId()
        {
            return "Editor";
        }

        public void ShareAppMessage(string title)
        {
            Log.Info($"[Share] 分享 {title}");
        }

        public void CreateGameClubButton()
        {
            Log.Info($"[GameCenter] 打开游戏圈");
        }

        public void OpenGameClub()
        {
            Log.Info($"[GameCenter] OpenGameClub 打开游戏圈");
        }
    }
}