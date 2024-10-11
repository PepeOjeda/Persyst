using System.Reflection;
using UnityEngine;
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

namespace Persyst
{
    public static class ReflectionUtilities
    {
        static Dictionary<Type, CachedData> cache = new();

        class CachedData
        {
            public Type[] interfaces;
            public Type[] genericArguments;
        }

        public static Type GetUnderlyingType(MemberInfo member)
        {
            switch (member.MemberType)
            {
                case MemberTypes.Event:
                    return ((EventInfo)member).EventHandlerType;
                case MemberTypes.Field:
                    return ((FieldInfo)member).FieldType;
                case MemberTypes.Method:
                    return ((MethodInfo)member).ReturnType;
                case MemberTypes.Property:
                    return ((PropertyInfo)member).PropertyType;
                default:
                    throw new ArgumentException
                    (
                    "Input MemberInfo must be if type EventInfo, FieldInfo, MethodInfo, or PropertyInfo"
                    );
            }
        }

        public static Type getCollectionElementType(Type collectionType)
        {
            if (collectionType.IsArray)
                return collectionType.GetElementType();
            else if (GetInterfaces(collectionType).Contains(typeof(IDictionary)))
                return typeof(KeyValuePair<,>).MakeGenericType(collectionType.GetGenericArguments()[0], collectionType.GetGenericArguments()[1]);
            else
                return collectionType.GetGenericArguments()[0];
        }

        public static object getValue(MemberInfo member, object obj)
        {
            if (member == null)
                return null;
            switch (member.MemberType)
            {
                case MemberTypes.Field:
                    return ((FieldInfo)member).GetValue(obj);
                case MemberTypes.Property:
                    return ((PropertyInfo)member).GetValue(obj);
                default:
                    Debug.LogError("Trying to get value of member that is not a field or property: " + member.Name);
                    return null;
            }
        }

        public static void setValue(MemberInfo member, object obj, object value)
        {
            switch (member.MemberType)
            {
                case MemberTypes.Field:
                    ((FieldInfo)member).SetValue(obj, value);
                    break;
                case MemberTypes.Property:
                    ((PropertyInfo)member).SetValue(obj, value);
                    break;
                default:
                    Debug.LogError("Trying to set value on member that is not a field or property: " + member.Name);
                    break;
            }
        }

        public static bool IsAssignableTo(this Type type, Type target)
        {
            return type == target || type.IsSubclassOf(target) || GetInterfaces(type).Contains(target);
        }

        public static bool isConstructedFrom(this Type type, Type target)
        {
            return type.IsConstructedGenericType && type.GetGenericTypeDefinition() == target;
        }

        public static Type[] GetInterfaces(Type type)
        {
            CachedData cachedData = GetCachedData(type);
            if (cachedData.interfaces == null)
                cachedData.interfaces = type.GetInterfaces();
            return cachedData.interfaces;
        }

        public static Type[] GetGenericArguments(Type type)
        {
            CachedData cachedData = GetCachedData(type);
            if (cachedData.genericArguments == null)
                cachedData.genericArguments = type.GetGenericArguments();
            return cachedData.genericArguments;
        }

        static CachedData GetCachedData(Type type)
        {
            CachedData cachedData;
            if (!cache.TryGetValue(type, out cachedData))
            {
                cachedData = new();
                cache[type] = cachedData;
            }
            return cachedData;
        }
    }
}