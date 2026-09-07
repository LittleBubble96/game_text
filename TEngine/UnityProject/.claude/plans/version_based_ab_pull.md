# 方案：按 App 版本拉对应版本 AB（多客户端版本兼容）

> 场景：仅 WebGL/微信小游戏；老 App(1.0) 拉 1.0 的 AB，新 App(2.0) 拉 2.0 的 AB，CDN 上两套并存互不污染。
> 机制：YooAsset 的 `IRemoteServices.GetRemoteMainURL(fileName)` = `HostServerURL + "/" + fileName`，
>      所以**让 HostServerURL 指向带 App 版本号的目录即可实现版本隔离**，无需改 YooAsset 内部。

---

## 一、现状结论（已验证）

| 项 | 现状 | 文件:行 |
|---|---|---|
| App 版本号来源 | `Application.version`（PlayerSettings.bundleVersion，建议填三段式如 `1.0.0`） | `ProjectSettings/ProjectSettings.asset:144` |
| 框架内 App 版本字段 | `ApplicableGameVersion` 是**死字段**，从未被赋值 | `ResourceModule.cs:67,74` |
| 资源版本号(PackageVersion) | `日期-分钟`（如 `2026-09-07-925`），打包时写入 | `BuildConfig.cs:119` / `ReleaseTools.cs:286` |
| 远端地址 | 固定 `.../webgl/StreamingAssets/package/DefaultPackage`，**不带版本/平台子目录** | `UpdateSetting.asset:32` |
| 地址拼接 | `GetResDownLoadPath()` 把 `projectName/platform` 拼接**注释掉**了 | `UpdateSetting.cs:167-172` |
| 客户端拉取 | 被动读远端 `.version` 拿"当前最新版"号，无法选版本 | `ProcedureInitResources.cs:77-96` |
| YooAsset 远端结构 | `{HostServerURL}/` 下平铺 `.version`/清单/bundle | `DefaultWebRemoteFileSystem` + `IRemoteServices` |

**核心问题**：所有 App 版本都拉同一个固定 CDN 目录，无法分版本隔离。

---

## 二、目标 CDN 结构

> 命名规则：`v{Application.version}`，采用三段式语义版本号（如 `v1.0.0`）。
> `Application.version` 在 PlayerSettings 里填 `1.0.0`（支持三段式，URL 点号合法）。

```
https://xunzi.setworld.net/webgl/
  v1.0.0/StreamingAssets/package/DefaultPackage/   ← App 1.0.0 拉（.version/清单/bundle 平铺）
  v2.0.0/StreamingAssets/package/DefaultPackage/   ← App 2.0.0 拉
```

每个 App 版本目录独立、完整、稳定（发版才变）。目录内仍是 YooAsset 标准平铺结构，
`.version` 文件指向该 App 版本内的"当前最新资源版本"。

---

## 三、改动清单（5 处，最小侵入）

### 改动 1：`UpdateSetting.cs` — 地址拼接带 App 版本号
**文件**：`Assets/TEngine/Runtime/Core/UpdateSetting.cs:167-182`

恢复 `GetResDownLoadPath()`/`GetFallbackResDownLoadPath()` 的拼接逻辑，
但拼的是 **App 版本号子目录**（WebGL 单平台，不加平台子目录）：

```csharp
public string GetResDownLoadPath()
{
    // 拼成: {ResDownLoadPath}/{AppVersion}/StreamingAssets/package/DefaultPackage
    // AppVersion 来自 Application.version（如 "1.0"），发版才变，作为稳定分版本目录
    string appVersion = Application.version;
    return Path.Combine(ResDownLoadPath, $"v{appVersion}", "StreamingAssets", "package", "DefaultPackage")
        .Replace("\\", "/");
}
```

> `ResDownLoadPath` 配置值改为只到 webgl 这一级（见改动 5），由代码补全后续路径。

### 改动 2：`ResourceModuleDriver.cs` — 激活 ApplicableGameVersion 死字段
**文件**：`Assets/TEngine/Runtime/Module/ResourceModule/ResourceModuleDriver.cs:253-270`（Start 方法）

在 `_resourceModule.Initialize()` 前补一行，把 App 版本号赋给框架字段（用于调试器展示 + 统一来源）：

```csharp
_resourceModule.ApplicableGameVersion = Application.version;   // 新增：激活死字段
```

> 同时需要把 `ResourceModule._applicableGameVersion` 改为可 set，或在 `IResourceModule` 暴露 setter。
> 现状 `ResourceModule.cs:74` `public string ApplicableGameVersion => _applicableGameVersion;` 只有 getter。
> 方式：在 `ResourceModule.cs` 给 `ApplicableGameVersion` 加 `private set`，或新增 `SetApplicableGameVersion`。
> 因 `ResourceModuleDriver` 拿到的是 `IResourceModule` 接口，需在接口加 setter 或公开方法（见改动 3）。

### 改动 3：`IResourceModule.cs` — 暴露 App 版本号 setter
**文件**：`Assets/TEngine/Runtime/ResourceModule/IResourceModule.cs`

接口里 `string ApplicableGameVersion { get; }` 改为 `{ get; set; }`，
`ResourceModule.cs` 对应实现加 set。这样 `ResourceModuleDriver` 能注入 `Application.version`。

### 改动 4：`ReleaseTools.cs` — 打包输出带 App 版本子目录
**文件**：`Assets/TEngine/Editor/ReleaseTools/ReleaseTools.cs:273-281`（BuildWithConfig 的 outputRoot 拼接）

打包时让 AB 输出落到 `Bundles/WebGL/v1.0/...`，这样打完直接整目录上传 CDN 的 `v1.0/` 即可：

```csharp
// outputRoot 基础上拼 App 版本号子目录
outputRoot = Path.Combine(outputRoot, $"v{Application.version}").Replace('\\', '/');
buildParameters.BuildOutputRoot = outputRoot;
```

> 注意：YooAsset 内部仍会在 BuildOutputRoot 下按 PackageVersion 再分目录（`Bundles/WebGL/v1.0/DefaultPackage/2026-09-07-925/`），
> 上传 CDN 时取该 PackageVersion 目录内的文件平铺到 `v1.0/StreamingAssets/package/DefaultPackage/`。
> 上传步骤可由人工或后续脚本完成，本方案先保证运行时拉取正确。

### 改动 5：`UpdateSetting.asset` — 远端地址只留到 webgl 前缀
**文件**：`Assets/TEngine/Settings/UpdateSetting.asset:32-33`

```
ResDownLoadPath: https://xunzi.setworld.net/webgl
FallbackResDownLoadPath: https://xunzi.setworld.net/webgl
```
（去掉末尾 `/StreamingAssets/package/DefaultPackage`，交给改动 1 的代码拼接补全）

---

## 四、为什么不动 PackageVersion

- `PackageVersion`（日期-分钟）是**同一个 App 版本目录内**"哪个资源版本最新"的标识，
  由 `.version` 文件指向。它负责同一 App 版本内的资源迭代（如 1.0 版本下修 bug 发新资源）。
- 改方案后：App 版本目录隔离不同大版本（1.0 vs 2.0，不兼容），PackageVersion 隔离同版本内迭代。
  两者职责正交，互不冲突，**无需改 PackageVersion 生成逻辑**。

---

## 五、风险与兼容

| 风险 | 应对 |
|---|---|
| 改后老客户端(已发布)仍拉旧固定地址 → 新地址下没文件 | 老客户端已上线不可改。新地址 `v1.0/` 下放当前线上同一份资源即可兼容老包（老包地址固定指向 v1.0 目录）|
| 微信小游戏文件系统（WechatFileSystem）路径拼接 | 复用同一 `HostServerURL`，逻辑一致，已确认走 `IRemoteServices` 同一路径 |
| EditorSimulateMode 不走远端 | 不受影响，`playMode == EditorSimulateMode` 时不读 HostServerURL |
| `Application.version` 为空或含特殊字符 | `1.0` 合法；如发版改 `1.0.0` 也合法。已校验 Path.Combine 安全 |

---

## 六、验证步骤

1. EditorSimulateMode 启动 → 不触发远端，流程不回归
2. 切 WebPlayMode（Remote）启动 → 日志打印 `HostServerURL` 含 `/v1.0/StreamingAssets/package/DefaultPackage`
3. CDN 上建 `v1.0/` 目录放当前资源 → 客户端能正常 `RequestPackageVersionAsync` + 拉清单 + 下载
4. 模拟 `v2.0/` 放新版资源 → 改 `Application.version=2.0` 重打 → 客户端拉 2.0 目录，1.0 客户端仍拉 1.0

---

## 七、落地顺序

1. 改 `IResourceModule.cs` + `ResourceModule.cs`（ApplicableGameVersion 加 set）
2. 改 `ResourceModuleDriver.cs`（Start 注入 Application.version）
3. 改 `UpdateSetting.cs`（GetResDownLoadPath 拼 App 版本）
4. 改 `UpdateSetting.asset`（地址缩到 webgl 前缀）
5. 改 `ReleaseTools.cs`（打包输出带 v{AppVersion} 子目录）
6. Editor 验证 WebPlayMode 地址拼接正确
