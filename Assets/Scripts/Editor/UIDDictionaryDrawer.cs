using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Persyst.Internal
{

    [CustomPropertyDrawer(typeof(UIDDictionary))]
    public class UIDDictionaryDrawer : PropertyDrawer
    {
        const string arrayName = "entries";
        const string keyName = "key";
        const string valueName = "value";
        const string categoryName = "Category";

        Dictionary<string, bool> isExpanded = new();

        public override float GetPropertyHeight(SerializedProperty dictionary, GUIContent label)
        {
            SerializedProperty entries = dictionary.FindPropertyRelative(arrayName);
            List<string> categories = GetCategories(entries);
            float size = categories.Count * EditorGUIUtility.singleLineHeight;
            foreach(string category in categories)
            {
                isExpanded.TryGetValue(category, out bool expanded);
                if(expanded)
                    size += Filter(entries, category).Count * EditorGUIUtility.singleLineHeight;
            }

            return size;
        }

        public override void OnGUI(Rect rect, SerializedProperty dictionary, GUIContent label)
        {
            EditorGUI.BeginProperty(rect, label, dictionary);
            var entries = dictionary.FindPropertyRelative(arrayName);

            List<string> categories = GetCategories(entries);

            // this is not necessarily the most elegant way to prevent editing, but it will do, I guess
            GUI.enabled = false;
            foreach(string category in categories)
            {
                rect = DrawItems(Filter(entries, category), rect, category);
            }
            GUI.enabled = true;

            EditorGUI.EndProperty();
        }

        List<string> GetCategories(SerializedProperty entries)
        {
            HashSet<string> cats = new();
            for (int i = 0; i < entries.arraySize; i++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                string category = entry.FindPropertyRelative(categoryName).stringValue;
                cats.Add(category);
            }
            return cats.ToList();
        }

        Rect DrawItems(List<SerializedProperty> entries, Rect rect, string category)
        {
            if(!isExpanded.TryGetValue(category, out _))
                isExpanded[category] = false;

            Rect rectFoldout = new Rect(rect.min.x, rect.min.y, rect.size.x, EditorGUIUtility.singleLineHeight);
            isExpanded[category] = EditorGUI.Foldout(rectFoldout, isExpanded[category], category == ""? "Other" : category);
            if (isExpanded[category])
            {
                Rect next = new Rect(rect.min.x, rect.min.y + EditorGUIUtility.singleLineHeight, rect.size.x * 0.5f, EditorGUIUtility.singleLineHeight);
                for (int i = 0; i < entries.Count; i++)
                {
                    SerializedProperty entry = entries[i];
                    EditorGUI.PropertyField(next, entry.FindPropertyRelative(keyName), GUIContent.none);

                    Rect secondElement = next;
                    secondElement.x += next.width;
                    EditorGUI.PropertyField(secondElement, entry.FindPropertyRelative(valueName), GUIContent.none);
                    next.y += EditorGUIUtility.singleLineHeight;
                }
                return new Rect(rect.min.x, next.min.y, rect.width, rect.height);
            }
            else
                return new Rect(rect.min.x, rect.min.y + EditorGUIUtility.singleLineHeight, rect.width, rect.height);
        }

        List<SerializedProperty> Filter(SerializedProperty entries, string category)
        {
            List<SerializedProperty> filteredList = new();
            for (int i = 0; i < entries.arraySize; i++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                if(entry.FindPropertyRelative(categoryName).stringValue == category)
                    filteredList.Add(entry);
            }
            return filteredList;
        }
    }
}