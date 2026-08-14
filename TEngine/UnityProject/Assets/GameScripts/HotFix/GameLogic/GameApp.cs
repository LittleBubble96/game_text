using System.Collections.Generic;
using System.Reflection;
using Cysharp.Threading.Tasks;
using GameLogic;
using GameLogic.Localization;
#if ENABLE_OBFUZ
using Obfuz;
#endif
using TEngine;
using WeChatWASM;

#pragma warning disable CS0436


/// <summary>
/// 游戏App。
/// </summary>
#if ENABLE_OBFUZ
[ObfuzIgnore(ObfuzScope.TypeName | ObfuzScope.MethodName)]
#endif
public partial class GameApp
{
    private static List<Assembly> _hotfixAssembly;

    /// <summary>
    /// 热更域App主入口。
    /// </summary>
    /// <param name="objects"></param>
    public static void Entrance(object[] objects)
    {
        GameEventHelper.Init();
        _hotfixAssembly = (List<Assembly>)objects[0];
        Log.Warning("======= 看到此条日志代表你成功运行了热更新代码 =======");
        Log.Warning("======= Entrance GameApp =======");
        Utility.Unity.AddDestroyListener(Release);
        Log.Warning("======= StartGameLogic =======");
        StartGameLogic().Forget();
    }
    
    private static async UniTask StartGameLogic()
    {
        NetTimeSystem.Instance.Activate();
        Log.Warning("======= StartGameLogic Init =======");
        await InitConfig();
        Log.Warning("======= InitConfig Complete =======");
        // GameEvent.Get<ILoginUI>().ShowLoginUI();
        await GameManager.Instance.InitMgr();
        Log.Warning("======= GameManager.Instance.InitMgr Complete =======");
        // 加载设置（语言等），初始化多语言管理器
        GameLocalizationManager.Instance.Active();
        Log.Warning("======= GameLocalizationManager.Instance.Active Complete =======");
        GameSystem.Instance.Activate();
        // 初始化 UI
        await GameModule.UI.ShowUIAsyncAwait<UITop>();
        Log.Warning("======= UITop Active Complete =======");
        GameModule.UI.ShowUIAsync<UIHome>();
        GMSingle.Instance.Activate();
        InitSetting();
        AudioSystem.Instance.PlayBgm(AudioDefine.game_Bgm ,0.4f);
        Log.Warning("======= StartGameLogic Complete =======");
    }

    private static void InitSetting()
    {
        var cacheData = GameManager.Instance?.CacheManager?.CacheData?.gameSettingsData;
        if (cacheData != null)
        {
            AudioSystem.Instance.SetMusicVolume(cacheData.MusicVolume);
            AudioSystem.Instance.SetSoundVolume(cacheData.SoundVolume);
        }
    }

    private static async UniTask InitConfig()
    {
        await ConfigSystem.Instance.Tables.TbLevelAsync();
        await ConfigSystem.Instance.Tables.TbItemAsync();
        await ConfigSystem.Instance.Tables.TbLanguageAsync();
        await ConfigSystem.Instance.Tables.TbRewardAsync();

    }
    
    private static void Release()
    {
        SingletonSystem.Release();
        Log.Warning("======= Release GameApp =======");
    }
}