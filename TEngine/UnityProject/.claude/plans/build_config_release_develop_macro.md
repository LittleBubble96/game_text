# BuildConfig 添加 Release/Develop 宏 + WebGL 一键打包区分 release/debug 方案

## 任务等级
L3（跨 3 文件：BuildConfig / BuildPipelineWindow / ReleaseTools，涉及数据模型、UI、持久化、打包流程、宏时序）

## 决策结论（已与用户确认）
1. **宏关系**：互斥构建模式（二选一），用枚举 `EBuildMode` 表达，不能同时存在两个模式宏
2. **WebGL 入口**：拆成两个菜单项 `一键打包Webgl(Release)` / `一键打包Webgl(Develop)`
3. **宏命名**：`TE_RELEASE` / `TE_DEVELOP`（TEngine 专属前缀）
   - 映射：`EBuildMode.Release` ↔ `TE_RELEASE`，`EBuildMode.Develop` ↔ `TE_DEVELOP`

## 涉及文件
| 文件 | 改动 |
|------|------|
| `Assets/TEngine/Editor/ReleaseTools/BuildConfig.cs` | 新增 `EBuildMode` 枚举 + 宏常量 + `BuildMode` 字段 + `ApplyBuildModeDefines()` 方法 |
| `Assets/TEngine/Editor/ReleaseTools/ReleaseTools.cs` | 拆分 WebGL 入口为 Release/Develop 两菜单；在 `BuildWithConfig` 开头应用宏 |
| `Assets/TEngine/Editor/ReleaseTools/BuildPipelineWindow.cs` | UI 新增构建模式下拉；持久化 `TEngine_BP_BuildMode`；CloneConfig 补字段 |

## 复用的现成机制（无需新造轮子）
- **宏增删 API**：`TEngine.Editor.ScriptingDefineSymbols`（`Assets/TEngine/Editor/DefineSymbols/ScriptingDefineSymbols.cs`）
  - `AddScriptingDefineSymbol(string)` —— 跨所有平台加宏（Standalone/iOS/Android/WSA/WebGL）
  - `RemoveScriptingDefineSymbol(string)` —— 跨所有平台删宏
  - `HasScriptingDefineSymbol(BuildTargetGroup, string)` —— 检测
  - 已处理 `UNITY_6000_0_OR_NEWER` 的 `NamedBuildTarget` 兼容
- **BuildPipelineWindow 持久化范式**：`EditorPrefs.GetInt/GetBool("TEngine_BP_*", default)` + `SetInt/SetBool`
- **CreateDefault / CloneConfig**：新增字段需同步补上，否则默认值丢失

## 详细改动

### 1. BuildConfig.cs

```csharp
// 新增枚举（放在 BuildConfig 类外或类内顶部）
public enum EBuildMode
{
    Release,  // 发布模式 -> TE_RELEASE
    Develop,  // 开发模式 -> TE_DEVELOP
}

public class BuildConfig
{
    // ===== 新增：构建模式 =====
    public EBuildMode BuildMode = EBuildMode.Develop;

    // 宏定义常量
    public const string ReleaseDefine = "TE_RELEASE";
    public const string DevelopDefine = "TE_DEVELOP";

    // ...原有字段不变...

    // 新增方法：根据 BuildMode 互斥应用宏（加当前模式宏、移除另一模式宏）
    public void ApplyBuildModeDefines()
    {
        // 先把两个模式宏都移除，保证互斥干净
        ScriptingDefineSymbols.RemoveScriptingDefineSymbol(ReleaseDefine);
        ScriptingDefineSymbols.RemoveScriptingDefineSymbol(DevelopDefine);

        // 再加当前模式对应的宏
        switch (BuildMode)
        {
            case EBuildMode.Release:
                ScriptingDefineSymbols.AddScriptingDefineSymbol(ReleaseDefine);
                break;
            case EBuildMode.Develop:
                ScriptingDefineSymbols.AddScriptingDefineSymbol(DevelopDefine);
                break;
        }
    }

    // CreateDefault() 内补：BuildMode = EBuildMode.Develop（已是默认值，但显式写出便于阅读）
}
```

> 命名空间：`BuildConfig` 在 `namespace TEngine`，而 `ScriptingDefineSymbols` 在 `namespace TEngine.Editor`。
> `BuildConfig.cs` 当前 using 列表无 `TEngine.Editor`，需新增 `using TEngine.Editor;` 才能调用。
> （`BuildDLLCommand.cs` 已验证此 using 模式可行，见其第 12 行。）

### 2. ReleaseTools.cs

**改动 A：拆分 WebGL 一键打包入口**

把现有单个 `AutomationBuildWebgl()` 替换为两个菜单项：

```csharp
[MenuItem("TEngine/Build/一键打包Webgl(Release)", false, 30)]
public static void AutomationBuildWebglRelease()
{
    AutomationBuildWebglInternal(EBuildMode.Release);
}

[MenuItem("TEngine/Build/一键打包Webgl(Develop)", false, 31)]
public static void AutomationBuildWebglDevelop()
{
    AutomationBuildWebglInternal(EBuildMode.Develop);
}

private static void AutomationBuildWebglInternal(EBuildMode mode)
{
    var config = BuildConfig.CreateDefault();
    config.BuildTarget = BuildTarget.WebGL;
    config.OutputRoot = Application.dataPath + "/../Builds/WebGL";
    config.BuildPlayer = false;
    config.BuildMode = mode;          // ← 新增：把模式写进 config
    BuildWithConfig(config, buildPlayer: false);
    // 微信小游戏转换逻辑不变
    if (WXConvertCore.DoExport() == WXConvertCore.WXExportError.SUCCEED)
    {
        Debug.Log("[Build] WebGL 转换为微信小游戏成功");
    }
    else
    {
        Debug.LogError("[Build] WebGL 转换为微信小游戏失败");
    }
}
```

> 原始 `AutomationBuildWebgl()` 无调用方（Grep 确认仅菜单引用），删除安全。
> 保留优先级递增(30→31)维持菜单顺序。

**改动 B：在 `BuildWithConfig` 开头应用宏**

```csharp
public static void BuildWithConfig(BuildConfig config, bool buildPlayer)
{
    // 0. [新增] 应用构建模式宏（互斥设置 TE_RELEASE / TE_DEVELOP）
    config.ApplyBuildModeDefines();
    Debug.Log($"[BuildWithConfig] 构建模式: {config.BuildMode} (宏: {GetModeDefine(config.BuildMode)})");

    // 1. [可选] 编译热更DLL —— 此处用最新宏编译 HybridCLR 热更 DLL
    if (config.BuildHotFixDll)
    {
        Debug.Log("[BuildWithConfig] 编译热更DLL...");
        BuildDLLCommand.BuildAndCopyDlls();
    }
    // ...后续不变...
}

private static string GetModeDefine(EBuildMode mode)
    => mode == EBuildMode.Release ? BuildConfig.ReleaseDefine : BuildConfig.DevelopDefine;
```

> 放在流程最前，保证后续 BuildHotFixDll 编译的热更 DLL 和最终 BuildPlayer 编译 il2cpp 都用最新宏。
> `ApplyBuildModeDefines` 内部 `Remove`+`Add` 会触发 Unity 重新编译域；`BuildWithConfig` 后续 `AssetDatabase.Refresh()` 和 `BuildDLLCommand` 已有刷新逻辑，无需额外等待编译。

**改动 C：其它一键入口（Window/Android/iOS）同步支持模式**

这些入口当前用 `BuildConfig.CreateDefault()`（默认 Develop）。为保持一致性，不动它们的默认值，但它们会因 `BuildWithConfig` 改动 B 自动应用 Develop 宏。**若用户希望这些入口也能选模式，可作为后续扩展**——本方案不强制改动，避免超出范围。

### 3. BuildPipelineWindow.cs

**改动 A：UI 新增「构建模式」下拉**（放在 DrawBasicSettings 目标平台之后）

```csharp
// 新增名称数组（与 PlatformNames 等并列）
private static readonly string[] BuildModeNames = new string[]
{
    "Release (发布模式)",
    "Develop (开发模式)",
};

// DrawBasicSettings 内，目标平台 popup 之后：
int buildModeIndex = (int)_config.BuildMode;
buildModeIndex = EditorGUILayout.Popup("构建模式", buildModeIndex, BuildModeNames);
_config.BuildMode = (EBuildMode)buildModeIndex;
EditorGUILayout.HelpBox(
    $"当前模式宏: {(_config.BuildMode == EBuildMode.Release ? BuildConfig.ReleaseDefine : BuildConfig.DevelopDefine)}\n" +
    "Release/Develop 互斥。打包时自动设置对应宏并触发重编译。",
    MessageType.Info);
```

**改动 B：LoadSettings / SaveSettings 持久化**

```csharp
// LoadSettings 内补（默认 Develop=1）
_config.BuildMode = (EBuildMode)EditorPrefs.GetInt("TEngine_BP_BuildMode", 1);

// SaveSettings 内补
EditorPrefs.SetInt("TEngine_BP_BuildMode", (int)_config.BuildMode);
```

**改动 C：CloneConfig 补字段**

```csharp
BuildMode = source.BuildMode,
```

**改动 D：执行构建时应用宏**

```csharp
// ExecuteBuild 内、调用 ReleaseTools.BuildWithConfig 前：
// （其实 BuildWithConfig 内部已 ApplyBuildModeDefines，这里无需重复；
//  ExecuteBuildPlayerOnly 走的是 ReleaseTools.BuildImp 不经过 BuildWithConfig，
//  若需要支持模式，可在 ExecuteBuildPlayerOnly 开头加 _config.ApplyBuildModeDefines()）
```

> 决策：`ExecuteBuildPlayerOnly` 调 `BuildImp`（绕过 `BuildWithConfig`），为完整覆盖，在其中开头也加 `ApplyBuildModeDefines()`。

## 宏时序验证（为何无需额外异步等待）
- `ScriptingDefineSymbols.Add/RemoveScriptingDefineSymbol` → `PlayerSettings.SetScriptingDefineSymbols` → Unity 自动触发编译域重载
- `BuildWithConfig` 后续步骤：
  1. `BuildDLLCommand.BuildAndCopyDlls()` → HybridCLR `CompileDllCommand.CompileDll(target)` 用**最新宏**编译热更 DLL ✓
  2. `BuildPipeline.BuildPlayer` → il2cpp 用**最新宏**编译 Player ✓
- `EditorUtility` 无同步等待编译的 API；但 HybridCLR 的 `CompileDll` 是独立编译流程，会自己处理依赖。现有 `ENABLE_HYBRIDCLR` 宏开关采用同样的"设置宏后直接调 BuildDLLCommand"模式（见 `BuildDLLCommand.EnableHybridCLR`），证明此模式可行。

## 边界与风险
1. **首次设置宏会触发编译域重载**：若在 `ExecuteBuild` 同步流程中调 `ApplyBuildModeDefines`，Unity 可能在编译期间阻止部分 API 调用。**缓解**：参考 `BuildDLLCommand` 现有做法（设置宏后直接 `ForceUpdateAssemblies` + 后续编译），此模式已被项目验证可行。若实测发现编译未完成导致热更 DLL 用旧宏，可在 `ApplyBuildModeDefines` 后加 `AssetDatabase.Refresh()`。
2. **微信小游戏 `WXConvertCore`**：其 `DoExport()` 签名不变，无需区分 release/debug 参数（宏已作用于编译产物）。
3. **命名空间跨引用**：`BuildConfig.cs` 需加 `using TEngine.Editor;`。已验证 `TEngine.Editor.asmdef` 引用了相关程序集，且 `BuildDLLCommand` 已用此 using。
4. **默认值**：新建 `BuildConfig` 默认 `Develop`，避免误打包成 Release。`CreateDefault` 显式赋值。

## 不做的事（明确边界）
- 不改动 `Window/Android/iOS` 一键入口的默认模式（保持 Develop），仅让它们经 `BuildWithConfig` 自动应用 Develop 宏
- 不引入新 asmdef 依赖
- 不改动微信小游戏转换 API
- 不为宏添加游戏内 #if 使用示例（由业务方按需使用）
