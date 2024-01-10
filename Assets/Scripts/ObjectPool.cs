using System.Collections.Generic;
using UnityEngine;

public class ObjectPool
{
    private Queue<GameObject> objects = new Queue<GameObject>();
    private GameObject prefab;

    public ObjectPool(GameObject prefab, int initialSize)
    {
        this.prefab = prefab;

        for (int i = 0; i < initialSize; i++)
        {
            GameObject obj = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity);
            obj.SetActive(false);
            objects.Enqueue(obj);
        }
    }

    public GameObject GetObject()
    {
        if (objects.Count > 0)
        {
            GameObject obj = objects.Dequeue();
            obj.SetActive(true);
            return obj;
        }
        else
        {
            GameObject obj = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity);
            return obj;
        }
    }

    public void ReturnObject(GameObject obj)
    {
        obj.SetActive(false);
        objects.Enqueue(obj);
    }
}
