using TEngine;

namespace GameLogic
{
    public class AudioSystem : Singleton<AudioSystem>
    {
        private string _mainBgmPath = AudioDefine.None;
        

        public void PlayBgm(string audioPath , float volume = 1)
        {
            if (_mainBgmPath.Equals(audioPath))
            {
                return;
            }
            _mainBgmPath = audioPath;
            AudioAgent agent = GameModule.Audio.Play(AudioType.Music, audioPath , true , volume , true , false);
        }

        public void PlayAudio(string audioPath, float volume = 1f)
        {
            GameModule.Audio.Play(AudioType.Sound, audioPath, false, volume, true, true);
        }
        
        public void SetMusicVolume(float volume)
        {
            GameModule.Audio.MusicVolume = volume;
        }
        
        public void SetSoundVolume(float volume)
        {
            GameModule.Audio.SoundVolume = volume;
        }
    }
}