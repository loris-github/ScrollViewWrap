using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using Util;

public class TestObject1: MonoBehaviour {

    public StuffCommonLayout svw;

    void Awake() 
    {
        Transform parentTrans = transform.Find("ScrollView/Parent");
        svw = StuffCommonLayout.configure(parentTrans, 5, 4, false, false, true);
    }

	void Start () {
        List<WarpDate> dataList = new List<WarpDate>();
        for (int i = 0; i < 60; i++) 
        {
            dataList.Add(new WarpDate(i, dataList, svw));
        }
        svw.display(4,0,dataList.ToArray());
	
	}
	
	// Update is called once per frame
	void Update () {
	    
	}


    public class WarpDate : IStuff
    {
        int id;
        List<WarpDate> list;
        StuffCommonLayout svw;

        public WarpDate(int id, List<WarpDate> list, StuffCommonLayout svw) 
        {
            this.id = id;
            this.list = list;
            this.svw = svw;
        }

        public void SetContentOn(Transform trans) 
        {
            trans.Find("Label").GetComponent<UILabel>().text = id.ToString();
            UIEventListener.Get(trans.gameObject).onClick = OnChildClick;
        }

        public void OnChildClick(GameObject go) 
        {
            //Debug.Log("当前的方块数字是：" + id);
            //bool isRemoved = false;
            //if (null != list && list.Contains(this))
            //{
            //    isRemoved = list.Remove(this);
            //}

            //if (isRemoved && null != svw)
            //{
            //    svw.RefreshByReset(false, list.ToArray());
            //}
            svw.testMoveUp();
        }
    }
}
