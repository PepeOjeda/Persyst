using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;

namespace Persyst{
    [DefaultExecutionOrder(-10)]
    public class GameSaver : MonoBehaviour
    {
        public static GameSaver instance;
        public static event System.Action OnSaveFileLoaded;

        Dictionary<ulong, JRaw> jsonDictionary;

        [System.NonSerialized] public bool isFileLoaded;
        void Start()
        {
            if(instance!=null && instance!=this){
                Destroy(instance);
            }
            instance = this;
            isFileLoaded = false;
        }

        public void SaveObject(ulong UID, JRaw value){
            jsonDictionary[UID] = value;
        }

        public JRaw RetrieveObject(ulong UID){
            if(!Application.isPlaying)
                return null;

            if(jsonDictionary == null){
                Debug.LogWarning("No savegame dictionary has been loaded! (readFile() was never called) Creating an empty one.");
                jsonDictionary = new Dictionary<ulong, JRaw>();
            } 

            if(jsonDictionary.TryGetValue(UID, out JRaw value))
                return value;
            else
                return null;
        }

        [NaughtyAttributes.Button("Read")]
        void readFile(string path = "Assets/saveFile.json"){
            string allText="{}";
            if(File.Exists(path))
                allText = File.ReadAllText(path);
            else
                Debug.LogWarning($"No save file found on path \"{path}\". Creating an empty dictionary.");
            jsonDictionary = JsonConvert.DeserializeObject<Dictionary<ulong,JRaw>>(allText);

            isFileLoaded = true;
            OnSaveFileLoaded?.Invoke();
        }

        public static event System.Action saveTheGame;

        [NaughtyAttributes.Button("Write")]
        void writeToFile(string path = "Assets/saveFile.json"){
            if(!Application.isPlaying){
                Debug.Log("Can only save during play mode");
                return;
            }

            saveTheGame?.Invoke();
            string jsonString = JsonConvert.SerializeObject(jsonDictionary);
            File.WriteAllText(path, jsonString);
        }

    }
}