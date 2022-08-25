using System;


namespace Persyst{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class SaveAsInstanceType : Attribute
    {
        public SaveAsInstanceType(){}
    }
}

