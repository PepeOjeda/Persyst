using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using System.Reflection;
using System.Linq;
using System;
using Newtonsoft.Json.Linq;


namespace Persyst{

    [ExecuteInEditMode][DefaultExecutionOrder(-1)][DisallowMultipleComponent]
    public class PersistentObject : MonoBehaviour
    {
        [SerializeField] public ulong myUID;
		[SerializeField] bool loadAutomatically = true;
		[SerializeField] bool saveAutomatically = true;
        [SerializeField][HideInInspector] bool assigned=false;
        static BindingFlags bindingFlags = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;
        static JsonSerializerSettings regularSerializerSettings = new JsonSerializerSettings{ReferenceLoopHandling = ReferenceLoopHandling.Ignore, 
            ContractResolver = new ForceJSONSerializePrivatesResolver(),
            TypeNameHandling = TypeNameHandling.All};

		
		
        public void registerCustomSaveEvent(System.EventHandler eventHandler){
			eventHandler += saveCallback;
		}

		public void removeCustomSaveEvent(System.EventHandler eventHandler){
			eventHandler -= saveCallback;
		}

		public void registerCustomLoadEvent(System.EventHandler eventHandler){
			eventHandler += loadCallback;
		}
		public void removeCustomLoadEvent(System.EventHandler eventHandler){
			eventHandler -= loadCallback;
		}

		List<ISaveable> currentSaveableTrace;
        public void SaveObject(){
            Dictionary<string,JRaw> savedScripts = new Dictionary<string, JRaw>();
            ISaveable[] scriptList = GetComponents<ISaveable>();

            foreach(var script in scriptList){
				currentSaveableTrace = new List<ISaveable>();
                string typeName = $"{script.GetType().FullName}, {script.GetType().Assembly.GetName().Name}";
                savedScripts[typeName] = serializeISaveable(script, script.GetType(), false);
            }
            
            GameSaver.instance.SaveObject(myUID, new JRaw(JsonConvert.SerializeObject(savedScripts, Formatting.Indented)) );
        }

        
		public void LoadObject(){
            if(Application.isPlaying && this != null)
                LoadJson(GameSaver.instance.RetrieveObject(myUID));
        }




		bool initializeOnStart = false;

		void Awake(){
			if(UIDManager.instance!=null)
				initializeOnStart = true;
			else
				UIDManager.OnManagerAvailable += Initialize;
		}
		void Start(){
			if(initializeOnStart)
				Initialize();
		}

        public void Initialize(){
			UIDManager.OnManagerAvailable -= Initialize;
            if(assigned){
                UIDManager.instance.refreshReference(gameObject, myUID);
				if(loadAutomatically){
					if(GameSaver.instance!=null && GameSaver.instance.isFileLoaded)
                    	LoadObject();

                	GameSaver.OnSaveFileLoaded += LoadObject;
				}
                return;
            }
            myUID=UIDManager.instance.generateUID(gameObject);
            
            bool shouldsetAssigned = true;

            #if UNITY_EDITOR
            shouldsetAssigned = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage() == null; //don't set the "assigned" flag if opening a prefab asset
            #endif

            if(shouldsetAssigned)
                assigned=true;
        }

        

        void OnEnable(){
			if(saveAutomatically)
            	GameSaver.OnSavingGame += SaveObject;
        }

        void OnDestroy(){
            if(saveAutomatically)
				GameSaver.OnSavingGame -= SaveObject;
            
			if(!Application.isPlaying && gameObject.scene.isLoaded){
                UIDManager.instance.removeUID(myUID);
                assigned = false;
            }
        }
        
		void saveCallback(object sender, System.EventArgs args){
			SaveObject();
		}
		void loadCallback(object sender, System.EventArgs args){
			LoadObject();
		}

        //Saving
        JRaw serializeISaveable(ISaveable isaveable, Type delaredType, bool asTypeOfInstance){
			if( currentSaveableTrace.Any(x=> !x.GetType().IsValueType && object.ReferenceEquals(x, isaveable)) ){
				//Reference Loop!
				Debug.LogWarning($"Reference loop detected in object {isaveable}. Setting reference to null.");
				return new JRaw("null");
			}

			currentSaveableTrace.Add(isaveable);

            Dictionary<string, JRaw> jsonDict = new Dictionary<string, JRaw>();

            Type typeToUse = asTypeOfInstance? isaveable.GetType() : delaredType;
            var members = typeToUse.GetMembers(bindingFlags);
            jsonDict["class"] = new JRaw($"\"{typeToUse.FullName}, {typeToUse.Assembly.GetName().Name}\"");

            foreach(MemberInfo memberInfo in members){
                if(  !( memberInfo.IsDefined(typeof(SaveThis)) | memberInfo.IsDefined(typeof(SaveAsInstanceType)) )  )
                    continue;

                object value = ReflectionUtilities.getValue(memberInfo, isaveable);
                if( memberInfo.IsDefined(typeof(SaveThis)) )
                    jsonDict[memberInfo.Name] = serializeMember(value, ReflectionUtilities.GetUnderlyingType(memberInfo), false);
                else if( memberInfo.IsDefined(typeof(SaveAsInstanceType)) )
                    jsonDict[memberInfo.Name] = serializeMember(value, value.GetType(), true);
            }

			currentSaveableTrace.Remove(isaveable);
            return new JRaw(JsonConvert.SerializeObject(jsonDict));
        }
        JRaw serializeMember(object value, Type type, bool asTypeOfInstance){
            if(value==null)
                return null;

            //serialize reference
            if( type.IsAssignableTo(typeof(UnityEngine.Object)) ){
                return serializeReference(value as UnityEngine.Object);
            }

            //serialize value
            else 
                return serializeValue(type, value, asTypeOfInstance);
        }

        JRaw serializeValue(Type type, object value, bool asTypeOfInstance){
            if( type.GetInterfaces().Contains(typeof(ISaveable)) ){
                return new JRaw(serializeISaveable(value as ISaveable, type, asTypeOfInstance) ); //recursion!
            }
            else if(type.GetInterfaces().Contains(typeof(ICollection))){
                return serializeCollection(value as ICollection, type, asTypeOfInstance);
            }
            else{
                return new JRaw( JsonConvert.SerializeObject(value, regularSerializerSettings));
            }
        }

        JRaw serializeCollection(ICollection collection, Type collectionType, bool asTypeOfInstance){
            if(collection == null)
                return null;
            
            if((collectionType.IsConstructedGenericType && 
                    collectionType.GetGenericArguments().Any(t => t.IsAssignableTo(typeof(UnityEngine.Object)) )  
                )
                ||
                collectionType.IsArray && collectionType.GetElementType().IsAssignableTo(typeof(UnityEngine.Object))
            ){
                Debug.LogError("Collections of UnityEngine.Object are not supported. You can get around this by using an ISaveable wrapper (e.g. List<RefWrapper<GameObject>>");
                return null;
            }

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            bool firstElement=true; //for commas

            //dictionaries and lists of keyValuePairs
            if( collectionType.IsConstructedGenericType && 
                    (
                        collectionType.GetInterfaces().Contains(typeof(IDictionary))
                        ||
                        collectionType.GenericTypeArguments.Any(t => t.isConstructedFrom(typeof(KeyValuePair<,>)) )
                    ) 
            ){
                
                Type typeOfKey = collectionType.GetGenericArguments()[0];
                Type typeOfValue = collectionType.GetGenericArguments()[1];
                sb.Append("[");
                foreach(object entry in collection){
                    if(!firstElement)
                        sb.Append(",");
                    firstElement = false;

                    object Key = entry.GetType().GetProperty("Key").GetValue(entry);
                    object Value = entry.GetType().GetProperty("Value").GetValue(entry);
                    sb.Append("{\"Key\":"+serializeMember(Key, typeOfKey, asTypeOfInstance)+",");
                    sb.Append("\"Value\":"+serializeMember(Value, typeOfValue, asTypeOfInstance)+"}");
                }
                sb.Append("]");
            }
            //lists, sets, stacks, etc
            else{
                Type elementType = collectionType.IsArray? collectionType.GetElementType() : collectionType.GetGenericArguments()[0];
                sb.Append("[");
                foreach(object element in collection){
                    if(!firstElement)
                        sb.Append(",");
                    firstElement = false;
                    sb.Append(serializeMember(element, elementType, asTypeOfInstance));
                }
                sb.Append("]");
            }

            return new JRaw(sb.ToString());
        }

        JRaw serializeReference(UnityEngine.Object value){
            if(value == null){
                return null;
            }

            Type type = value.GetType();
            if( type.IsAssignableTo(typeof(UnityEngine.Component)) || type.IsAssignableTo(typeof(GameObject)) ){
                MethodInfo method =  type.GetMethod("GetComponent", 1, new Type[]{}).MakeGenericMethod(typeof(PersistentObject));
                PersistentObject persistentObject = (PersistentObject) method.Invoke(value, new object[]{});
                ulong uid = persistentObject.myUID;
                return new JRaw( uid.ToString() );
            }
            else if( type.IsAssignableTo(typeof(SerializableScriptableObject))  ){
                return new JRaw( (value as SerializableScriptableObject).myUID.ToString() );
            }

            Debug.LogError($"Serializing a reference to a UnityEngine.Object that is not a Component, GameObject or SerializableScriptableObject is not supported. Type: {type}");
            return null;
        }

        //Loading
        void LoadJson(JRaw jsonString){
            if(jsonString==null)
                return;
            Dictionary<string, JRaw> jsonDict = JsonConvert.DeserializeObject<Dictionary<string, JRaw>>(jsonString.ToString());

            foreach(KeyValuePair<string, JRaw> entry in jsonDict){
                Type type = Type.GetType(entry.Key);
                MethodInfo method =  typeof(GameObject).GetMethod("GetComponent", 1, new Type[]{}).MakeGenericMethod(type);
                object script = method.Invoke(gameObject, new object[]{});
                if(script==null)
                    script = typeof(GameObject).GetMethod("AddComponent", 1, new Type[]{}).MakeGenericMethod(type).Invoke(gameObject, new object[]{});
                DeserializeISavable(ref script, entry.Value);
            }
        }

        void DeserializeISavable(ref object script, JRaw jsonString){
            Dictionary<string, JRaw> jsonDict = JsonConvert.DeserializeObject<Dictionary<string, JRaw>>(jsonString.ToString());
            string typeName = JsonConvert.DeserializeObject<string>( jsonDict["class"].ToString() );
            Type serializedType = Type.GetType(typeName);
            jsonDict.Remove("class");

            //if the object was serialized with a type that's different from the type of its current value, create a new one of the serialized type
            //this is a slightly dangerous thing, because it means you can potentially lose information that you had already assigned to the object prior to loading the save file
            //but the alternative is worse, so.
            if(script == null || serializedType != script.GetType() ) 
                script = Activator.CreateInstance(serializedType);

            foreach(KeyValuePair<string, JRaw> entry in jsonDict){ 
                MemberInfo memberInfo = null;
                try{
                    memberInfo = serializedType.GetMember(entry.Key, bindingFlags)[0];
                }catch(Exception)
                {
                    Debug.LogError($"Serialized member \"{entry.Key}\" not found on type \"{serializedType}\". Ignoring it.");
                    continue;
                }
                object value = DeserializeMember(ref script, memberInfo, entry.Value);
                ReflectionUtilities.setValue(memberInfo, script, value);
            }
        }

        object DeserializeMember(ref object script, MemberInfo memberInfo, JRaw jsonValue){
            Type variableType = ReflectionUtilities.GetUnderlyingType(memberInfo);
            //serialized reference
            if(variableType.IsAssignableTo(typeof(UnityEngine.Object)) ){
                if(ulong.TryParse(jsonValue.ToString(), out ulong ref_UID) ){
                    return DeserializeReference(ref script, ref_UID, memberInfo);
                }
                return null;
            }
            //serialized value
            else{
                object value = ReflectionUtilities.getValue(memberInfo, script);
                return DeserializeValue(variableType, value, jsonValue);
            }
        }
        object DeserializeReference(ref object script, ulong ref_UID, MemberInfo memberInfo){
            Type variableType = ReflectionUtilities.GetUnderlyingType(memberInfo);
            UnityEngine.Object referencedObject = UIDManager.instance.GetObject(ref_UID);
            if (referencedObject == null){
                UIDManager.instance.registerPendingReference(script, ref_UID, memberInfo);
                return null;
            }

            if(variableType.IsAssignableTo(typeof(Component))  ){
                MethodInfo method =  typeof(GameObject).GetMethod("GetComponent", 1, new Type[]{}).MakeGenericMethod(variableType);
                object referencedComponent = method.Invoke(referencedObject, new object[]{});
                return referencedComponent;
            }
            else
                return referencedObject;
        }

        object DeserializeValue(Type variableType, object currentValue, JRaw jraw){

            if(variableType.GetInterfaces().Contains(typeof(ISaveable))){
                DeserializeISavable(ref currentValue, jraw); //recursion!
                return currentValue;
            }
            else if( variableType.GetInterfaces().Contains(typeof(ICollection)) ){
                return DeserializeCollection(variableType, jraw);
            }
            else{
                MethodInfo method =  typeof(JsonConvert).GetMethod("DeserializeObject", 1, new Type[]{typeof(string)}).MakeGenericMethod(variableType);
                object value = method.Invoke(null, new object[]{jraw.ToString()});
                return value;
            }
        }

        object DeserializeCollection(Type collectionType, JRaw collectionJson){
            //ICollection collection = (ICollection) Activator.CreateInstance(variableType);
            List<JRaw> jrawCollection = JsonConvert.DeserializeObject<List<JRaw>>(collectionJson.ToString());
            if(jrawCollection == null)
                return null;
            Type elementType = ReflectionUtilities.getCollectionElementType(collectionType);
            
            List<object> tempList = new List<object>();
            foreach(JRaw jrawElement in jrawCollection){
                object element;
                if(elementType.isConstructedFrom(typeof(KeyValuePair<,>))  ){
                    var kvP_jraw = JsonConvert.DeserializeObject<KeyValuePair<JRaw,JRaw>>(jrawElement.ToString());
                    object key = DeserializeCollectionElement(elementType.GetGenericArguments()[0], kvP_jraw.Key);
                    object value = DeserializeCollectionElement(elementType.GetGenericArguments()[1], kvP_jraw.Value);
                    element = Activator.CreateInstance(elementType, key, value);
                }
                else
                    element= DeserializeCollectionElement(elementType, jrawElement);
                
                tempList.Add(element);
            }

            return ICollectionExtension.fromList(collectionType, tempList);
        }

        object DeserializeCollectionElement(Type elementType, JRaw jrawElement){
            object value;
            if(elementType.GetInterfaces().Contains(typeof(ISaveable))){
                value = Activator.CreateInstance(elementType);
                DeserializeISavable(ref value, jrawElement);
            }
            else
                value = DeserializeValue(elementType, null, jrawElement);
            
            return value;
        }

    }
}