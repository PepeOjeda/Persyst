
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
    public class IdentifiableObject : MonoBehaviour
    {
        [SerializeField]
        [NaughtyAttributes.ReadOnly]
         public long myUID;

        

        void Awake()
        {
#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.sceneClosing += serializeDrivenUID;
            RegisterDrivenProperty();
#endif
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
                UnregisterDrivenProperty();
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
#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.sceneClosing -= serializeDrivenUID;
#endif
        }

        
#if UNITY_EDITOR
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
            var assembly = System.Reflection.Assembly.Load("UnityEngine.CoreModule");
            var type = assembly.GetType("UnityEngine.DrivenPropertyManager");
            var method = type.GetMethod("UnregisterProperty");
            method.Invoke(null, new object[]{this, this, "myUID"} );
        }

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
            InputCustomUIDWindow window = ScriptableObject.CreateInstance<InputCustomUIDWindow>();
            window.position = new Rect(Screen.width, Screen.height/2 , 350, 250);
            window.inputText = myUID.ToString();
            window.identifiableObject = this;
            window.ShowPopup();
        }

        // this code is meant to get rid of the IsDestroying assertion error due to driven properties... but id doesnt quite work. It's a bit of a mess, honestly
        long UID_copy = 0;
        protected virtual void OnEnable()
        {
            if(Application.isPlaying)
                return;
            RemoveOverrideState(true);
        }

        void serializeDrivenUID(UnityEngine.SceneManagement.Scene scene, bool removingScene)
        {
            if(scene != gameObject.scene)
                return;
            storeUID();
            UnregisterDrivenProperty();
            myUID = UID_copy;
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(gameObject.scene);
            Debug.Log("Saving scene modifications");
            UnityEditor.SceneManagement.EditorSceneManager.sceneClosing -= serializeDrivenUID;
        }

        protected void OnDisable()
        {
            if(Application.isPlaying)
                return;           
            
            storeUID();
            UnregisterDrivenProperty();
            myUID = UID_copy;
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

    }

}
