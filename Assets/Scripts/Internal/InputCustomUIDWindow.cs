#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using Persyst;
public class InputCustomUIDWindow : EditorWindow
{
    public IdentifiableObject identifiableObject;
    public string inputText;
    
    Vector2 lastMousePos;
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

        Vector2 currentMousePos = GUIUtility.GUIToScreenPoint(Event.current.mousePosition);
        if (Event.current.type == EventType.MouseDown)
            lastMousePos = currentMousePos;
        if (Event.current.type == EventType.MouseDrag) 
        {
            Vector2 delta = currentMousePos - lastMousePos;
            Rect currentpos = position;
            currentpos.x += delta.x;
            currentpos.y += delta.y;
            position = currentpos;
            Event.current.Use ();
            lastMousePos = currentMousePos;
        }

    }

}
#endif