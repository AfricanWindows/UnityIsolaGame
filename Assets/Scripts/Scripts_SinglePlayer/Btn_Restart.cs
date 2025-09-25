using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;

public class Btn_Restart : MonoBehaviour
{
    public static Action OnClickRestart;

    public void OnMouseDown()
    {
        OnClickRestart?.Invoke();
    }

}
