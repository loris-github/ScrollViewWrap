using UnityEngine;
using System.Collections;

namespace UI2D
{
    /// <summary>
    /// 用于对 父对象下面的子对象进行 增加、显隐 控制
    /// </summary>
    public class ObjectManager
    {
        public enum GROWTH_DIRECTION { GROWTH_VERTIAL, GROWTH_HORIZONTAL, GROWTH_NULL }

        public enum ANCHOR_STYLE { ANCHOR_GRID, ANCHOR_COLLIDER }

        Transform TransContainer, TransPrefab;
        int Count;  //子对象计数器
        int CountOfChild;   //原有子对象数量
        GROWTH_DIRECTION GrowthDirection = GROWTH_DIRECTION.GROWTH_NULL;
        Vector2 SizeOfObject, LocalPosOfObject;
        int CountPerCR = 100; //每行 或 每列 显示对象数量
        // 行或列之间的距离，在没有boxcolider时使用
        float distance = 0f;
        private string levelName;
        private int index;

        public ObjectManager(Transform Container, Transform Prefab, GROWTH_DIRECTION Direction, int CountPerRowOrCol)
        {
            CountOfChild = Container.childCount;
            Count = 0;
            TransContainer = Container;
            TransPrefab = Prefab;
            levelName = TransPrefab.name;
            GrowthDirection = Direction;
            CountPerCR = CountPerRowOrCol;
        }
        public ObjectManager(Transform Container, Transform Prefab, GROWTH_DIRECTION Direction)
        {
            CountOfChild = Container.childCount;
            Count = 0;
            TransContainer = Container;
            TransPrefab = Prefab;
            levelName = TransPrefab.name;
            GrowthDirection = Direction;
        }

        public ObjectManager(Transform Container, Transform Prefab, GROWTH_DIRECTION Direction, float dis)
        {
            CountOfChild = Container.childCount;
            Count = 0;
            TransContainer = Container;
            TransPrefab = Prefab;
            levelName = TransPrefab.name;
            GrowthDirection = Direction;
            distance = dis;
        }
        
        public ObjectManager(Transform Container, Transform Prefab )
        {
            CountOfChild = Container.childCount;
            Count = 0;
            TransContainer = Container;
            TransPrefab = Prefab;
            levelName = TransPrefab.name;
        }

        public void Initialize()
        {
            CountOfChild = TransContainer.childCount;
            Count = 0;
            string end = levelName.Substring(levelName.Length - 1);
            int result = 0;
            if (int.TryParse(end, out result))
            {
                string start = levelName.Substring(0, levelName.Length - 1);
                levelName = start;
            }
            UIToggle tog = null;
            for (int i = 0; i < CountOfChild; i++)
            {
                tog = TransContainer.GetChild(i).GetComponent<UIToggle>();
                if (tog != null)
                {
                    tog.value = false;
                }
                TransContainer.GetChild(i).gameObject.SetActive(false);
            }
            LocalPosOfObject = TransPrefab.localPosition;
            if (GrowthDirection != GROWTH_DIRECTION.GROWTH_NULL)
            {
                BoxCollider c = TransPrefab.GetComponent<BoxCollider>();
                if(c != null)
                {
                    SizeOfObject = c.size;
                    return;
                }
                if (distance > 0)
                {
                    if (GrowthDirection == GROWTH_DIRECTION.GROWTH_VERTIAL)
                    {
                        SizeOfObject.y = distance;
                    }
                    else if (GrowthDirection == GROWTH_DIRECTION.GROWTH_HORIZONTAL)
                    {
                        SizeOfObject.x = distance;
                    }
                }
            }
        }
        /*
         * objectId 返回指定索引的子对象。
         */
        public Transform GetChild(int objectId)
        {
            if ( TransContainer.childCount == 0 )
                return null;
            return TransContainer.GetChild(objectId);
        }
        /*
         * objectName 用于设置新创建的对象名称。
         */
        public Transform GetChild(string objectName)
        {
            Transform transNewObject = GetChild();
            transNewObject.name = objectName;
            return transNewObject;
        }
        public Transform GetChild()
        {
            Transform transNewObject;
            if (Count < CountOfChild)
            {
                transNewObject = TransContainer.GetChild(Count++);
                transNewObject.gameObject.SetActive(true);
            }
            else
            {
                transNewObject = NGUITools.AddChild(TransContainer.gameObject, TransPrefab.gameObject).transform;
                if (GrowthDirection != GROWTH_DIRECTION.GROWTH_NULL)
                {
                    if (GrowthDirection == GROWTH_DIRECTION.GROWTH_VERTIAL)
                    {
                        int col = Count / CountPerCR, row = Count % CountPerCR;
                        transNewObject.localPosition = new Vector3(LocalPosOfObject.x + SizeOfObject.x * col, LocalPosOfObject.y - SizeOfObject.y * row, 0);
                    }
                    else
                    {
                        int row = Count / CountPerCR, col = Count % CountPerCR;
                        transNewObject.localPosition = new Vector3(LocalPosOfObject.x + SizeOfObject.x * col, LocalPosOfObject.y - SizeOfObject.y * row, 0);
                    }
                }
                Count++;
            }
            transNewObject.localScale = TransPrefab.localScale;
            transNewObject.name = string.Format("{0}{1}", levelName, Count);
            return transNewObject;
        }
        public T GetChild<T>() where T : MonoBehaviour
        {
            Transform child = GetChild();
            T item = child.GetComponent<T>();
            if(item == null)
                item = child.gameObject.AddComponent<T>();
            return item;
        }
        public void RecycleChild(Transform child)
        {
            if(child.parent == TransContainer)
            {
                child.gameObject.SetActive(false);
            }
        }

        public Transform getContainer()
        {
            return TransContainer;
        }

        public int GetCount()
        {
            return this.Count;
        }
    }
}