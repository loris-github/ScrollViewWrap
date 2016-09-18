using UnityEngine;
using System.Collections;
using Util;

public class TestObject: MonoBehaviour {

    public ScrollViewWrap svw;

    void Awake() 
    {
        Transform parentTrans = transform.Find("ScrollView/Parent");
        svw = ScrollViewWrap.configure(parentTrans);
    }

	void Start () {
        
	
	}
	
	// Update is called once per frame
	void Update () {
	
	}


    public class WarpDate : IScrollViewChild
    {
        int order;
        public WarpDate(int i) 
        {
            order = i;
        
        }

        public void SetContentOn(Transform trans) 
        {
            trans.Find("Label").GetComponent<UILabel>().text = order.ToString();
        }

        public void OnChildClick(GameObject go) 
        {
        
        
        }
    
    }


}
