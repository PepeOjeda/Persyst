using Persyst;
using UnityEngine;

public class TransformSaver : MonoBehaviour, ISaveable
{
    [SaveThis] public Vector3 position {
		get {return transform.position;}
		set {transform.position = value;}
	}
	[SaveThis] public Quaternion rotation{
		get {return transform.rotation;}
		set {transform.rotation = value;}
	}
	[SaveThis] public Vector3 localScale {
		get {return transform.localScale;}
		set {transform.localScale = value;}
	}
}
