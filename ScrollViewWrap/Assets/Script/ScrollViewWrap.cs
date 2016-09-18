using UI2D;
using UnityEngine;
using System.Collections.Generic;

namespace Util 
{
    public interface IScrollViewChild
    {
        void SetContentOn(Transform trans);
        void OnChildClick(GameObject go);
    }
    //用于数据绑定
    public class TransData
    {
        public GameObject obj; // 因为每次panel 移动时都会调用所以用一个object来存
        public int dataIndex;   // 数据在dataCacheList里的位置 除以最大显示个数之后也是childObjectCacheList缓存中的位置
        public int rowIndex;
        public int colIndex;
        public Vector3 originalHangingPoint; //用于记录初始化的位置

        public TransData() { }
        public TransData(GameObject childObj) 
        {
            this.obj = childObj;
            this.dataIndex = -1;
            this.rowIndex = -1;
            this.colIndex = -1;
            this.originalHangingPoint = Vector3.zero;
        }
    }

    //两种排列方式
    public class ScrollViewWrap : MonoBehaviour
    {
        // page 可显示的范围 即clip范围
        // cell 一个逻辑上的分块单元 用于trans定位
        // team 在移动时 一组cell（一行或一列） 同进同退 一起变化

        # region 数据
        private int rowsPerPage = 1; // 每页有多少行
        private int columnsPerPage = 1; // 每页有多少列
        private bool isPageMode = false; //页模式，一整页一整页的移动
        private bool isLoop = false; //是否循环显示
        private bool isAutoTrim = false; //是否自动微调到cell的位置
        //public bool isMaxCellNumLimited = false; // 是否限制最大摆放
        private int hiddenPageNum = 1; //默认最少是1,隐藏的页数 
        private int hiddenCellTeamNum = 1; // 如果是横向 就是每页有几列, 如果是纵向的 就是每页有几行，相当于预加载的数量，当isPageMode为true, 自动等于显示的行列数 


        private bool isCellMatchChildBound = true;
        //public bool isAlignment = true; //是否自动对齐到view中 
        //public bool isCenterOnChild = false; //是否居中显示
        //public bool keepWithInPanel = false;
        //public float gap_H = 0f; //横向页边距
        //public float gap_V = 0f; //纵向页边距

        private UIPanel mPanel;
        private UIScrollView mScroll;
        private Transform mParent;
        private Transform mChild;
        private Bounds childBounds; //用来确定与原点的偏移
        private Dictionary<int, List<TransData>> cellTeamMap = new Dictionary<int, List<TransData>>();

        private HashSet<TransData> pageStartCell = new HashSet<TransData>();
        private List<TransData> cellList = new List<TransData>();
        //private List<IScrollViewChild> dataCacheList = null;
        private IScrollViewChild[] dataCacheArray = null;

        private string childName = null;

        private bool isHorizontal = true; // 方向
        private bool isActiveSuperfluousChildObj = false; //是否显示多余的childObj
        private bool isContactEdge = false; //移动时是否达到了边界

        public int currentPageIndex { get; private set; } // 0代表第一页
        private const float springStrength = 13f;
        private Vector3 springOffset = Vector3.zero;

        private Vector2 cellSize = new Vector2(100f, 100f); //一个单元的大小
        private int logicallyCellTeamMaxNum = 1; // 理论上team的最大数 用于位移的检测 当isloop为false时所有index不能超过这个值
        private float logicallyCellTeamMaxSpan = 1f; // 理论上team的最大数的跨度 用于计算scrollbar
        private int cellTeamAmount = 1; // 一共生成的team数量 包含隐藏的team 如果是横向 就是列, 如果是纵向的 就是行 
        private int cellNumPerPage = 1; // 每页cell的数量 不包含隐藏的team里的cell
        private int cellTeamNumPerPage = 1; //每页的team数量 不包含隐藏的team
        private int cellNumPerTeam = 1; // 每个team的长度
        private float halfCellTeamAmountSpan = 0f;
        private int cellColumns = 1; // 需要生成的列 包含隐藏
        private int cellRows = 1; //需要生成的行 包含隐藏
        private int cellCount = 1; // 一共生成的cell数量 包含隐藏的team
        private Vector3 cellWarpSpan = Vector3.zero; // 在warpContent时 发生的位移；
        private float cellSpan = 1f; //一个cell的宽或者高

        private TransData closestCellToLeftTop = null;
        private TransData closestPageStartCellToLeftTop = null;
        private float offsetOfClosestCellToLeftTop = 0f;

        private UIProgressBar horScrollBar;
        private UIProgressBar verScrollBar;
        private Vector2 originalScrollOffset; //初始化时 scroll的offset 此量记录offset一共偏移多少 用于进度的定位
        private float viewSize = 0f;
        private float scroollBarSizeOfLoopMode = 0f;
        private Pagination pagination = null;
        private int alignOffset = 0;

        # endregion

        //确定整个结构正常
        public bool checkStructureAndInit()
        {
            if (null == mParent) mParent = transform;
            if (null == mParent) return false;
            UITable uiTable = mParent.GetComponent<UITable>();
            if (null != uiTable) GameObject.DestroyImmediate(uiTable);

            if (null == mScroll) mScroll = mParent.parent.GetComponent<UIScrollView>();
            if (null == mScroll) return false;
            mPanel = null == mScroll.panel ? mScroll.gameObject.GetComponent<UIPanel>() : mScroll.panel;
            if (null == mPanel) return false;

            originalScrollOffset = mPanel.clipOffset;
            viewSize = getViewSize();

            horScrollBar = mScroll.horizontalScrollBar;
            mScroll.horizontalScrollBar = null;
            if (null != horScrollBar) EventDelegate.Remove(horScrollBar.onChange, mScroll.OnScrollBar);

            verScrollBar = mScroll.verticalScrollBar;
            mScroll.verticalScrollBar = null;
            if (null != verScrollBar) EventDelegate.Remove(verScrollBar.onChange, mScroll.OnScrollBar);

            if (null == mChild) mChild = mParent.GetChild(0);
            if (null == mChild) return false;

            childName = mChild.gameObject.name;
            cellList.Add(new TransData(mChild.gameObject));
            childBounds = NGUIMath.CalculateRelativeWidgetBounds(mChild, true);
            isHorizontal = mScroll.movement == UIScrollView.Movement.Horizontal ? true : false;
            isAutoTrim = isPageMode ? true : isAutoTrim;

            mScroll.onDragFinished = null;
            if (isAutoTrim) mScroll.onDragFinished = () => { trimOnDragFinish(); };

            mPanel.onClipMove = (panel) => { wrapOnClipMove(); };
            return true;
        }

        private void initCellParameters()
        {
            if (isCellMatchChildBound)
            {
                //rowsPerPage = Mathf.CeilToInt(mPanel.height / childBounds.size.y);
                //columnsPerPage = Mathf.CeilToInt(mPanel.width / childBounds.size.x);

                float heightNum = mPanel.height / childBounds.size.y;
                rowsPerPage = heightNum > 1 ? (int)heightNum : 1;
                float widthNum = mPanel.width / childBounds.size.x;
                columnsPerPage = widthNum > 1 ? (int)widthNum : 1;
            }

            cellTeamNumPerPage = isHorizontal ? columnsPerPage : rowsPerPage;
            cellNumPerPage = columnsPerPage * rowsPerPage;

            if (isCellMatchChildBound)
            {
                cellSize = childBounds.size;
            }
            else
            {
                cellSize = new Vector2(mPanel.width / columnsPerPage, mPanel.height / rowsPerPage);
            }
            cellSpan = isHorizontal ? cellSize.x : cellSize.y;

            //要生成的单位的行数和列数(包含隐藏的)
            hiddenCellTeamNum = isPageMode ? (isHorizontal ? columnsPerPage * hiddenPageNum : rowsPerPage * hiddenPageNum) : hiddenCellTeamNum;
            cellColumns = isHorizontal ? columnsPerPage + hiddenCellTeamNum * 2 : columnsPerPage;
            cellRows = isHorizontal ? rowsPerPage : rowsPerPage + hiddenCellTeamNum * 2;
            //cellColumns = columnsPerPage;
            //cellRows = rowsPerPage;
            cellCount = cellColumns * cellRows;
        }

        private void adjustCellParametersWithDataCacheList(int alignTargetDataIndex = 0, int alignToTeamIndex = 0)
        {
            //保证trans的数量小于等于data的数量
            if (dataCacheArray != null && dataCacheArray.Length > 0)
            {
                float dataCacheListCount = (float)dataCacheArray.Length;
                //没有那么多可显示
                if (dataCacheArray.Length <= columnsPerPage)
                {
                    cellColumns = isPageMode ? columnsPerPage : dataCacheArray.Length;
                    cellRows = 1;
                }
                else
                {
                    int dataMaxRow = Mathf.CeilToInt(dataCacheListCount / columnsPerPage);
                    cellRows = isPageMode ? cellRows : dataMaxRow < cellRows ? dataMaxRow : cellRows;
                }

                cellCount = cellColumns * cellRows; //获得需要显示的cell数量(当扩到不是最大时 保证trans够用) 
                logicallyCellTeamMaxNum = isHorizontal ? Mathf.CeilToInt(dataCacheListCount / cellRows) : Mathf.CeilToInt(dataCacheListCount / cellColumns);
                logicallyCellTeamMaxNum = isPageMode ? Mathf.CeilToInt((float)logicallyCellTeamMaxNum / columnsPerPage) * columnsPerPage : logicallyCellTeamMaxNum;
                logicallyCellTeamMaxSpan = cellSpan * logicallyCellTeamMaxNum;
                scroollBarSizeOfLoopMode = viewSize / logicallyCellTeamMaxSpan;
            }

            //使childCache适应dataCache
            int newObjNum = cellCount - cellList.Count;
            if (newObjNum > 0) //说明transform不够 需要添加
            {
                for (int n = 0; n < newObjNum; n++)
                {
                    TransData transData = new TransData(NGUITools.AddChild(mParent.gameObject, mChild.gameObject));
                    transData.obj.transform.localScale = mChild.transform.localScale;
                    transData.obj.SetActive(isActiveSuperfluousChildObj);
                    cellList.Add(transData);
                }
            }
            mChild.gameObject.SetActive(isActiveSuperfluousChildObj);

            cellNumPerTeam = isHorizontal ? cellRows : cellColumns;
            cellTeamAmount = isHorizontal ? cellColumns : cellRows;

            halfCellTeamAmountSpan = cellSpan * cellTeamAmount * 0.5f;
            cellWarpSpan = isHorizontal ? new Vector3(halfCellTeamAmountSpan * 2f, 0f, 0f) : new Vector3(0f, halfCellTeamAmountSpan * 2f, 0f);

            int pageStartCellLogicallyTeamIndex = 0;
            if (dataCacheArray != null && dataCacheArray.Length > 0 && alignToTeamIndex < cellTeamNumPerPage)
            {
                int targetTeamIndex = getLogicallyTeamIndexByDataIndex(alignTargetDataIndex);
                int pageStartCellDataIndex = getPageStartCellDataIndex(targetTeamIndex, alignToTeamIndex);
                pageStartCellLogicallyTeamIndex = getLogicallyTeamIndexByDataIndex(pageStartCellDataIndex);
                alignOffset = pageStartCellLogicallyTeamIndex;
            }
        }

        private void setChildren() // alignTo 是对齐到一页中第几个（从0开始 从左到右 从上到下）teamIndex 小于一页的teamIndex长度
        {
            int cellRowsStartIndex = 0;
            int cellColsStartIndex = 0;
            if (alignOffset - hiddenCellTeamNum < 0) //说明超过了开始的上限
            {
                if (isLoop)
                {

                }
            }
            else 
            {
                if (isPageMode)
                {
                    cellRowsStartIndex = -alignOffset;
                }
                else 
                {
                    if (isHorizontal)
                    {
                        cellColsStartIndex = -alignOffset;
                    }
                    else 
                    {
                        cellRowsStartIndex = -alignOffset;
                    }
                }
            }
            

            Vector3 leftTopCellHangingPoint = getLeftTopCellHangingPoint();
            if (null != cellList && cellList.Count > 0)
            {
                cellTeamMap.Clear();
                pageStartCell.Clear();
                var it_cell = cellList.GetEnumerator();

                for (int i = cellRowsStartIndex; i < cellRows; i++)
                {
                    for (int j = cellColsStartIndex; j < cellColumns; j++)
                    {
                        //int _objIndex = getObjIndex(i, j);
                        //if (_objIndex < cellList.Count)
                        if(it_cell.MoveNext())
                        {
                            //TransData td = cellList[_objIndex];
                            TransData td = it_cell.Current;
                            float posX = leftTopCellHangingPoint.x + cellSize.x * j;
                            float poxY = leftTopCellHangingPoint.y - cellSize.y * i;
                            td.originalHangingPoint = new Vector3(posX, poxY, mChild.localPosition.z);
                            td.obj.transform.localPosition = td.originalHangingPoint;

                            int teamIndex = isHorizontal ? j : i;
                            int indexInTeam = isHorizontal ? i : j;

                            //td.rowIndex = i;
                            td.rowIndex = getCalculatedRowIndex(i, alignOffset);
                            td.colIndex = getCalculatedColIndex(j, alignOffset);
                            updateChild(td);

                            if (!cellTeamMap.ContainsKey(teamIndex)) cellTeamMap.Add(teamIndex, new List<TransData>());
                            if (!cellTeamMap[teamIndex].Contains(td)) cellTeamMap[teamIndex].Add(td);
                            if (teamIndex % cellTeamNumPerPage == 0) pageStartCell.Add(td);

                            if (i == 0 && j == 0)
                            {
                                closestCellToLeftTop = td;
                                closestPageStartCellToLeftTop = td;
                            }
                        }
                    }
                }
            }
            offsetOfClosestCellToLeftTop = 0f;
            currentPageIndex = alignOffset / cellTeamNumPerPage;
            updateProgressBar();
        }

        private void wrapOnClipMove()
        {
            Vector3[] corners = getPanelCornersRelativeToParent();
            Vector3 center = Vector3.Lerp(corners[0], corners[2], 0.5f);
            Vector3 leftTopCellHangingPoint = getLeftTopCellHangingPoint(corners);
            Vector3 pickingPoint = leftTopCellHangingPoint - mScroll.currentMomentum * (mScroll.momentumAmount * 0.1f);
            float min_Cell = float.MaxValue;
            float min_PageStartCell = float.MaxValue;

            if (isHorizontal)
            {
                var it_Team = cellTeamMap.GetEnumerator();
                while (it_Team.MoveNext())
                {
                    List<TransData> team = it_Team.Current.Value;
                    TransData firstTd = team[0];
                    float sqrDist = Vector3.SqrMagnitude(firstTd.obj.transform.localPosition - pickingPoint);
                    if (sqrDist < min_Cell)
                    {
                        min_Cell = sqrDist;
                        closestCellToLeftTop = firstTd;
                        offsetOfClosestCellToLeftTop = firstTd.obj.transform.localPosition.x - leftTopCellHangingPoint.x;
                    };

                    if (pageStartCell.Contains(firstTd) && sqrDist < min_PageStartCell)
                    {
                        min_PageStartCell = sqrDist;
                        closestPageStartCellToLeftTop = firstTd;
                    }

                    float distance = firstTd.obj.transform.localPosition.x - center.x;
                    if (distance < -halfCellTeamAmountSpan) //向左
                    {
                        if (firstTd.colIndex + cellTeamAmount + alignOffset < logicallyCellTeamMaxNum)
                        {
                            setTeamPosWhenWrap(team, cellWarpSpan);
                            setTeamDataWhenWrap(team, false, cellTeamAmount);
                            isContactEdge = false;
                        }
                        else
                        {
                            if (isLoop)
                            {
                                setTeamPosWhenWrap(team, cellWarpSpan);
                                setTeamDataWhenWrap(team, false, cellTeamAmount - logicallyCellTeamMaxNum);
                            }
                            isContactEdge = true;
                            
                        }
                    }
                    else if (distance > halfCellTeamAmountSpan) //向右
                    {
                        if (firstTd.colIndex + 1 + alignOffset - cellTeamAmount > 0)
                        {
                            setTeamPosWhenWrap(team, -cellWarpSpan);
                            setTeamDataWhenWrap(team, false, -cellTeamAmount);
                            isContactEdge = false;
                        }
                        else
                        {
                            if (isLoop)
                            {
                                setTeamPosWhenWrap(team, -cellWarpSpan);
                                setTeamDataWhenWrap(team, false, logicallyCellTeamMaxNum - cellTeamAmount);
                                isContactEdge = true;
                            }
                            
                        }
                    }
                }
            }
            else
            {
                var it_Team = cellTeamMap.GetEnumerator();
                while (it_Team.MoveNext())
                {
                    List<TransData> team = it_Team.Current.Value;
                    TransData firstTd = team[0];

                    float sqrDist = Vector3.SqrMagnitude(firstTd.obj.transform.localPosition - pickingPoint);
                    if (sqrDist < min_Cell)
                    {
                        min_Cell = sqrDist;
                        closestCellToLeftTop = firstTd;
                        offsetOfClosestCellToLeftTop = firstTd.obj.transform.localPosition.y - leftTopCellHangingPoint.y;
                    };

                    if (pageStartCell.Contains(firstTd) && sqrDist < min_PageStartCell)
                    {
                        min_PageStartCell = sqrDist;
                        closestPageStartCellToLeftTop = firstTd;
                    }

                    float distance = firstTd.obj.transform.localPosition.y - center.y;
                    if (distance < -halfCellTeamAmountSpan) //向下
                    {
                        if (firstTd.rowIndex + 1 + alignOffset - cellTeamAmount > 0) //这种计算方式会有问题 >= 与 > 有区别 在初始位置设定的情况下 与“向右” 情况不一致
                        {
                            setTeamPosWhenWrap(team, cellWarpSpan);
                            setTeamDataWhenWrap(team, true, -cellTeamAmount);
                            isContactEdge = false;
                        }
                        else
                        {
                            if (isLoop)
                            {
                                setTeamPosWhenWrap(team, cellWarpSpan);
                                setTeamDataWhenWrap(team, true, logicallyCellTeamMaxNum - cellTeamAmount);
                                isContactEdge = true;
                            }
                            
                        }
                    }
                    else if (distance > halfCellTeamAmountSpan) //向上
                    {
                        if (firstTd.rowIndex + cellTeamAmount + alignOffset < logicallyCellTeamMaxNum)
                        {
                            setTeamPosWhenWrap(team, -cellWarpSpan);
                            setTeamDataWhenWrap(team, true, cellTeamAmount);
                            isContactEdge = false;
                        }
                        else
                        {
                            if (isLoop)
                            {
                                setTeamPosWhenWrap(team, -cellWarpSpan);
                                setTeamDataWhenWrap(team, true, cellTeamAmount - logicallyCellTeamMaxNum);
                            }
                            isContactEdge = true;
                        }
                    }
                }
            }

            if (isPageMode)
            {
                if (null != closestPageStartCellToLeftTop)
                {
                    springOffset = closestPageStartCellToLeftTop.obj.transform.localPosition - leftTopCellHangingPoint;
                    int teamIndex = getLogicallyTeamIndexByDataIndex(closestPageStartCellToLeftTop.dataIndex);
                    currentPageIndex = teamIndex / cellTeamNumPerPage;
                }
            }
            else
            {
                if (null != closestCellToLeftTop)
                {
                    springOffset = closestCellToLeftTop.obj.transform.localPosition - leftTopCellHangingPoint;
                    int teamIndex = getLogicallyTeamIndexByDataIndex(closestCellToLeftTop.dataIndex);
                    currentPageIndex = teamIndex / cellTeamNumPerPage;
                }
            }
            updateProgressBar();
            //Debug.Log("当前的的页数是 =============》 " + currentPageIndex);
            if (null != pagination)
                pagination.updateView(currentPageIndex + 1);
        }

        private void setTeamPosWhenWrap(List<TransData> team, Vector3 posOffset)
        {
            var it_Cell = team.GetEnumerator();
            while (it_Cell.MoveNext())
            {
                it_Cell.Current.obj.transform.localPosition += posOffset;
            }
        }

        private void setTeamDataWhenWrap(List<TransData> team, bool isRow, int indexOffset)
        {
            var it_Cell = team.GetEnumerator();
            if (isRow)
            {
                while (it_Cell.MoveNext())
                {
                    it_Cell.Current.rowIndex += indexOffset;
                    updateChild(it_Cell.Current);
                }
            }
            else
            {
                while (it_Cell.MoveNext())
                {
                    it_Cell.Current.colIndex += indexOffset;
                    updateChild(it_Cell.Current);
                }
            }
        }

        private void updateChild(TransData td)
        {
            int _objIndex = cellColumns * td.rowIndex + td.colIndex;
            int _dataIndex = isHorizontal ? (isPageMode ? getDataIndexInPageMode(td.rowIndex, td.colIndex) : _objIndex) : _objIndex;





            if (null != dataCacheArray && _dataIndex >= 0 && _dataIndex < dataCacheArray.Length)
            {
                td.dataIndex = _dataIndex;
                dataCacheArray[_dataIndex].SetContentOn(td.obj.transform);
                UIEventListener.Get(td.obj).onClick = dataCacheArray[_dataIndex].OnChildClick;
                td.obj.SetActive(true);
                setChildName(td, _objIndex);
            }
            else
            {
                td.obj.SetActive(isActiveSuperfluousChildObj);
            }
        }

        private void setChildName(TransData td, int objIndex)
        {
            //string objInfo = string.Format("[R:{0},C:{1},D:{2}]", td.rowIndex, td.colIndex, td.dataIndex);
            string objName = string.Format("{0}_{1}", childName, objIndex);
            td.obj.name = objName;
        }

        //----------------------------------------------------------------------------------------------
        #region ProgressBar

        private void updateProgressBar()
        {
            if (mPanel == null) return;
            if (horScrollBar != null || verScrollBar != null)
            {
                Bounds b = mScroll.bounds;
                Vector2 bmin = b.min;
                Vector2 bmax = b.max;

                if (horScrollBar != null)
                {
                    float contentMin = bmax.x > bmin.x ?
                        mPanel.clipOffset.x - originalScrollOffset.x + cellSpan * alignOffset :
                        cellSpan * alignOffset;

                    contentMin = contentMin % logicallyCellTeamMaxSpan;
                    float contentMax = logicallyCellTeamMaxSpan - contentMin - viewSize;
                    updateScrollbars(horScrollBar, contentMin, contentMax, false);
                }

                if (verScrollBar != null)
                {
                    float contentMin = bmax.y > bmin.y ?
                        -(mPanel.clipOffset.y - originalScrollOffset.y - cellSpan * alignOffset) :
                        -(cellSpan * alignOffset);

                    contentMin = contentMin % logicallyCellTeamMaxSpan;
                    float contentMax = logicallyCellTeamMaxSpan - contentMin - viewSize;
                    updateScrollbars(verScrollBar, contentMin, contentMax, false);
                }
            }
        }

        private void updateScrollbars(UIProgressBar slider, float contentMin, float contentMax, bool inverted)
        {
            if (slider == null) return;
            else
            {
                float contentPadding;

                if (viewSize < logicallyCellTeamMaxSpan)
                {
                    contentMin = Mathf.Clamp01(contentMin / logicallyCellTeamMaxSpan);
                    contentMax = Mathf.Clamp01(contentMax / logicallyCellTeamMaxSpan);

                    contentPadding = contentMin + contentMax;
                    slider.value = inverted ? ((contentPadding > 0.001f) ? 1f - contentMin / contentPadding : 0f) :
                        ((contentPadding > 0.001f) ? contentMin / contentPadding : 1f);
                }
                else
                {
                    contentMin = Mathf.Clamp01(-contentMin / logicallyCellTeamMaxSpan);
                    contentMax = Mathf.Clamp01(-contentMax / logicallyCellTeamMaxSpan);

                    contentPadding = contentMin + contentMax;
                    slider.value = inverted ? ((contentPadding > 0.001f) ? 1f - contentMin / contentPadding : 0f) :
                        ((contentPadding > 0.001f) ? contentMin / contentPadding : 1f);

                    if (logicallyCellTeamMaxSpan > 0)
                    {
                        contentMin = Mathf.Clamp01(contentMin / logicallyCellTeamMaxSpan);
                        contentMax = Mathf.Clamp01(contentMax / logicallyCellTeamMaxSpan);
                        contentPadding = contentMin + contentMax;
                    }
                }

                UIScrollBar sb = slider as UIScrollBar;
                if (sb != null) sb.barSize = isLoop ? scroollBarSizeOfLoopMode : 1f - contentPadding;
            }
        }

        #endregion

        //----------------------------------------------------------------------------------------------
        #region trim
        private void trimOnDragFinish()
        {
            if (isLoop || isPageMode)
            {
                trim();
            }
            else if (!isContactEdge)
            {
                trim();
            }
            isContactEdge = false;
        }

        private void trim()
        {
            mScroll.currentMomentum = Vector3.zero;
            if (!mScroll.canMoveHorizontally) springOffset.x = 0f;
            if (!mScroll.canMoveVertically) springOffset.y = 0f;
            springOffset.z = 0f;

            SpringPanel.Begin(mPanel.cachedGameObject,
                    mPanel.cachedTransform.localPosition - springOffset, springStrength).onFinished = null;
        }

        #endregion
        // --------------------------------------------------------------------------------------
        // 辅助方法

        private float getViewSize()
        {
            Vector4 clip = mPanel.finalClipRegion;
            int intViewSize = isHorizontal ? Mathf.RoundToInt(clip.z) : Mathf.RoundToInt(clip.w);
            float halfViewSize = intViewSize * 0.5f;

            if (mPanel.clipping == UIDrawCall.Clipping.SoftClip)
            {
                if (isHorizontal)
                {
                    halfViewSize -= mPanel.clipSoftness.x;
                }
                else
                {
                    halfViewSize -= mPanel.clipSoftness.y;
                }
            }
            return halfViewSize * 2f;
        }

        private Vector3[] getPanelCornersRelativeToParent()
        {
            Vector3[] corners = mPanel.worldCorners;
            for (int i = 0; i < 4; ++i)
            {
                Vector3 v = corners[i];
                v = mParent.InverseTransformPoint(v);
                corners[i] = v;
            }
            return corners;
        }

        private Vector3 getLeftTopCellHangingPoint(Vector3[] panelCornersWorldPos = null)
        {
            Vector3[] corners = null == panelCornersWorldPos ? getPanelCornersRelativeToParent() : panelCornersWorldPos;
            return new Vector3(corners[1].x + cellSize.x * 0.5f, corners[1].y - cellSize.y * 0.5f, corners[1].z) - childBounds.center;
        }

        //private int getObjIndex(int rowIndex, int colIndex)
        //{
        //    return cellColumns * rowIndex + colIndex;
        //}

        private int getDataIndexInPageMode(int rowIndex, int colIndex)
        {
            int perPage = ((colIndex / columnsPerPage) * cellNumPerPage);
            int currPage = columnsPerPage * rowIndex + colIndex % columnsPerPage;
            return currPage + perPage;
        }

        private int getLogicallyTeamIndexByDataIndex(int dataIndex)
        {
            if (isPageMode)
            {
                return ((dataIndex / cellNumPerPage) * cellTeamNumPerPage) + ((dataIndex % cellNumPerPage) % cellTeamNumPerPage);
            }
            else
            {
                return isHorizontal ? dataIndex % logicallyCellTeamMaxNum : Mathf.CeilToInt((float)dataIndex / cellNumPerTeam);
            }
        }

        private int getPageStartCellDataIndex(int logicallyTeamIndex, int alignToTeamIndex)
        {
            int TeamIndexInPage = logicallyTeamIndex % cellTeamNumPerPage;
            int pageStartCellLogicallyTeamIndex = logicallyTeamIndex - TeamIndexInPage;
            if (isPageMode)
            {
                int rowIndex = isHorizontal ? 0 : pageStartCellLogicallyTeamIndex;
                int colIndex = isHorizontal ? pageStartCellLogicallyTeamIndex : 0;
                return getDataIndexInPageMode(rowIndex, colIndex);
            }
            else
            {
                if (TeamIndexInPage > alignToTeamIndex) //相当于向左移动
                {
                    pageStartCellLogicallyTeamIndex = (logicallyTeamIndex - alignToTeamIndex) % logicallyCellTeamMaxNum;
                    if (logicallyCellTeamMaxNum - cellTeamNumPerPage < pageStartCellLogicallyTeamIndex) //到了队尾
                    {
                        pageStartCellLogicallyTeamIndex = isLoop ? pageStartCellLogicallyTeamIndex : logicallyCellTeamMaxNum - cellTeamNumPerPage;
                    }
                }
                else if (TeamIndexInPage < alignToTeamIndex) // 相当于向右移动
                {
                    pageStartCellLogicallyTeamIndex = (pageStartCellLogicallyTeamIndex + alignToTeamIndex) % logicallyCellTeamMaxNum;
                    if (logicallyTeamIndex < cellTeamNumPerPage)
                    {
                        pageStartCellLogicallyTeamIndex = isLoop ? logicallyCellTeamMaxNum - alignToTeamIndex : 0;
                    }
                }

                int rowIndex = isHorizontal ? 0 : pageStartCellLogicallyTeamIndex;
                int colIndex = isHorizontal ? pageStartCellLogicallyTeamIndex : 0;
                return cellColumns * rowIndex + colIndex;
            }
        }

        private int getCalculatedColIndex(int colIndex, int alignOffset)
        {
            //int calculatedColIndex = colIndex + pageStartCellLogicallyTeamIndex;
            int calculatedColIndex = isHorizontal ? alignOffset + colIndex : colIndex;
            if (calculatedColIndex >= logicallyCellTeamMaxNum)
            {
                if (isLoop)
                {
                    calculatedColIndex = calculatedColIndex % logicallyCellTeamMaxNum;
                }
            }
            return calculatedColIndex;
        }

        private int getCalculatedRowIndex(int rowIndex, int alignOffset) 
        {
            int calculatedRowIndex = isHorizontal ? rowIndex : alignOffset + rowIndex;

            if (calculatedRowIndex >= logicallyCellTeamMaxNum)
            {
                if (isLoop)
                {
                    calculatedRowIndex = calculatedRowIndex % cellRows;
                }
            }
            return calculatedRowIndex;
        }


        // =====================================================================================
        // 外部常用方法

        //public Transform getChildTransform(int _dataIndex)
        //{
        //    var it_CellTeam = cellTeamMap.GetEnumerator();
        //    while (it_CellTeam.MoveNext())
        //    {
        //        var it_Cell = it_CellTeam.Current.Value.GetEnumerator();
        //        while (it_Cell.MoveNext())
        //        {
        //            if (it_Cell.Current.dataIndex == _dataIndex)
        //            {
        //                return it_Cell.Current.obj.transform;
        //            }
        //        }
        //    }
        //    return null;
        //}

        public void display(params IScrollViewChild[] dataArray)
        {
            display(0, 0, dataArray);
        }

        public void display(int alignTargetIndex = 0, int alignTo = 0, params IScrollViewChild[] dataArray) // alignTo 是目标所在的Team的位置 因此小于每页的team
        {
            //  alignPos
            dataCacheArray = dataArray;
            initCellParameters();
            adjustCellParametersWithDataCacheList(alignTargetIndex, alignTo);
            setChildren();

            if (null != dataCacheArray)
            {
                float cellCount = (float)dataCacheArray.Length;
                float cellNumInOnePage = (float)cellNumPerPage;

                int pageNum = Mathf.CeilToInt(cellCount / cellNumInOnePage);
                pageNum = pageNum <= 1 ? 1 : pageNum;
                mScroll.enabled = pageNum > 1;
                if (null != pagination)
                    pagination.setView(pageNum, currentPageIndex + 1);
            }
        }

        public void RefreshByReset(bool isReset, params IScrollViewChild[] dataArray)
        {
            int alignTargetIndex = isPageMode ?
                (null != closestPageStartCellToLeftTop ? closestPageStartCellToLeftTop.dataIndex : 0) :
                (null != closestCellToLeftTop ? closestCellToLeftTop.dataIndex : 0);

            alignTargetIndex = null != dataArray ?
                (dataArray.Length > alignTargetIndex ? alignTargetIndex : dataArray.Length - 1) :
                alignTargetIndex;

            alignTargetIndex = isReset ? 0 : alignTargetIndex;
            display(alignTargetIndex, 0, dataArray);
        }

        public void setPagination(Transform markContainer, Transform markItem)
        {
            pagination = new Pagination(markContainer, markItem);
        }

        public static ScrollViewWrap configure(Transform trans, int row = 0, int col = 0, bool isPageMode = false, bool isLoop = false, bool isAutoTrim = false)
        {
            if (null == trans) return null;
            ScrollViewWrap svw = trans.gameObject.GetComponent<ScrollViewWrap>();
            if (null == svw)
            {
                svw = trans.gameObject.AddComponent<ScrollViewWrap>();
                svw.rowsPerPage = row;
                svw.columnsPerPage = col;
                svw.isPageMode = isPageMode;
                svw.isLoop = isLoop;
                svw.isAutoTrim = isAutoTrim;
                svw.isCellMatchChildBound = !isAutoTrim && (row == 0 || col == 0);
                //svw.init();
                if (!svw.checkStructureAndInit()) Debug.Log("Initialise failed!!!!");
            }
            return svw;
        }

    }
}