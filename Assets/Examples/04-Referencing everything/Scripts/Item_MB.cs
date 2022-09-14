using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Persyst;

public class Item_MB : MonoBehaviour, ISaveable
{
	[SaveThis] ItemWrapper itemA;
	[SaveThis] ItemWrapper itemB;

	//This is so you can see some info in the inspector
	[NaughtyAttributes.ShowNativeProperty] public string printA {
		get{
			if(itemA ==null || itemA.item == null){
				return "";
			}
			else
			 	return itemA.item.someTestValue.ToString();
		}
	}
	[NaughtyAttributes.ShowNativeProperty] public string printB {
		get{
			if(itemB ==null || itemB.item == null){
				return "";
			}
			else
			 	return itemB.item.someTestValue.ToString();
		}
	}
	
	
	void Start(){
		itemA = new ItemWrapper();
		itemB = new ItemWrapper();
	}

	[NaughtyAttributes.Button("Create separate Items")]
	void createSeparate(){
		itemA.item = new Item();
		itemB.item = new Item();
		itemA.item.someTestValue = 5;
	}

	[NaughtyAttributes.Button("Create shared Item")]
	void createShared(){
		itemA.item = new Item();
		itemB.item = itemA.item;
		itemA.item.someTestValue = 5;
	}

	[NaughtyAttributes.Button("Recover references")]
	void recover(){
		GetComponent<PersistentObject>().LoadObject();
	}

	[NaughtyAttributes.Button("Change Value of A")]
	void changeA(){
		itemA.item.someTestValue++;
	}
}

public class Item : ISaveable {
	public ulong uid {get;} //the uid is set in the constructor and cannot be modified
	
	[SaveThis] public int someTestValue; //this is just some meaningless value so you can more easily tell diferent instances apart in this example

	public Item(){
		uid = Item_tracker.instance.registerItem(this);
	}
}

public class ItemWrapper : ISaveable{
	[SaveThis] ulong uid{
		get{return item!=null? item.uid : 0;}
		set{item = Item_tracker.items[value];} //the object instance is recovered automatically when you read the UID from the savefile!
	}

	public Item item;
}