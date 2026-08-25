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
                viewItem.ShowEmptyState();
                _slotItems.Add(viewItem);
                viewItem.PlayEnterAnim();
            }

            if (_hasLayoutData)
            {
                LayoutSlots();
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
        /// 规则：
        /// 1. slot 水平靠左排布，垂直居中
        /// 2. 缩放最大为 1，超出边界则缩放
        /// 3. 一排放不下时自动换 2 排、3 排
        /// 4. 间距和大小随缩放变化
        /// </summary>
        public void LayoutSlots()
        {
            if (_slotItems.Count == 0 || !_hasLayoutData) return;

            // 计算可用区域
            float leftBound = _currentLayoutData.Left.x;
            float rightBound = _currentLayoutData.Right.x;
            float topBound = _currentLayoutData.Top.y;
            float bottomBound = _currentLayoutData.Bottom.y;
            float centerY = _currentLayoutData.Center.y;

            float availableWidth = rightBound - leftBound;
            float availableHeight = topBound - bottomBound;

            if (availableWidth <= 0 || availableHeight <= 0) return;

            int slotCount = _slotItems.Count;

            // 先算每行最多能放几个（按 scale=1 算）
            int maxColsPerRow = Mathf.FloorToInt((availableWidth + _spacing) / (_slotSize + _spacing));
            if (maxColsPerRow < 1) maxColsPerRow = 1;

            // 遍历所有行数，取缩放最大的方案
            // 排布规则：前 rows-1 行排满 maxColsPerRow 个，最后一行放剩余
            float bestScale = 0f;
            int bestRows = 1;
            int bestMaxCols = maxColsPerRow;

            int minRows = Mathf.CeilToInt((float)slotCount / maxColsPerRow);
            for (int rows = minRows; rows <= slotCount; rows++)
            {
                int fullRows = rows - 1;
                int lastRowCount = slotCount - fullRows * maxColsPerRow;
                if (lastRowCount <= 0) break; // 行数太多，不需要了

                float scale = CalculateScale(maxColsPerRow, rows, availableWidth, availableHeight);
                scale = Mathf.Min(scale, 1f);

                if (scale > bestScale)
                {
                    bestScale = scale;
                    bestRows = rows;
                    bestMaxCols = maxColsPerRow;
                }

                if (Mathf.Approximately(scale, 1f))
                {
                    break; // 已经达到最大，无需继续
                }
            }

            if (bestScale <= 0f) return;

            // 计算缩放后的实际参数
            float scaledSlotSize = _slotSize * bestScale;
            float scaledSpacing = _spacing * bestScale;

            // 计算所有行的总高度
            float totalRowsHeight = bestRows * scaledSlotSize + (bestRows - 1) * scaledSpacing;
            // 垂直居中
            float startY = centerY + totalRowsHeight * 0.5f - scaledSlotSize * 0.5f;

            // 水平靠左：起始 x 为 leftBound
            float startX = leftBound;

            int placedCount = 0;
            for (int row = 0; row < bestRows; row++)
            {
                // 前 bestRows-1 行排满，最后一行放剩余的
                int colsInRow = (row < bestRows - 1) ? bestMaxCols : (slotCount - placedCount);

                for (int col = 0; col < colsInRow; col++)
                {
                    int i = placedCount + col;
                    float x = startX + col * (scaledSlotSize + scaledSpacing) + scaledSlotSize * 0.5f;
                    float y = startY - row * (scaledSlotSize + scaledSpacing);

                    GameSlotViewItem item = _slotItems[i];
                    if (item != null)
                    {
                        item.transform.position = new Vector3(x, y, 0);
                        item.transform.localScale = Vector3.one * bestScale;
                    }
                }
                placedCount += colsInRow;
            }
        }

        /// <summary>
        /// 计算在指定行列数下的缩放比例
        /// </summary>
        private float CalculateScale(int cols, int rows, float availableWidth, float availableHeight)
        {
            float widthScale = availableWidth / (cols * _slotSize + (cols - 1) * _spacing);
            float heightScale = availableHeight / (rows * _slotSize + (rows - 1) * _spacing);
            return Mathf.Min(widthScale, heightScale);
        }

        public void PlayBeginGameAnim()
        {

        }

        public void PlayEndGameAnim()
        {

        }
    }
}