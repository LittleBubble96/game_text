namespace GameLogic
{
    public static class SDK
    {
        private static ISdk _sdk;

        public static void InitSdk()
        {
#if UNITY_EDITOR
            _sdk = new EditorSDK();
#elif UNITY_WEBGL
            _sdk = new WXSDK();
#endif
        }
        
        public static string GetOpenId()
        {
            return _sdk?.GetOpenId();
        }

        public static void ShareAppMessage(string title)
        {
            _sdk?.ShareAppMessage(title);
        } 
    }
}