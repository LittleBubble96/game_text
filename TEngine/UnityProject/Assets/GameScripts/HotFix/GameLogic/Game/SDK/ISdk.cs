namespace GameLogic
{
    public interface ISdk
    {
        string GetOpenId();

        void ShareAppMessage(string title);
    }
}