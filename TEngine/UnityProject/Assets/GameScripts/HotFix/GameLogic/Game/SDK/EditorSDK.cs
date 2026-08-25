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
    }
}