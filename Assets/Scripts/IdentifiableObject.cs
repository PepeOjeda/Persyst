using UnityEngine;
using System;
using Persyst.Internal;

namespace Persyst
{
    [ExecuteInEditMode]
    [DefaultExecutionOrder(-1)]
    [DisallowMultipleComponent]
    /// <summary>
    /// It gets a UID and refreshes it automatically, like a persistent object would, but does not save or load any data
    /// The point is to use this for cases where a central manager object handles all the serialization, but it still needs to reference other objects by UID
    /// </summary>
    public class IdentifiableObject : MonoBehaviour, IReferentiable, ISerializationCallbackReceiver
    {
        [SerializeField]
        [NaughtyAttributes.ReadOnly]
        protected long myUID;
        public long GetUID() => myUID;

        public void SetUID(long value) => myUID = value;


        void Awake()
        {
            CheckUIDAndInitialize();
        }

        protected virtual void OnDestroy()
        {
            if (!Application.isPlaying && gameObject.scene.isLoaded)
                RemoveUID();
        }

        protected virtual void OnEnable()
        {}

        void Reset()
        {
            CheckUIDAndInitialize();
        }

        void CheckUIDAndInitialize()
        {
#if UNITY_EDITOR
            //don't do anything when opening a prefab
            if (UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                return;
            }
#endif
            UnityEngine.Object objectWithThisUID = UIDManager.instance.GetObject(myUID);
            bool uidIsTaken = objectWithThisUID != null && objectWithThisUID != gameObject;
            if (myUID == 0 || uidIsTaken)
            {
                myUID = UIDManager.instance.generateUID(gameObject);
                Debug.Log($"Generated UID {myUID} (object {gameObject.name} scene {gameObject.scene.name})");

#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(gameObject);
                if (!Application.isPlaying)
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
            }
            else
                Initialize();
        }

        public virtual void Initialize()
        {
            UIDManager.instance.refreshReference(gameObject, myUID);
        }

        void RemoveUID()
        {
            Debug.Log($"Removing UID {myUID} from manager (object {gameObject.name} scene {gameObject.scene.name})");
            UIDManager.instance.removeUID(myUID);
        }


#if UNITY_EDITOR

        [NaughtyAttributes.Button("Remove from UIDManager")]
        protected void ManualUIDRemove()
        {
            if (UnityEditor.EditorUtility.DisplayDialog("You sure?",
                "Removing this object from the UIDManager means other objects will not be able to find it through Persyst any more, unless you give it a new UID and make sure to update all corresponding references to it.",
                "Remove it", "Cancel"))
            {
                RemoveUID();
            }
        }

        /// <summary>
        /// Allows you to set the current value of myUID to map to this object in the UIDManager. 
        /// This is not something you should use often, just for very special cases that you want 
        /// to have a particular, easily identifiable UID, or to fix a broken setup
        /// </summary>
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

        public void OnBeforeSerialize()
        {
#if UNITY_EDITOR
            // IMPORTANT: EditorApplication checks must be done first.
            // Otherise Unity may report errors like "Objects are trying to be loaded during a domain backup"
            if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode || UnityEditor.EditorApplication.isUpdating)
                return;
            // Validate the type of your prefab. Useful pre-check.
            if (UnityEditor.PrefabUtility.GetPrefabAssetType(this) != UnityEditor.PrefabAssetType.Regular)
                return;

            // Override properties only if this is a prefab asset on disk and not any of its scene instances
            if (!string.IsNullOrWhiteSpace(gameObject.scene.path))
                return;
            // Finally, re-set any fields to initial or specific values for the shared asset prefab on disk
            // This protects these fields when "Apply Override" gets called from any of prefab's scene instances
            myUID = 0;
#endif
        }

        public void OnAfterDeserialize()
        { }


    }

}
