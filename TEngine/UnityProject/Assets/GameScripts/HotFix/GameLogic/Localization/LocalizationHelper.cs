namespace GameLogic.Localization
{
    public static class LocalizationHelper
    {
        public static string GetLocalText(string key)
        {
            return GameLocalizationManager.Instance.GetText(key);
        }
    }
}