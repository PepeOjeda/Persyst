using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Persyst;

/*
IMPORTANT:
The save system does not actually save any information about the scriptableObject. This class is only so that *references to the scriptableObject* can be serialized.
ScriptableObjects are considered inmutable during runtime. This is not a technical limitation, it's a design decision, to do with what ScriptableObjects represent.
*/

[CreateAssetMenu(fileName = "Data", menuName = "ScriptableObjects/SerializableScriptableObject", order = 1)][DefaultExecutionOrder(-1)]
public class SerializableScriptableObject : ScriptableObject
{
    [SerializeField] public ulong myUID;
    [SerializeField] bool assigned=false;


    void OnEnable(){
        if(UIDManager.instance != null)
            Initialize();
        
        UIDManager.OnManagerAvailable += Initialize;
    }
    void OnDestroy(){
        RemoveFromUIDManager();
    }

    void Initialize(){
        if(assigned){
            UIDManager.instance.refreshReference(this, myUID);
            return;
        }
        myUID=UIDManager.instance.generateUID(this);
        assigned=true;
    }

    public void RemoveFromUIDManager(){
        UIDManager.instance.removeUID(myUID);
    }
}
