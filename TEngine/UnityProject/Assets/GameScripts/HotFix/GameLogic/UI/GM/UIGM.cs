using System;
using System.Collections.Generic;
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
            _topBtnTf = FindChildComponent<RectTransform>("UITopBtns");
            _scrollContent = FindChildComponent<RectTransform>("ScrollView/Viewport/Content");
            _closeBtn = CreateWidget<XYButton>("CloseBtn");
            _closeBtn.OnAddListener(Hide);
        }

        protected override void OnRefresh()
        {
            base.OnRefresh();
            if (_groupDic != null) return; // 防止重复生成
            GenerateGmItems();
        }

        #region 公共接口

        private string _curTitle;
        private GmGroupWidget _curGroup;
        private Dictionary<string, List<GmGroupWidget>> _groupDic;

        private void CreateTopBtn(string title)
        {
            if (_groupDic == null)
                _groupDic = new Dictionary<string, List<GmGroupWidget>>();

            _curTitle = title;
            _groupDic.Add(title, new List<GmGroupWidget>());
            GmTopBtnWidget btnWidget = CreateWidgetByPath<GmTopBtnWidget>(_topBtnTf, TopBtnRes);
            btnWidget.Init(title, OnClickTopBtn);
        }

        private GmGroupWidget CreateGmGroup(string groupTitle)
        {
            if (_groupDic.TryGetValue(_curTitle, out var groups))
            {
                GmGroupWidget groupWidget = CreateWidgetByPath<GmGroupWidget>(_scrollContent, GmGroupRes);
                _curGroup = groupWidget;
                _curGroup.SetTitle(groupTitle);
                groups.Add(groupWidget);
            }
            return _curGroup;
        }

        private void CreateBtn(string s, Action action)
        {
            if (_curGroup == null)
                return;
            _curGroup.CreateBtn(s, action);
        }

        private void CreateBtnAndInput(string s, Action<string> action)
        {
            if (_curGroup == null)
                return;
            _curGroup.CreateBtnAndInput(s, action);
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

        private void GenerateGmItems()
        {
            #region 局内

            CreateTopBtn("局内");

            CreateGmGroup("关卡");
            CreateBtnAndInput("跳转关卡", (levelId) =>
            {
                if (int.TryParse(levelId, out int id) && id >= 1)
                {
                    GameManager.Instance.ResetProgress();
                    GameManager.Instance.CurrentGamePlay?.LoadLevel(id);
                    Debug.Log($"[GM] 跳转至关卡 {id}");
                }
            });
            CreateBtn("下一关", () =>
            {
                GameManager.Instance.LoadNextCorePlayLevel();
                Debug.Log("[GM] 加载下一关");
            });
            CreateBtn("直接通关", () =>
            {
                var gameplay = GameManager.Instance.CurrentGamePlay as CorePlayGamePlay;
                if (gameplay != null && gameplay.CurrentLevelData != null)
                {
                    int levelId = gameplay.CurrentLevelId;
                    gameplay.EndGame();
                    GameEvent.Send(EventDefine.Event_LevelCompleted, levelId);
                    Debug.Log($"[GM] 直接通关 关卡 {levelId}");
                }
            });
            CreateBtn("重置进度", () =>
            {
                GameManager.Instance.ResetProgress();
                Debug.Log("[GM] 进度已重置");
            });

            CreateGmGroup("道具");
            
            CreateBtnAndInput("设置提示数量", (num) =>
            {
                if (int.TryParse(num, out int count))
                {
                    PropDefine.InitPropCounts(count, PropDefine.CoinCount);
                    Debug.Log($"[GM] 提示道具设为 {count}");
                }
            });
            CreateBtnAndInput("设置金币数量", (num) =>
            {
                if (int.TryParse(num, out int count))
                {
                    PropDefine.InitPropCounts(PropDefine.TipCount, count);
                    Debug.Log($"[GM] 金币设为 {count}");
                }
            });
            CreateBtn("道具设为999", () =>
            {
                PropDefine.InitPropCounts(999, 999);
                Debug.Log("[GM] 道具均设为 999");
            });

            CreateGmGroup("状态");
            CreateBtn("打印当前状态", () =>
            {
                var gm = GameManager.Instance;
                var gp = gm.CurrentGamePlay as CorePlayGamePlay;
                Debug.Log($"[GM] === 游戏状态 ===");
                Debug.Log($"[GM] 当前关卡ID: {gp?.CurrentLevelId ?? -1}");
                Debug.Log($"[GM] 关卡总数: {gp?.TotalLevelCount ?? 0}");
                Debug.Log($"[GM] 游戏运行中: {gp?.IsGameRunning ?? false}");
                Debug.Log($"[GM] 提示道具: {PropDefine.TipCount}");
                Debug.Log($"[GM] 金币: {PropDefine.CoinCount}");
                Debug.Log($"[GM] 基字: {gp?.GetBaseCharacter() ?? "无"}");
                Debug.Log($"[GM] 已找到答案: {gp?.FoundAnswerIndices?.Count ?? 0}");
                Debug.Log($"[GM] ================");
            });

            #endregion

            #region 活动

            CreateTopBtn("活动");
            CreateGmGroup("活动");
            CreateBtn("打开活动弹窗", () =>
            {
                // TODO: 接入活动弹窗
                Debug.Log("[GM] 活动弹窗（待实现）");
            });

            #endregion

            #region 其他

            CreateTopBtn("其他");
            CreateGmGroup("导航");
            CreateBtn("返回主界面", () =>
            {
                GameManager.Instance.ReturnToHome();
                Debug.Log("[GM] 返回主界面");
            });
            CreateBtn("关闭GM面板", () =>
            {
                GameModule.UI.CloseUI<UIGM>();
                Debug.Log("[GM] GM面板已关闭");
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