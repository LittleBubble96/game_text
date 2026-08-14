using WeChatWASM;

namespace GameLogic
{
    public class WeChatReadCacheFileSystem : IReadCacheFileSystem
    {
        private readonly string _gameKey = "gameCache";
        
        public string ReadCache()
        {
            return WXBase.StorageGetStringSync(_gameKey , "");
        }

        public void WriteCache(string cacheJson)
        {
            WXBase.StorageSetStringSync(_gameKey, cacheJson);
        }

        public void DeleteAll()
        {
            WX.RemoveStorageSync(_gameKey);
        }
    }
}