using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Btn_YTlink : MonoBehaviour
{
    public void OpenWebsite(string url = "https://www.youtube.com/@DoctorAlan555")
    {
        Application.OpenURL(url);
    }
}
