using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;

public class phMenuLogic : MonoBehaviourPunCallbacks
{
    private Dictionary<string, GameObject> _unityObjects;





    #region MonoBehaviour
    void Awake()
    {
        InitAwake();
    }
    void Start()
    {
        InitStart();
    }

    #endregion





    #region Logic

    private void InitAwake()
    {
        _unityObjects = new Dictionary<string, GameObject>();                       // set screens to dictionary
        GameObject[] unityObj = GameObject.FindGameObjectsWithTag("UnityObject");
        foreach (GameObject obj in unityObj)
        {
            _unityObjects.Add(obj.name, obj);
        }
    }
    private void InitStart()
    {
        PhotonNetwork.AutomaticallySyncScene = true; // sync scenes
        PhotonNetwork.ConnectUsingSettings();   // connect using the app id we put inside the settings

    }

    private void UpdateStatus(string txt)
    {
        _unityObjects["Txt_Status"].GetComponent<TextMeshProUGUI>().text = txt;
    }

    #endregion





    #region Server Callbacks

    public override void OnConnectedToMaster()
    {
        Debug.Log("OnConnectedToMaster");
        Debug.Log("Connected to Server");
    }
    // ZOOM 44:19 ZOOM 44:19 ZOOM 44:19 ZOOM 44:19 ZOOM 44:19 ZOOM 44:19 ZOOM 44:19 ZOOM 44:19 ZOOM 44:19

    #endregion












}
