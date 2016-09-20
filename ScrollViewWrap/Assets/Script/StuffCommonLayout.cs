using UI2D;
using UnityEngine;
using System.Collections.Generic;

namespace Util 
{
    public interface IStuff
    {
        void SetContentOn(Transform trans);
    }

    //两种排列方式
    public class StuffCommonLayout : MonoBehaviour
    {
        //用于数据绑定
        public class ChildInfo
        {
            public GameObject obj; // 因为每次panel 移动时都会调用所以用一个object来存
            public int dataIndex;   // 数据在dataCacheList里的位置 除以最大显示个数之后也是childObjectCacheList缓存中的位置
            public int rowIndex;
            public int colIndex;
            //public Vector3 originalHangingPoint; //用于记录初始化的位置

            public ChildInfo() { }
            public ChildInfo(GameObject childObj)
            {
                this.obj = childObj;
                this.dataIndex = -1;
                this.rowIndex = -1;
                this.colIndex = -1;
                //this.originalHangingPoint = Vector3.zero;
            }
        }

        // page 可显示的范围 即clip范围
        // child 一个逻辑上的分块单元 用于trans定位
        // team 在移动时 一组child（一行或一列） 同进同退 一起变化

        # region 数据
        private int rowsPerPage = 1; // 每页有多少行
        private int columnsPerPage = 1; // 每页有多少列
        private bool isPageMode = false; //页模式，一整页一整页的移动
        private bool isLoop = false; //是否循环显示
        private bool isAutoTrim = false; //是否自动微调到cell的位置
        private int hiddenPageNum = 1; //默认最少是1,隐藏的页数 
        private int hiddenChildTeamNum = 1; // 如果是横向 就是每页有几列, 如果是纵向的 就是每页有几行，相当于预加载的数量，当isPageMode为true, 自动等于显示的行列数 
        private bool hasScrollView = false;
        //private bool isCellMatchChildBound = true;

        private UIPanel mPanel;
        private UIScrollView mScroll;
        private Transform mParent;
        private Transform mChild;
        private Bounds childBounds; //用来确定与原点的偏移

        private Dictionary<int, List<ChildInfo>> childTeamMap = new Dictionary<int, List<ChildInfo>>();
        private HashSet<ChildInfo> pageStartChild = new HashSet<ChildInfo>();
        private List<ChildInfo> childInfoList = new List<ChildInfo>();
        private IStuff[] dataCacheArray = null;
        //private IStuff[] stuffCacheArray = null;

        private string childName = null;

        public bool isHorizontal = true; // 方向
        private bool isActiveSuperfluousChildObj = false; //是否显示多余的childObj
        private bool isContactEdge = false; //移动时是否达到了边界

        public int currentPageIndex { get; private set; } // 0代表第一页
        private const float springStrength = 13f;
        private Vector3 springOffset = Vector3.zero;

        private Vector2 childSize = new Vector2(100f, 100f); //一个单元的大小
        private int logicallyChildTeamMaxNum = 1; // 理论上team的最大数 用于位移的检测 当isloop为false时所有index不能超过这个值
        private float logicallyCellTeamMaxSpan = 1f; // 理论上team的最大数的跨度 用于计算scrollbar
        private int childTeamAmount = 1; // 一共生成的team数量 包含隐藏的team 如果是横向 就是列, 如果是纵向的 就是行 
        private int childNumPerPage = 1; // 每页cell的数量 不包含隐藏的team里的cell
        private int childTeamNumPerPage = 1; //每页的team数量 不包含隐藏的team
        private int childNumPerTeam = 1; // 每个team的长度
        private float halfCellTeamAmountSpan = 0f;
        private int childColumns = 1; // 需要生成的列 包含隐藏
        private int childRows = 1; //需要生成的行 包含隐藏
        private int childCount = 1; // 一共生成的cell数量 包含隐藏的team
        private Vector3 childWarpSpan = Vector3.zero; // 在warpContent时 发生的位移；
        private float childSpan = 1f; //一个cell的宽或者高

        private ChildInfo closestCellToLeftTop = null;
        private ChildInfo closestPageStartCellToLeftTop = null;
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
            //check parent
            if (null == mParent) mParent = transform;
            if (null == mParent) return false;
            UITable uiTable = mParent.GetComponent<UITable>();
            if (null != uiTable) GameObject.DestroyImmediate(uiTable);
            UIGrid uiGrid = mParent.GetComponent<UIGrid>();
            if (null != uiGrid) GameObject.DestroyImmediate(uiTable);

            //check child
            if (null == mChild) mChild = mParent.GetChild(0);
            if (null == mChild) return false;
            childName = mChild.gameObject.name;
            

            //check scrollview
            if (null == mScroll) mScroll = mParent.parent.GetComponent<UIScrollView>();
            hasScrollView = null != mScroll;
            if (hasScrollView) 
            {
                mPanel = null == mScroll.panel ? mScroll.gameObject.GetComponent<UIPanel>() : mScroll.panel;
                if (null == mPanel) return false;
                mPanel.onClipMove = (panel) => { wrapOnClipMove(); };

                originalScrollOffset = mPanel.clipOffset;
                viewSize = getViewSize();

                horScrollBar = mScroll.horizontalScrollBar;
                mScroll.horizontalScrollBar = null;
                if (null != horScrollBar) EventDelegate.Remove(horScrollBar.onChange, mScroll.OnScrollBar);

                verScrollBar = mScroll.verticalScrollBar;
                mScroll.verticalScrollBar = null;
                if (null != verScrollBar) EventDelegate.Remove(verScrollBar.onChange, mScroll.OnScrollBar);

                mScroll.onDragFinished = null;
                if (isAutoTrim) mScroll.onDragFinished = () => { trimOnDragFinish(); };
            }

            childInfoList.Add(new ChildInfo(mChild.gameObject));
            childBounds = NGUIMath.CalculateRelativeWidgetBounds(mChild, true);
            isHorizontal = null != mScroll ? (mScroll.movement == UIScrollView.Movement.Horizontal ? true : false) : true;
            isAutoTrim = isPageMode ? true : isAutoTrim;

            return true;
        }

        private void calculateParameters(int alignTargetDataIndex = 0, int alignToTeamIndex = 0)
        {
            # region 根据行列设置进行初步计算

            if (!isAutoTrim && (rowsPerPage == 0 || columnsPerPage == 0)) //是不是自动根据child的size匹配生成
            {
                float heightNum = mPanel.height / childBounds.size.y;
                rowsPerPage = heightNum > 1 ? (int)heightNum : 1;
                float widthNum = mPanel.width / childBounds.size.x;
                columnsPerPage = widthNum > 1 ? (int)widthNum : 1;
                childSize = childBounds.size;
            }
            else
            {
                childSize = new Vector2(mPanel.width / columnsPerPage, mPanel.height / rowsPerPage);
            }

            childTeamNumPerPage = isHorizontal ? columnsPerPage : rowsPerPage;
            childNumPerPage = columnsPerPage * rowsPerPage;
            childSpan = isHorizontal ? childSize.x : childSize.y;

            //要生成的单位的行数和列数(包含隐藏的)
            hiddenChildTeamNum = isPageMode ? (isHorizontal ? columnsPerPage * hiddenPageNum : rowsPerPage * hiddenPageNum) : hiddenChildTeamNum;
            childColumns = isHorizontal ? columnsPerPage + hiddenChildTeamNum * 2 : columnsPerPage;
            childRows = isHorizontal ? rowsPerPage : rowsPerPage + hiddenChildTeamNum * 2;
            childCount = childColumns * childRows;

            #endregion
            /*--------------------------------------------------------------------------------------------------------------------------------*/
            #region 根据dataCacheArray进一步计算

            //保证trans的数量小于等于data的数量
            if (dataCacheArray != null && dataCacheArray.Length > 0)
            {
                float dataCacheListCount = (float)dataCacheArray.Length;
                //没有那么多可显示
                if (dataCacheArray.Length <= columnsPerPage)
                {
                    childColumns = isPageMode ? columnsPerPage : dataCacheArray.Length;
                    childRows = 1;
                }
                else
                {
                    int dataMaxRow = Mathf.CeilToInt(dataCacheListCount / columnsPerPage);
                    childRows = isPageMode ? childRows : dataMaxRow < childRows ? dataMaxRow : childRows;
                }

                childCount = childColumns * childRows; //获得需要显示的cell数量(当扩到不是最大时 保证trans够用) 
                logicallyChildTeamMaxNum = isHorizontal ? Mathf.CeilToInt(dataCacheListCount / childRows) : Mathf.CeilToInt(dataCacheListCount / childColumns);
                logicallyChildTeamMaxNum = isPageMode ? Mathf.CeilToInt((float)logicallyChildTeamMaxNum / columnsPerPage) * columnsPerPage : logicallyChildTeamMaxNum;
                logicallyCellTeamMaxSpan = childSpan * logicallyChildTeamMaxNum;
                scroollBarSizeOfLoopMode = viewSize / logicallyCellTeamMaxSpan;
            }

            //使childCache适应dataCache
            int newObjNum = childCount - childInfoList.Count;
            if (newObjNum > 0) //说明transform不够 需要添加
            {
                for (int n = 0; n < newObjNum; n++)
                {
                    ChildInfo info = new ChildInfo(NGUITools.AddChild(mParent.gameObject, mChild.gameObject));
                    info.obj.transform.localScale = mChild.transform.localScale;
                    info.obj.SetActive(isActiveSuperfluousChildObj);
                    childInfoList.Add(info);
                }
            }
            mChild.gameObject.SetActive(isActiveSuperfluousChildObj);

            childNumPerTeam = isHorizontal ? childRows : childColumns;
            childTeamAmount = isHorizontal ? childColumns : childRows;

            halfCellTeamAmountSpan = childSpan * childTeamAmount * 0.5f;
            childWarpSpan = isHorizontal ? new Vector3(halfCellTeamAmountSpan * 2f, 0f, 0f) : new Vector3(0f, halfCellTeamAmountSpan * 2f, 0f);

            int pageStartChildLogicallyTeamIndex = 0;
            if (dataCacheArray != null && dataCacheArray.Length > 0 && alignToTeamIndex < childTeamNumPerPage)
            {
                int targetTeamIndex = getLogicallyTeamIndexByDataIndex(alignTargetDataIndex);
                int pageStartChildDataIndex = getPageStartCellDataIndex(targetTeamIndex, alignToTeamIndex);
                pageStartChildLogicallyTeamIndex = getLogicallyTeamIndexByDataIndex(pageStartChildDataIndex);
                alignOffset = pageStartChildLogicallyTeamIndex;
            }

            #endregion
        }

        private void setChildren() // alignTo 是对齐到一页中第几个（从0开始 从左到右 从上到下）teamIndex 小于一页的teamIndex长度
        {
            int childRowsStartIndex = 0;
            int cellColsStartIndex = 0;
            if (alignOffset - hiddenChildTeamNum < 0) //说明超过了开始的上限
            {
                if (isLoop)
                {

                }
            }
            else 
            {
                if (isPageMode)
                {
                    //childRowsStartIndex = -alignOffset;
                }
                else 
                {
                    if (isHorizontal)
                    {
                        cellColsStartIndex = -(alignOffset <= hiddenChildTeamNum ? alignOffset : hiddenChildTeamNum);
                    }
                    else 
                    {
                        childRowsStartIndex = -(alignOffset <= hiddenChildTeamNum ? alignOffset : hiddenChildTeamNum);
                    }
                }
            }
            

            Vector3 leftTopCellHangingPoint = getLeftTopCellHangingPoint();
            if (null != childInfoList && childInfoList.Count > 0)
            {
                childTeamMap.Clear();
                pageStartChild.Clear();
                var it_cell = childInfoList.GetEnumerator();

                for (int i = childRowsStartIndex; i < childRows; i++)
                {
                    for (int j = cellColsStartIndex; j < childColumns; j++)
                    {
                        //int _objIndex = getObjIndex(i, j);
                        //if (_objIndex < cellList.Count)
                        if(it_cell.MoveNext())
                        {
                            //TransData td = cellList[_objIndex];
                            //ChildInfo td = it_cell.Current;
                            float posX = leftTopCellHangingPoint.x + childSize.x * j;
                            float poxY = leftTopCellHangingPoint.y - childSize.y * i;
                            //it_cell.Current.originalHangingPoint = new Vector3(posX, poxY, mChild.localPosition.z);
                            it_cell.Current.obj.transform.localPosition = new Vector3(posX, poxY, mChild.localPosition.z);

                            int teamIndex = isHorizontal ? j : i;
                            int indexInTeam = isHorizontal ? i : j;

                            //td.rowIndex = i;
                            it_cell.Current.rowIndex = getCalculatedRowIndex(i, alignOffset);
                            it_cell.Current.colIndex = getCalculatedColIndex(j, alignOffset);
                            updateChild(it_cell.Current);

                            if (!childTeamMap.ContainsKey(teamIndex)) childTeamMap.Add(teamIndex, new List<ChildInfo>());
                            if (!childTeamMap[teamIndex].Contains(it_cell.Current)) childTeamMap[teamIndex].Add(it_cell.Current);
                            if (teamIndex % childTeamNumPerPage == 0) pageStartChild.Add(it_cell.Current);

                            if (i == 0 && j == 0)
                            {
                                closestCellToLeftTop = it_cell.Current;
                                closestPageStartCellToLeftTop = it_cell.Current;
                            }
                        }
                    }
                }

                //把剩下的obj隐藏
                while (it_cell.MoveNext()) 
                {
                    it_cell.Current.obj.SetActive(isActiveSuperfluousChildObj);
                }
            }
            offsetOfClosestCellToLeftTop = 0f;
            currentPageIndex = alignOffset / childTeamNumPerPage;
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
                var it_Team = childTeamMap.GetEnumerator();
                while (it_Team.MoveNext())
                {
                    List<ChildInfo> team = it_Team.Current.Value;
                    ChildInfo firstTd = team[0];
                    float sqrDist = Vector3.SqrMagnitude(firstTd.obj.transform.localPosition - pickingPoint);
                    if (sqrDist < min_Cell)
                    {
                        min_Cell = sqrDist;
                        closestCellToLeftTop = firstTd;
                        offsetOfClosestCellToLeftTop = firstTd.obj.transform.localPosition.x - leftTopCellHangingPoint.x;
                    };

                    if (pageStartChild.Contains(firstTd) && sqrDist < min_PageStartCell)
                    {
                        min_PageStartCell = sqrDist;
                        closestPageStartCellToLeftTop = firstTd;
                    }

                    float distance = firstTd.obj.transform.localPosition.x - center.x;
                    if (distance < -halfCellTeamAmountSpan) //向左
                    {
                        if (firstTd.colIndex + childTeamAmount < logicallyChildTeamMaxNum)
                        {
                            setTeamPosWhenWrap(team, childWarpSpan);
                            setTeamDataWhenWrap(team, false, childTeamAmount);
                            isContactEdge = false;
                        }
                        else
                        {
                            if (isLoop)
                            {
                                setTeamPosWhenWrap(team, childWarpSpan);
                                setTeamDataWhenWrap(team, false, childTeamAmount - logicallyChildTeamMaxNum);
                            }
                            isContactEdge = true;
                            
                        }
                    }
                    else if (distance > halfCellTeamAmountSpan) //向右
                    {
                        if (firstTd.colIndex + 1 - childTeamAmount > 0)
                        {
                            setTeamPosWhenWrap(team, -childWarpSpan);
                            setTeamDataWhenWrap(team, false, -childTeamAmount);
                            isContactEdge = false;
                        }
                        else
                        {
                            if (isLoop)
                            {
                                setTeamPosWhenWrap(team, -childWarpSpan);
                                setTeamDataWhenWrap(team, false, logicallyChildTeamMaxNum - childTeamAmount);
                                isContactEdge = true;
                            }
                        }
                    }
                }
            }
            else
            {
                var it_Team = childTeamMap.GetEnumerator();
                while (it_Team.MoveNext())
                {
                    List<ChildInfo> team = it_Team.Current.Value;
                    ChildInfo firstTd = team[0];

                    float sqrDist = Vector3.SqrMagnitude(firstTd.obj.transform.localPosition - pickingPoint);
                    if (sqrDist < min_Cell)
                    {
                        min_Cell = sqrDist;
                        closestCellToLeftTop = firstTd;
                        offsetOfClosestCellToLeftTop = firstTd.obj.transform.localPosition.y - leftTopCellHangingPoint.y;
                    };

                    if (pageStartChild.Contains(firstTd) && sqrDist < min_PageStartCell)
                    {
                        min_PageStartCell = sqrDist;
                        closestPageStartCellToLeftTop = firstTd;
                    }

                    float distance = firstTd.obj.transform.localPosition.y - center.y;

                    if (distance < -halfCellTeamAmountSpan) //向下
                    {
                        isContactEdge = firstTd.rowIndex + 1  - childTeamAmount <= 0;
                        if (firstTd.rowIndex + 1  - childTeamAmount > 0) //这种计算方式会有问题 >= 与 > 有区别 在初始位置设定的情况下 与“向右” 情况不一致
                        {
                            setTeamPosWhenWrap(team, childWarpSpan);
                            setTeamDataWhenWrap(team, true, -childTeamAmount);
                            //isContactEdge = false;
                        }
                        else
                        {
                            if (isLoop)
                            {
                                setTeamPosWhenWrap(team, childWarpSpan);
                                setTeamDataWhenWrap(team, true, logicallyChildTeamMaxNum - childTeamAmount);
                                //isContactEdge = true;
                            }
                        }
                    }
                    else if (distance > halfCellTeamAmountSpan) //向上
                    {
                        isContactEdge = firstTd.rowIndex + childTeamAmount >= logicallyChildTeamMaxNum;
                        if (firstTd.rowIndex + childTeamAmount < logicallyChildTeamMaxNum)
                        {
                            setTeamPosWhenWrap(team, -childWarpSpan);
                            setTeamDataWhenWrap(team, true, childTeamAmount);
                            //isContactEdge = false;
                        }
                        else
                        {
                            if (isLoop)
                            {
                                setTeamPosWhenWrap(team, -childWarpSpan);
                                setTeamDataWhenWrap(team, true, childTeamAmount - logicallyChildTeamMaxNum);
                                //isContactEdge = true;
                            }
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
                    currentPageIndex = teamIndex / childTeamNumPerPage;
                }
            }
            else
            {
                if (null != closestCellToLeftTop)
                {
                    springOffset = closestCellToLeftTop.obj.transform.localPosition - leftTopCellHangingPoint;
                    Debug.Log("dataIndex Of closestCellToLeftTop is " + closestCellToLeftTop.dataIndex.ToString());
                    int teamIndex = getLogicallyTeamIndexByDataIndex(closestCellToLeftTop.dataIndex);
                    currentPageIndex = teamIndex / childTeamNumPerPage;
                }
            }
            updateProgressBar();
            //Debug.Log("当前的的页数是 =============》 " + currentPageIndex);
            if (null != pagination)
                pagination.updateView(currentPageIndex + 1);
        }

        private void setTeamPosWhenWrap(List<ChildInfo> team, Vector3 posOffset)
        {
            var it_Cell = team.GetEnumerator();
            while (it_Cell.MoveNext())
            {
                it_Cell.Current.obj.transform.localPosition += posOffset;
            }
        }

        private void setTeamDataWhenWrap(List<ChildInfo> team, bool isRow, int indexOffset)
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

        private void updateChild(ChildInfo td)
        {
            int _objIndex = childColumns * td.rowIndex + td.colIndex;
            int _dataIndex = isHorizontal ? (isPageMode ? getDataIndexInPageMode(td.rowIndex, td.colIndex) : _objIndex) : _objIndex;
            if (null != dataCacheArray && _dataIndex >= 0 && _dataIndex < dataCacheArray.Length)
            {
                td.dataIndex = _dataIndex;
                dataCacheArray[_dataIndex].SetContentOn(td.obj.transform);             
                td.obj.SetActive(true);
                setChildName(td, _objIndex);
            }
            else
            {
                td.obj.SetActive(isActiveSuperfluousChildObj);
            }
        }

        private void setChildName(ChildInfo td, int objIndex)
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
                        mPanel.clipOffset.x - originalScrollOffset.x + childSpan * alignOffset :
                        childSpan * alignOffset;

                    contentMin = contentMin % logicallyCellTeamMaxSpan;
                    float contentMax = logicallyCellTeamMaxSpan - contentMin - viewSize;
                    updateScrollbars(horScrollBar, contentMin, contentMax, false);
                }

                if (verScrollBar != null)
                {
                    float contentMin = bmax.y > bmin.y ?
                        -(mPanel.clipOffset.y - originalScrollOffset.y - childSpan * alignOffset) :
                        -(childSpan * alignOffset);

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
            Debug.Log("======================> trim");
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
            return new Vector3(corners[1].x + childSize.x * 0.5f, corners[1].y - childSize.y * 0.5f, corners[1].z) - childBounds.center;
        }

        private int getDataIndexInPageMode(int rowIndex, int colIndex)
        {
            int perPage = ((colIndex / columnsPerPage) * childNumPerPage);
            int currPage = columnsPerPage * rowIndex + colIndex % columnsPerPage;
            return currPage + perPage;
        }

        private int getLogicallyTeamIndexByDataIndex(int dataIndex)
        {
            if (isPageMode)
            {
                return ((dataIndex / childNumPerPage) * childTeamNumPerPage) + ((dataIndex % childNumPerPage) % childTeamNumPerPage);
            }
            else
            {
                return isHorizontal ? dataIndex % logicallyChildTeamMaxNum : Mathf.CeilToInt((float)dataIndex / childNumPerTeam);
            }
        }

        private int getPageStartCellDataIndex(int logicallyTeamIndex, int alignToTeamIndex)
        {
            int TeamIndexInPage = logicallyTeamIndex % childTeamNumPerPage;
            int pageStartChildLogicallyTeamIndex = logicallyTeamIndex - TeamIndexInPage;
            if (isPageMode)
            {
                int rowIndex = isHorizontal ? 0 : pageStartChildLogicallyTeamIndex;
                int colIndex = isHorizontal ? pageStartChildLogicallyTeamIndex : 0;
                return getDataIndexInPageMode(rowIndex, colIndex);
            }
            else
            {
                if (TeamIndexInPage > alignToTeamIndex) //相当于向左移动
                {
                    pageStartChildLogicallyTeamIndex = (logicallyTeamIndex - alignToTeamIndex) % logicallyChildTeamMaxNum;
                    if (logicallyChildTeamMaxNum - childTeamNumPerPage < pageStartChildLogicallyTeamIndex) //到了队尾
                    {
                        pageStartChildLogicallyTeamIndex = isLoop ? pageStartChildLogicallyTeamIndex : logicallyChildTeamMaxNum - childTeamNumPerPage;
                    }
                }
                else if (TeamIndexInPage < alignToTeamIndex) // 相当于向右移动
                {
                    pageStartChildLogicallyTeamIndex = (pageStartChildLogicallyTeamIndex + alignToTeamIndex) % logicallyChildTeamMaxNum;
                    if (logicallyTeamIndex < childTeamNumPerPage)
                    {
                        pageStartChildLogicallyTeamIndex = isLoop ? logicallyChildTeamMaxNum - alignToTeamIndex : 0;
                    }
                }

                int rowIndex = isHorizontal ? 0 : pageStartChildLogicallyTeamIndex;
                int colIndex = isHorizontal ? pageStartChildLogicallyTeamIndex : 0;
                return childColumns * rowIndex + colIndex;
            }
        }

        private int getCalculatedColIndex(int colIndex, int alignOffset)
        {
            //int calculatedColIndex = colIndex + pageStartChildLogicallyTeamIndex;
            int calculatedColIndex = isHorizontal ? alignOffset + colIndex : colIndex;
            if (calculatedColIndex >= logicallyChildTeamMaxNum)
            {
                if (isLoop)
                {
                    calculatedColIndex = calculatedColIndex % logicallyChildTeamMaxNum;
                }
            }
            return calculatedColIndex;
        }

        private int getCalculatedRowIndex(int rowIndex, int alignOffset) 
        {
            int calculatedRowIndex = isHorizontal ? rowIndex : alignOffset + rowIndex;

            if (calculatedRowIndex >= logicallyChildTeamMaxNum)
            {
                if (isLoop)
                {
                    calculatedRowIndex = calculatedRowIndex % childRows;
                }
            }
            return calculatedRowIndex;
        }


        // =====================================================================================
        // 外部常用方法
        public void display(params IStuff[] dataArray)
        {
            display(0, 0, dataArray);
        }

        public void display(int alignTargetIndex = 0, int alignTo = 0, params IStuff[] dataArray) // alignTo 是目标所在的Team的位置 因此小于每页的team
        {
            //  alignPos
            dataCacheArray = dataArray;
            calculateParameters(alignTargetIndex, alignTo);
            setChildren();

            if (null != dataCacheArray)
            {
                float childCount = (float)dataCacheArray.Length;
                float cellNumInOnePage = (float)childNumPerPage;

                int pageNum = Mathf.CeilToInt(childCount / cellNumInOnePage);
                pageNum = pageNum <= 1 ? 1 : pageNum;
                mScroll.enabled = pageNum > 1;
                if (null != pagination)
                    pagination.setView(pageNum, currentPageIndex + 1);
            }
        }

        public void RefreshByReset(bool isReset, params IStuff[] dataArray)
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

        public static StuffCommonLayout configure(Transform trans, int row = 0, int col = 0, bool isPageMode = false, bool isLoop = false, bool isAutoTrim = false)
        {
            if (null == trans) return null;
            StuffCommonLayout scl = trans.gameObject.GetComponent<StuffCommonLayout>();
            if (null == scl)
            {
                scl = trans.gameObject.AddComponent<StuffCommonLayout>();
                scl.rowsPerPage = row;
                scl.columnsPerPage = col;
                scl.isPageMode = isPageMode;
                scl.isLoop = isLoop;
                scl.isAutoTrim = isAutoTrim;
                if (!scl.checkStructureAndInit()) Debug.Log("Initialise failed!!!!");
            }
            return scl;
        }

    }
}