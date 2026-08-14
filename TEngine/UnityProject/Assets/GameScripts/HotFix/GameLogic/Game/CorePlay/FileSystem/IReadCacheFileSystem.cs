namespace GameLogic
{
    public interface IReadCacheFileSystem
    {
        string ReadCache();

        void WriteCache(string cacheJson);

        void DeleteAll();
    }
}