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
        [SerializeField] bool assigned=false;
        static BindingFlags bindingFlags = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;
        static JsonSerializerSettings jsonSerializerSettings = new JsonSerializerSettings{ReferenceLoopHandling = ReferenceLoopHandling.Ignore};

        void Start(){
            #if UNITY_EDITOR
            if(UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage() !=null) //dont automatically assign a UID to the prefab itself when you open it 
                return;
            #endif

            if(assigned){
                UIDManager.instance.refreshReference(gameObject, myUID);
                if(Application.isPlaying)
                    LoadObject(GameSaver.instance.RetrieveObject(myUID));
                return;
            }
            myUID=UIDManager.instance.generateUID(gameObject);
            assigned=true;

        }
                
        void OnEnable(){
            GameSaver.saveTheGame += SaveObject;
        }
        void OnDestroy(){
            GameSaver.saveTheGame -= SaveObject;
            if(!Application.isPlaying && gameObject.scene.isLoaded){
                UIDManager.instance.removeUID(myUID);
                assigned = false;
            }
        }

        

        //Saving
        void SaveObject(){
            Dictionary<string,JRaw> savedScripts = new Dictionary<string, JRaw>();
            ISaveable[] scriptList = GetComponents<ISaveable>();

            foreach(var script in scriptList){
                savedScripts[script.GetType().ToString()] = serializeScript(script);
            }
            
            GameSaver.instance.SaveObject(myUID, new JRaw(JsonConvert.SerializeObject(savedScripts, Formatting.Indented)) );
        }

        JRaw serializeScript(object script){
            Dictionary<string, JRaw> jsonDict = new Dictionary<string, JRaw>();

            var members = script.GetType().GetMembers(bindingFlags).Where(member => member.IsDefined(typeof(SaveThis)) );
            
            foreach(MemberInfo memberInfo in members){
               jsonDict[memberInfo.Name] = serializeMember(ReflectionUtilities.getValue(memberInfo, script));
            }

            return new JRaw(JsonConvert.SerializeObject(jsonDict, jsonSerializerSettings));
        }
        JRaw serializeMember(object value){
            if(value==null)
                return null;
            Type type = value.GetType();
            //serialize reference
            if( type.IsAssignableTo(typeof(UnityEngine.Object)) ){
                return serializeReference(type, value as UnityEngine.Object);
            }

            //serialize value
            else 
                return serializeValue(type, value);
        }

        JRaw serializeValue(Type type, object value){
            if( type.GetInterfaces().Contains(typeof(ISaveable)) ){
                return new JRaw(serializeScript(value)); //recursion!
            }
            else if(type.GetInterfaces().Contains(typeof(ICollection))){
                return serializeCollection(value as ICollection);
            }
            else{
                return new JRaw( JsonConvert.SerializeObject(value, jsonSerializerSettings));
            }
        }

        JRaw serializeCollection(ICollection collection){
            if(collection == null)
                return null;
            Type type = collection.GetType();
            
            if((type.IsConstructedGenericType && 
                    type.GetGenericArguments().Any(t => t.IsAssignableTo(typeof(UnityEngine.Object)) )  
                )
                ||
                type.IsArray && type.GetElementType().IsAssignableTo(typeof(UnityEngine.Object))
            ){
                Debug.LogError("Collections of UnityEngine.Object are not supported. You can get around this by using an ISaveable wrapper (e.g. List<RefWrapper<GameObject>>");
                return null;
            }

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            bool firstElement=true; //for commas

            //dictionaries and lists of keyValuePairs
            if( type.IsConstructedGenericType && 
                    (
                        type.GetInterfaces().Contains(typeof(IDictionary))
                        ||
                        type.GenericTypeArguments.Any(t => t.isConstructedFrom(typeof(KeyValuePair<,>)) )
                    ) 
            ){
                sb.Append("[");
                foreach(object entry in collection){
                    if(!firstElement)
                        sb.Append(",");
                    firstElement = false;

                    object Key = entry.GetType().GetProperty("Key").GetValue(entry);
                    object Value = entry.GetType().GetProperty("Value").GetValue(entry);
                    sb.Append("{\"Key\":"+serializeMember(Key)+",");
                    sb.Append("\"Value\":"+serializeMember(Value)+"}");
                }
                sb.Append("]");
            }
            //lists, sets, stacks, etc
            else{
                sb.Append("[");
                foreach(object element in collection){
                    if(!firstElement)
                        sb.Append(",");
                    firstElement = false;
                    sb.Append(serializeMember(element));
                }
                sb.Append("]");
            }

            return new JRaw(sb.ToString());
        }

        JRaw serializeReference(Type type, UnityEngine.Object value){
            if(value == null){
                return null;
            }
            else if( type.IsAssignableTo(typeof(UnityEngine.Component)) || type.IsAssignableTo(typeof(GameObject)) ){
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
        public void LoadObject(JRaw jsonString){
            if(jsonString==null)
                return;
            Dictionary<string, JRaw> jsonDict = JsonConvert.DeserializeObject<Dictionary<string, JRaw>>(jsonString.ToString());

            foreach(KeyValuePair<string, JRaw> entry in jsonDict){
                Type type = Type.GetType(entry.Key);
                MethodInfo method =  typeof(GameObject).GetMethod("GetComponent", 1, new Type[]{}).MakeGenericMethod(type);
                object script = method.Invoke(gameObject, new object[]{});
                DeserializeScript(ref script, entry.Value);
            }
        }

        void DeserializeScript(ref object script, JRaw jsonString){
            Dictionary<string, JRaw> jsonDict = JsonConvert.DeserializeObject<Dictionary<string, JRaw>>(jsonString.ToString());
            Type scriptType = script.GetType();

            foreach(KeyValuePair<string, JRaw> entry in jsonDict){ 
                MemberInfo memberInfo = scriptType.GetMember(entry.Key, bindingFlags)[0];
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
                if(currentValue == null)
                    currentValue = Activator.CreateInstance(variableType);
                DeserializeScript(ref currentValue, jraw); //recursion!
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
                DeserializeScript(ref value, jrawElement);
            }
            else
                value = DeserializeValue(elementType, null, jrawElement);
            
            return value;
        }
        
    }
}