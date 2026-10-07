# BI 事件说明

业务入口：`BiMgr`；底层调用链：`BiMgr → SDK → ISdk → WXSDK.ReportEvent`。
编辑器通过 `EditorSDK` 输出 `[BI]` 日志。参数均为字符串；`BiMgr.Enabled` 可关闭上报。
返回 true 表示 SDK 已在本地接收事件（可能等待登录或重试），不代表服务端已收到。

## 启动天梯

启动阶段使用 `GameSDK.StartupTelemetry`，只负责节点、计时和去重，统一调用 `SDK.ReportEvent`。`GameSDK` 为独立常驻程序集，Procedure 可直接引用，热更 GameLogic 显式引用它；不依赖热更业务类型。`StartupTelemetry.Enabled` 单独控制启动事件。

- `startup_step`：执行步骤时发送 `start`，结束时发送 `success` 或 `fail`；不适用的分支发送一次 `skip`。
- `startup_complete`：首页就绪且启动遮罩关闭后发送，每次启动只触发一次；模拟进度到 100% 不会直接触发。
- 固定编号：1 `launch`、2 `init_package`、3 `request_version`、4 `update_manifest`、5 `create_downloader`、6 `download_patch`、7 `clear_cache`、8 `preload`、9 `load_assembly`、10 保留（不再发送）、11 `init_config`、12 `init_manager`、13 `init_system`、14 `load_top_ui`、15 `load_home`、16 `finalize_startup`、17 `complete`。
- 公共参数：`startup_id`（本次启动唯一 ID）、`index`（固定步骤编号）、`step`（当前步骤）、`status`、`attempt`（从 1 开始，步骤重试递增；skip 为 0）、`elapsed_ms`（从 Unity 启动流程入口累计耗时）、`app_version`、`resource_version`（未知时为空）、`play_mode`。参数值均为字符串。
- 步骤结束附带 `duration_ms`，全部完成时该值为总耗时；计时使用单调时钟，不受 timeScale 影响，包含后台停留时间。起点不包含进入 Unity 流程之前的平台加载时间。
- 失败附带 `error_code`、`error_msg`（最多 256 字符）；跳过附带 `reason`；创建下载器成功、下载成功附带 `download_count`、`download_bytes`。
- 重复开始、重复结束、重复完成回调去重。步骤失败后重试使用原 index；允许容错继续的预加载失败保留 fail，不改报 success。整体可在容错后成功完成。
- 登录成功前，由 SDK 统一暂存所有启动及业务 BI，最多 512 条，满时丢弃新事件并输出警告。仅内存缓存，退出应用即丢失；不保证服务端送达。
- 登录及 openid 获取是并行流程，不阻塞资源加载和首页展示。原热更初始化节点 index=10 已保留停用，其他编号不变。
- 配置阶段并行读取五张表（含语言内容表），并提前发起 UITop/UIHome 的 Prefab 加载，与配置和首关预热重叠。UI 仍先初始化 UITop 再初始化 UIHome；index=14/15 的耗时是进入该阶段后的剩余资源等待及 UI 创建耗时，不代表完整下载耗时。总启动速度以 `startup_complete.elapsed_ms` 比较。

## SDK 登录与统一缓存

- `ProcedureLaunch.OnEnter` 的最早业务调用为 `SDK.InitializeAndLogin()`，幂等且不重复发起并发登录。热更不再初始化 SDK。使用 `using GameSDK;` 直接访问 SDK；平台初始化、微信登录和云函数调用均在 `WXSDK` 内。
- `SDK.IsLoggedIn` / `SDK.LoginState` / `SDK.OpenId` 表示本次运行的状态。每次启动从未登录开始；原生 SDK 初始化、WX.Login、getOpenid 云函数返回有效 openid 后才登录成功。本地 openid 只缓存身份，不用于绕过本次登录。
- 登录单次超时 60 秒，自动最多尝试 3 次，失败后分别等待 2、4 秒；耗尽后保留队列，可调用 `SDK.RetryLogin()` 再发起一轮。迟到或重复回调不能覆盖当前状态。
- 登录事件 `sdk_login` 也走相同缓存入口：`status=start/success/fail`、`attempt`、`error_msg`。因此失败信息只有后续成功登录才会发送。
- 所有自定义 BI 附加 `sdk_session_id`、`event_seq`、`event_time_ms`（事件产生时 UTC Unix 毫秒）。入队复制参数，补发和重试不改变原始内容、序号和时间；补发期间新事件追加队尾。
- 登录成功后先设置身份和状态，再按 FIFO 补发。平台调用抛异常时保留队首，最多调用 3 次，间隔 2、4 秒；耗尽后停止自动补发，不越过失败事件，显式 `SDK.RetryPendingEvents()` 可恢复。`SDK.PendingBiCount` 可查看队列长度。
- 原生 `SDK.ReportGameStart()` 同样等待登录，由同一个队列调用平台原生方法，不转换成自定义事件。
- 编辑器使用 EditorSDK 模拟登录成功，BI 输出 `[BI]` 日志。`BiMgr.Enabled` 控制热更业务入口；关闭入口不撤销已入队的事件。登录诊断事件由 SDK 自己产生。
- `GameSDK/link.xml` 保留供热更调用的常驻 API，避免 IL2CPP 裁剪。首次迁移需要重新构建播放器及热更程序集，不能只替换 GameLogic 热更 DLL。

验证：运行 `TEngine/Tools/Test-StartupSdk.ps1`，33 项检查通过；独立测试 `TEngine/Tools/StartupTelemetryTests.cs` 覆盖统一登录门禁、重试、迟到回调、FIFO、缓存副本、异常隔离、容量及启动节点去重。微信云函数登录、后台实际接收与 IL2CPP 真机构建仍需验证。

## 已接入的 1–11 项

| 序号 | 事件名 | 时机 | 主要字段 |
| --- | --- | --- | --- |
| 1 | `level_start` | 有效关卡加载并启动玩法；仅预览/切换数据不报 | `entry`：home/next；`restored`：0/1；进度字段 |
| 2 | `answer_submit` | 玩家提交答案 | `result`：correct/wrong/duplicate/empty；`answer`；`stroke_count`；进度字段 |
| 3 | `level_complete` | 本次关卡尝试完成，仅一次 | `completion_method`：manual/next_prop/restored_complete；进度字段 |
| 4 | `level_leave` | 未完成关卡时返回首页 | `reason`：home；进度字段 |
| 5 | `prop_use` | 提示/下一关道具实际执行成功或失败 | `prop`：tip/next；`payment`：inventory/coin/none；`cost`；`result`：success/failed；`reason` |
| 6 | `resource_change` | 金币或道具数量发生实际变更 | `resource`：coin/tip/reset/next；`delta`；`balance`；`source` |
| 7 | `guide_step` | 引导开始、每个答案选好/提交成功、引导结束 | `step`：start/answer_N_selected/answer_N_submitted/complete；`duration_sec` |
| 8 | `share_click` | 调用分享前 | `entry`：home/finish/get_coin |
| 9 | `finish_action` | 结算页首次点击下一关/首页 | `action`：next/home；`duration_sec` 为结算页停留时长 |
| 10 | `home_action` | 首页点击开始/游戏圈/分享/设置 | `action`：start/game_club/share/setting；`level_id` 为存档将进入的关卡，全通关时为末关 |
| 11 | `all_levels_complete` | 通关末关 | `last_level_id`；`content_version` |

## 字段口径

- 关卡上下文：`level_id`、`level_name`、`base_character`、`attempt_id`、`required_count`。同一次进入关卡共用 `attempt_id`，恢复存档重新进入会生成新值。
- 进度字段：`found_count`、`submit_count`、`wrong_count`、`tip_count`、`duration_sec`。
- `submit_count` 包含空提交、错误、重复和正确提交；`wrong_count` 仅统计 wrong；`tip_count` 仅统计成功提示。道具自动填入答案不计提交次数。
- 时长单位为秒，保留三位小数，排除 `OnApplicationPause` 通知的后台时间。恢复进度后的时长和操作次数从本次进入重新计算。
- `answer` 是匹配到的答案字；empty/wrong 时为空字符串。
- `prop_use.cost` 是实际扣除量，inventory 时单位为道具个数，coin 时单位为金币；广告接入后 payment 可为 ad，cost 为 0，完整观看并实际生效才记录成功。失败原因包括 payment_failed/no_remaining_answer/invalid_answer_strokes/completion_prepare_failed。因配置错误造成扣费后失败时，保留实际花费。
- `resource_change.delta` 正数为获得，负数为消耗；`balance` 是变化后余额。来源包括 level_reward/daily_claim/share_reward/tip/next/reset/set_counts；未显式传入来源的扩展调用为 unknown。
- 游戏内资源变化携带关卡上下文；返回首页后清除上下文，首页领取不归属上一关。
- 首页分享会同时产生 home_action 与 share_click，分别用于首页按钮和分享入口分析。
- 分享只记录点击；现有分享奖励发放产生 resource_change，不表示用户已成功分享。
- `all_levels_complete` 以“应用版本:最大关卡 ID”为内容版本，在存档字段 `biAllLevelsCompletedVersion` 去重；清档会重置该标记。该标记记录本地触发，不保证远端送达。
- 未接入生命周期事件；广告事件已接入，详见《广告接入说明》。免费全选/清空操作不记为付费道具使用。

## 验证

独立编译 GameLogic 源码（使用项目 Unity 引用和代码生成器）通过；现有编译警告未改动。
使用真实 BiMgr、CorePlayGamePlay、PropDefine 源码和外部依赖替身的 24 项检查通过，覆盖有效/无效加载、答题分类、跳关、恢复、去重、后台计时和资源变动。
微信端事件接收与后台展示仍需在小游戏真机环境验证。
