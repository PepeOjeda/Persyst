
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
        [SerializeField][NaughtyAttributes.ReadOnly] public long myUID;

        

        void Awake()
        {
            RegisterDrivenProperty();
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
            //UnregisterDrivenProperty();
        }

        // prevent the UID from being considered a prefab override
        // This is a bit of a mess, because the driven property manager is not public, it is internal to the UnityEngine.CoreModule assembly, so it has to be done through reflection
        // using something from a non-public API is not great, buuuuuut... 
        void RegisterDrivenProperty()
        {
#if UNITY_EDITOR
            var assembly = System.Reflection.Assembly.Load("UnityEngine.CoreModule");
            var type = assembly.GetType("UnityEngine.DrivenPropertyManager");
            var method = type.GetMethod("RegisterProperty");
            method.Invoke(null, new object[]{this, this, "myUID"} );
#endif
        }

        void UnregisterDrivenProperty()
        {
#if UNITY_EDITOR
            var assembly = System.Reflection.Assembly.Load("UnityEngine.CoreModule");
            var type = assembly.GetType("UnityEngine.DrivenPropertyManager");
            var method = type.GetMethod("UnregisterProperty");
            method.Invoke(null, new object[]{this, this, "myUID"} );
#endif
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
            InputCustomUIDWindow window = ScriptableObject.CreateInstance<InputCustomUIDWindow>();
            window.position = new Rect(Screen.width / 2, Screen.height / 2, 350, 250);
            window.inputText = myUID.ToString();
            window.identifiableObject = this;
            window.ShowPopup();
        }

#endif
        // this code is meant to get rid of the IsDestroying assertion error due to driven properties... but id doesnt quite work. It's a bit of a mess, honestly
        //void OnEnable()
        //{
        //    RegisterDrivenPorperty();
        //}
        //void OnDisable()
        //{
        //    long UID_copy = myUID;
        //    UnityEditor.SerializedObject serializedObject = new UnityEditor.SerializedObject(this);
        //    UnityEditor.SerializedProperty serializedPropertyMyInt = serializedObject.FindProperty("myUID");
        //    UnityEditor.PrefabUtility.RevertPropertyOverride(serializedPropertyMyInt, UnityEditor.InteractionMode.AutomatedAction);
        //    myUID = UID_copy;
        //}

    }

}
