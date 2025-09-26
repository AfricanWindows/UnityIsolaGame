using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Btn_MultiPlayer : MonoBehaviour
{
    public MenuLogic menuLogic; // reference to MenuLogic from inspector
    public void Click()
    {
        if (menuLogic != null)
            menuLogic.Btn_MultiPlayerMenu();   // use function from menuLogic
    }
}
