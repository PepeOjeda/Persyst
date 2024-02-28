#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using Persyst;
public class InputCustomUIDWindow : EditorWindow
{
    public IdentifiableObject identifiableObject;
    public string inputText;
    
    void OnGUI()
    {
        inputText = EditorGUILayout.TextField( "New UID: ", inputText );
        
        if(GUILayout.Button("Accept"))
        {
            if(!long.TryParse(inputText, out long uid))
            {
                Debug.LogError("Could not parse new uid!");
            }
            else if(identifiableObject)
            {
                UIDManager.instance.removeUID(identifiableObject.myUID);
                identifiableObject.myUID = uid;
                UIDManager.instance.refreshReference(identifiableObject.gameObject, uid);
                Close();
            }
            else
                Debug.LogError("Identifiable object is null!");

        }

        if(GUILayout.Button("Cancel"))
        {
            Close();
        }
    }

}
#endif