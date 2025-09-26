using System.Collections.Generic;
using Photon.Pun;
using Photon.Pun.UtilityScripts;
using TMPro;
using UnityEngine;
using SlotState = GameBoard.SlotState;  // shorter name

public class GameLogic : MonoBehaviourPunCallbacks, IPunTurnManagerCallbacks
{
    public PunTurnManager turnMgr;
    // private bool _isTimeOut = false;
    // private float _startTime;
    private int _offsetIndex = 0;    // used to align turn -> actor mapping
    private bool _isMyTurn = false;
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

    // Force master to be first on the next OnTurnBegins (set when restarting)
    private bool _forceMasterFirst = false;

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
                t.SetClickable(false); // don't allow clicks until turn begins

                tiles[x, y] = t;
            }

        if (mainCamera != null)
            mainCamera.transform.position = new Vector3((width - 1) / 2f, (height - 1) / 2f, -10f);
    }

    // new field: global switch that controls whether tiles accept clicks
    private bool _boardInteractable = false;

    // helper: enable/disable clickable on all tiles
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

                // only non-broken tiles are clickable when board is interactable
                bool clickable = interactable && _board.Get(x, y) != SlotState.Broken;
                tile.SetClickable(clickable);
            }
        }
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
        _gameOver = false;
        _board = new GameBoard(width, height);
        _check = new BoardStateCheck();
        _current = Player.P1;
        phase = Phase.Move;
        _isFirstTurn = true;

        // hide popup
        if (_gameOverPopup != null)
            _gameOverPopup.SetActive(false);

        // clear existing tile sprites (don't re-instantiate tiles)
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

        // update UI immediately to show that Blue will start
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
        // guard: only allow input if game running, my turn, and it's my player turn logically, and index is in bounds.
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

    // ---------------- Helper: explicit move checks ----------------
    // Use concrete legal-move lists (avoid ambiguity from CurrentHasMove/OpponentHasMove)
    private bool Player1HasMoves()
    {
        var p1pos = _board.GetP1Pos();
        var moves = _board.GetLegalMovesFrom(p1pos);
        return moves != null && moves.Count > 0;
    }
    private bool Player2HasMoves()
    {
        var p2pos = _board.GetP2Pos();
        var moves = _board.GetLegalMovesFrom(p2pos);
        return moves != null && moves.Count > 0;
    }

    // ---------------- Placement (adjusted win logic) ----------------
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

            // After performing a MOVE, check opponent's available moves explicitly:
            bool p1Has = Player1HasMoves();
            bool p2Has = Player2HasMoves();

            // If opponent (not the current player) has no moves -> current player wins
            bool opponentHasMoves = currentIsP1 ? p2Has : p1Has;
            if (!opponentHasMoves)
            {
                // current player wins
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

            // After performing a BREAK, see if the player who just moved (current) trapped themself.
            // IMPORTANT: currentIsP1 refers to the player who performed the break (we computed above).
            bool p1Has = Player1HasMoves();
            bool p2Has = Player2HasMoves();

            bool currentHasMoves = currentIsP1 ? p1Has : p2Has;
            if (!_board.AreAdjacent(_board.GetP1Pos(), _board.GetP2Pos()) && !currentHasMoves)
            {
                // The current player trapped themself -> other player wins
                EndGame(currentIsP1 ? Player.P2 : Player.P1, "Player trapped themself");
                return;
            }

            // switch logical current player (for the clients that already applied the final move)
            _current = currentIsP1 ? Player.P2 : Player.P1;
            phase = Phase.Move;

            ShowTurnText(_current == Player.P1 ? "<color=blue>BLUE TURN</color>" : "<color=red>RED TURN</color>");

            // After switching, check if the new current player has any moves (if none -> other wins)
            p1Has = Player1HasMoves();
            p2Has = Player2HasMoves();
            bool newCurrentHasMoves = (_current == Player.P1) ? p1Has : p2Has;
            if (!newCurrentHasMoves)
            {
                // new current can't move -> the other player (who just moved) wins
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
        // explicit check: if P1 has no moves => P2 wins, if P2 has no moves => P1 wins
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

    // Returns player order list with MasterClient first, then others sorted ascending
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

    private int GetExpectedActorForTurn(int turn)   // tell us who's actor number for this turn
    {
        var list = GetPlayerOrderList();
        if (list.Count == 0) return -1;

        int idx = (turn - 1 + _offsetIndex) % list.Count;
        return list[idx];
    }

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

            // UI change example (keep/modify to your prefab names)
            if (unityObjects.ContainsKey("Screen_MultiPlayerGame"))
                unityObjects["Screen_MultiPlayerGame"].SetActive(true);
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
        // _isTimeOut = false;
        // _startTime = Time.time;

        // If we forced master-first on restart, compute offset so that this `turn` maps to master (list[0])
        if (_forceMasterFirst)
        {
            var list = GetPlayerOrderList();
            if (list.Count > 0)
            {
                int n = list.Count;
                int turnMod = ((turn - 1) % n + n) % n; // safe positive mod
                _offsetIndex = (n - turnMod) % n;       // (turn-1 + offset) % n == 0 -> yields master
                _forceMasterFirst = false;
                Debug.Log($"[Turn] force master first -> offsetIndex={_offsetIndex}");
            }
        }

        int expectedActor = GetExpectedActorForTurn(turn);
        if (expectedActor < 0)
        {
            Debug.LogError("[Photon] OnTurnBegins: turnOrder not set yet");
        }

        // set logical current player according to player-order list and expectedActor
        var list2 = GetPlayerOrderList();
        if (list2.Count > 0)
        {
            int indexInOrder = list2.IndexOf(expectedActor);
            _current = (indexInOrder == 0) ? Player.P1 : Player.P2;
        }

        _isMyTurn = PhotonNetwork.LocalPlayer.ActorNumber == expectedActor;

        SetBoardInteractable(_isMyTurn && !_gameOver);


        // update turn text to match logical current
        ShowTurnText(_current == Player.P1 ? "<color=blue>BLUE TURN</color>" : "<color=red>RED TURN</color>");

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
