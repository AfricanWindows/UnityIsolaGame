using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class mySlider : MonoBehaviour
{
    private Slider _slider;
    public TextMeshProUGUI sliderText;   // get from inspector

    void Awake()
    {
        _slider = GetComponent<Slider>();
        if (_slider == null)
            Debug.LogError("Cant find slider !!!");
        if (sliderText == null)
            Debug.LogError("TextMeshProUGUI not assigned !!!");
    }

    void Update()
    {
        if (_slider != null && sliderText != null)
        {
            if (sliderText.name == "Txt_textDollar")
                sliderText.text = _slider.value.ToString("0") + "$"; // show as integer$
            else
                sliderText.text = _slider.value.ToString("0"); // show as integer

        }

    }
}
