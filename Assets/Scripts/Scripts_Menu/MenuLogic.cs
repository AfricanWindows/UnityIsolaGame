using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class MenuLogic : MonoBehaviour
{
    private enum Screens
    {
        MainMenu, SinglePlayer, Options, MultiPlayerMenu, StudentInfo, MultiPlayer
    };

    [SerializeField] private GameObject img_bg;

    private Screens _currentScreen;
    private Stack<Screens> _history = new Stack<Screens>(); // Stack instead of _prevScreen
    private Dictionary<string, GameObject> _unityObjects;

    void Awake()
    {
        InitAwake();
    }
    void Start()
    {
        InitStart();
    }

    private void InitAwake()
    {
        _unityObjects = new Dictionary<string, GameObject>();                       // set screens to dictionary
        GameObject[] unityObj = GameObject.FindGameObjectsWithTag("UnityObject");
        foreach (GameObject obj in unityObj)
        {
            _unityObjects.Add(obj.name, obj);
        }
        Debug.Log("There are " + _unityObjects.Count + " Screens");
    }

    private void InitStart()
    {
        _history.Clear();   // for safety
        _currentScreen = Screens.MainMenu;

        // _unityObjects["Screen_MainMenu"].SetActive(true);
        // _unityObjects["Screen_SinglePlayer"].SetActive(false);
        // _unityObjects["Screen_Options"].SetActive(false);
        // _unityObjects["Screen_MultiPlayer"].SetActive(false);
        // _unityObjects["Screen_StudentInfo"].SetActive(false);
        // _unityObjects["Screen_PlayingMulti"].SetActive(false);

        foreach (GameObject s in _unityObjects.Values)
        {
            s.SetActive(false);
        }
        _unityObjects["Screen_MainMenu"].SetActive(true);

    }

    private void ChangeScreen(Screens toScreen, bool pushHistory = true)
    {
        if (_currentScreen == toScreen) return;

        if (pushHistory)
            _history.Push(_currentScreen);

        _unityObjects["Screen_" + _currentScreen].SetActive(false); // turn off current
        _unityObjects["Screen_" + toScreen].SetActive(true);        // turn on new
        _currentScreen = toScreen;                                  // update current
    }

    public void Btn_Back()
    {
        Debug.Log("Btn_Back");

        var prev = _history.Pop();                  // remove the last element
        ChangeScreen(prev, pushHistory: false);     // change screen back without saving prev screen
    }


    public void Btn_SinglePlayer()
    {
        Debug.Log("Btn_SinglePlayer");
        img_bg.SetActive(false);
        ChangeScreen(Screens.SinglePlayer);
    }

    public void Btn_MultiPlayer()
    {
        Debug.Log("Btn_MultiPlayerMenu");
        ChangeScreen(Screens.MultiPlayerMenu);
    }
    public void Btn_StudentInfo()
    {
        Debug.Log("Btn_StudentInfo");
        ChangeScreen(Screens.StudentInfo);
    }
    public void Btn_PlayingMulti()
    {
        Debug.Log("Btn_MultiPlayer");
        img_bg.SetActive(false);
        ChangeScreen(Screens.MultiPlayer);
    }

    public void Btn_Options()
    {
        Debug.Log("Btn_Options");
        ChangeScreen(Screens.Options);
    }
}
