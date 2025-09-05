using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Btn_Play : MonoBehaviour
{
    public MenuLogic menuLogic; // reference to MenuLogic from inspector
    public void Click()
    {
        if (menuLogic != null)
            menuLogic.Btn_PlayingMulti();   // use function from menuLogic
    }
}
