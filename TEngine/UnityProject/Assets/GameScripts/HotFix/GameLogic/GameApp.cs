using GameSDK;
using System.Collections.Generic;
using System.Reflection;
using Cysharp.Threading.Tasks;
using GameLogic;
using GameLogic.Localization;
#if ENABLE_OBFUZ
using Obfuz;
#endif
using TEngine;
using UnityEngine;
using YooAsset;

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
        int startupStep = 11;
        AssetHandle topPrefab = null;
        AssetHandle homePrefab = null;
        try
        {
            NetTimeSystem.Instance.Activate();
            Log.Warning("======= StartGameLogic Init =======");
            startupStep = 11;
            StartupTelemetry.Start(startupStep);
            // 只提前加载资源，不实例化 UI，也不触发其业务回调。
            // 两个请求立即启动，与配置加载、InitMgr 内的首关预热重叠。
            topPrefab = GameModule.Resource.LoadAssetAsyncHandle<GameObject>("UITop");
            homePrefab = GameModule.Resource.LoadAssetAsyncHandle<GameObject>("UIHome");
            await InitConfig();
            StartupTelemetry.Success(startupStep);
            Log.Warning("======= InitConfig Complete =======");
            // GameEvent.Get<ILoginUI>().ShowLoginUI();
            startupStep = 12;
            StartupTelemetry.Start(startupStep);
            await GameManager.Instance.InitMgr();
            StartupTelemetry.Success(startupStep);
            Log.Warning("======= GameManager.Instance.InitMgr Complete =======");
            // 加载设置（语言等），初始化多语言管理器
            startupStep = 13;
            StartupTelemetry.Start(startupStep);
            GameLocalizationManager.Instance.Active();
            Log.Warning("======= GameLocalizationManager.Instance.Active Complete =======");
            GameSystem.Instance.Activate();
            StartupTelemetry.Success(startupStep);
            // 初始化 UI
            startupStep = 14;
            StartupTelemetry.Start(startupStep);
            await WaitForStartupPrefab(topPrefab, "UITop");
            if (await GameModule.UI.ShowUIAsyncAwait<UITop>() == null) throw new System.InvalidOperationException("UITop failed to load");
            StartupTelemetry.Success(startupStep);
            Log.Warning("======= UITop Active Complete =======");
            // 启动完成事件应在首页就绪后发送，进度条无需额外固定延迟兜底。
            startupStep = 15;
            StartupTelemetry.Start(startupStep);
            // 顶部先完成事件注册，首页 OnRefresh 才能安全发送顶部刷新事件。
            await WaitForStartupPrefab(homePrefab, "UIHome");
            if (await GameModule.UI.ShowUIAsyncAwait<UIHome>() == null) throw new System.InvalidOperationException("UIHome failed to load");
            StartupTelemetry.Success(startupStep);
            startupStep = 16;
            StartupTelemetry.Start(startupStep);
            GMSingle.Instance.Activate();
            InitSetting();
            AudioSystem.Instance.PlayBgm(AudioDefine.game_Bgm ,0.4f);
            StartupTelemetry.Success(startupStep);
            GameEvent.Send(EventDefine.GameStartSuccessEvent);
            GameManager.Instance.PreloadFinishUI();
            SDK.CreateGameClubButton();
            SDK.ReportGameStart();
            Log.Warning("======= StartGameLogic Complete =======");
        }
        catch (System.Exception e)
        {
            StartupTelemetry.Fail(startupStep, "startup_exception", e.Message);
            throw;
        }
        finally
        {
            // UI 实例由资源模块持有自己的引用，这里只归还临时预加载引用。
            // 配置或管理器初始化失败时，也会释放尚在加载的预加载句柄。
            topPrefab?.Dispose();
            homePrefab?.Dispose();
        }
    }

    private static async UniTask WaitForStartupPrefab(AssetHandle handle, string location)
    {
        await handle.ToUniTask();
        if (handle.Status != EOperationStatus.Succeed || handle.AssetObject == null)
            throw new System.InvalidOperationException($"Startup UI preload failed: {location}. {handle.LastError}");
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
        // 当前五张表没有跨表 ResolveRef 依赖，一次发起所有读取。
        // 语言内容表也必须提前加载，避免 UI 首次取文本时触发同步加载。
        var tables = ConfigSystem.Instance.Tables;
        await UniTask.WhenAll(
            tables.TbLevelAsync(),
            tables.TbItemAsync(),
            tables.TbLanguageAsync(),
            tables.TbLanguageContentAsync(),
            tables.TbRewardAsync());
    }
    
    private static void Release()
    {
        SingletonSystem.Release();
        Log.Warning("======= Release GameApp =======");
    }
}
