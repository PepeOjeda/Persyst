using System;


namespace Persyst{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class SaveThis : Attribute
    {
        public SaveThis(){}
    }
}
