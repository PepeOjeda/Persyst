
using UnityEngine;

namespace Persyst
{

    [ExecuteInEditMode]
    [DefaultExecutionOrder(-1)]
    [DisallowMultipleComponent]
    /// <summary>
    /// It gets a UID and refreshes it automatically, like a persistent object would, but does not save or load any data
    /// The point is to use this for cases where a central manager object handles all the serialization, but it still needs to reference other objects by UID
    /// </summary>
    public class IdentifiableObject : MonoBehaviour
    {
        [SerializeField] public ulong myUID;

        void Awake()
        {
#if UNITY_EDITOR
            //don't do anything when opening a prefab
            if(UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage() != null) 
                return;
#endif
            if (myUID != 0)
                Initialize();
            else
                myUID = UIDManager.instance.generateUID(gameObject);
        }

        public virtual void Initialize()
        {
            UIDManager.instance.refreshReference(gameObject, myUID);
        }

        /// <summary>
        /// Allows you to set the current value of myUID to map to this object in the UIDManager. 
        /// This is not something you should use often, just for very special cases that you want 
        /// to have a particular, easily identifyable UID, or to fix a broken setup
        /// 
        /// Can cause weird behaviour if you use it to overwrite an existing UID
        /// </summary>
        [NaughtyAttributes.Button("Register UID manually")]
        protected void ManualUIDRegister()
        {
            UIDManager.instance.refreshReference(gameObject, myUID);
        }


        protected virtual void OnDestroy()
        {
            if (!Application.isPlaying && gameObject.scene.isLoaded)
                UIDManager.instance.removeUID(myUID);
        }
    }

}
