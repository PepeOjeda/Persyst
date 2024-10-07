using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Persyst;
using Persyst.Internal;

/*
IMPORTANT:
The save system does not actually save any information about the scriptableObject. This class is only so that *references to the scriptableObject* can be serialized.
ScriptableObjects are considered inmutable during runtime. This is not a technical limitation, it's a design decision, to do with what ScriptableObjects represent.
*/

namespace Persyst
{

    [CreateAssetMenu(fileName = "Data", menuName = "ScriptableObjects/IdentifiableScriptableObject", order = 1)]
    [DefaultExecutionOrder(-1)]
    public class IdentifiableScriptableObject : ScriptableObject, IReferentiable
    {
        //ScriptableObjects are reset multiple times in a weird unpredictable way during creation and duplication, which messes things up quite a bit
        //this static data lets us retrieve a UID that was already assigned to this object before the reset, to prevent ending up with multiple entries for a single object in the UIDManager
        static Dictionary<int, long> UIDofInstance = new();


        [SerializeField]
        [NaughtyAttributes.ReadOnly] long myUID;


        void OnEnable()
        {
            if (UIDManager.instance != null)
                Initialize();

            UIDManager.OnManagerAvailable += Initialize;
        }

        void OnDestroy()
        {
            RemoveFromUIDManager();
        }

        void Reset()
        {
            RetrieveUIDAfterReset();
        }

        void RetrieveUIDAfterReset()
        {
            if (UIDofInstance.TryGetValue(GetInstanceID(), out long auxUID))
                myUID = auxUID;
        }

        void Initialize()
        {
            RetrieveUIDAfterReset();
            UnityEngine.Object objectWithThisUID = UIDManager.instance.GetObject(myUID);
            bool uidIsTaken = objectWithThisUID != null && objectWithThisUID != this;
            if (myUID == 0 || uidIsTaken)
            {
                myUID = UIDManager.instance.generateUID(this);
                Debug.Log($"Generated UID {myUID} for object {name}");
            }
            UIDofInstance[GetInstanceID()] = myUID;
        }

        public void RemoveFromUIDManager()
        {
            UIDManager.instance.removeUID(myUID);
        }


        public long GetUID()
        {
            return myUID;
        }

        public void SetUID(long value)
        {
            myUID = value;
            UIDofInstance[GetInstanceID()] = myUID;
        }

        public virtual string InspectorCategory()
        {
            return "Scriptable Objects";
        }

#if UNITY_EDITOR
        [NaughtyAttributes.Button("Register UID manually")]
        protected void ManualUIDRegister()
        {
            InputCustomUIDWindow window = ScriptableObject.CreateInstance<InputCustomUIDWindow>();
            window.position = new Rect(Screen.width, Screen.height / 2, 350, 250);
            window.inputText = myUID.ToString();
            window.identifiableObject = this;
            window.ShowPopup();
        }
#endif
    }

}