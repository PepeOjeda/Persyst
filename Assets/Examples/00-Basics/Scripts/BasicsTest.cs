using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Persyst;

namespace PersystExamples
{
    public class BasicsTest : MonoBehaviour, ISaveable
    {
        [SerializeField][SaveThis] int saveableInteger;
        [SerializeField][SaveThis] GameObject saveableReference;
    }
}