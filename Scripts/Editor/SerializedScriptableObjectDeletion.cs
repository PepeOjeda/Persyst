using UnityEditor;
using UnityEngine;

namespace Persyst{
    public class SerializedScriptableObjectDeletion : UnityEditor.AssetModificationProcessor
    {
        static AssetDeleteResult OnWillDeleteAsset(string path, RemoveAssetOptions opt)
        {
            if (AssetDatabase.GetMainAssetTypeAtPath(path) == typeof(SerializableScriptableObject))
            {
                var sso = (SerializableScriptableObject) AssetDatabase.LoadAssetAtPath(path, typeof(SerializableScriptableObject));
                sso.RemoveFromUIDManager();
            }
            return AssetDeleteResult.DidNotDelete;
        }
    }
}