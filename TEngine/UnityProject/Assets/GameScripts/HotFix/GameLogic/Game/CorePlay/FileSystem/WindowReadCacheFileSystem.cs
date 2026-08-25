using System.IO;
using TEngine;
using UnityEngine;

namespace GameLogic
{
    public class WindowReadCacheFileSystem : IReadCacheFileSystem
    {
        private const string SaveFileName = "game_cache.json";

        public string ReadCache()
        {
            string filePath = GetSaveFilePath();
            if (File.Exists(filePath))
            {
                return File.ReadAllText(filePath);
            }
            return null;
        }

        public void WriteCache(string cacheJson)
        {
            string filePath = GetSaveFilePath();
            File.WriteAllText(filePath, cacheJson);
            Log.Info($"[GameCache] 缓存已保存: {filePath}");
        }

        public void DeleteAll()
        {
            string filePath = GetSaveFilePath();
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                Log.Info("[GameCache] 缓存文件已删除");
            }
        }

        /// <summary>获取存档文件路径</summary>
        private string GetSaveFilePath()
        {
            return Path.Combine(Application.persistentDataPath, SaveFileName);
        }
    }
}