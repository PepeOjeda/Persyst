using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Persyst;

public class BasicsTest : MonoBehaviour, ISaveable
{
    [SerializeField][SaveThis] int saveableInteger;
    [SerializeField][SaveThis] GameObject saveableReference;
}
