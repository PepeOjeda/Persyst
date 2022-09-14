using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Persyst;

public class Item_tracker : MonoBehaviour, ISaveable
{
	public static Dictionary<ulong, Item> items = new Dictionary<ulong, Item>();
    public static Item_tracker instance;

	[SaveThis] Dictionary<ulong, Item> _items{
		get{return items;}
		set{items = value;}
	}

	void OnEnable(){
		if(instance!=null && instance != this)
			Destroy(instance);

		instance = this;
	}

	public ulong registerItem(Item item){
        System.Random random = new System.Random();
		byte[] buf = new byte[8]; 
		ulong value;
		do{
			random.NextBytes(buf);
			value = (ulong)System.BitConverter.ToInt64(buf, 0);
		}while(items.ContainsKey(value));
		
		items.Add(value, item);
		return value;
	}

}
