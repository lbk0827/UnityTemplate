using System;
using UnityEngine;

namespace BK.Kit
{
    // Serialized object references only; behavior is owned by BK_Kit.
    public sealed class VisualBindings : MonoBehaviour
    {
        [Serializable] public sealed class Entry { public string key; public UnityEngine.Object value; }
        public string role;
        public Entry[] entries = Array.Empty<Entry>();
        public T Get<T>(string key) where T : UnityEngine.Object
        {
            foreach (var entry in entries)
                if (entry.key == key)
                {
                    if (entry.value is T result) return result;
                    if (entry.value is Component component) return typeof(T)==typeof(GameObject) ? component.gameObject as T : component.GetComponent(typeof(T)) as T;
                    if (entry.value is GameObject go) return go.GetComponent(typeof(T)) as T;
                }
            return null;
        }
    }
}
