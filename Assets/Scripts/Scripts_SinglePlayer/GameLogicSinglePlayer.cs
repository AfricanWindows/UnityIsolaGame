using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SlotState = GameBoard.SlotState;  // shorter name

// Single-player game logic: blue = player, red = AI
public class GameLogicSinglePlayer : MonoBehaviour
{
    [Header("Prefabs & Scene")]
    public Slot tilePrefab;       // prefab for each board cell
    public Transform boardParent; // parent object for tiles
    public Camera mainCamera;     // camera to center the board

    [Header("Board size")]
    public int width = 5;
    public int height = 5;

    [Header("Sprites (assign in Inspector)")]
    public Sprite bluePlayerSprite;
    public Sprite redPlayerSprite;
    public Sprite brokenSprite;

    private Slot[,] tiles;        // visual tile instances
    private GameBoard _board;     // game data (cells, player positions)
    private BoardStateCheck _check; // helper for move/break rules

    private enum Player { P1, P2 }
    private Player _current = Player.P1; // whose logical turn it is (blue starts)

    private enum Phase { Move, Break }
    private Phase phase = Phase.Move;   // first do Move, then Break

    private bool _gameOver = false;     // true when game finished

    [Header("UI GameOver Popup")]
    [SerializeField] private GameObject _gameOverPopup; // popup object for end
    [SerializeField] private TextMeshProUGUI _gameOverText;  // shows winner
    [SerializeField] private TextMeshProUGUI _whosTurnText;  // shows whose turn

    private float _aiDelayMove = 0.5f;  // delay before AI move (seconds)
    private float _aiDelayBreak = 0.2f; // delay after AI break

    [Header("Sounds")]
    public AudioSource gameoverSound;
    public AudioSource stepSound;
    public AudioSource breakingSound;

    private bool _aiBusy = false;       // prevent input while AI thinking

    // ---------------- UNITY EVENTS ----------------
    void OnEnable()
    {
        // subscribe to tile click and restart events
        Slot.OnClickSlot += OnClickSlot;
        Btn_Restart.OnClickRestart += OnClickRestart;
    }
    void OnDisable()
    {
        // unsubscribe to avoid leaks
        Slot.OnClickSlot -= OnClickSlot;
        Btn_Restart.OnClickRestart -= OnClickRestart;
    }


    void Start()
    {
        // hide popup at start
        if (_gameOverPopup != null)
            _gameOverPopup.SetActive(false);

        // create visual tiles
        BuildBoard();

        // create data structures
        _board = new GameBoard(width, height);
        _check = new BoardStateCheck();

        // place players on starting positions
        PlacePlayersStart();

        // update visuals
        Redraw();

        ShowTurnText("BLUE TURN");


    }

    // ---------------- BUILD ----------------
    private void BuildBoard()
    {
        tiles = new Slot[width, height];
        int idx = 0;

        // spawn tile prefabs in grid and set basic values
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++, idx++)
            {
                var t = Instantiate(tilePrefab, new Vector3(x, y, 0), Quaternion.identity, boardParent);
                t.name = "Slot" + idx;
                t.slotIndex = idx;
                t.SetSprite(null);     // empty visual
                t.SetClickable(true);  // allow clicks (singleplayer handles when to ignore)
                tiles[x, y] = t;
            }

        // center camera on board
        if (mainCamera != null)
            mainCamera.transform.position = new Vector3((width - 1) / 2f, (height - 1) / 2f, -10f);
    }

    private void PlacePlayersStart()
    {
        // reset data board and flags, place players at default positions
        _board.ResetStart();   // board: players placed in ResetStart()
        _current = Player.P1;   // blue starts
        phase = Phase.Move;
        _gameOver = false;
    }



    // ---------------- RESTART THE GAME  WITH BUTTON ----------------

    private void OnClickRestart()
    {
        Debug.Log("GameLogic: Restarting game");

        // hide popup if visible
        if (_gameOverPopup != null)
            _gameOverPopup.SetActive(false);

        // recreate data objects
        _board = new GameBoard(width, height);
        _check = new BoardStateCheck();
        ShowTurnText("BLUE TURN");

        // reset positions and flags
        PlacePlayersStart();

        // update visuals to empty board
        Redraw();

        // immediate gameover check
        if (CheckImmediateGameOver()) return;

        // if red to move -> run AI
        TryStartRedAI();
    }


    // ---------------- PLAYER INPUT ----------------
    private void OnClickSlot(int slotIndex)
    {
        if (_gameOver || _current == Player.P2 || _aiBusy || !_check.IndexInBounds(_board, slotIndex)) return;

        if (phase == Phase.Move)
        {
            // MOVE phase: player moves their piece to an adjacent empty cell
            if (_check.IsLegalMoveForCurrent(_board, true, slotIndex))
            {
                Vector2Int dst = _check.FromIndex(_board, slotIndex);
                Vector2Int cur = _board.GetP1Pos();

                // update data board for move
                _board.Set(cur.x, cur.y, SlotState.Empty);
                _board.Set(dst.x, dst.y, SlotState.P1);
                _board.SetP1Pos(dst);

                Redraw();

                // play step sound if assigned
                if (stepSound != null)
                    stepSound.Play();

                // if opponent (red) has no moves -> player wins
                if (!_check.OpponentHasMove(_board, true))
                {
                    EndGame(Player.P1, "Red stuck");
                    return;
                }

                // switch to break phase (player must break a cell next)
                phase = Phase.Break;
            }
        }
        else
        {
            // BREAK phase: player breaks an empty cell
            if (_check.IsLegalBreakAtIndex(_board, slotIndex))
            {
                Vector2Int pos = _check.FromIndex(_board, slotIndex);
                _board.Set(pos.x, pos.y, SlotState.Broken);

                if (breakingSound != null)
                    breakingSound.Play();

                Redraw();

                // if player broke and trapped themself -> red wins
                if (!_board.AreAdjacent(_board.GetP1Pos(), _board.GetP2Pos()) &&
                    !_check.CurrentHasMove(_board, true))
                {
                    EndGame(Player.P2, "Blue trapped itself");
                    return;
                }

                // switch turn to red (AI)
                _current = Player.P2;
                phase = Phase.Move;

                ShowTurnText("RED TURN");

                // if red has no moves -> player wins
                if (!_check.CurrentHasMove(_board, false))
                {
                    EndGame(Player.P1, "Red stuck");
                    return;
                }

                // start AI sequence
                TryStartRedAI();
            }
        }
    }

    private void ShowTurnText(string tx)
    {
        if (_whosTurnText != null)
            _whosTurnText.text = tx;
    }


    // ---------------- AI LOGIC ----------------
    private void TryStartRedAI()
    {
        // start the AI coroutine only if game running and it's AI's turn and AI not busy
        if (!_gameOver && _current == Player.P2 && !_aiBusy)
            StartCoroutine(RedTurn());
    }

    private IEnumerator RedTurn()
    {
        _aiBusy = true;

        // small delay to make AI feel natural
        yield return new WaitForSeconds(_aiDelayMove);

        // if AI has no moves -> blue wins
        if (!_check.CurrentHasMove(_board, false))
        {
            EndGame(Player.P1, "Red stuck");
            _aiBusy = false;
            yield break;
        }

        // MOVE: pick random legal move for red
        var moves = _board.GetLegalMovesFrom(_board.GetP2Pos());
        Vector2Int move = moves[Random.Range(0, moves.Count)];
        Vector2Int old = _board.GetP2Pos();

        _board.Set(old.x, old.y, SlotState.Empty);
        _board.Set(move.x, move.y, SlotState.P2);
        _board.SetP2Pos(move);

        Redraw();

        if (stepSound != null)
            stepSound.Play();

        // optional short delay between move and break
        yield return new WaitForSeconds(_aiDelayMove);

        // BREAK: pick random empty cell to break
        var empties = _board.GetAllEmpty();
        if (empties.Count > 0)
        {
            Vector2Int b = empties[Random.Range(0, empties.Count)];
            _board.Set(b.x, b.y, SlotState.Broken);

            if (breakingSound != null)
                breakingSound.Play();
        }

        Redraw();
        yield return new WaitForSeconds(_aiDelayBreak);

        // check if AI trapped itself after breaking
        if (!_board.AreAdjacent(_board.GetP1Pos(), _board.GetP2Pos()) &&
            !_check.CurrentHasMove(_board, false))
        {
            EndGame(Player.P1, "Red trapped itself");
            _aiBusy = false;
            yield break;
        }

        // switch back to player
        _current = Player.P1;
        phase = Phase.Move;

        ShowTurnText("BLUE TURN");

        // if player is stuck now -> AI wins
        if (!_check.CurrentHasMove(_board, true))
        {
            EndGame(Player.P2, "Blue stuck");
            _aiBusy = false;
            yield break;
        }

        _aiBusy = false;
    }


    // ---------------- RENDER ----------------
    private void Redraw()
    {
        // update tile visuals based on board data
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
                    tile.SetClickable(false); // broken can't be clicked
                }
            }
        }
    }

    // ---------------- GAME OVER ----------------
    private void EndGame(Player winner, string reason)
    {
        _gameOver = true;
        Debug.Log($"Game Over! {reason}. Winner: {NameOf(winner)}");
        if (gameoverSound != null)
            gameoverSound.Play();

        if (_gameOverPopup != null)
            _gameOverPopup.SetActive(true);

        if (_gameOverText != null)
            _gameOverText.text = $"Winner: {NameOf(winner)}";
    }

    private bool CheckImmediateGameOver()
    {
        // if current player has no moves at start -> other wins
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
}
