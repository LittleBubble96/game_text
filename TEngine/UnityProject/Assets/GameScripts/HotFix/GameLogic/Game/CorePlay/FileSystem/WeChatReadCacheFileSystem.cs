using TTSDK;

namespace GameLogic
{
    public class WeChatReadCacheFileSystem : IReadCacheFileSystem
    {
        private readonly string _gameKey = "gameCache";
        
        public string ReadCache()
        {
            return TT.LoadSaving<string>(_gameKey);
        }

        public void WriteCache(string cacheJson)
        {
            TT.Save(cacheJson, _gameKey);
        }

        public void DeleteAll()
        {
            TT.DeleteSaving<string>(_gameKey);
        }
    }
}