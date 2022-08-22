using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Persyst{
    public class PrefabOverrideApplier : MonoBehaviour
    {
        [NaughtyAttributes.Button("Apply Prefab Overrides")]
        void ApplyPrefabOverrides(){
#if UNITY_EDITOR

            var objectOverrides = PrefabUtility.GetObjectOverrides(gameObject, false);
            foreach(var overr in objectOverrides){
                if(overr.instanceObject.GetType() != typeof(PersistentObject))
                    overr.Apply();
            }

            var addedGameObjectsOverrides = PrefabUtility.GetAddedGameObjects(gameObject);
            foreach(var overr in addedGameObjectsOverrides){
                overr.Apply();
            }

            var addedComponentOverrides = PrefabUtility.GetAddedComponents(gameObject);
            foreach(var overr in addedComponentOverrides){
                overr.Apply();
            }

            var removedComponentOverrides = PrefabUtility.GetRemovedComponents(gameObject);
            foreach(var overr in removedComponentOverrides){
                overr.Apply();
            }
#endif

        }
    }
}