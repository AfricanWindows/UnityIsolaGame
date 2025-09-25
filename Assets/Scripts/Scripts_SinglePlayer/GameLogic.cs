using System.Collections.Generic;
using Photon.Pun;
using Photon.Pun.UtilityScripts;
using TMPro;
using UnityEngine;
using SlotState = GameBoard.SlotState;  // shorter name

public class GameLogic : MonoBehaviourPunCallbacks, IPunTurnManagerCallbacks
{
    public PunTurnManager turnMgr;
    private bool _isTimeOut = false;
    private float _startTime;
    private int _offsetIndex = 0;    // P1 start the game
    private bool _isMyTurn = false;
    private bool _isFirstTurn = true;
    private bool _isGameOver;

    private Dictionary<string, GameObject> unityObjects;

    [Header("Prefabs & Scene")]
    public Slot tilePrefab;       // prefab for each board cell
    public Transform boardParent; // parent object for tiles
    public Camera mainCamera;     // main camera to center board

    [Header("Board size")]
    public int width = 5;
    public int height = 5;

    [Header("Sprites (assign in Inspector)")]
    public Sprite bluePlayerSprite;
    public Sprite redPlayerSprite;
    public Sprite brokenSprite;

    private Slot[,] tiles;        // all visual tiles
    private GameBoard _board;     // board state (data)
    private BoardStateCheck _check; // helper for rules

    private enum Player { P1, P2 }
    private Player _current = Player.P1; // which logical player is moving now

    private Player _myPlayer = Player.P1; // which player am I locally (set in AssignMySign)

    private enum Phase { Move, Break }
    private Phase phase = Phase.Move;   // first phase is move

    private bool _gameOver = false;

    [Header("UI GameOver Popup")]
    [SerializeField] private GameObject _gameOverPopup; // drag PopupGameOver here
    [SerializeField] private TextMeshProUGUI _gameOverText;  // text to show winner
    [SerializeField] private TextMeshProUGUI _whosTurnText;
    [SerializeField] private TextMeshProUGUI _mySignTurnText;

    [Header("Sounds")]
    public AudioSource gameoverSound;
    public AudioSource stepSound;
    public AudioSource breakingSound;

    // ---------------- UNITY EVENTS ----------------
    public override void OnEnable()
    {
        Slot.OnClickSlot += OnClickSlot;
        Btn_Restart.OnClickRestart += OnClickRestart;
        MenuLogic.OnStartGame += OnStartGame;
    }
    public override void OnDisable()
    {
        Slot.OnClickSlot -= OnClickSlot;
        Btn_Restart.OnClickRestart -= OnClickRestart;
        MenuLogic.OnStartGame -= OnStartGame;
    }

    void Awake()
    {
        if (turnMgr != null)
            turnMgr.TurnManagerListener = this;

        unityObjects = new Dictionary<string, GameObject>();
        GameObject[] unityObj = GameObject.FindGameObjectsWithTag("UnityObject");
        foreach (GameObject obj in unityObj)
        {
            unityObjects.Add(obj.name, obj);
        }
    }

    void Start()
    {
        InitStart();
    }
    private void InitStart()
    {
        if (_gameOverPopup != null)
            _gameOverPopup.SetActive(false); // hide popup at start

        BuildBoard();

        _board = new GameBoard(width, height);
        _check = new BoardStateCheck();

        PlacePlayersStart();
        Redraw();
        ShowTurnText(_current == Player.P1 ? "<color=blue>BLUE TURN</color>" : "<color=red>RED TURN</color>");
    }

    // ---------------- BUILD ----------------
    private void BuildBoard()
    {
        tiles = new Slot[width, height];
        int idx = 0;

        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++, idx++)
            {
                var t = Instantiate(tilePrefab, new Vector3(x, y, 0), Quaternion.identity, boardParent);
                t.name = "Slot" + idx;
                t.slotIndex = idx;
                t.SetSprite(null);
                t.SetClickable(true);
                tiles[x, y] = t;
            }

        if (mainCamera != null)
            mainCamera.transform.position = new Vector3((width - 1) / 2f, (height - 1) / 2f, -10f);
    }

    private void PlacePlayersStart()
    {
        _board.ResetStart();
        _current = Player.P1;
        phase = Phase.Move;
        _gameOver = false;
    }

    // ---------------- RESTART ----------------
    private void OnClickRestart()
    {
        Debug.Log("GameLogic: Restarting game");

        // InitStart();
        OnLocalRestartMatch();
    }






    private void OnLocalRestartMatch()
    {
        Debug.Log("Local requested restart -> broadcasting RPC to all");
        photonView.RPC(nameof(RPC_RestartMatch), RpcTarget.All);
    }

    [PunRPC]
    private void RPC_RestartMatch()
    {
        Debug.Log("[RPC] Restart match received");
        ResetMatchLocal();
    }

    // Reset local state to initial (called via RPC on all clients)
    private void ResetMatchLocal()
    {
        _isGameOver = false;
        _board = new GameBoard(width, height);
        _check = new BoardStateCheck();
        _current = Player.P1;
        phase = Phase.Move;
        _isFirstTurn = true;

        // hide popup
        if (_gameOverPopup != null)
            _gameOverPopup.SetActive(false);

        // reset visuals
        for (int y = 0; y < _board.height; y++)
            for (int x = 0; x < _board.width; x++)
                tiles[x, y].SetSprite(null);

        Redraw();


        BuildBoard();

        _board = new GameBoard(width, height);
        _check = new BoardStateCheck();

        PlacePlayersStart();
        Redraw();
        ShowTurnText(_current == Player.P1 ? "<color=blue>BLUE TURN</color>" : "<color=red>RED TURN</color>");


        // master starts the turn sequence
        if (PhotonNetwork.IsMasterClient && turnMgr != null)
        {
            Debug.Log("[Restart] Master begins first turn");
            turnMgr.BeginTurn();
        }
    }










    // ---------------- PLAYER INPUT ----------------
    private void OnClickSlot(int slotIndex)
    {
        // One big guard: only allow input if game running, my turn, and it's my player turn logically,
        // and index is in bounds.
        if (_gameOver || !_isMyTurn || _current != _myPlayer || !_check.IndexInBounds(_board, slotIndex))
            return;

        // Move phase: do local placement and send intermediate move (finished=false)
        if (phase == Phase.Move)
        {
            if (_check.IsLegalMoveForCurrent(_board, _current == Player.P1, slotIndex))
            {
                Placement(slotIndex);            // apply locally (instant feedback)
                SendMove(slotIndex, false);      // send intermediate move to other clients
                // do NOT set _isMyTurn=false yet: still need to Break
            }
        }
        else // Break phase: apply local break and send final move (finished=true)
        {
            if (_check.IsLegalBreakAtIndex(_board, slotIndex))
            {
                Placement(slotIndex);            // apply break locally
                SendMove(slotIndex, true);       // send final move to others
                _isMyTurn = false;               // my turn finished after break
                // master will call turnMgr.BeginTurn() in OnPlayerFinished to start next turn
            }
        }
    }

    private void SendMove(int slotIndex, bool finished)
    {
        if (turnMgr == null)
        {
            Debug.LogError("SendMove called but no PunTurnManager assigned");
            return;
        }

        if (turnMgr.IsFinishedByMe && finished)
        {
            Debug.Log("Already finished this turn — not sending final move");
            return;
        }

        turnMgr.SendMove(slotIndex, finished);
        Debug.Log($"[Photon] Sent move: {slotIndex} (finished={finished})");
    }

    // Placement now handles both players depending on _current
    private void Placement(int index)
    {
        bool currentIsP1 = (_current == Player.P1);

        if (phase == Phase.Move && _check.IsLegalMoveForCurrent(_board, currentIsP1, index))
        {
            Vector2Int dst = _check.FromIndex(_board, index);

            if (currentIsP1)
            {
                Vector2Int cur = _board.GetP1Pos();
                _board.Set(cur.x, cur.y, SlotState.Empty);
                _board.Set(dst.x, dst.y, SlotState.P1);
                _board.SetP1Pos(dst);
            }
            else
            {
                Vector2Int cur = _board.GetP2Pos();
                _board.Set(cur.x, cur.y, SlotState.Empty);
                _board.Set(dst.x, dst.y, SlotState.P2);
                _board.SetP2Pos(dst);
            }

            Redraw();
            stepSound?.Play();

            // opponent no moves -> current wins
            if (!_check.OpponentHasMove(_board, currentIsP1))
            {
                EndGame(currentIsP1 ? Player.P1 : Player.P2, "Opponent stuck");
                return;
            }

            phase = Phase.Break;
        }
        else if (phase != Phase.Move && _check.IsLegalBreakAtIndex(_board, index))
        {
            Vector2Int pos = _check.FromIndex(_board, index);
            _board.Set(pos.x, pos.y, SlotState.Broken);

            breakingSound?.Play();
            Redraw();

            // check if the current player trapped themself
            if (!_board.AreAdjacent(_board.GetP1Pos(), _board.GetP2Pos()) &&
                !_check.CurrentHasMove(_board, currentIsP1))
            {
                EndGame(currentIsP1 ? Player.P2 : Player.P1, "Player trapped themself");
                return;
            }

            // switch logical current player (for the clients that already applied the final move)
            _current = currentIsP1 ? Player.P2 : Player.P1;
            phase = Phase.Move;

            ShowTurnText(_current == Player.P1 ? "<color=blue>BLUE TURN</color>" : "<color=red>RED TURN</color>");

            if (!_check.CurrentHasMove(_board, _current == Player.P1))
            {
                EndGame(currentIsP1 ? Player.P2 : Player.P1, "Opponent stuck");
                return;
            }

            // In multiplayer there is no local AI; other player's client will act when their OnTurnBegins sets _isMyTurn
        }
    }

    private void ShowTurnText(string tx)
    {
        if (_whosTurnText != null)
            _whosTurnText.text = tx;
    }

    // ---------------- RENDER ----------------
    private void Redraw()
    {
        for (int y = 0; y < _board.height; y++)
        {
            for (int x = 0; x < _board.width; x++)
            {
                SlotState state = _board.Get(x, y);
                Slot tile = tiles[x, y];

                if (state == SlotState.Empty)
                {
                    tile.SetSprite(null);
                    tile.SetClickable(true);
                }
                else if (state == SlotState.P1)
                {
                    tile.SetSprite(bluePlayerSprite);
                    tile.SetClickable(true);
                }
                else if (state == SlotState.P2)
                {
                    tile.SetSprite(redPlayerSprite);
                    tile.SetClickable(true);
                }
                else if (state == SlotState.Broken)
                {
                    tile.SetSprite(brokenSprite);
                    tile.SetClickable(false);
                }
            }
        }
    }

    // ---------------- GAME OVER ----------------
    private void EndGame(Player winner, string reason)
    {
        _gameOver = true;
        Debug.Log($"Game Over! {reason}. Winner: {NameOf(winner)}");
        gameoverSound?.Play();

        if (_gameOverPopup != null)
            _gameOverPopup.SetActive(true);

        if (_gameOverText != null)
            _gameOverText.text = $"Winner: {NameOf(winner)}";
    }

    private bool CheckImmediateGameOver()
    {
        bool currentIsP1 = (_current == Player.P1);
        if (!_check.CurrentHasMove(_board, currentIsP1))
        {
            EndGame(Other(_current), $"{NameOf(_current)} stuck");
            return true;
        }
        return false;
    }

    // ---------------- SMALL HELPERS ----------------
    private Player Other(Player p) => (p == Player.P1) ? Player.P2 : Player.P1;
    private string NameOf(Player p) => (p == Player.P1) ? "BLUE" : "RED";

    private int GetExpectedActorForTurn(int turn)   // tell us who's actor number for this turn
    {
        var room = PhotonNetwork.CurrentRoom;
        if (room == null) return -1;

        var list = new List<int>();
        foreach (var kvp in room.Players)
            list.Add(kvp.Key);

        list.Sort();

        if (list.Count == 0) return -1;

        int idx = (turn - 1 + _offsetIndex) % list.Count;
        return list[idx];
    }

    private void AssignMySign()
    {
        var room = PhotonNetwork.CurrentRoom;
        if (room == null) return;

        var list = new List<int>(room.Players.Keys);
        list.Sort();

        int myIndex = list.IndexOf(PhotonNetwork.LocalPlayer.ActorNumber);
        _myPlayer = (myIndex == 0) ? Player.P1 : Player.P2;

        string spriteKey = (_myPlayer == Player.P1) ? "you are BluePlayer" : "you are RedPlayer";
        _mySignTurnText.text = spriteKey;
        Debug.Log($"[Photon] Assigned sign {spriteKey} (playerIndex={myIndex})");
    }

    private void FirstTurnLogic()
    {
        if (_isFirstTurn)
        {
            _isFirstTurn = false;
            _isGameOver = false;
            AssignMySign();

            // UI change example (keep/modify to your prefab names)
            if (unityObjects.ContainsKey("Screen_SinglePlayer"))
                unityObjects["Screen_SinglePlayer"].SetActive(true);
            if (unityObjects.ContainsKey("MenuScreen"))
                unityObjects["MenuScreen"].SetActive(false);
        }
    }

    #region Server Events

    private void OnStartGame()
    {
        Debug.Log("OnStartGame");
        turnMgr?.BeginTurn();
    }

    public void OnTurnBegins(int turn)
    {
        _isTimeOut = false;
        _startTime = Time.time;

        int expectedActor = GetExpectedActorForTurn(turn);
        if (expectedActor < 0)
        {
            Debug.LogError("[Photon] OnTurnBegins: turnOrder not set yet");
        }

        // set logical current player according to turn order
        var room = PhotonNetwork.CurrentRoom;
        if (room != null)
        {
            var list = new List<int>();
            foreach (var kvp in room.Players) list.Add(kvp.Key);
            list.Sort();

            // the actor for this turn already computed
            int actor = expectedActor;
            int indexInOrder = list.IndexOf(actor);
            _current = (indexInOrder == 0) ? Player.P1 : Player.P2;
        }

        _isMyTurn = PhotonNetwork.LocalPlayer.ActorNumber == expectedActor;

        FirstTurnLogic();
        Debug.Log("OnTurnBegins - Is My Turn: " + _isMyTurn + ", Current: " + _current);
    }

    public void OnTurnCompleted(int turn) { }

    public void OnPlayerMove(Photon.Realtime.Player player, int turn, object move)
    {
        // apply intermediate move from other players (skip local)
        if (player.ActorNumber == PhotonNetwork.LocalPlayer.ActorNumber) return;

        if (move is int index)
        {
            Debug.Log($"[Photon] OnPlayerMove from {player.ActorNumber}: {index}");
            // Ensure _current is set correctly for remote: OnTurnBegins should already have set it.
            Placement(index);
        }
    }

    public void OnPlayerFinished(Photon.Realtime.Player player, int turn, object move)
    {
        Debug.Log($"[Photon] OnPlayerFinished {player.ActorNumber} turn {turn} move={move}");

        // apply remote player's final move (break)
        if (player.ActorNumber != PhotonNetwork.LocalPlayer.ActorNumber && move != null)
        {
            if (move is int index)
            {
                Placement(index);
            }
        }

        // The master client advances the turn
        if (PhotonNetwork.IsMasterClient && turnMgr != null)
        {
            turnMgr.BeginTurn();
        }
    }

    public void OnTurnTimeEnds(int turn) { }

    #endregion
}
