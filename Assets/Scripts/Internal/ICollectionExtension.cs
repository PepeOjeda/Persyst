using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System;

namespace Persyst
{
    public static class ICollectionExtension
    {
        public static ICollection fromList(Type collectionType, IList list)
        {
            Type elementType = ReflectionUtilities.getCollectionElementType(collectionType);

            if (collectionType.isConstructedFrom(typeof(List<>)))
            {
                ICollection list_to_return = (ICollection)Activator.CreateInstance(collectionType);
                foreach (object obj in list)
                {
                    collectionType.GetMethod("Add").Invoke(list_to_return, new object[] { obj });
                }
                return list_to_return;
            }
            else if (collectionType.IsArray)
            {
                Array arr = Array.CreateInstance(elementType, list.Count);
                for (int i = 0; i < list.Count; i++)
                {
                    arr.SetValue(list[i], i);
                }
                return arr;
            }
            else if (collectionType.IsAssignableTo(typeof(IDictionary)))
            {
                ICollection dict = (ICollection)Activator.CreateInstance(collectionType);
                foreach (object obj in list)
                {
                    object key = elementType.GetProperty("Key").GetValue(obj);
                    object value = elementType.GetProperty("Value").GetValue(obj);
                    collectionType.GetMethod("Add").Invoke(dict, new object[] { key, value });
                }
                return dict;
            }
            else
            {
                Debug.LogError($"Collection of type {collectionType} not supported for serialization. Sorry!");
                return null;
            }
        }
    }
}