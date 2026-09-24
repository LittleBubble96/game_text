# BI 事件说明

业务入口：`BiMgr`；底层调用链：`BiMgr → SDK → ISdk → WXSDK.ReportEvent`。
编辑器通过 `EditorSDK` 输出 `[BI]` 日志。参数均为字符串；`BiMgr.Enabled` 可关闭上报。
返回成功只表示平台方法调用成功，不代表服务端已收到。当前无缓存、重试。

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
