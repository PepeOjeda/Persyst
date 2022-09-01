using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;

namespace Persyst{
    [DefaultExecutionOrder(-10)][ExecuteInEditMode]
    public class GameSaver : MonoBehaviour
    {
        public static GameSaver instance;
        public static event System.Action OnSaveFileLoaded;

        Dictionary<ulong, JRaw> jsonDictionary;

        [System.NonSerialized] public bool isFileLoaded;
        void OnEnable()
        {
            if(instance!=null && instance!=this){
                Destroy(instance);
            }
            instance = this;
            isFileLoaded = false;
            jsonDictionary = new Dictionary<ulong, JRaw>();
        }

        public void SaveObject(ulong UID, JRaw value){
            jsonDictionary[UID] = value;
        }

        public JRaw RetrieveObject(ulong UID){
            if(!isFileLoaded)
                return null;

            if(jsonDictionary.TryGetValue(UID, out JRaw value))
                return value;
            else
                return null;
        }

        [NaughtyAttributes.Button("Read")]
        public void readFile(string path = "Assets/saveFile.json", bool fireLoadEvent = true){
            string allText="{}";
            if(File.Exists(path))
                allText = File.ReadAllText(path);
            else
                Debug.LogWarning($"No save file found on path \"{path}\". Creating an empty dictionary.");
            jsonDictionary = JsonConvert.DeserializeObject<Dictionary<ulong,JRaw>>(allText);

            isFileLoaded = true;
            if(fireLoadEvent)
                OnSaveFileLoaded?.Invoke();
        }

        public static event System.Action saveTheGame;

        [NaughtyAttributes.Button("Write")]
        public void writeToFile(string path = "Assets/saveFile.json", bool fireSaveEvent = true){
            if(fireSaveEvent)
				saveTheGame?.Invoke();
            string jsonString = JsonConvert.SerializeObject(jsonDictionary);
            File.WriteAllText(path, jsonString);
        }

    }
}