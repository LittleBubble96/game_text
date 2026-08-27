# 修复：结算返回后重开游戏，DrawCharacter 视图重叠

## 根因

`CorePlayView` 在 `ReturnToHome()` 后**不被销毁、`_corePlayView` 字段不置 null**，再开始游戏时被复用。
通关结算时 DrawCharacter A 的笔画**仍渲染在场景**（`OnLevelCompleted` 只弹 `UIFinish`，不清笔画）。

复用路径 `StartCorePlay → Initialize → CreateDrawCharacter()`：
- `CreateDrawCharacter()` **无条件 `new GameObject("DrawCharacter")`**，不检查/不清理已有 `_drawCharacter`，也不清 `CharacterRoot` 残留。
- `ReturnToHome` 里 `OnEndGameAnim()` 用的是 `Destroy(_drawCharacter.gameObject)` —— **延迟销毁，帧末才真正移除**。

竞态：返回时 `Destroy(A)` 入队但本帧未落，再进游戏同帧/紧接 `CreateDrawCharacter` 又挂上 B → A、B 同时存在于 `CharacterRoot` → 笔画重叠。

`DrawCharacter.Clear()` 本身没问题（`DestroyImmediate` 倒序清子物体正确），缺陷在**调用时机与复用幂等性**。

## 修复方案（即时清理 + 复用幂等）

### 改动 1：`DrawCharacter.Clear()` 用即时销毁（已是 DestroyImmediate，无需改）
确认无需改动。`Clear()` 已用 `DestroyImmediate`，正确。

### 改动 2：`CorePlayView.CreateDrawCharacter()` 复用幂等
文件：`Assets/GameScripts/HotFix/GameLogic/Game/CorePlay/View/CorePlayView.cs`

`CreateDrawCharacter()` 在 new 新 DrawCharacter 前，先清理旧的：
1. 若 `_drawCharacter != null`，`DestroyImmediate(_drawCharacter.gameObject)`，置 null。
2. 兜底：遍历 `_gameViewRoot.CharacterRoot` 子物体，`DestroyImmediate` 全部清掉（防止任何残留的 DrawCharacter/孤儿笔画）。
3. 再 new 新的 DrawCharacter。

这样无论 `ReturnToHome` 的延迟 `Destroy` 是否落帧，复用入口都保证 CharacterRoot 干净。

### 改动 3：`CorePlayView.OnEndGameAnim()` 即时销毁
同文件，`OnEndGameAnim()` 里把 `Destroy(_drawCharacter.gameObject)` 改为 `DestroyImmediate(_drawCharacter.gameObject)`，消除延迟销毁与新渲染的竞态窗口。

### 改动 4：`GameManager.ReturnToHome()` 保持现状
已调用 `OnEndGameAnim()`（现会即时销毁 DrawCharacter），无需额外改。`_corePlayView` 复用现状保留（避免 GameViewRoot prefab 反复加载）。

## 不改动的部分
- `DrawCharacter.Clear()`：正确，不动。
- `GameManager`：`ReturnToHome`/`StartCorePlay` 逻辑保留。
- 通关结算不清笔画：保留现状（结算页叠在上方，符合预期）。

## 验证点
1. 通关 → 结算"返回" → 首页"开始" → 仅一套笔画，无重叠。
2. 通关 → 结算"下一关" → 切关无重叠（此路径 `RenderLevelAsync` 在旧 DrawCharacter 上 `Clear` 重画，本不重叠；改动后更安全）。
3. 多次往返返回-开始，不累积 GameObject（Hierarchy 中 CharacterRoot 下始终至多一个 DrawCharacter）。
