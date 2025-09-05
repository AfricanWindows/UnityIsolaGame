using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Btn_SinglePlayer : MonoBehaviour
{
    public MenuLogic menuLogic; // reference to MenuLogic from inspector
    public void Click()
    {
        if (menuLogic != null)
            menuLogic.Btn_SinglePlayer();   // use function from menuLogic
    }
}
