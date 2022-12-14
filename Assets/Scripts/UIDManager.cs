using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif
using UnityEngine.Rendering;
using System.Reflection;

namespace Persyst{
    [ExecuteInEditMode][DefaultExecutionOrder(-100)]
    public class UIDManager : MonoBehaviour
    {
        public static UIDManager instance;
        public static event System.Action OnManagerAvailable{
			add{
				if(_OnManagerAvailable == null || !_OnManagerAvailable.GetInvocationList().Contains(value) ) //can subscribe only once
					_OnManagerAvailable += value;
			}
			remove{
				_OnManagerAvailable -= value;
			}
		}

		private static event System.Action _OnManagerAvailable;

        System.Random random;
        [SerializeField] SerializedDictionary<ulong, UnityEngine.Object> UIDs;
        UIDManager inst => instance;
        
        void Start(){
            Initialize(); 
        }


        [SerializeField] [HideInInspector] bool initialized = false;
        void Initialize(){
            if(instance!=null && instance != this)
                Destroy(instance);

            instance = this; 
            random = new System.Random();
            _OnManagerAvailable?.Invoke();
            if(initialized)
                return; 
            initialized = true;       
        }

        internal ulong generateUID(UnityEngine.Object unityObject){
            byte[] buf = new byte[8]; 
            ulong value;
            do{
                random.NextBytes(buf);
                value = (ulong)System.BitConverter.ToInt64(buf, 0);
            }while(UIDs.ContainsKey(value));
            
            UIDs.Add(value, unityObject);

#if UNITY_EDITOR
            EditorUtility.SetDirty (inst);
            EditorSceneManager.MarkSceneDirty (gameObject.scene);
            
            EditorUtility.SetDirty (unityObject);
            if(unityObject.GetType().IsAssignableTo(typeof(GameObject)))
                EditorUtility.SetDirty ( (unityObject as GameObject).GetComponent<PersistentObject>() );
            if(unityObject is GameObject)
                EditorSceneManager.MarkSceneDirty ((unityObject as GameObject).scene);
#endif
            return value;
        }

        public UnityEngine.Object GetObject(ulong UID){
            if(UIDs.TryGetValue(UID, out UnityEngine.Object go))
                return go;
            return null;
        }

        internal void removeUID(ulong _uid){
            UIDs.Remove(_uid);
        }

        //called by the presistentObject when it is loaded
        internal void refreshReference(UnityEngine.Object unityObject, ulong _uid){
            UIDs[_uid]=unityObject;

            //if some object was waiting for this one to be loaded, pass it a reference
            if(pendingReferences.TryGetValue(_uid, out List<PendingReference> references) ){
                var newPendingReferences = new List<PendingReference>();
                foreach(PendingReference pending in references){
                    object referencedObject = null;
                    Type variableType =ReflectionUtilities.GetUnderlyingType(pending.memberInfo);

                    if( variableType.IsAssignableTo(typeof(Component)) ){
                        MethodInfo method =  typeof(GameObject).GetMethod("GetComponent", 1, new Type[]{}).MakeGenericMethod(variableType);
                        referencedObject = method.Invoke(unityObject, new object[]{});
                    }
                    else if( variableType.IsAssignableTo(typeof(GameObject)) || variableType.IsAssignableTo(typeof(SerializableScriptableObject)) ){
                        referencedObject = unityObject;
                    }
                    else{
                        Debug.LogError("Wrong subtype of UnityEngine.Object used in the UID system");
                    }

                    if(pending.memberInfo == null || pending.holderScript==null){
                        newPendingReferences.Add(pending);
                        continue;
                    }
                    ReflectionUtilities.setValue(pending.memberInfo, pending.holderScript, referencedObject);
                }
                pendingReferences[_uid] = newPendingReferences;
            }
        }
        

        Dictionary<ulong, List<PendingReference>> pendingReferences = new Dictionary<ulong, List<PendingReference>>();
        internal void registerPendingReference(object holder, ulong _uid, MemberInfo memberInfo){
            if(holder.GetType().IsValueType){
                Debug.LogWarning("Serializing references inside structs is not a good idea! The \"pending references\" system wont handle them if they are not valid at the time of loading.");
                return;
            }
            
            if( !pendingReferences.TryGetValue(_uid, out List<PendingReference> references) ){
                references = new List<PendingReference>();
            }
            references.Add(new PendingReference(holder, memberInfo));
            pendingReferences[_uid] = references;
        }
        

        //for logging, as the unity inspector displays ulongs as if they were longs
        long reinterpretAsLong(ulong value){
            byte[] bytes = BitConverter.GetBytes(value);
            return (long) System.BitConverter.ToInt64(bytes, 0);
        }

#if UNITY_EDITOR
        [NaughtyAttributes.Button("Clear all entries")]
        void clearAllUIDs(){
            bool clearThem =EditorUtility.DisplayDialog("Clear UIDs?", "Are you sure you want to clear all UIDs? This will create problems if you have existing PersistentObjects", "Clear them!", "Cancel");
            if(clearThem)
                UIDs.Clear();
        }
#endif

    }

    //references to objects that are not yet loaded
    struct PendingReference{
        public object holderScript;
        public MemberInfo memberInfo;
        public PendingReference(object script, MemberInfo fi){
            holderScript = script;
            memberInfo = fi;
        }
    }
}