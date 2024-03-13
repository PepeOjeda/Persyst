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

namespace Persyst
{
    [ExecuteInEditMode]
    [DefaultExecutionOrder(-100)]
    public class UIDManager : MonoBehaviour
    {
        static UIDManager _instance;
        public static UIDManager instance
        {
            get
            {
                if(_instance == null)
                {
                    UIDManager existing = FindObjectOfType(typeof(UIDManager)) as UIDManager;
                    if (existing)
                    {
                        _instance = existing;
                        _instance.Initialize();
                    }
                }
                
                return _instance;
            }
        }
        public static event Action OnManagerAvailable
        {
            add
            {
                if (_OnManagerAvailable == null || !_OnManagerAvailable.GetInvocationList().Contains(value)) //can subscribe only once
                    _OnManagerAvailable += value;
            }
            remove
            {
                _OnManagerAvailable -= value;
            }
        }

        private static event Action _OnManagerAvailable;

        System.Random random;
        [SerializeField] SerializedDictionary<long, UnityEngine.Object> UIDs;

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
        static void SetupAfterDomainReload()
        {
            if(!Application.isPlaying)
            {
                UIDManager existing = FindObjectOfType(typeof(UIDManager)) as UIDManager;
                if(existing)
                    existing.Initialize();
            }
        }
#endif


        void Initialize()
        {
            if (_instance != null && _instance != this)
                Destroy(_instance);

            _instance = this;
            random = new System.Random();
            _OnManagerAvailable?.Invoke();
            Debug.Log("UIDManager initialized");
        }

        internal long generateUID(UnityEngine.Object unityObject)
        {
            byte[] buf = new byte[8];
            long value;
            do
            {
                random.NextBytes(buf);
                value = (long)System.BitConverter.ToInt64(buf, 0);
            } while (UIDs.ContainsKey(value));

            UIDs.Add(value, unityObject);

#if UNITY_EDITOR
            EditorUtility.SetDirty(_instance);
            if(!Application.isPlaying)
                EditorSceneManager.MarkSceneDirty(gameObject.scene);

            EditorUtility.SetDirty(unityObject);
            if (unityObject.GetType().IsAssignableTo(typeof(GameObject)))
                EditorUtility.SetDirty((unityObject as GameObject).GetComponent<IdentifiableObject>());
            if (!Application.isPlaying && unityObject is GameObject)
                EditorSceneManager.MarkSceneDirty((unityObject as GameObject).scene);
#endif
            return value;
        }

        public UnityEngine.Object GetObject(long UID)
        {
            if (UIDs.TryGetValue(UID, out UnityEngine.Object go))
                return go;
            return null;
        }

        internal void removeUID(long _uid)
        {
            UIDs.Remove(_uid);
            if(!Application.isPlaying)
            {
                EditorUtility.SetDirty(_instance);
                EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }
        }

        //called by the presistentObject when it is loaded
        internal void refreshReference(UnityEngine.Object unityObject, long _uid)
        {
            UIDs[_uid] = unityObject;
#if UNITY_EDITOR
            if(!Application.isPlaying)
            {
                EditorUtility.SetDirty(_instance);
                EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }
#endif

            //if some object was waiting for this one to be loaded, pass it a reference
            if (pendingReferences.TryGetValue(_uid, out List<PendingReference> references))
            {
                var newPendingReferences = new List<PendingReference>();
                foreach (PendingReference pending in references)
                {
                    object referencedObject = null;
                    Type variableType = ReflectionUtilities.GetUnderlyingType(pending.memberInfo);

                    if (variableType.IsAssignableTo(typeof(Component)))
                    {
                        MethodInfo method = typeof(GameObject).GetMethod("GetComponent", 1, new Type[] { }).MakeGenericMethod(variableType);
                        referencedObject = method.Invoke(unityObject, new object[] { });
                    }
                    else if (variableType.IsAssignableTo(typeof(GameObject)) || variableType.IsAssignableTo(typeof(SerializableScriptableObject)))
                    {
                        referencedObject = unityObject;
                    }
                    else
                    {
                        Debug.LogError("Wrong subtype of UnityEngine.Object used in the UID system");
                    }

                    if (pending.memberInfo == null || pending.holderScript == null)
                    {
                        newPendingReferences.Add(pending);
                        continue;
                    }
                    ReflectionUtilities.setValue(pending.memberInfo, pending.holderScript, referencedObject);
                }
                pendingReferences[_uid] = newPendingReferences;
            }
        }


        Dictionary<long, List<PendingReference>> pendingReferences = new Dictionary<long, List<PendingReference>>();
        internal void registerPendingReference(object holder, long _uid, MemberInfo memberInfo)
        {
            if (holder.GetType().IsValueType)
            {
                Debug.LogWarning("Serializing references inside structs is not a good idea! The \"pending references\" system wont handle them if they are not valid at the time of loading.");
                return;
            }

            if (!pendingReferences.TryGetValue(_uid, out List<PendingReference> references))
            {
                references = new List<PendingReference>();
            }
            references.Add(new PendingReference(holder, memberInfo));
            pendingReferences[_uid] = references;
        }


#if UNITY_EDITOR
        [NaughtyAttributes.Button("Clear all entries")]
        void clearAllUIDs()
        {
            bool clearThem = EditorUtility.DisplayDialog("Clear UIDs?", "Are you sure you want to clear all UIDs? This will create problems if you have existing IdentifiableObjects", "Clear them!", "Cancel");
            if (clearThem)
                UIDs.Clear();
        }
        
        [SerializeField] long UIDtoDelete;
        [NaughtyAttributes.Button("Delete Entry")]
        void DeleteEntry()
        {
            UIDs.Remove(UIDtoDelete);
        }
#endif

    }

    //references to objects that are not yet loaded
    struct PendingReference
    {
        public object holderScript;
        public MemberInfo memberInfo;
        public PendingReference(object script, MemberInfo fi)
        {
            holderScript = script;
            memberInfo = fi;
        }
    }
}