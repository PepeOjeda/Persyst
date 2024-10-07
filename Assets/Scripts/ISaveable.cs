using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Persyst
{
    public interface ISaveable
    {
        public virtual bool SaveInEditMode => true;
    }
}
