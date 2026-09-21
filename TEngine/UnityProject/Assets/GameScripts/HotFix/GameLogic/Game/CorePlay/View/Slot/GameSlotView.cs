using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TEngine;
using UnityEngine;

namespace GameLogic.GamePlay.CorePlay.View
{
    public struct SlotViewLayoutData
    {
        public Vector3 Center;
        public Vector3 Top;
        public Vector3 Bottom;
        public Vector3 Left;
        public Vector3 Right;
    }

    public struct ContentViewLayoutData
    {
        public Vector3 Top;
        public Vector3 Bottom;
        public Vector3 Left;
        public Vector3 Right;

        /// <summary>可用世界宽度（Right.x - Left.x）</summary>
        public float AvailableWidth;
        /// <summary>可用世界高度（Top.y - Bottom.y）</summary>
        public float AvailableHeight;
    }

    public class GameSlotView
    {
        private List<GameSlotViewItem> _slotItems = new List<GameSlotViewItem>();

        private Transform _slotRoot;

        private const float _spacing = 0.06f;
        private const float _slotSize = 0.55f;

        private SlotViewLayoutData _currentLayoutData;
        private bool _hasLayoutData;


        public void OnCreate(Transform tf)
        {
            _slotRoot = tf;
            GameEvent.AddEventListener<SlotViewLayoutData>(EventDefine.Event_SlotLayoutUpdate, OnLayoutUpdate);
            GameEvent.AddEventListener<bool, string, string>(EventDefine.Event_AnswerSubmitted, OnAnswerSubmitted);
        }

        public void OnDestroy()
        {
            GameEvent.RemoveEventListener<SlotViewLayoutData>(EventDefine.Event_SlotLayoutUpdate, OnLayoutUpdate);
            GameEvent.RemoveEventListener<bool, string, string>(EventDefine.Event_AnswerSubmitted, OnAnswerSubmitted);
            RecycleSlotView();
        }

        /// <summary>
        /// 收到 UI 传来的布局数据
        /// </summary>
        private void OnLayoutUpdate(SlotViewLayoutData layoutData)
        {
            _currentLayoutData = layoutData;
            _hasLayoutData = true;
            // 如果已经有 slot，重新布局
            if (_slotItems.Count > 0)
            {
                LayoutSlots();
            }
        }

        //游戏开始 初始化slot（异步：对象池冷启动时需加载资源，逐个 await 分配）
        public async UniTask InitSlotViewAsync(int count)
        {
            RecycleSlotView();
            for (int i = 0; i < count; i++)
            {
                GameSlotViewItem viewItem = await GameDataPoolManager.Instance.AllocateComponentAsync<GameSlotViewItem>(
                    GameSlotViewItem.ResPath, _slotRoot);
                viewItem.transform.localScale = Vector3.zero;
                viewItem.ShowEmptyState();
                _slotItems.Add(viewItem);
            }

            if (_hasLayoutData)
            {
                LayoutSlots();
            }

            foreach (var slotItem in _slotItems)
            {
                // 入场动画只缩放 item 内部的 root，外层保留布局算出的缩放。
                if (!_hasLayoutData) slotItem.transform.localScale = Vector3.one;
                slotItem.PlayEnterAnim();
            }
        }

        /// <summary>答案提交成功时，填充下一个空 slot</summary>
        private void OnAnswerSubmitted(bool success, string answerCharacter, string message)
        {
            if (!success || string.IsNullOrEmpty(answerCharacter)) return;
            FillNextSlot(answerCharacter);
        }

        /// <summary>填充下一个未填充的 slot（带动画）</summary>
        private void FillNextSlot(string answerCharacter)
        {
            foreach (var item in _slotItems)
            {
                if (!item.IsFilled)
                {
                    item.SetContentAndPlay(answerCharacter);
                    return;
                }
            }
        }

        /// <summary>依次填充答案，每个答案的动画起点间隔指定帧数</summary>
        public async UniTask PlayAnswersSequentiallyAsync(IReadOnlyList<string> answerCharacters, int intervalFrames)
        {
            if (answerCharacters == null || answerCharacters.Count == 0) return;

            var cancellationToken = _slotRoot.GetCancellationTokenOnDestroy();
            for (int i = 0; i < answerCharacters.Count; i++)
            {
                FillNextSlot(answerCharacters[i]);
                if (i < answerCharacters.Count - 1 && intervalFrames > 0)
                {
                    await UniTask.DelayFrame(intervalFrames, cancellationToken: cancellationToken);
                }
            }

            await UniTask.Delay(400, cancellationToken: cancellationToken);
        }

        /// <summary>从存档恢复已找到的答案（无动画）</summary>
        public void RestoreAnswers(List<string> foundAnswers)
        {
            if (foundAnswers == null || foundAnswers.Count == 0) return;
            int count = Mathf.Min(foundAnswers.Count, _slotItems.Count);
            for (int i = 0; i < count; i++)
            {
                _slotItems[i].SetContentImmediate(foundAnswers[i]);
            }
        }

        /// <summary>清空所有 slot（重置道具使用：已填答案归零，回到空状态，无动画）</summary>
        public void ClearAllSlots()
        {
            foreach (var item in _slotItems)
            {
                if (item != null) item.ShowEmptyState();
            }
        }

        private void RecycleSlotView()
        {
            foreach (var slotItem in _slotItems)
            {
                GameDataPoolManager.Instance.RecycleComponent<GameSlotViewItem>(slotItem, GameSlotViewItem.ResPath);
            }
            _slotItems.Clear();
        }
        

        /// <summary>
        /// 布局所有 slot。
        /// 单行从左边界排列；多行排列区域整体水平居中，末行沿用相同列位置，不单独居中。
        /// 按宽高限制统一缩小，格子和横纵间距等比缩放，整体垂直居中。
        /// 前面的行按缩放后的容量填满，不通过拉大间距强行撑满宽度。
        /// </summary>
        public void LayoutSlots()
        {
            if (_slotItems.Count == 0 || !_hasLayoutData) return;

            float leftBound = _currentLayoutData.Left.x;
            float availableWidth = _currentLayoutData.Right.x - leftBound;
            float availableHeight = _currentLayoutData.Top.y - _currentLayoutData.Bottom.y;
            if (availableWidth <= 0 || availableHeight <= 0) return;

            var (columns, rows, scale) = CalculateGrid(_slotItems.Count, availableWidth, availableHeight);
            float size = _slotSize * scale;
            float spacingY = _spacing * scale;
            float spacingX = spacingY;
            float totalHeight = rows * size + (rows - 1) * spacingY;
            float totalWidth = columns * size + (columns - 1) * spacingX;
            float centerY = (_currentLayoutData.Top.y + _currentLayoutData.Bottom.y) * 0.5f;
            float offsetX = rows > 1 ? (availableWidth - totalWidth) * 0.5f : 0f;
            float firstX = leftBound + offsetX + size * 0.5f;
            float firstY = centerY + (totalHeight - size) * 0.5f;

            for (int i = 0; i < _slotItems.Count; i++)
            {
                var item = _slotItems[i];
                if (item == null) continue;
                int row = i / columns;
                int column = i % columns;
                item.transform.position = new Vector3(
                    firstX + column * (size + spacingX),
                    firstY - row * (size + spacingY), 0);
                item.transform.localScale = Vector3.one * scale;
            }
        }

        /// <summary>
        /// 在所有可行列数中选择最大的格子；缩放相同时优先增加列数，填满前面的行。
        /// 一次计算同时考虑宽高，不再在“缩放”和“换行”之间反复迭代。
        /// </summary>
        private static (int columns, int rows, float scale) CalculateGrid(int count, float width, float height)
        {
            int bestColumns = 1;
            float bestScale = 0f;
            for (int columns = 1; columns <= count; columns++)
            {
                int rows = (count + columns - 1) / columns;
                float widthScale = width / (columns * _slotSize + (columns - 1) * _spacing);
                float heightScale = height / (rows * _slotSize + (rows - 1) * _spacing);
                float scale = Mathf.Min(1f, Mathf.Min(widthScale, heightScale));
                if (scale < bestScale) continue;
                bestColumns = columns;
                bestScale = scale;
            }
            return (bestColumns, (count + bestColumns - 1) / bestColumns, bestScale);
        }

        public void PlayBeginGameAnim()
        {

        }

        public void PlayEndGameAnim()
        {

        }
    }
}
