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
                slotItem.transform.localScale = Vector3.one;
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
        /// 1. 水平靠左、整体垂直居中
        /// 2. 上方行“补满”：排到当前缩放下单行实际可容纳的最大数量，右边不留空
        /// 3. 缩放上限为 1，超出边界则按宽高约束缩小
        /// 4. 迭代收敛：按当前行数算缩放 → 用缩放后容量补满上方行 → 若补满后所需行数减少
        ///    （例如两排补满后发现一排即可放下）则用更少行数重算，直到行数不再下降
        /// 5. 间距与尺寸随缩放等比变化
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

            // scale=1 时单行最多容量，作为初始行数估计
            int maxColsFullScale = Mathf.FloorToInt((availableWidth + _spacing) / (_slotSize + _spacing));
            if (maxColsFullScale < 1) maxColsFullScale = 1;

            int rows = Mathf.CeilToInt((float)slotCount / maxColsFullScale);
            float scale = 0f;
            int colsCap = maxColsFullScale; // 缩放后单行实际容量（上方行补满到此数量）

            // 迭代：rows 每轮单调递减，最多 slotCount 轮内必收敛
            for (int iter = 0; iter < slotCount; iter++)
            {
                // 让 rows 行放下的最小每行数量
                int cols = Mathf.CeilToInt((float)slotCount / rows);
                scale = Mathf.Min(CalculateScale(cols, rows, availableWidth, availableHeight), 1f);
                if (scale <= 0f) break;

                // 缩放后单行实际容量（上方行补满的目标数量）
                float scaledSlotSize = _slotSize * scale;
                float scaledSpacing = _spacing * scale;
                colsCap = Mathf.FloorToInt((availableWidth + scaledSpacing) / (scaledSlotSize + scaledSpacing));
                if (colsCap < 1) colsCap = 1;

                // 补满后若所需行数不再减少，收敛
                int newRows = Mathf.CeilToInt((float)slotCount / colsCap);
                if (newRows >= rows) break;
                rows = newRows;
            }

            if (scale <= 0f || colsCap < 1) return;

            // 最终排布参数
            float finalSlotSize = _slotSize * scale;
            float finalSpacing = _spacing * scale;
            int finalRows = Mathf.CeilToInt((float)slotCount / colsCap);

            // 整体垂直居中
            float totalRowsHeight = finalRows * finalSlotSize + (finalRows - 1) * finalSpacing;
            float startY = centerY + totalRowsHeight * 0.5f - finalSlotSize * 0.5f;
            // 水平靠左
            float startX = leftBound;

            int placedCount = 0;
            for (int row = 0; row < finalRows; row++)
            {
                // 上方行补满 colsCap 个，最后一行靠左放剩余
                int colsInRow = (row < finalRows - 1) ? colsCap : (slotCount - placedCount);

                for (int col = 0; col < colsInRow; col++)
                {
                    int i = placedCount + col;
                    float x = startX + col * (finalSlotSize + finalSpacing) + finalSlotSize * 0.5f;
                    float y = startY - row * (finalSlotSize + finalSpacing);

                    GameSlotViewItem item = _slotItems[i];
                    if (item != null)
                    {
                        item.transform.position = new Vector3(x, y, 0);
                        item.transform.localScale = Vector3.one * scale;
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