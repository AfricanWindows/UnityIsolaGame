using System.Collections.Generic;
using Photon.Pun;
using Photon.Pun.UtilityScripts;
using TMPro;
using UnityEngine;
using SlotState = GameBoard.SlotState;  // shorter name

// Multiplayer game logic using Photon PunTurnManager
public class GameLogic : MonoBehaviourPunCallbacks, IPunTurnManagerCallbacks
{
    public PunTurnManager turnMgr;
    // private bool _isTimeOut = false;
    // private float _startTime;
    private int _offsetIndex = 0;    // aligns turn number to actor order
    private bool _isMyTurn = false;  // is it this client's turn now?
    private bool _isFirstTurn = true;

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

    private Slot[,] tiles;        // visual tiles array
    private GameBoard _board;     // data board
    private BoardStateCheck _check; // rules helper

    private enum Player { P1, P2 }
    private Player _current = Player.P1; // which logical player is moving now

    private Player _myPlayer = Player.P1; // which player this client controls

    private enum Phase { Move, Break }
    private Phase phase = Phase.Move;   // current phase (Move then Break)

    private bool _gameOver = false;

    [Header("UI GameOver Popup")]
    [SerializeField] private GameObject _gameOverPopup; // popup object
    [SerializeField] private TextMeshProUGUI _gameOverText;  // winner text
    [SerializeField] private TextMeshProUGUI _whosTurnText;
    [SerializeField] private TextMeshProUGUI _mySignTurnText;

    [Header("Sounds")]
    public AudioSource gameoverSound;
    public AudioSource stepSound;
    public AudioSource breakingSound;

    // When restarting, force master to act as first player on next turn
    private bool _forceMasterFirst = false;

    // ---------------- UNITY EVENTS ----------------
    public override void OnEnable()
    {
        // subscribe to slot clicks and restart events and menu start
        Slot.OnClickSlot += OnClickSlot;
        Btn_Restart.OnClickRestart += OnClickRestart;
        MenuLogic.OnStartGame += OnStartGame;
    }
    public override void OnDisable()
    {
        // unsubscribe
        Slot.OnClickSlot -= OnClickSlot;
        Btn_Restart.OnClickRestart -= OnClickRestart;
        MenuLogic.OnStartGame -= OnStartGame;
    }

    void Awake()
    {
        if (turnMgr != null)
            turnMgr.TurnManagerListener = this; // register callbacks

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

        // instantiate tile prefabs in a grid
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++, idx++)
            {
                var t = Instantiate(tilePrefab, new Vector3(x, y, 0), Quaternion.identity, boardParent);
                t.name = "Slot" + idx;
                t.slotIndex = idx;
                t.SetSprite(null);
                t.SetClickable(false); // clicks off until turns start

                tiles[x, y] = t;
            }

        // center camera on the board
        if (mainCamera != null)
            mainCamera.transform.position = new Vector3((width - 1) / 2f, (height - 1) / 2f, -10f);
    }

    // global flag: make tiles accept clicks or not
    private bool _boardInteractable = false;

    // enable or disable clickable on all tiles
    private void SetBoardInteractable(bool interactable)
    {
        _boardInteractable = interactable;
        if (tiles == null || _board == null) return;

        for (int y = 0; y < _board.height; y++)
        {
            for (int x = 0; x < _board.width; x++)
            {
                var tile = tiles[x, y];
                if (tile == null) continue;

                // only non-broken tiles are clickable
                bool clickable = interactable && _board.Get(x, y) != SlotState.Broken;
                tile.SetClickable(clickable);
            }
        }
    }

    private void PlacePlayersStart()
    {
        // reset board data and flags
        _board.ResetStart();
        _current = Player.P1;
        phase = Phase.Move;
        _gameOver = false;
    }

    // ---------------- RESTART ----------------
    private void OnClickRestart()
    {
        Debug.Log("GameLogic: Restarting game");
        OnLocalRestartMatch();
    }

    private void OnLocalRestartMatch()
    {
        // broadcast RPC to restart on all clients
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
        _gameOver = false;
        _board = new GameBoard(width, height);
        _check = new BoardStateCheck();
        _current = Player.P1;
        phase = Phase.Move;
        _isFirstTurn = true;

        // hide popup
        if (_gameOverPopup != null)
            _gameOverPopup.SetActive(false);

        // clear existing tile sprites and make clickable
        if (tiles != null)
        {
            for (int y = 0; y < _board.height; y++)
                for (int x = 0; x < _board.width; x++)
                {
                    if (tiles[x, y] != null)
                    {
                        tiles[x, y].SetSprite(null);
                        tiles[x, y].SetClickable(true);
                    }
                }
        }
        AssignMySign();
        // Reinitialize data and visuals so players are placed correctly
        PlacePlayersStart();
        Redraw();

        SetBoardInteractable(false);

        // Force next OnTurnBegins to treat master as first player
        _forceMasterFirst = true;

        // update UI immediately to show Blue starts
        ShowTurnText("<color=blue>BLUE TURN</color>");

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
        // guard: only accept input if running, it's my network turn, logical current matches me and index valid
        if (_gameOver || !_isMyTurn || _current != _myPlayer || !_check.IndexInBounds(_board, slotIndex))
            return;

        // Move phase: local placement + send intermediate move (finished=false)
        if (phase == Phase.Move)
        {
            if (_check.IsLegalMoveForCurrent(_board, _current == Player.P1, slotIndex))
            {
                Placement(slotIndex);            // apply locally
                SendMove(slotIndex, false);      // send intermediate move
                // do NOT set _isMyTurn=false yet: still must Break
            }
        }
        else // Break phase: apply local break and send final move
        {
            if (_check.IsLegalBreakAtIndex(_board, slotIndex))
            {
                Placement(slotIndex);            // apply break locally
                SendMove(slotIndex, true);       // send final move
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

        turnMgr.SendMove(slotIndex, finished); // send through PunTurnManager
        Debug.Log($"[Photon] Sent move: {slotIndex} (finished={finished})");
    }

    // ---------------- Helper: explicit move checks ----------------
    // Check if player1 has any legal moves
    private bool Player1HasMoves()
    {
        var p1pos = _board.GetP1Pos();
        var moves = _board.GetLegalMovesFrom(p1pos);
        return moves != null && moves.Count > 0;
    }
    // Check if player2 has any legal moves
    private bool Player2HasMoves()
    {
        var p2pos = _board.GetP2Pos();
        var moves = _board.GetLegalMovesFrom(p2pos);
        return moves != null && moves.Count > 0;
    }

    // ---------------- Placement (move & break logic + win checks) ----------------
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

            // After MOVE, check opponent moves. If opponent stuck -> current wins
            bool p1Has = Player1HasMoves();
            bool p2Has = Player2HasMoves();

            bool opponentHasMoves = currentIsP1 ? p2Has : p1Has;
            if (!opponentHasMoves)
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

            // After BREAK, check if the player who broke trapped themself.
            bool p1Has = Player1HasMoves();
            bool p2Has = Player2HasMoves();

            bool currentHasMoves = currentIsP1 ? p1Has : p2Has;
            if (!_board.AreAdjacent(_board.GetP1Pos(), _board.GetP2Pos()) && !currentHasMoves)
            {
                // current trapped themself -> other wins
                EndGame(currentIsP1 ? Player.P2 : Player.P1, "Player trapped themself");
                return;
            }

            // switch logical current player and go to Move phase
            _current = currentIsP1 ? Player.P2 : Player.P1;
            phase = Phase.Move;

            ShowTurnText(_current == Player.P1 ? "<color=blue>BLUE TURN</color>" : "<color=red>RED TURN</color>");

            // check if new current has moves; if none -> other wins
            p1Has = Player1HasMoves();
            p2Has = Player2HasMoves();
            bool newCurrentHasMoves = (_current == Player.P1) ? p1Has : p2Has;
            if (!newCurrentHasMoves)
            {
                EndGame(_current == Player.P1 ? Player.P2 : Player.P1, "Opponent stuck");
                return;
            }

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
        if (_board == null || tiles == null) return;

        for (int y = 0; y < _board.height; y++)
        {
            for (int x = 0; x < _board.width; x++)
            {
                SlotState state = _board.Get(x, y);
                Slot tile = tiles[x, y];

                if (state == SlotState.Empty)
                {
                    tile.SetSprite(null);
                    tile.SetClickable(_boardInteractable);
                }
                else if (state == SlotState.P1)
                {
                    tile.SetSprite(bluePlayerSprite);
                    tile.SetClickable(_boardInteractable);
                }
                else if (state == SlotState.P2)
                {
                    tile.SetSprite(redPlayerSprite);
                    tile.SetClickable(_boardInteractable);
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
        // explicit check for stuck players
        bool p1Has = Player1HasMoves();
        bool p2Has = Player2HasMoves();

        if (!p1Has)
        {
            EndGame(Player.P2, "Blue stuck");
            return true;
        }
        if (!p2Has)
        {
            EndGame(Player.P1, "Red stuck");
            return true;
        }
        return false;
    }

    // ---------------- SMALL HELPERS ----------------
    private Player Other(Player p) => (p == Player.P1) ? Player.P2 : Player.P1;
    private string NameOf(Player p) => (p == Player.P1) ? "BLUE" : "RED";

    // Return list of players actor numbers with Master first
    private List<int> GetPlayerOrderList()
    {
        var room = PhotonNetwork.CurrentRoom;
        var ordered = new List<int>();
        if (room == null) return ordered;

        int master = PhotonNetwork.MasterClient.ActorNumber;
        ordered.Add(master);

        var others = new List<int>();
        foreach (var kvp in room.Players)
        {
            if (kvp.Key == master) continue;
            others.Add(kvp.Key);
        }
        others.Sort();
        ordered.AddRange(others);
        return ordered;
    }

    // Get expected actor number for a given turn (maps turn -> actor)
    private int GetExpectedActorForTurn(int turn)
    {
        var list = GetPlayerOrderList();
        if (list.Count == 0) return -1;

        int idx = (turn - 1 + _offsetIndex) % list.Count;
        return list[idx];
    }

    // Assign which player this client is (P1 or P2) based on order
    private void AssignMySign()
    {
        var list = GetPlayerOrderList();
        if (list.Count == 0) return;

        int myIndex = list.IndexOf(PhotonNetwork.LocalPlayer.ActorNumber);
        _myPlayer = (myIndex == 0) ? Player.P1 : Player.P2;

        string spriteKey = (_myPlayer == Player.P1) ? "you are BluePlayer" : "you are RedPlayer";
        if (_mySignTurnText != null)
            _mySignTurnText.text = spriteKey;
        Debug.Log($"[Photon] Assigned sign {spriteKey} (playerIndex={myIndex})");
    }

    private void FirstTurnLogic()
    {
        if (_isFirstTurn)
        {
            _isFirstTurn = false;
            _gameOver = false;
            AssignMySign();

            // show multiplayer screen and hide menu
            if (unityObjects.ContainsKey("Screen_MultiPlayerGame"))
                unityObjects["Screen_MultiPlayerGame"].SetActive(true);
            if (unityObjects.ContainsKey("MenuScreen"))
                unityObjects["MenuScreen"].SetActive(false);
        }
    }

    #region Server Events

    private void OnStartGame()
    {
        // called from menu to start turn manager
        Debug.Log("OnStartGame");
        turnMgr?.BeginTurn();
    }

    public void OnTurnBegins(int turn)
    {
        // When a turn starts, figure who should move

        // If restart forced master-first, compute offset so master is first in order
        if (_forceMasterFirst)
        {
            var list = GetPlayerOrderList();
            if (list.Count > 0)
            {
                int n = list.Count;
                int turnMod = ((turn - 1) % n + n) % n; // safe positive mod
                _offsetIndex = (n - turnMod) % n;       // align so that index 0 = master
                _forceMasterFirst = false;
                Debug.Log($"[Turn] force master first -> offsetIndex={_offsetIndex}");
            }
        }

        int expectedActor = GetExpectedActorForTurn(turn);
        if (expectedActor < 0)
        {
            Debug.LogError("[Photon] OnTurnBegins: turnOrder not set yet");
        }

        // set logical current player according to order
        var list2 = GetPlayerOrderList();
        if (list2.Count > 0)
        {
            int indexInOrder = list2.IndexOf(expectedActor);
            _current = (indexInOrder == 0) ? Player.P1 : Player.P2;
        }

        // check if this client is the expected actor
        _isMyTurn = PhotonNetwork.LocalPlayer.ActorNumber == expectedActor;

        // enable board input only if it's my turn and game not over
        SetBoardInteractable(_isMyTurn && !_gameOver);

        // update UI
        ShowTurnText(_current == Player.P1 ? "<color=blue>BLUE TURN</color>" : "<color=red>RED TURN</color>");

        FirstTurnLogic();
        Debug.Log("OnTurnBegins - Is My Turn: " + _isMyTurn + ", Current: " + _current);
    }

    public void OnTurnCompleted(int turn) { }

    public void OnPlayerMove(Photon.Realtime.Player player, int turn, object move)
    {
        // handle intermediate move from other player
        if (player.ActorNumber == PhotonNetwork.LocalPlayer.ActorNumber) return;

        if (move is int index)
        {
            Debug.Log($"[Photon] OnPlayerMove from {player.ActorNumber}: {index}");
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

        // master advances the turn to next player
        if (PhotonNetwork.IsMasterClient && turnMgr != null)
        {
            turnMgr.BeginTurn();
        }
    }

    public void OnTurnTimeEnds(int turn) { }

    #endregion
}
