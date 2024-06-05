#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using Persyst;
using UnityEditor.SceneManagement;
using Persyst.Internal;

namespace Persyst
{

    public class InputCustomUIDWindow : EditorWindow
    {
        public IReferentiable identifiableObject;
        public string inputText;

        Vector2 lastMousePos;
        void OnGUI()
        {
            inputText = EditorGUILayout.TextField("New UID: ", inputText);

            if (GUILayout.Button("Accept"))
            {
                if (!long.TryParse(inputText, out long uid))
                {
                    Debug.LogError("Could not parse new uid!");
                }
                else if (identifiableObject != null)
                {
                    UIDManager.instance.removeUID(identifiableObject.GetUID());
                    identifiableObject.SetUID(uid);
                    if (uid != 0)
                        UIDManager.instance.refreshReference(identifiableObject as Object, uid);

                    EditorUtility.SetDirty(identifiableObject as Object);
                    if (!Application.isPlaying && identifiableObject is MonoBehaviour)
                        EditorSceneManager.MarkSceneDirty((identifiableObject as MonoBehaviour).gameObject.scene);

                    Close();
                }
                else
                    Debug.LogError("Identifiable object is null!");

            }

            if (GUILayout.Button("Cancel"))
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
                Event.current.Use();
                lastMousePos = currentMousePos;
            }

        }

    }
}
#endif