using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Persyst
{
    public interface ISaveable
    {
        // It is sometimes convenient to have a specific type of component to be omitted from files generated during Edit Mode (due to requiring runtime initialization)
        public virtual bool SaveInEditMode => true;

        public enum OperationStatus{NotDone, Done}

        // By default, the PersistentObject component will automatically handle serialization through reflection
        // override this method and return "Done" if you want to avoid that and do the serialization yourself
        public OperationStatus Serialize(JsonTextWriter writer)
        {
            return OperationStatus.NotDone;
        }

        // Same deal as Serialize()
        public OperationStatus Deserialize(JRaw jraw)
        {
            return OperationStatus.NotDone;
        }
    }
}
