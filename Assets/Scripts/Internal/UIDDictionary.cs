using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Persyst.Internal
{
    [System.Serializable]
    public class UIDDictionary : Dictionary<long, Object>, ISerializationCallbackReceiver
    {
        [SerializeField] List<Entry> entries = new();

        [System.Serializable]
        public struct Entry
        {
            public long key;
            public Object value;
            public string Category; // for displaying in the editor
        }

        public void OnAfterDeserialize()
        {
            foreach (Entry entry in entries)
            {
                this[entry.key] = entry.value;
            }
        }

        public void OnBeforeSerialize()
        {
            entries.RemoveAll(entry => !DictionaryContains(entry.key));

            foreach (var pair in this)
            {
                //Categories for inspector display only
                //if the class 
                string category = "";
                if (pair.Value is GameObject go && go != null)
                    category = go.scene.name;
                else if (pair.Value is IdentifiableScriptableObject so)
                    category = so.InspectorCategory();

                Entry entry = new() { key = pair.Key, value = pair.Value, Category = category };

                int index = entries.FindIndex(e => e.key == entry.key);
                if (index >= 0)
                {
                    if (entry.Category == "")
                        entry.Category = entries[index].Category;
                    entries[index] = entry;
                }
                else
                    entries.Add(entry);
            }
        }

        bool DictionaryContains(long key)
        {
            return TryGetValue(key, out _);
        }

    }

}