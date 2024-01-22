using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;

namespace Persyst
{
    [DefaultExecutionOrder(-10)]
    [ExecuteInEditMode]
    public class GameSaver : MonoBehaviour
    {
        public static GameSaver instance;

        [SerializeField] string defaultFilePath = "Assets/saveFile.json";

        Dictionary<ulong, JRaw> jsonDictionary;

        [System.NonSerialized] public bool isFileLoaded;
        void OnEnable()
        {
            if (instance != null && instance != this)
            {
                Destroy(instance);
            }
            instance = this;
            isFileLoaded = false;
            jsonDictionary = new Dictionary<ulong, JRaw>();
        }

        internal void SaveObject(ulong UID, JRaw value)
        {
            jsonDictionary[UID] = value;
        }

        internal JRaw RetrieveObject(ulong UID)
        {
            if (!isFileLoaded)
                return null;

            if (jsonDictionary.TryGetValue(UID, out JRaw value))
                return value;
            else
                return null;
        }

        public static event System.Action OnSaveFileLoaded;
        [NaughtyAttributes.Button("Read")]
        public void readFile(string path = "", bool fireLoadEvent = true)
        {
            if(path == "")
                path = defaultFilePath;
            string allText = "{}";
            if (File.Exists(path))
                allText = File.ReadAllText(path);
            else
                Debug.LogWarning($"No save file found on path \"{path}\". Creating an empty dictionary.");
            jsonDictionary = JsonConvert.DeserializeObject<Dictionary<ulong, JRaw>>(allText);

            isFileLoaded = true;
            if (fireLoadEvent)
                OnSaveFileLoaded?.Invoke();
        }

        public static event System.Action OnSavingGame;

        [NaughtyAttributes.Button("Write")]
        public void writeToFile(string path = "", bool fireSaveEvent = true)
        {
            if(path == "")
                path = defaultFilePath;
            if (fireSaveEvent)
                OnSavingGame?.Invoke();
            string jsonString = JsonConvert.SerializeObject(jsonDictionary);
            File.WriteAllText(path, jsonString);
        }

    }
}