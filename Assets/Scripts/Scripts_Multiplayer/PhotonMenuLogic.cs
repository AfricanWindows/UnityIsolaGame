using Photon.Pun;
using Photon.Realtime;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PhotonMenuLogic : MonoBehaviourPunCallbacks
{
    public static Action OnStartGame;

    private Dictionary<string, GameObject> phObjects;
    private int searchValue = 100;
    private int maxPlayers = 2;
    private string my_password = "alan";

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
        GameObject[] obj = GameObject.FindGameObjectsWithTag("phObjects");
        foreach (GameObject g in obj)
            phObjects.Add(g.name, g);

    }

    private void InitStart()
    {
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
            {"sv",searchValue},
            {"pwd", my_password }
        };

        var roomOptions = new RoomOptions
        {
            MaxPlayers = maxPlayers,
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
        int max = maxPlayers;
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
            {"sv",searchValue},
            {"pwd", my_password}
        };

        var op = new OpJoinRandomRoomParams
        {
            ExpectedCustomRoomProperties = expected,
        };

        PhotonNetwork.JoinRandomRoom(op.ExpectedCustomRoomProperties, maxPlayers);
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

        if (!string.IsNullOrEmpty(my_password))
        {
            var expectedHash = PhotonNetwork.CurrentRoom.CustomProperties["pwd"].ToString();
            var myHash = my_password;
            if (!string.IsNullOrEmpty(expectedHash) && my_password != expectedHash)
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







}
