#if UNITY_EDITOR
//#define USE_DRIVEN_PROPERTIES
#endif

using UnityEngine;
using System;



namespace Persyst
{
    [ExecuteInEditMode]
    [DefaultExecutionOrder(-1)]
    [DisallowMultipleComponent]
    /// <summary>
    /// It gets a UID and refreshes it automatically, like a persistent object would, but does not save or load any data
    /// The point is to use this for cases where a central manager object handles all the serialization, but it still needs to reference other objects by UID
    /// </summary>
    public class IdentifiableObject : MonoBehaviour, ISerializationCallbackReceiver
    {
        [SerializeField]
        [NaughtyAttributes.ReadOnly]
         public long myUID;
#if USE_DRIVEN_PROPERTIES
        long UID_copy = 0;
#endif


        void Awake()
        {
#if USE_DRIVEN_PROPERTIES
            UnityEditor.EditorApplication.playModeStateChanged += ModeStateChanged ;
            UnityEditor.SceneManagement.EditorSceneManager.sceneSaving += BeforeSaveSceneCallback;
            UnityEditor.SceneManagement.EditorSceneManager.sceneSaved += AfterSaveSceneCallback;
            if(!Application.isPlaying)
                RegisterDrivenProperty();
#endif
            CheckUIDAndInitialize();
        }

        protected virtual void OnDestroy()
        {
            if (!Application.isPlaying && gameObject.scene.isLoaded)
                UIDManager.instance.removeUID(myUID);
#if USE_DRIVEN_PROPERTIES
            UnityEditor.EditorApplication.playModeStateChanged -= ModeStateChanged ;
            UnityEditor.SceneManagement.EditorSceneManager.sceneSaving -= BeforeSaveSceneCallback;
            UnityEditor.SceneManagement.EditorSceneManager.sceneSaved -= AfterSaveSceneCallback;
#endif
        }

        protected virtual void OnEnable()
        {
#if USE_DRIVEN_PROPERTIES
            if(UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            RemoveOverrideState(true);
#endif
        }

        protected void OnDisable()
        {
#if USE_DRIVEN_PROPERTIES
            if(Application.isPlaying)
                return;           
            storeUID();
            UnregisterDrivenProperty();
            myUID = UID_copy;
#endif
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
#if USE_DRIVEN_PROPERTIES
                UnregisterDrivenProperty();
                myUID = 0;
#endif
                return;
            }
#endif
            UnityEngine.Object objectWithThisUID = UIDManager.instance.GetObject(myUID);
            bool uidIsTaken = objectWithThisUID != null && objectWithThisUID != this;
            if (myUID == 0 || uidIsTaken)
            {
                myUID = UIDManager.instance.generateUID(gameObject);
                Debug.Log($"Generated UID {myUID} for object {gameObject.name}");
                
#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(gameObject);
                if(!Application.isPlaying)
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
        /// </summary>
        [NaughtyAttributes.Button("Register UID manually")]
        protected void ManualUIDRegister()
        {
            InputCustomUIDWindow window = ScriptableObject.CreateInstance<InputCustomUIDWindow>();
            window.position = new Rect(Screen.width, Screen.height/2 , 350, 250);
            window.inputText = myUID.ToString();
            window.identifiableObject = this;
            window.ShowPopup();
        }
#endif

#if USE_DRIVEN_PROPERTIES

        // prevent the UID from being considered a prefab override
        // This is a bit of a mess, because the driven property manager is not public, it is internal to the UnityEngine.CoreModule assembly, so it has to be done through reflection
        // using something from a non-public API is not great, buuuuuut... 
        void RegisterDrivenProperty()
        {
            RemoveOverrideState(false);

            var assembly = System.Reflection.Assembly.Load("UnityEngine.CoreModule");
            var type = assembly.GetType("UnityEngine.DrivenPropertyManager");
            var method = type.GetMethod("RegisterProperty");
            method.Invoke(null, new object[]{this, this, "myUID"} );
            
            myUID = UID_copy;
        }

        void UnregisterDrivenProperty()
        {
            UID_copy = myUID;
            var assembly = System.Reflection.Assembly.Load("UnityEngine.CoreModule");
            var type = assembly.GetType("UnityEngine.DrivenPropertyManager");
            var method = type.GetMethod("UnregisterProperty");
            method.Invoke(null, new object[]{this, this, "myUID"} );
            myUID = UID_copy;
        }

        void ModeStateChanged (UnityEditor.PlayModeStateChange state)
        {
            if (state == UnityEditor.PlayModeStateChange.ExitingEditMode)
                UnregisterDrivenProperty();
        }

        void AfterSaveSceneCallback(UnityEngine.SceneManagement.Scene scene)
        {
            RemoveOverrideState(true);
        }

        void BeforeSaveSceneCallback(UnityEngine.SceneManagement.Scene scene, string path)
        {
            if (scene != gameObject.scene)
                return;
            UnregisterDrivenProperty();
        }

        void RemoveOverrideState(bool registerProperty)
        {
            storeUID();
            if(UnityEditor.PrefabUtility.IsPartOfAnyPrefab(gameObject))
            {
                UnityEditor.SerializedObject serializedObject = new UnityEditor.SerializedObject(this);
                UnityEditor.SerializedProperty serializedPropertyMyInt = serializedObject.FindProperty("myUID");
                UnityEditor.PrefabUtility.RevertPropertyOverride(serializedPropertyMyInt, UnityEditor.InteractionMode.AutomatedAction);
            }
            if(registerProperty)
                RegisterDrivenProperty();
            myUID = UID_copy;
        }

        void storeUID()
        {
            if(myUID !=0)
                UID_copy = myUID;
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
        {}

    }

}
