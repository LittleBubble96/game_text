using System;
using UnityEngine;

namespace GameLogic.Data
{
    /// <summary>
    /// 游戏设置缓存数据
    /// </summary>
    [Serializable]
    public class GameSettingsData
    {
        // ===== 语言 =====
        public string language = "Ch";
        // ==== 音效大小 =====
        public float MusicVolume = 1f;
        public float SoundVolume = 1f;
        
        public void SetMusicVolume(float volume)
        {
            MusicVolume = Mathf.Clamp01(volume);
        }
        
        public void SetSoundVolume(float volume)
        {
            SoundVolume = Mathf.Clamp01(volume);
        }
    }
}
