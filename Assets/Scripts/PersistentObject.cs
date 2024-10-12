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
                    Type type = script.GetType();
                    ReflectionData reflectionData = GetReflectionData(type);
                    writer.WritePropertyName(reflectionData.fullName);
                    serializeISaveable(script, type, false, writer);
                }
                writer.WriteEndObject();

            }

            GameSaver.instance.SaveObject(myUID, JObject.Parse(stringWriter.ToString())); //TODO check this
        }


        public void LoadObject()
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            if (Application.isPlaying && this != null)
                LoadJson(GameSaver.instance.RetrieveObject(myUID));
            stopwatch.Stop();
            // Debug.Log($"Deserializing object {myUID} took {stopwatch.Elapsed.TotalMilliseconds}ms");
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

        static Dictionary<Type, ReflectionData> reflectionCache = new();
        class ReflectionData
        {
            public string fullName;
            public List<MemberData> memberDatas;
        }
        struct MemberData
        {
            public MemberInfo memberInfo;
            public List<Type> attributes;
        }

        void serializeISaveable(ISaveable isaveable, Type declaredType, bool asTypeOfInstance, JsonTextWriter writer)
        {
            // if the class implements manual serialization, we do not need to do anything! :)
            if (isaveable.Serialize(writer) == ISaveable.OperationStatus.Done)
                return;

            if (currentSaveableTrace.Any(x => !x.GetType().IsValueType && object.ReferenceEquals(x, isaveable)))
            {
                //Reference Loop!
                Debug.LogWarning($"Reference loop detected in object {isaveable}. Setting reference to null.");
                writer.WriteRawValue("null");
                return;
            }

            currentSaveableTrace.Add(isaveable);

            Type typeToUse = asTypeOfInstance ? isaveable.GetType() : declaredType;
            ReflectionData reflectionData = GetReflectionData(typeToUse);

            writer.Formatting = GameSaver.jsonSerializer.Formatting;
            writer.WriteStartObject();
            writer.WritePropertyName("class");
            writer.WriteValue(reflectionData.fullName);

            void processMember(MemberData serializationInfo)
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


            for (int i = 0; i < reflectionData.memberDatas.Count; i++)
            {
                processMember(reflectionData.memberDatas[i]);
            }
            writer.WriteEndObject();

            currentSaveableTrace.Remove(isaveable);
        }

        ReflectionData GetReflectionData(Type type)
        {
            void cacheSerializationInfo(MemberInfo memberInfo, List<MemberData> memberDatas)
            {
                bool hasSaveThis = memberInfo.IsDefined(typeof(SaveThis));
                bool hasSaveAsInstanceType = memberInfo.IsDefined(typeof(SaveAsInstanceType));
                bool hasOmitInEditor = memberInfo.IsDefined(typeof(OmitInEditor));
                if (!hasSaveThis && !hasSaveAsInstanceType && !hasOmitInEditor)
                    return;

                MemberData data = new();
                data.memberInfo = memberInfo;
                data.attributes = new();
                if (hasSaveThis)
                    data.attributes.Add(typeof(SaveThis));
                if (hasSaveAsInstanceType)
                    data.attributes.Add(typeof(SaveAsInstanceType));
                if (hasOmitInEditor)
                    data.attributes.Add(typeof(OmitInEditor));
                memberDatas.Add(data);
            }

            ReflectionData reflectionData;
            if (!reflectionCache.TryGetValue(type, out reflectionData))
            {
                reflectionData = new()
                {
                    fullName = $"{type.FullName}, {type.Assembly.GetName().Name}",
                    memberDatas = new()
                };
                FieldInfo[] fields = type.GetFields(bindingFlags);
                PropertyInfo[] properties = type.GetProperties(bindingFlags);
                for (int i = 0; i < fields.Length; i++)
                    cacheSerializationInfo(fields[i], reflectionData.memberDatas);
                for (int i = 0; i < properties.Length; i++)
                    cacheSerializationInfo(properties[i], reflectionData.memberDatas);
                reflectionCache[type] = reflectionData;
            }

            return reflectionData;
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
            if (ReflectionUtilities.GetInterfaces(type).Contains(typeof(ISaveable)))
                serializeISaveable(value as ISaveable, type, asTypeOfInstance, writer); //recursion!
            else if (ReflectionUtilities.GetInterfaces(type).Contains(typeof(ICollection)))
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
                    ReflectionUtilities.GetGenericArguments(collectionType).Any(t => t.IsAssignableTo(typeof(UnityEngine.Object)))
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
                        ReflectionUtilities.GetInterfaces(collectionType).Contains(typeof(IDictionary))
                        ||
                        collectionType.GenericTypeArguments.Any(t => t.isConstructedFrom(typeof(KeyValuePair<,>)))
                    )
            )
            {

                Type typeOfKey = ReflectionUtilities.GetGenericArguments(collectionType)[0];
                Type typeOfValue = ReflectionUtilities.GetGenericArguments(collectionType)[1];
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
                Type elementType = collectionType.IsArray ? collectionType.GetElementType() : ReflectionUtilities.GetGenericArguments(collectionType)[0];
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
        void LoadJson(JObject jObject)
        {
            if (jObject == null)
                return;

            foreach (JProperty property in jObject.Properties())
            {
                Type type = Type.GetType(property.Name);
                MethodInfo method = typeof(GameObject).GetMethod("GetComponent", 1, new Type[] { }).MakeGenericMethod(type);
                ISaveable script = (ISaveable)method.Invoke(gameObject, new object[] { });
                if (script == null)
                    script = (ISaveable)typeof(GameObject).GetMethod("AddComponent", 1, new Type[] { }).MakeGenericMethod(type).Invoke(gameObject, new object[] { });

                // if the class implements manual deserialization, let's just do that!
                if (script.Deserialize(property.Value) == ISaveable.OperationStatus.Done)
                    continue;

                object asObject = script;
                DeserializeISaveable(ref asObject, property.Value as JObject);
            }
        }

        void DeserializeISaveable(ref object script, JObject jObject)
        {
            if (jObject == null)
                return;

            string typeName = jObject["class"].Value<string>();
            Type serializedType = Type.GetType(typeName);
            if (serializedType == null)
            {
                Debug.LogError($"Cannot find type {typeName} when deserializing {script}");
                return;
            }

            //if the object was serialized with a type that's different from the type of its current value, create a new one of the serialized type
            //this is a slightly dangerous thing, because it means you can potentially lose information that you had already assigned to the object prior to loading the save file
            //but the alternative is worse, so.
            if (script == null || serializedType != script.GetType())
                script = Activator.CreateInstance(serializedType);

            foreach (JProperty property in jObject.Properties())
            {
                if (property.Name == "class")
                    continue;

                MemberInfo memberInfo = serializedType.GetField(property.Name, bindingFlags); // is it a field?
                if (memberInfo == null)
                    memberInfo = serializedType.GetProperty(property.Name, bindingFlags); // is it a property?
                if (memberInfo == null)
                {
                    Debug.LogError($"Serialized member \"{property.Name}\" not found on type \"{serializedType}\". Ignoring it.");
                    continue;
                }
                object value = DeserializeMember(ref script, memberInfo, property.Value);
                ReflectionUtilities.setValue(memberInfo, script, value);
            }
        }

        object DeserializeMember(ref object script, MemberInfo memberInfo, JToken jToken)
        {
            Type variableType = ReflectionUtilities.GetUnderlyingType(memberInfo);
            //serialized reference
            if (variableType.IsAssignableTo(typeof(UnityEngine.Object)))
            {
                try
                {
                    return DeserializeReference(ref script, jToken.Value<long>(), memberInfo);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Exception {e.Message} when tring to read UID: {jToken}");
                    return null;
                }
            }
            //serialized value
            else
            {
                object value = ReflectionUtilities.getValue(memberInfo, script);
                return DeserializeValue(variableType, value, jToken);
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

        object DeserializeValue(Type variableType, object currentValue, JToken jToken)
        {
            if (ReflectionUtilities.GetInterfaces(variableType).Contains(typeof(ISaveable)))
            {
                DeserializeISaveable(ref currentValue, jToken as JObject); //recursion!
                return currentValue;
            }
            else if (ReflectionUtilities.GetInterfaces(variableType).Contains(typeof(ICollection)))
            {
                return DeserializeCollection(variableType, jToken as JArray);
            }
            else
            {
                //this seems to be a bit of a hotspot because of the jsonSerializer having to iterate over all converters to find the right one for the variableType
                // I tried caching and whatnot, but it turns out most of the time it ends up not finding any matching converter at all and instead calling a manual serialization function (which is internal, so we can't just do that ourselves)
                // It might be worth looking into if we ever want to optimize deserialization any further
                return jToken.ToObject(variableType, GameSaver.jsonSerializer); 
            }
        }

        object DeserializeCollection(Type collectionType, JArray collectionJson)
        {
            if (collectionJson == null)
                return null;
            Type elementType = ReflectionUtilities.getCollectionElementType(collectionType);

            List<object> tempList = new List<object>();
            foreach (JToken elementToken in collectionJson)
            {
                object element;
                if (elementType.isConstructedFrom(typeof(KeyValuePair<,>)))
                {
                    JObject kvP = elementToken as JObject;
                    object key = DeserializeCollectionElement(ReflectionUtilities.GetGenericArguments(elementType)[0], kvP["Key"]);
                    object value = DeserializeCollectionElement(ReflectionUtilities.GetGenericArguments(elementType)[1], kvP["Value"]);
                    element = Activator.CreateInstance(elementType, key, value);
                }
                else
                    element = DeserializeCollectionElement(elementType, elementToken);

                tempList.Add(element);
            }

            return ICollectionExtension.fromList(collectionType, tempList);
        }

        object DeserializeCollectionElement(Type elementType, JToken jToken)
        {
            object value = null;
            if (jToken == null)
                return null;
            else if (ReflectionUtilities.GetInterfaces(elementType).Contains(typeof(ISaveable)))
            {
                DeserializeISaveable(ref value, jToken as JObject);
            }
            else
                value = DeserializeValue(elementType, null, jToken);

            return value;
        }

    }
}