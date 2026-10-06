using System.Collections.Generic;
using UnityEngine;

namespace ColorStackRush
{
    /// <summary>
    /// Lightweight GameObject pool. Built around an inactive template object
    /// so no prefab assets are required. Zero allocations after warm-up.
    /// </summary>
    public class ObjectPool
    {
        public event System.Action<PooledObject> Released;
        readonly GameObject template;
        readonly Transform parent;
        readonly Stack<GameObject> inactive = new Stack<GameObject>(32);

        public ObjectPool(GameObject template, Transform parent, int prewarm = 0)
        {
            this.template = template;
            this.parent = parent;
            template.SetActive(false); // templates never render

            for (int i = 0; i < prewarm; i++)
                inactive.Push(CreateInstance());
        }

        /// <summary>Takes an instance from the pool (or creates one) and activates it.</summary>
        public GameObject Get(Vector3 position, Quaternion rotation)
        {
            GameObject go = inactive.Count > 0 ? inactive.Pop() : CreateInstance();
            var marker = go.GetComponent<PooledObject>();
            marker.IsLeased = true;
            Juice.ForgetTransform(go.transform);
            go.transform.localScale = template.transform.localScale;
            go.transform.SetPositionAndRotation(position, rotation);
            go.SetActive(true);
            return go;
        }

        /// <summary>Deactivates the instance and stores it for reuse.</summary>
        public void Release(GameObject go)
        {
            if (go == null) return;
            var marker = go.GetComponent<PooledObject>();
            if (marker == null || marker.Owner != this || !marker.IsLeased) return;
            marker.IsLeased = false;
            Juice.ForgetTransform(go.transform);
            go.SetActive(false);
            Released?.Invoke(marker);
            inactive.Push(go);
        }

        GameObject CreateInstance()
        {
            var go = Object.Instantiate(template, parent);
            go.name = template.name;
            var pooled = go.GetComponent<PooledObject>();
            if (pooled == null) pooled = go.AddComponent<PooledObject>();
            pooled.Owner = this;
            go.SetActive(false);
            return go;
        }
    }

    /// <summary>
    /// Marker component stored on every pooled instance so any system can
    /// return the object to its owning pool without knowing about the pool.
    /// </summary>
    public class PooledObject : MonoBehaviour
    {
        public ObjectPool Owner;
        public bool IsLeased { get; internal set; }

        /// <summary>Returns this object to its pool (or deactivates it as a fallback).</summary>
        public void Release()
        {
            if (Owner != null) Owner.Release(gameObject);
            else gameObject.SetActive(false);
        }
    }
}
