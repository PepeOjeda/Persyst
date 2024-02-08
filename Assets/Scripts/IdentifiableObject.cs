
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
            CheckUIDAndInitialize();
        }

        void Reset()
        {
            CheckUIDAndInitialize();
        }

        void CheckUIDAndInitialize()
        {
#if UNITY_EDITOR
            //don't do anything when opening a prefab
            if(UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage() != null) 
            {
                myUID = 0;
                return;
            }
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

        protected virtual void OnDestroy()
        {
            if (!Application.isPlaying && gameObject.scene.isLoaded)
                UIDManager.instance.removeUID(myUID);
        }

        
#if UNITY_EDITOR
        [NaughtyAttributes.Button("Remove from UIDManager")]
        protected void ManualUIDRemove()
        {
            if(UnityEditor.EditorUtility.DisplayDialog("You sure?",
                "Removing this object from the UIDManager means other objects will not be able to find it through Persyst any more, unless you give it a new UID and make sure to update all corresponding references to it.", 
                "Remove it", "Cancel"))
            {

                UIDManager.instance.removeUID(myUID);
            }
        }

        /// <summary>
        /// Allows you to set the current value of myUID to map to this object in the UIDManager. 
        /// This is not something you should use often, just for very special cases that you want 
        /// to have a particular, easily identifiable UID, or to fix a broken setup
        /// 
        /// Can cause weird behaviour if you use it to overwrite an existing UID
        /// </summary>
        [NaughtyAttributes.Button("Register UID manually")]
        protected void ManualUIDRegister()
        {
            if(UnityEditor.EditorUtility.DisplayDialog("Is this what you are trying to do?",
                "This will set the current value of myUID to map to this object in the UIDManager. It will not remove the old value from the UIDManager (if it exists), and will not handle problems caused by choosing a UID that's already in use by other objects. In general, it is preferrable to use the automatically assigned UIDs.", 
                "Do it", "Cancel"))
            {
            UIDManager.instance.refreshReference(gameObject, myUID);
            }
        }

#endif

    }

}
