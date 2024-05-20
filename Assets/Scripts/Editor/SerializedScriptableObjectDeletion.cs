using UnityEditor;
using UnityEngine;

namespace Persyst.Internal
{
    public class SerializedScriptableObjectDeletion : UnityEditor.AssetModificationProcessor
    {
        static AssetDeleteResult OnWillDeleteAsset(string path, RemoveAssetOptions opt)
        {
            if (AssetDatabase.GetMainAssetTypeAtPath(path) == typeof(IdentifiableScriptableObject))
            {
                var sso = (IdentifiableScriptableObject)AssetDatabase.LoadAssetAtPath(path, typeof(IdentifiableScriptableObject));
                sso.RemoveFromUIDManager();
            }
            return AssetDeleteResult.DidNotDelete;
        }
    }
}