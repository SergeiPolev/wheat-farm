using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class Coroutines: MonoBehaviour
{
    private static Coroutines instanse
    {
        get {
            if (_instanse == null)
            {
                var go = new GameObject("[COROUTINE MANAGER]");
                _instanse = go.AddComponent<Coroutines>();
                DontDestroyOnLoad(go);
            }
            return _instanse;
        }
    }

    private static Coroutines _instanse;

    public static Coroutine StartRoutine(IEnumerator enumerator)
    {
        return instanse.StartCoroutine(enumerator);
    }

    public static void StopRoutine(Coroutine routine)
    {
        if (routine != null)
        {
            instanse.StopCoroutine(routine);
        }
    }
}
