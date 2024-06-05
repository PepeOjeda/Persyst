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
        [SerializeField]
        [NaughtyAttributes.ReadOnly] public long myUID;
        [SerializeField] bool assigned = false;


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

        void Initialize()
        {
            if (assigned)
            {
                UIDManager.instance.refreshReference(this, myUID);
                return;
            }
            myUID = UIDManager.instance.generateUID(this);
            assigned = true;
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