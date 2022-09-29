using System;
using TwinCAT.TypeSystem;

namespace ADSPersist
{
    [Serializable]
    public class SerializableValueSymbol
    {
        public string Path;
        public string Name;
        public dynamic Value;

        public SerializableValueSymbol(DynamicSymbol symbol)
        {
            Path = symbol.InstancePath;
            Name = symbol.InstanceName;
            Value = symbol.ReadValue();
        }

        public SerializableValueSymbol()
        {
            Path = "";
            Name = "";
            Value = false;
        }
    }
}
