# 修复：结算返回重开游戏，关卡被渲染两次导致笔画重叠

## 根因（用户诊断正确，非 DrawCharacter.Clear 问题）

`CurrentGamePlay` 是单例，`_currentLevelData` 在上一局 `LoadLevel` 时已设置，
`EndGame()` / `ReturnToHome()` **不清空 `_currentLevelData`**。

结算"返回"后重开游戏，`GameManager.StartCorePlay()` 顺序：
1. 行 148 `_corePlayView.Initialize(...)`：
   - `Initialize` 行 122-126：因 `cp.CurrentLevelData != null`（残留上一关）→
     `RenderLevelAsync(上一关数据).Forget()` —— **渲染 #1，且渲染的是上一关**
2. 行 151 `_corePlayGamePlay.LoadLevel(下一关...)` → `OnLevelLoaded` →
   `RenderLevelAsync(下一关数据)` —— **渲染 #2，渲染的是下一关**

两个 fire-and-forget 异步 `RenderLevelAsync` 并发：
- 行 277-280 `if (_drawCharacter == null) CreateDrawCharacter()` 的 null 检查竞态 →
  各自 new 一个 DrawCharacter，挂同一 `CharacterRoot` → 两套笔画永久叠加
  （甚至上一关 + 下一关笔画同时存在）
- 或在同一个 DrawCharacter 上 `DrawAsync`→`Clear()` 互踩 → 笔画 GameObject 泄漏

**首次进入无此问题**：`InitLevelId` 只设 ID 不加载数据，`CurrentLevelData == null`，
Initialize 兜底不触发，仅 `LoadLevel` 一次渲染。

## 流程乱点

`Initialize` 的"立刻渲染当前关卡"是给"视图已复用+关卡早加载好"场景的兜底，
但 `StartCorePlay` 总是紧跟 `LoadLevel`，二者职责重叠 → 复用路径必双重渲染。

## 修复方案

### 改动 1（治根，理清流程）：移除 Initialize 的冗余兜底渲染
文件：`CorePlayView.cs`，`Initialize()` 行 122-126

删除：
```csharp
if (_gamePlay is CorePlayGamePlay cp && cp.CurrentLevelData != null)
{
    RenderLevelAsync(cp.CurrentLevelData).Forget();
}
```

理由：`Initialize` 唯一调用点 `StartCorePlay` 紧跟 `LoadLevel`，渲染统一由
`LoadLevel → OnLevelLoaded` 触发（首次+复用同一路径，单一权威）。
已验证 `Initialize` 全代码库仅 `GameManager.cs:148` 一处调用。

`Initialize` 仍保留：建 DrawCharacter、建输入处理器、绑事件、置 `_isInitialized`。

### 改动 2（兜底，并发安全）：RenderLevelAsync 串行化
文件：`CorePlayView.cs`，`RenderLevelAsync`

新增字段 `private int _renderVersion;`（单调递增版本号，无需 CancellationToken）。
`RenderLevelAsync` 开头 `int myVersion = ++_renderVersion;`，每个 await 后判
`if (myVersion != _renderVersion) return;` —— 后到的渲染作废先到的。
保证同一时刻只有一次有效渲染，防任何并发竞态（双保险）。

注：MonoBehaviour 可用 `GetCancellationTokenOnDestroy()`，但版本号更简单、
且能区分"对象未销但重复渲染"。优先用版本号。

### 改动 3：回退上一轮有害的 CreateDrawCharacter 激进清理
文件：`CorePlayView.cs`，`CreateDrawCharacter()`

上一轮加的"遍历 DestroyImmediate 清空整个 CharacterRoot"过于激进，在并发下会
互相清掉对方刚建的 DrawCharacter。回退为克制版：仅当 `_drawCharacter != null`
时 `DestroyImmediate` 旧的并置 null，不清全 root。

### 改动 4：保留 OnEndGameAnim 即时销毁（无害）
`OnEndGameAnim` 用 `DestroyImmediate` 即时销毁 DrawCharacter，无害，保留。

## 验证点
1. 通关→结算"返回"→首页"开始"：仅一次渲染，无重叠（上一关笔画不再被 Initialize 渲染）。
2. 通关→结算"下一关"：单次渲染切换。
3. 首次进入：行为不变（本就单次渲染）。
4. Hierarchy 中 CharacterRoot 下始终至多一个 DrawCharacter。
