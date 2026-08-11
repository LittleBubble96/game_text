using System.IO;
using Cysharp.Threading.Tasks;
using Luban;
using GameConfig;
using TEngine;
using UnityEngine;

/// <summary>
/// 配置加载器。
/// </summary>
public class ConfigSystem
{
    private static ConfigSystem _instance;

    public static ConfigSystem Instance => _instance ??= new ConfigSystem();

    private bool _init = false;

    private Tables _tables;

    public Tables Tables
    {
        get
        {
            if (!_init)
            {
                Load();
            }

            return _tables;
        }
    }
    
    private IResourceModule _resourceModule;

    /// <summary>
    /// 加载配置。
    /// </summary>
    public void Load()
    {
        _tables = new Tables(LoadByteBuf,LoadByteBufAsync);
        _init = true;
    }

    /// <summary>
    /// 加载二进制配置。
    /// </summary>
    /// <param name="file">FileName</param>
    /// <returns>ByteBuf</returns>
    private ByteBuf LoadByteBuf(string file)
    {
        if (_resourceModule == null)
        {
            _resourceModule = ModuleSystem.GetModule<IResourceModule>();
        }
        TextAsset textAsset = _resourceModule.LoadAsset<TextAsset>(file);
        byte[] bytes = textAsset.bytes;
        return new ByteBuf(bytes);
    }
    
    /// <summary>
    /// 加载二进制配置。
    /// </summary>
    /// <param name="file">FileName</param>
    /// <returns>ByteBuf</returns>
    private async UniTask<ByteBuf> LoadByteBufAsync(string file)
    {
        if (_resourceModule == null)
        {
            _resourceModule = ModuleSystem.GetModule<IResourceModule>();
        }
        TextAsset textAsset = await _resourceModule.LoadAssetAsync<TextAsset>(file);
        byte[] bytes = textAsset.bytes;
        return new ByteBuf(bytes);
    }

#if UNITY_EDITOR

    public void LoadEditor()
    {
        _tables = new Tables(AttrByteLoaderEditor , null);
        _init = true;
    }

    private static ByteBuf AttrByteLoaderEditor(string file)
    {
        string path = "Assets/AssetRaw/Configs/bytes/" + file + ".bytes";
        var asset = File.ReadAllBytes(Path.Combine(Application.dataPath.Replace("Assets", ""), path));
        return new ByteBuf(asset);
    }
#endif
}