using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GameLogic.Data;
using GameLogic.GamePlay.CorePlay;
using TEngine;
using UnityEngine;

namespace GameLogic
{
    [Window(UILayer.System)]
    class UIGM : UIWindow
    {
        private const string TopBtnRes = "TopBtn_GM";
        private const string GmGroupRes = "GmGroup";

        private RectTransform _topBtnTf;
        private RectTransform _scrollContent;

        private XYButton _closeBtn;

        protected override void OnCreate()
        {
            base.OnCreate();
            _topBtnTf = FindChildComponent<RectTransform>("Panel/UITopBtns");
            _scrollContent = FindChildComponent<RectTransform>("Panel/ScrollView/Viewport/Content");
            _closeBtn = CreateWidget<XYButton>("Panel/CloseBtn");
            _closeBtn.OnAddListener(Hide);
        }

        protected override void OnRefresh()
        {
            base.OnRefresh();
            if (_groupDic != null) return; // 防止重复生成
            // OnRefresh 由框架同步调用，无法 await；异步生成以 fire-and-forget 方式启动，
            // 内部按顺序 await 各 CreateWidgetByPathAsync，保证 _curTitle/_curGroup 状态流正确。
            GenerateGmItemsAsync().Forget();
        }

        #region 公共接口


        private string _curTitle;
        private GmGroupWidget _curGroup;
        private Dictionary<string, List<GmGroupWidget>> _groupDic;

        private async UniTask CreateTopBtnAsync(string title)
        {
            if (_groupDic == null)
                _groupDic = new Dictionary<string, List<GmGroupWidget>>();

            _curTitle = title;
            _groupDic.Add(title, new List<GmGroupWidget>());
            GmTopBtnWidget btnWidget = await CreateWidgetByPathAsync<GmTopBtnWidget>(_topBtnTf, TopBtnRes);
            btnWidget.Init(title, OnClickTopBtn);
        }

        private async UniTask<GmGroupWidget> CreateGmGroupAsync(string groupTitle)
        {
            if (_groupDic.TryGetValue(_curTitle, out var groups))
            {
                GmGroupWidget groupWidget = await CreateWidgetByPathAsync<GmGroupWidget>(_scrollContent, GmGroupRes);
                _curGroup = groupWidget;
                _curGroup.SetTitle(groupTitle);
                groups.Add(groupWidget);
            }
            return _curGroup;
        }

        private async UniTask CreateBtnAsync(string s, Action action)
        {
            if (_curGroup == null)
                return;
            await _curGroup.CreateBtnAsync(s, action);
        }

        private async UniTask CreateBtnAndInputAsync(string s, Action<string> action)
        {
            if (_curGroup == null)
                return;
            await _curGroup.CreateBtnAndInputAsync(s, action);
        }

        private void OnClickTopBtn(string title)
        {
            foreach (var group in _groupDic)
            {
                foreach (var items in group.Value)
                {
                    items.Visible = false;
                }
            }
            if (_groupDic.TryGetValue(title, out var groups))
            {
                foreach (var group in groups)
                {
                    group.Visible = true;
                }
            }
        }

        #endregion

        #region 具体功能

        private async UniTaskVoid GenerateGmItemsAsync()
        {
            #region 局内

            await CreateTopBtnAsync("局内");

            await CreateGmGroupAsync("关卡");
            await CreateBtnAndInputAsync("跳转关卡", (levelId) =>
            {
                if (int.TryParse(levelId, out int id) && id >= 1)
                {
                    GameManager.Instance.ResetProgress();
                    GameManager.Instance.CurrentGamePlay?.LoadLevel(id);
                    Log.Info($"[GM] 跳转至关卡 {id}");
                }
            });
            await CreateBtnAsync("下一关", () =>
            {
                GameManager.Instance.LoadNextCorePlayLevel();
                Log.Info("[GM] 加载下一关");
            });
            await CreateBtnAsync("直接通关", () =>
            {
                var gameplay = GameManager.Instance.CurrentGamePlay as CorePlayGamePlay;
                if (gameplay != null && gameplay.CurrentLevelData != null)
                {
                    int levelId = gameplay.CurrentLevelId;
                    gameplay.EndGame();
                    GameEvent.Send(EventDefine.Event_LevelCompleted, levelId);
                    Log.Info($"[GM] 直接通关 关卡 {levelId}");
                }
            });
            await CreateBtnAsync("重置进度", () =>
            {
                GameManager.Instance.ResetProgress();
                Log.Info("[GM] 进度已重置");
            });

            await CreateGmGroupAsync("道具");

            await CreateBtnAndInputAsync("设置提示数量", (num) =>
            {
                if (int.TryParse(num, out int count))
                {
                    PropDefine.InitPropCounts(count, PropDefine.CoinCount, PropDefine.ResetCount);
                    Log.Info($"[GM] 提示道具设为 {count}");
                }
            });
            
            await CreateBtnAndInputAsync("设置重置数量", (num) =>
            {
                if (int.TryParse(num, out int count))
                {
                    PropDefine.InitPropCounts(PropDefine.TipCount, PropDefine.CoinCount, count);
                    Log.Info($"[GM] 重置道具设为 {count}");
                }
            });
            
            await CreateBtnAndInputAsync("设置金币数量", (num) =>
            {
                if (int.TryParse(num, out int count))
                {
                    PropDefine.InitPropCounts(PropDefine.TipCount, count, PropDefine.ResetCount);
                    Log.Info($"[GM] 金币设为 {count}");
                }
            });
            await CreateBtnAsync("道具设为999", () =>
            {
                PropDefine.InitPropCounts(999, 999, 999);
                Log.Info("[GM] 道具均设为 999");
            });

            await CreateGmGroupAsync("状态");
            await CreateBtnAsync("打印当前状态", () =>
            {
                var gm = GameManager.Instance;
                var gp = gm.CurrentGamePlay as CorePlayGamePlay;
                Log.Info($"[GM] === 游戏状态 ===");
                Log.Info($"[GM] 当前关卡ID: {gp?.CurrentLevelId ?? -1}");
                Log.Info($"[GM] 关卡总数: {gp?.TotalLevelCount ?? 0}");
                Log.Info($"[GM] 游戏运行中: {gp?.IsGameRunning ?? false}");
                Log.Info($"[GM] 提示道具: {PropDefine.TipCount}");
                Log.Info($"[GM] 金币: {PropDefine.CoinCount}");
                Log.Info($"[GM] 基字: {gp?.GetBaseCharacter() ?? "无"}");
                Log.Info($"[GM] 已找到答案: {gp?.FoundAnswerIndices?.Count ?? 0}");
                Log.Info($"[GM] ================");
            });

            #endregion

            #region 活动

            await CreateTopBtnAsync("活动");
            await CreateGmGroupAsync("活动");
            await CreateBtnAsync("打开活动弹窗", () =>
            {
                // TODO: 接入活动弹窗
                Log.Info("[GM] 活动弹窗（待实现）");
            });

            #endregion

            #region 其他

            await CreateTopBtnAsync("其他");
            await CreateGmGroupAsync("导航");
            await CreateBtnAsync("返回主界面", () =>
            {
                GameManager.Instance.ReturnToHome();
                Log.Info("[GM] 返回主界面");
            });
            await CreateBtnAsync("关闭GM面板", () =>
            {
                GameModule.UI.CloseUI<UIGM>();
                Log.Info("[GM] GM面板已关闭");
            });

            #endregion

            // 默认显示第一个 Tab
            if (_groupDic.Count > 0)
            {
                var enumerator = _groupDic.GetEnumerator();
                enumerator.MoveNext();
                OnClickTopBtn(enumerator.Current.Key);
            }
        }

        #endregion
    }
}
