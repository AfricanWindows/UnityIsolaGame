using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using Hashtable = ExitGames.Client.Photon.Hashtable;

// Controls main menu, connects to Photon, creates/joins rooms and switches screens
public class MenuLogic : MonoBehaviourPunCallbacks
{
    public static Action OnStartGame; // event to tell GameLogic to start

    private Dictionary<string, GameObject> phObjects;
    private int _searchValue = 0;       // room search value (sv)
    private int _maxPlayers = 2;        // players per room
    private string _roomPassword = "alan"; // simple room password (manual check)

    [SerializeField] private Slider _slider;
    [SerializeField] private TextMeshProUGUI _sliderLabel;

    private enum Screens
    {
        MainMenu, SinglePlayer, Options, MultiPlayerMenu, StudentInfo, MultiPlayerGame
    };

    [SerializeField] private GameObject img_bg;

    private Screens _currentScreen;
    private Stack<Screens> _history = new Stack<Screens>(); // history stack for Back button
    private Dictionary<string, GameObject> _unityObjects;

    void Awake()
    {
        InitAwake();

        if (_slider != null)
        {
            // setup slider event
            _slider.onValueChanged.RemoveAllListeners();
            _slider.onValueChanged.AddListener(OnSliderChanged);
            _slider.value = _searchValue;
            UpdateSliderLabel();
        }
    }

    void Start()
    {
        InitStart();
    }

    private void OnSliderChanged(float v)
    {
        // save slider value and update label
        _searchValue = Mathf.RoundToInt(v);
        UpdateSliderLabel();
    }

    private void UpdateSliderLabel()
    {
        if (_sliderLabel != null)
            _sliderLabel.text = $"{_searchValue}$"; // show value
    }

    private void InitAwake()
    {
        // cache objects tagged "phObjects" (buttons, status text, etc.)
        phObjects = new Dictionary<string, GameObject>();
        GameObject[] phobj = GameObject.FindGameObjectsWithTag("phObjects");
        foreach (GameObject g in phobj)
            phObjects.Add(g.name, g);

        // cache screens and other unity objects tagged "UnityObject"
        _unityObjects = new Dictionary<string, GameObject>();
        GameObject[] unityObj = GameObject.FindGameObjectsWithTag("UnityObject");
        foreach (GameObject obj in unityObj)
        {
            _unityObjects.Add(obj.name, obj);
        }
        Debug.Log("There are " + _unityObjects.Count + " Screens");
    }

    private void InitStart()
    {
        _history.Clear();   // reset history
        _currentScreen = Screens.MainMenu;

        // hide all screens that start with "Screen_"
        foreach (var kvp in _unityObjects)
        {
            if (kvp.Key.StartsWith("Screen_"))
            {
                kvp.Value.SetActive(false);
            }
        }

        // show main menu
        _unityObjects["Screen_MainMenu"].SetActive(true);

        // disable play until connected
        phObjects["Btn_Play"].GetComponent<Button>().interactable = false;
        PhotonNetwork.AutomaticallySyncScene = true;
        PhotonNetwork.ConnectUsingSettings();   // connect to Photon server
    }

    private void UpdateStatus(string txt)
    {
        // update status text in UI
        phObjects["Text_Status"].GetComponent<TextMeshProUGUI>().text = txt;
    }

    private void CreateRoom()
    {
        // Build custom room properties. "sv" is used for matching in lobby.
        var roomProperties = new Hashtable
        {
            {"sv", _searchValue},
            {"pwd", _roomPassword}
        };

        var roomOptions = new RoomOptions
        {
            MaxPlayers = (byte)_maxPlayers,
            IsVisible = true,
            IsOpen = true,
            CustomRoomProperties = roomProperties,
            // expose only "sv" to the lobby for JoinRandom filter
            CustomRoomPropertiesForLobby = new[] { "sv" }
        };

        Debug.Log($"[MenuLogic] Creating room with sv={_searchValue}");
        PhotonNetwork.CreateRoom(null, roomOptions, TypedLobby.Default); // create room with options
    }

    private void StartGame()
    {
        // Only master client starts the match when room is full
        UpdateStatus("Starting Game...");
        var room = PhotonNetwork.CurrentRoom;
        if (room == null || !PhotonNetwork.IsMasterClient) return;

        int players = room.PlayerCount;
        int max = _maxPlayers;
        bool reachMax = (max > 0) && (players == max);
        if (!reachMax) return;

        // hide room from others and signal start
        room.IsVisible = false;
        room.IsOpen = false;

        OnStartGame?.Invoke();
    }

    // Photon callbacks
    public override void OnConnectedToMaster()
    {
        Debug.Log("OnConnectedToMaster");
        UpdateStatus("Connected to server");
        phObjects["Btn_Play"].GetComponent<Button>().interactable = true; // enable play
    }

    public override void OnJoinedLobby()
    {
        Debug.Log("OnJoinedLobby");

        // try to join random room that matches sv
        var expected = new Hashtable
        {
            {"sv", _searchValue}
        };

        Debug.Log($"[MenuLogic] JoinRandomRoom expected sv={_searchValue}, maxPlayers={_maxPlayers}");
        PhotonNetwork.JoinRandomRoom(expected, (byte)_maxPlayers);
    }

    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        // if no matching room -> create one
        Debug.Log("<color=yellow>OnJoinRandomFailed</color> returnCode=" + returnCode + " msg=" + message);
        UpdateStatus("Creating Room...");
        CreateRoom();
    }

    public override void OnJoinedRoom()
    {
        Debug.Log("OnJoinedRoom");
        UpdateStatus("Joined Room: " + PhotonNetwork.CurrentRoom.Name);

        // optional password check - leave if mismatch
        if (!string.IsNullOrEmpty(_roomPassword))
        {
            if (PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey("pwd"))
            {
                var expectedHash = PhotonNetwork.CurrentRoom.CustomProperties["pwd"]?.ToString();
                if (!string.IsNullOrEmpty(expectedHash) && _roomPassword != expectedHash)
                {
                    Debug.LogWarning("Passwords don't match - leaving room");
                    PhotonNetwork.LeaveRoom();
                    return;
                }
            }
        }
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        // when someone enters, check start conditions
        StartGame();
    }

    public void Btn_Play()
    {
        Debug.Log("Btn_Play");
        phObjects["Btn_Play"].GetComponent<Button>().interactable = false;
        PhotonNetwork.JoinLobby(); // join lobby to search rooms
        UpdateStatus("Searching for available rooms...");
    }

    private void ChangeScreen(Screens toScreen, bool pushHistory = true)
    {
        if (_currentScreen == toScreen) return;

        if (pushHistory)
            _history.Push(_currentScreen);

        // switch visuals
        _unityObjects["Screen_" + _currentScreen].SetActive(false); // turn off current
        _unityObjects["Screen_" + toScreen].SetActive(true);        // turn on new
        _currentScreen = toScreen;                                  // update current
    }

    public void Btn_Back()
    {
        Debug.Log("Btn_Back");

        var prev = _history.Pop();                  // get last screen
        ChangeScreen(prev, pushHistory: false);     // go back without adding to history
        if (_currentScreen == Screens.MainMenu)
        {
            if (img_bg != null) img_bg.SetActive(true);
        }
    }

    public void Btn_SinglePlayer()
    {
        Debug.Log("Btn_SinglePlayer");
        img_bg.SetActive(false);
        ChangeScreen(Screens.SinglePlayer);
    }

    public void Btn_MultiPlayerMenu()
    {
        Debug.Log("Btn_MultiPlayerMenu");
        ChangeScreen(Screens.MultiPlayerMenu);
    }
    public void Btn_StudentInfo()
    {
        Debug.Log("Btn_StudentInfo");
        ChangeScreen(Screens.StudentInfo);
    }

    public void Btn_Options()
    {
        Debug.Log("Btn_Options");
        ChangeScreen(Screens.Options);
    }
}
