using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using Util;

public class TestObject1: MonoBehaviour {

    public ScrollViewWrap svw;

    void Awake() 
    {
        Transform parentTrans = transform.Find("ScrollView/Parent");
        svw = ScrollViewWrap.configure(parentTrans, 5, 4, false, false, true);
    }

	void Start () {
        List<WarpDate> dataList = new List<WarpDate>();
        for (int i = 0; i < 24; i++) 
        {
            dataList.Add(new WarpDate(i, dataList, svw));
        }
        svw.display(dataList.ToArray());
	
	}
	
	// Update is called once per frame
	void Update () {
	    
	}


    public class WarpDate : IScrollViewChild
    {
        int id;
        List<WarpDate> list;
        ScrollViewWrap svw;

        public WarpDate(int id, List<WarpDate> list, ScrollViewWrap svw) 
        {
            this.id = id;
            this.list = list;
            this.svw = svw;
        }

        public void SetContentOn(Transform trans) 
        {
            trans.Find("Label").GetComponent<UILabel>().text = id.ToString();
        }

        public void OnChildClick(GameObject go) 
        {
            Debug.Log("当前的方块数字是：" + id);
            bool isRemoved = false;
            if (null != list && list.Contains(this))
            {
                isRemoved = list.Remove(this);
            }

            if (isRemoved && null != svw)
            {
                svw.RefreshByReset(false, list.ToArray());
            }

        }
    }
}
