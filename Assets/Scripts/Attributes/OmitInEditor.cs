using System;


namespace Persyst{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class OmitInEditor : Attribute
    {
        public OmitInEditor(){}
    }
}
