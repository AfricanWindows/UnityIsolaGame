using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Slider_Music : MonoBehaviour
{
    [SerializeField] private AudioSource _music_audio;
    [SerializeField] private AudioSource _sfx_audio1;
    [SerializeField] private AudioSource _sfx_audio2;
    [SerializeField] private AudioSource _sfx_audio3;
    [SerializeField] private Slider _slider_music;
    [SerializeField] private Slider _slider_sfx;
    [SerializeField] private TextMeshProUGUI _sliderTxtMusic;
    [SerializeField] private TextMeshProUGUI _sliderTxtSfx;

    void Awake()
    {
        if (!_slider_music || !_music_audio)
            Debug.Log("<color=red>Something wrong with music</color>");
        if (!_slider_sfx || !_sfx_audio1 || !_sfx_audio2 || _sfx_audio3)
            Debug.Log("<color=red>Something wrong with sfx</color>");
        if (!_sliderTxtMusic || !_sliderTxtSfx)
            Debug.Log("<color=red>There is no text values</color>");
    }

    void Update()
    {
        if (_slider_music && _music_audio)
            _music_audio.volume = (float)_slider_music.value / _slider_music.maxValue;  // normalize 0-1

        if (_slider_sfx && _sfx_audio1 && _sfx_audio2 && _sfx_audio3)
        {
            float _tempSfxValue = (float)_slider_sfx.value / _slider_sfx.maxValue;  // normalize 0-1
            _sfx_audio1.volume = _tempSfxValue;
            _sfx_audio2.volume = _tempSfxValue;
            _sfx_audio3.volume = _tempSfxValue;

        }
        if (_sliderTxtMusic && _sliderTxtSfx)
        {
            _sliderTxtMusic.text = _slider_music.value.ToString("0");
            _sliderTxtSfx.text = _slider_sfx.value.ToString("0");
        }
    }




}
