using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Persyst{
    [System.Serializable]
    public class RefWrapper<T> :ISaveable where T : UnityEngine.Object 
    {
        [SaveThis][SerializeField] public T reference;

        public RefWrapper(){}

        public RefWrapper (T reference){
            this.reference = reference;
        }
    }
}