
using TTSDK;
using TTSDK.UNBridgeLib.LitJson;

namespace GameLogic
{
    public class DYSDK : ISdk
    {
        private const string PlayerPrefsOpenIdKey = "WX_openid";
        
        public string GetOpenId()
        {
            return PlayerPrefs.GetString(PlayerPrefsOpenIdKey , "");
        }

        public void ShareAppMessage(string title)
        {
            JsonData shareJson = new JsonData();
            shareJson["title"] = "title";
            TT.ShareAppMessage(shareJson ,(data) =>
                {
                    // Share succeed
                }, (errMsg) =>
                {
                    // Share failed
                }, () =>
                {
                    // Share cancelled
                }
                );
        }
    }
}