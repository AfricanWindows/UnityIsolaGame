using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using Photon.Realtime;
using TMPro;

public class MenuLogic : MonoBehaviourPunCallbacks
{

    public static Action OnStartGame;

    private Dictionary<string, GameObject> phObjects;
    private int _searchValue = 100;
    private int _maxPlayers = 2;
    private string _roomPassword = "alan";




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
        phObjects = new Dictionary<string, GameObject>();
        GameObject[] phobj = GameObject.FindGameObjectsWithTag("phObjects");
        foreach (GameObject g in phobj)
            phObjects.Add(g.name, g);


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

        foreach (var kvp in _unityObjects)
        {
            if (kvp.Key.StartsWith("Screen_"))
            {
                kvp.Value.SetActive(false);
            }
        }

        _unityObjects["Screen_MainMenu"].SetActive(true);



        phObjects["Btn_Play"].GetComponent<Button>().interactable = false;
        PhotonNetwork.AutomaticallySyncScene = true;
        PhotonNetwork.ConnectUsingSettings();   // using api

    }


    private void UpdateStatus(string txt)
    {
        phObjects["Text_Status"].GetComponent<TextMeshProUGUI>().text = txt;
    }

    private void CreateRoom()
    {
        var roomProperties = new ExitGames.Client.Photon.Hashtable
        {
            {"sv",_searchValue},
            {"pwd", _roomPassword }
        };

        var roomOptions = new RoomOptions
        {
            MaxPlayers = _maxPlayers,
            IsVisible = true,
            IsOpen = true,
            CustomRoomProperties = roomProperties,
            CustomRoomPropertiesForLobby = new[] { "sv", "pwd" }   // if want filter
        };

        PhotonNetwork.CreateRoom(null, roomOptions, TypedLobby.Default);    // create and enter the room
    }

    private void StartGame()
    {
        UpdateStatus("Starting Game...");   // every one get Starting Gmae...
        var room = PhotonNetwork.CurrentRoom;
        if (room == null || !PhotonNetwork.IsMasterClient) return;

        int players = room.PlayerCount;
        int max = _maxPlayers;
        bool reachMax = (max > 0) && (players == max);
        if (reachMax == false)
            return;

        room.IsVisible = false;
        room.IsOpen = false;

        OnStartGame?.Invoke();

    }


    public override void OnConnectedToMaster()
    {
        Debug.Log("OnConnectedToMaster");
        UpdateStatus("Connected to server");
        phObjects["Btn_Play"].GetComponent<Button>().interactable = true;

    }

    public override void OnJoinedLobby()
    {
        Debug.Log("OnJoinedLobby");

        var expected = new ExitGames.Client.Photon.Hashtable
        {
            {"sv",_searchValue},
            {"pwd", _roomPassword}
        };

        var op = new OpJoinRandomRoomParams
        {
            ExpectedCustomRoomProperties = expected,
        };

        PhotonNetwork.JoinRandomRoom(op.ExpectedCustomRoomProperties, _maxPlayers);
    }


    public override void OnJoinRandomFailed(short returnCode, string message)   // firstly not will working
    {
        Debug.Log("<color=yellow>OnJoinRandomFailed</color>");
        UpdateStatus("Creating Room...");
        CreateRoom();
    }

    public override void OnJoinedRoom()
    {
        Debug.Log("OnJoinedRoom");
        UpdateStatus("Joined Room: " + PhotonNetwork.CurrentRoom.Name);

        if (!string.IsNullOrEmpty(_roomPassword))
        {
            var expectedHash = PhotonNetwork.CurrentRoom.CustomProperties["pwd"].ToString();
            var myHash = _roomPassword;
            if (!string.IsNullOrEmpty(expectedHash) && _roomPassword != expectedHash)
            {
                Debug.Log("Passwords doesnt match");
                PhotonNetwork.LeaveRoom();
                return;
            }
        }
    }




    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        StartGame();
    }


    public void Btn_Play()
    {
        Debug.Log("Btn_Play");
        phObjects["Btn_Play"].GetComponent<Button>().interactable = false;
        PhotonNetwork.JoinLobby();
        UpdateStatus("Searching for an available rooms");
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

    public void Btn_Options()
    {
        Debug.Log("Btn_Options");
        ChangeScreen(Screens.Options);
    }
}
