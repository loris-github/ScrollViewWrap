using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace UI2D
{
    public class Pagination
    {
        ObjectManager pageMarkManager;
        Transform markContainer;
        Dictionary<int, Transform> pageMarkDic = new Dictionary<int, Transform>(); // pageOrder, pageMark
        bool isShowMark;
        int currPageOrder; //当前第几页

        public Pagination(Transform markContainer, Transform markItem) 
        {
            this.markContainer = markContainer;
            pageMarkManager = new ObjectManager(markContainer, markItem, ObjectManager.GROWTH_DIRECTION.GROWTH_HORIZONTAL, 30f);
            isShowMark = false;
            markContainer.gameObject.SetActive(isShowMark);
            currPageOrder = 1;
        }

        public void setView(int pageNum, int pageOrder = 1)
        {
            pageMarkManager.Initialize();
            pageMarkDic.Clear();
            isShowMark = pageNum > 1;
            markContainer.gameObject.SetActive(isShowMark);
            if (isShowMark)
            {
                for (int i = 0; i < pageNum; i++) 
                {
                    Transform markItem = pageMarkManager.GetChild();
                    markItem.gameObject.SetActive(true);
                    pageMarkDic.Add(i + 1, markItem);
                }
                updateView(pageOrder);
            }
        }

        public void updateView(int pageOrder)
        {
            if (isShowMark) 
            {
                UICenterOnChild coc = markContainer.GetComponent<UICenterOnChild>();
                if (null != coc) coc.Recenter();
                var it = pageMarkDic.GetEnumerator();
                while(it.MoveNext())
                {
                    it.Current.Value.Find("Spr_nowPage").gameObject.SetActive(it.Current.Key == pageOrder);
                }
            }
        }
    }
}
