using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Persyst;

namespace PersystExamples
{
    public class Item_tracker : MonoBehaviour, ISaveable
    {
        public static Dictionary<long, Item> items = new Dictionary<long, Item>();
        public static Item_tracker instance;

        [SaveThis]
        Dictionary<long, Item> _items
        {
            get { return items; }
            set { items = value; }
        }

        void OnEnable()
        {
            if (instance != null && instance != this)
                Destroy(instance);

            instance = this;
        }

        public long registerItem(Item item)
        {
            System.Random random = new System.Random();
            byte[] buf = new byte[8];
            long value;
            do
            {
                random.NextBytes(buf);
                value = (long)System.BitConverter.ToInt64(buf, 0);
            } while (items.ContainsKey(value));

            items.Add(value, item);
            return value;
        }

    }
}