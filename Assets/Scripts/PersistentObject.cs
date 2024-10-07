using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using System.Reflection;
using System.Linq;
using System;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Text;
using System.Globalization;


namespace Persyst
{

    [ExecuteInEditMode]
    [DefaultExecutionOrder(-1)]
    [DisallowMultipleComponent]
    public class PersistentObject : IdentifiableObject
    {
        [SerializeField] bool loadAutomatically = true;
        [SerializeField] bool saveAutomatically = true;
        static BindingFlags bindingFlags = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;



        public void registerCustomSaveEvent(EventHandler eventHandler)
        {
            eventHandler += saveCallback;
        }

        public void removeCustomSaveEvent(EventHandler eventHandler)
        {
            eventHandler -= saveCallback;
        }

        public void registerCustomLoadEvent(EventHandler eventHandler)
        {
            eventHandler += loadCallback;
        }
        public void removeCustomLoadEvent(EventHandler eventHandler)
        {
            eventHandler -= loadCallback;
        }

        //for detecting reference loops
        List<ISaveable> currentSaveableTrace;
        public void SaveObject()
        {
            StringWriter stringWriter = new StringWriter(new StringBuilder(2000), CultureInfo.InvariantCulture);
            using (JsonTextWriter writer = new JsonTextWriter(stringWriter))
            {
                writer.Formatting = GameSaver.jsonSerializer.Formatting;
                writer.WriteStartObject();
                ISaveable[] scriptList = GetComponents<ISaveable>();

                if (scriptList.Length == 0)
                {
                    Debug.LogWarning($"Object {gameObject.name} has a PersistentObject component but no ISaveable MonoBehavious (so no data to save). If you only added the PersistentObject to be able to reference this object from another one, use an IdentifiableObject component instead");
                    return;
                }
                for (int i = 0; i < scriptList.Length; i++)
                {
                    ISaveable script = scriptList[i];
                    // An ISaveable class can request to be omitted from savefiles generated in edit mode (usually, if the values to be serialized only exist after runtime initialization)
                    if (!script.SaveInEditMode && !Application.isPlaying)
                        continue;

                    currentSaveableTrace = new List<ISaveable>();
                    string typeName = $"{script.GetType().FullName}, {script.GetType().Assembly.GetName().Name}";
                    writer.WritePropertyName($"{typeName}");
                    serializeISaveable(script, script.GetType(), false, writer);
                }
                writer.WriteEndObject();

            }

            GameSaver.instance.SaveObject(myUID, new JRaw(stringWriter.ToString()));
        }


        public void LoadObject()
        {
            if (Application.isPlaying && this != null)
                LoadJson(GameSaver.instance.RetrieveObject(myUID));
        }



        bool initialized = false;
        public override void Initialize()
        {
            base.Initialize();
            if (loadAutomatically && !initialized)
            {
                if (GameSaver.instance != null && GameSaver.instance.isFileLoaded)
                    LoadObject();

                GameSaver.OnSaveFileLoaded += LoadObject;
                initialized = true;
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (saveAutomatically)
                GameSaver.OnSavingGame += SaveObject;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            GameSaver.OnSavingGame -= SaveObject;
            GameSaver.OnSaveFileLoaded -= LoadObject;
        }

        void saveCallback(object sender, System.EventArgs args)
        {
            SaveObject();
        }
        void loadCallback(object sender, System.EventArgs args)
        {
            LoadObject();
        }

        //Saving
        //------------------------------------
        //------------------------------------

        static Dictionary<Type, List<SerializationInfo>> memberCache = new();
        struct SerializationInfo
        {
            public MemberInfo memberInfo;
            public List<Type> attributes;
        }

        void serializeISaveable(ISaveable isaveable, Type declaredType, bool asTypeOfInstance, JsonTextWriter writer)
        {
            if (currentSaveableTrace.Any(x => !x.GetType().IsValueType && object.ReferenceEquals(x, isaveable)))
            {
                //Reference Loop!
                Debug.LogWarning($"Reference loop detected in object {isaveable}. Setting reference to null.");
                writer.WriteRawValue("null");
                return;
            }

            currentSaveableTrace.Add(isaveable);

            void cacheSerializationInfo(MemberInfo memberInfo, List<SerializationInfo> serializationInfos)
            {
                bool hasSaveThis = memberInfo.IsDefined(typeof(SaveThis));
                bool hasSaveAsInstanceType = memberInfo.IsDefined(typeof(SaveAsInstanceType));
                bool hasOmitInEditor = memberInfo.IsDefined(typeof(OmitInEditor));
                if (!hasSaveThis && !hasSaveAsInstanceType && !hasOmitInEditor)
                    return;

                SerializationInfo info = new();
                info.memberInfo = memberInfo;
                info.attributes = new();
                if (hasSaveThis)
                    info.attributes.Add(typeof(SaveThis));
                if (hasSaveAsInstanceType)
                    info.attributes.Add(typeof(SaveAsInstanceType));
                if (hasOmitInEditor)
                    info.attributes.Add(typeof(OmitInEditor));
                serializationInfos.Add(info);
            }

            Type typeToUse = asTypeOfInstance ? isaveable.GetType() : declaredType;
            List<SerializationInfo> serializationInfos;
            if (!memberCache.TryGetValue(typeToUse, out serializationInfos))
            {
                serializationInfos = new();
                FieldInfo[] fields = typeToUse.GetFields(bindingFlags);
                PropertyInfo[] properties = typeToUse.GetProperties(bindingFlags);
                for (int i = 0; i < fields.Length; i++)
                    cacheSerializationInfo(fields[i], serializationInfos);
                for (int i = 0; i < properties.Length; i++)
                    cacheSerializationInfo(properties[i], serializationInfos);
                memberCache[typeToUse] = serializationInfos;
            }

            writer.Formatting = GameSaver.jsonSerializer.Formatting;
            writer.WriteStartObject();
            writer.WritePropertyName("class");
            writer.WriteRawValue($"\"{typeToUse.FullName}, {typeToUse.Assembly.GetName().Name}\"");

            void processMember(SerializationInfo serializationInfo)
            {
                bool hasSaveThis = serializationInfo.attributes.Contains(typeof(SaveThis));
                bool hasSaveAsInstanceType = serializationInfo.attributes.Contains(typeof(SaveAsInstanceType));
                bool hasSaveAttribute = hasSaveThis || hasSaveAsInstanceType;
                bool omittedBecauseEditor =
#if PERSYST_OMITTING_ENABLED
                    Application.isEditor && serializationInfo.attributes.Contains(typeof(OmitInEditor))
#else
                        false
#endif
                    ;
                if (!hasSaveAttribute || omittedBecauseEditor)
                    return;

                if (hasSaveThis)
                {
                    object value = ReflectionUtilities.getValue(serializationInfo.memberInfo, isaveable);
                    writer.WritePropertyName($"{serializationInfo.memberInfo.Name}");
                    serializeMember(value, ReflectionUtilities.GetUnderlyingType(serializationInfo.memberInfo), false, writer);
                }
                else if (hasSaveAsInstanceType)
                {
                    object value = ReflectionUtilities.getValue(serializationInfo.memberInfo, isaveable);
                    writer.WritePropertyName($"{serializationInfo.memberInfo.Name}");
                    serializeMember(value, value.GetType(), true, writer);
                }
            }


            for (int i = 0; i < serializationInfos.Count; i++)
            {
                processMember(serializationInfos[i]);
            }
            writer.WriteEndObject();

            currentSaveableTrace.Remove(isaveable);
        }

        void serializeMember(object value, Type type, bool asTypeOfInstance, JsonTextWriter writer)
        {
            if (value == null)
            {
                writer.WriteRawValue("null");
                return;
            }

            //serialize reference
            if (type.IsAssignableTo(typeof(UnityEngine.Object)))
                serializeReference(value as UnityEngine.Object, writer);

            //serialize value
            else
                serializeValue(type, value, asTypeOfInstance, writer);
        }

        void serializeValue(Type type, object value, bool asTypeOfInstance, JsonTextWriter writer)
        {
            if (type.GetInterfaces().Contains(typeof(ISaveable)))
                serializeISaveable(value as ISaveable, type, asTypeOfInstance, writer); //recursion!
            else if (type.GetInterfaces().Contains(typeof(ICollection)))
                serializeCollection(value as ICollection, type, asTypeOfInstance, writer);
            else
                SerializeObjectInternal(value, type, GameSaver.jsonSerializer, writer);
        }

        void serializeCollection(ICollection collection, Type collectionType, bool asTypeOfInstance, JsonTextWriter writer)
        {
            if (collection == null)
            {
                writer.WriteRawValue("null");
                return;
            }

            if ((collectionType.IsConstructedGenericType &&
                    collectionType.GetGenericArguments().Any(t => t.IsAssignableTo(typeof(UnityEngine.Object)))
                )
                ||
                collectionType.IsArray && collectionType.GetElementType().IsAssignableTo(typeof(UnityEngine.Object))
            )
            {
                Debug.LogError("Collections of UnityEngine.Object are not supported. You can get around this by using an ISaveable wrapper (e.g. List<RefWrapper<GameObject>>");
                writer.WriteRawValue("null");
                return;
            }

            //dictionaries and lists of keyValuePairs
            if (collectionType.IsConstructedGenericType &&
                    (
                        collectionType.GetInterfaces().Contains(typeof(IDictionary))
                        ||
                        collectionType.GenericTypeArguments.Any(t => t.isConstructedFrom(typeof(KeyValuePair<,>)))
                    )
            )
            {

                Type typeOfKey = collectionType.GetGenericArguments()[0];
                Type typeOfValue = collectionType.GetGenericArguments()[1];
                writer.WriteStartArray();
                foreach (object entry in collection)
                {
                    writer.WriteStartObject();
                    writer.WritePropertyName("Key");
                    object Key = entry.GetType().GetProperty("Key").GetValue(entry);
                    serializeMember(Key, typeOfKey, asTypeOfInstance, writer);

                    writer.WritePropertyName("Value");
                    object Value = entry.GetType().GetProperty("Value").GetValue(entry);
                    serializeMember(Value, typeOfValue, asTypeOfInstance, writer);
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
            }
            //lists, sets, stacks, etc
            else
            {
                Type elementType = collectionType.IsArray ? collectionType.GetElementType() : collectionType.GetGenericArguments()[0];
                writer.WriteStartArray();
                foreach (object element in collection)
                {
                    serializeMember(element, elementType, asTypeOfInstance, writer);
                }
                writer.WriteEndArray();
            }
        }

        void serializeReference(UnityEngine.Object value, JsonTextWriter writer)
        {
            if (value == null)
            {
                writer.WriteRawValue("null");
                return;
            }

            Type type = value.GetType();
            if (type.IsAssignableTo(typeof(Component)) || type.IsAssignableTo(typeof(GameObject)))
            {
                MethodInfo method = type.GetMethod("GetComponent", 1, new Type[] { }).MakeGenericMethod(typeof(IdentifiableObject));
                IdentifiableObject identifiableObject = (IdentifiableObject)method.Invoke(value, new object[] { });
                if (!identifiableObject)
                {
                    Debug.LogError($"Trying to serialize reference to object {value.name}, which does not have an IdentifiableObject or PersistentObject component! Value will be null");
                    writer.WriteRawValue("null");
                    return;
                }
                long uid = identifiableObject.GetUID();
                writer.WriteValue(uid);
                return;
            }
            else if (type.IsAssignableTo(typeof(IdentifiableScriptableObject)))
            {
                writer.WriteValue((value as IdentifiableScriptableObject).GetUID());
                return;
            }

            Debug.LogError($"Serializing a reference to a UnityEngine.Object that is not a Component, GameObject or SerializableScriptableObject is not supported. Type: {type}");
            writer.WriteRawValue("null");
        }

        private void SerializeObjectInternal(object value, Type type, JsonSerializer jsonSerializer, JsonTextWriter writer)
        {
            jsonSerializer.Serialize(writer, value, type);
        }

        //Loading
        void LoadJson(JRaw jsonString)
        {
            if (jsonString == null)
                return;
            Dictionary<string, JRaw> jsonDict = JsonConvert.DeserializeObject<Dictionary<string, JRaw>>(jsonString.ToString());

            foreach (KeyValuePair<string, JRaw> entry in jsonDict)
            {
                Type type = Type.GetType(entry.Key);
                MethodInfo method = typeof(GameObject).GetMethod("GetComponent", 1, new Type[] { }).MakeGenericMethod(type);
                object script = method.Invoke(gameObject, new object[] { });
                if (script == null)
                    script = typeof(GameObject).GetMethod("AddComponent", 1, new Type[] { }).MakeGenericMethod(type).Invoke(gameObject, new object[] { });
                DeserializeISaveable(ref script, entry.Value);
            }
        }

        void DeserializeISaveable(ref object script, JRaw jsonString)
        {
            if (jsonString.ToString() == "null")
                return;

            Dictionary<string, JRaw> jsonDict = JsonConvert.DeserializeObject<Dictionary<string, JRaw>>(jsonString.ToString());
            string typeName = JsonConvert.DeserializeObject<string>(jsonDict["class"].ToString());
            Type serializedType = Type.GetType(typeName);
            if (serializedType == null)
            {
                Debug.LogError($"Cannot find type {typeName} when deserializing {script}");
                return;
            }
            jsonDict.Remove("class");

            //if the object was serialized with a type that's different from the type of its current value, create a new one of the serialized type
            //this is a slightly dangerous thing, because it means you can potentially lose information that you had already assigned to the object prior to loading the save file
            //but the alternative is worse, so.
            if (script == null || serializedType != script.GetType())
                script = Activator.CreateInstance(serializedType);

            foreach (KeyValuePair<string, JRaw> entry in jsonDict)
            {
                MemberInfo memberInfo = null;
                memberInfo = serializedType.GetField(entry.Key, bindingFlags);
                if (memberInfo == null)
                    memberInfo = serializedType.GetProperty(entry.Key, bindingFlags);
                if (memberInfo == null)
                {
                    Debug.LogError($"Serialized member \"{entry.Key}\" not found on type \"{serializedType}\". Ignoring it.");
                    continue;
                }
                object value = DeserializeMember(ref script, memberInfo, entry.Value);
                ReflectionUtilities.setValue(memberInfo, script, value);
            }
        }

        object DeserializeMember(ref object script, MemberInfo memberInfo, JRaw jsonValue)
        {
            Type variableType = ReflectionUtilities.GetUnderlyingType(memberInfo);
            //serialized reference
            if (variableType.IsAssignableTo(typeof(UnityEngine.Object)))
            {
                if (long.TryParse(jsonValue.ToString(), out long ref_UID))
                {
                    return DeserializeReference(ref script, ref_UID, memberInfo);
                }
                return null;
            }
            //serialized value
            else
            {
                object value = ReflectionUtilities.getValue(memberInfo, script);
                return DeserializeValue(variableType, value, jsonValue);
            }
        }
        object DeserializeReference(ref object script, long ref_UID, MemberInfo memberInfo)
        {
            Type variableType = ReflectionUtilities.GetUnderlyingType(memberInfo);
            UnityEngine.Object referencedObject = UIDManager.instance.GetObject(ref_UID);
            if (referencedObject == null)
            {
                UIDManager.instance.registerPendingReference(script, ref_UID, memberInfo);
                return null;
            }

            if (variableType.IsAssignableTo(typeof(Component)))
            {
                MethodInfo method = typeof(GameObject).GetMethod("GetComponent", 1, new Type[] { }).MakeGenericMethod(variableType);
                object referencedComponent = method.Invoke(referencedObject, new object[] { });
                return referencedComponent;
            }
            else
                return referencedObject;
        }

        object DeserializeValue(Type variableType, object currentValue, JRaw jraw)
        {

            if (variableType.GetInterfaces().Contains(typeof(ISaveable)))
            {
                DeserializeISaveable(ref currentValue, jraw); //recursion!
                return currentValue;
            }
            else if (variableType.GetInterfaces().Contains(typeof(ICollection)))
            {
                return DeserializeCollection(variableType, jraw);
            }
            else
            {
                MethodInfo method = typeof(JsonConvert).GetMethod("DeserializeObject", 1, new Type[] { typeof(string) }).MakeGenericMethod(variableType);
                object value = method.Invoke(null, new object[] { jraw.ToString() });
                return value;
            }
        }

        object DeserializeCollection(Type collectionType, JRaw collectionJson)
        {
            //ICollection collection = (ICollection) Activator.CreateInstance(variableType);
            List<JRaw> jrawCollection = JsonConvert.DeserializeObject<List<JRaw>>(collectionJson.ToString());
            if (jrawCollection == null)
                return null;
            Type elementType = ReflectionUtilities.getCollectionElementType(collectionType);

            List<object> tempList = new List<object>();
            foreach (JRaw jrawElement in jrawCollection)
            {
                object element;
                if (elementType.isConstructedFrom(typeof(KeyValuePair<,>)))
                {
                    var kvP_jraw = JsonConvert.DeserializeObject<KeyValuePair<JRaw, JRaw>>(jrawElement.ToString());
                    object key = DeserializeCollectionElement(elementType.GetGenericArguments()[0], kvP_jraw.Key);
                    object value = DeserializeCollectionElement(elementType.GetGenericArguments()[1], kvP_jraw.Value);
                    element = Activator.CreateInstance(elementType, key, value);
                }
                else
                    element = DeserializeCollectionElement(elementType, jrawElement);

                tempList.Add(element);
            }

            return ICollectionExtension.fromList(collectionType, tempList);
        }

        object DeserializeCollectionElement(Type elementType, JRaw jrawElement)
        {
            object value;
            if (jrawElement.ToString() == "null")
                return null;
            else if (elementType.GetInterfaces().Contains(typeof(ISaveable)))
            {
                value = Activator.CreateInstance(elementType);
                DeserializeISaveable(ref value, jrawElement);
            }
            else
                value = DeserializeValue(elementType, null, jrawElement);

            return value;
        }

    }
}