using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Text;
using System.Globalization;
using System.Threading.Tasks;

namespace Persyst
{
    [DefaultExecutionOrder(-10)]
    [ExecuteInEditMode]
    public class GameSaver : MonoBehaviour
    {
        public static GameSaver instance;

        [SerializeField] string defaultFilePath = "Assets/saveFile.json";

        Dictionary<long, JRaw> jsonDictionary;


        
        public static JsonSerializerSettings regularSerializerSettings = new JsonSerializerSettings
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            ContractResolver = new ForceJSONSerializePrivatesResolver(),
            TypeNameHandling = TypeNameHandling.All
        };
        public static JsonSerializer jsonSerializer;


        public bool isFileLoaded{get; private set;}
        void OnEnable()
        {
            if (instance != null && instance != this)
            {
                Destroy(instance);
            }
            instance = this;
            isFileLoaded = false;
            jsonDictionary = new Dictionary<long, JRaw>();

            // initialize here rather than giving a construct-time value because we need the UnityConverterInitializer 
            // from the json-for-unity.converters package to run before we create the serializer
            jsonSerializer = JsonSerializer.CreateDefault(regularSerializerSettings); 
        }

        internal void SaveObject(long UID, JRaw value)
        {
            jsonDictionary[UID] = value;
        }

        internal JRaw RetrieveObject(long UID)
        {
            if (!isFileLoaded)
                return null;

            if (jsonDictionary.TryGetValue(UID, out JRaw value))
                return value;
            else
                return null;
        }

#region Loading
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
            ParseJsonString(allText);

            if (fireLoadEvent)
                OnSaveFileLoaded?.Invoke();
        }

        public void ParseJsonString(string jsonString)
        {
            jsonDictionary = JsonConvert.DeserializeObject<Dictionary<long, JRaw>>(jsonString);
            isFileLoaded = true;
        }

        public void FireLoadEvent()
        {
            OnSaveFileLoaded?.Invoke();
        }
#endregion
        

#region Saving
        public enum Formatting {Raw, Pretty}
        public static event System.Action OnSavingGame;


        /// <summary>
        /// (optionally) Gets all the data from PersistentObjects with LoadAutomatically enabled, and writes to file.
        /// </summary>
        [NaughtyAttributes.Button("Write")] 
        public void writeToFile(string path = "", bool fireSaveEvent = true, Formatting formatting = Formatting.Raw)
        {
            if(path == "")
                path = defaultFilePath;
            if (fireSaveEvent)
                OnSavingGame?.Invoke();

            string jsonString = GetJsonString(formatting);
            File.WriteAllText(path, jsonString);
        }

        /// <summary>
        /// Turns the current contents of the dictionary to a formatted json string. Does not gather the data from PersistentObjects or write to file.
        /// </summary>
        public string GetJsonString(Formatting formatting = Formatting.Raw)
        {
            StringWriter stringWriter = new StringWriter(new StringBuilder(1000), CultureInfo.InvariantCulture);
            using (JsonTextWriter writer = new JsonTextWriter(stringWriter))
            {
                writer.Formatting = jsonSerializer.Formatting;
                writer.WriteStartObject();
                foreach(var pair in jsonDictionary)
                {
                    writer.WritePropertyName($"{pair.Key}");
                    writer.WriteRawValue(pair.Value.ToString());
                }
                writer.WriteEndObject();
                if(formatting == Formatting.Pretty)
                {
                    return JToken.Parse(stringWriter.ToString()).ToString();
                }
                else 
                {
                    return stringWriter.ToString();
                }
            }
        }

        public void FireSaveEvent()
        {
            OnSavingGame?.Invoke();
        }
#endregion

        [NaughtyAttributes.Button("Empty dictionary")]
        void EmptyDictionary()
        {
            jsonDictionary = new();
            Debug.LogWarning("Deleted all data in the dictionary");
        }
    }
}