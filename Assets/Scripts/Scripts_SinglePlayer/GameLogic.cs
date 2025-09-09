using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SlotState = GameBoard.SlotState;  // shorter name

public class GameLogic : MonoBehaviour
{
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
    private Player current = Player.P1; // blue starts

    private enum Phase { Move, Break }
    private Phase phase = Phase.Move;   // first phase is move

    private bool _gameOver = false;

    [Header("UI GameOver Popup")]
    [SerializeField] private GameObject _gameOverPopup; // drag PopupGameOver here
    [SerializeField] private TextMeshProUGUI _gameOverText;  // text to show winner
    [SerializeField] private TextMeshProUGUI _whosTurnText;  // text to show winner

    private float _aiDelayMove = 0.5f;
    private float _aiDelayBreak = 0.2f;

    [Header("Sounds")]
    public AudioSource gameoverSound;
    public AudioSource stepSound;
    public AudioSource breakingSound;

    private bool _aiBusy = false;

    // ---------------- UNITY EVENTS ----------------
    void OnEnable()
    {
        Slot.OnClickSlot += OnClickSlot;
        Btn_Restart.OnClickRestart += OnClickRestart;
    }
    void OnDisable()
    {
        Slot.OnClickSlot -= OnClickSlot;
        Btn_Restart.OnClickRestart -= OnClickRestart;
    }


    void Start()
    {
        if (_gameOverPopup != null)
            _gameOverPopup.SetActive(false); // hide popup at start
        // build visual grid
        BuildBoard();

        // create data board
        _board = new GameBoard(width, height);
        _check = new BoardStateCheck();

        // put players at start positions
        PlacePlayersStart();

        // draw everything
        Redraw();

        ShowTurnText("BLUE TURN");


        // check if already stuck
        if (CheckImmediateGameOver()) return;

        // if it's red's turn, start AI
        TryStartRedAI();
    }

    // ---------------- BUILD ----------------
    private void BuildBoard()
    {
        tiles = new Slot[width, height];
        int idx = 0;

        // spawn tiles in grid
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

        // center camera
        if (mainCamera != null)
            mainCamera.transform.position = new Vector3((width - 1) / 2f, (height - 1) / 2f, -10f);
    }

    private void PlacePlayersStart()
    {
        _board.ResetStart();   // reset board with 2 players
        current = Player.P1;   // blue starts
        phase = Phase.Move;
        _gameOver = false;
    }



    // ---------------- RESTART THE GAME  WITH BUTTON ----------------

    private void OnClickRestart()
    {
        Debug.Log("GameLogic: Restarting game");

        // hide gameover UI
        if (_gameOverPopup != null)
            _gameOverPopup.SetActive(false);

        // reset data
        _board = new GameBoard(width, height);
        _check = new BoardStateCheck();
        ShowTurnText("BLUE TURN");


        // reset players + flags
        PlacePlayersStart();

        // redraw the tiles
        Redraw();

        // check gameover at start
        if (CheckImmediateGameOver()) return;

        // if red’s turn -> AI
        TryStartRedAI();
    }


    // ---------------- PLAYER INPUT ----------------
    private void OnClickSlot(int slotIndex)
    {
        if (_gameOver) return;             // stop if finished
        if (current == Player.P2) return; // only blue clicks
        if (_aiBusy) return;               // ignore while AI is thinking
        if (!_check.IndexInBounds(_board, slotIndex)) return;

        if (phase == Phase.Move)
        {
            // MOVE phase
            if (_check.IsLegalMoveForCurrent(_board, true, slotIndex))
            {
                Vector2Int dst = _check.FromIndex(_board, slotIndex);
                Vector2Int cur = _board.GetP1Pos();

                // update board
                _board.Set(cur.x, cur.y, SlotState.Empty);
                _board.Set(dst.x, dst.y, SlotState.P1);
                _board.SetP1Pos(dst);

                Redraw();

                // play step sound
                if (stepSound != null)
                    stepSound.Play();

                // check if red has no moves left
                if (!_check.OpponentHasMove(_board, true))
                {
                    EndGame(Player.P1, "Red stuck");
                    return;
                }

                // now go to break phase
                phase = Phase.Break;
            }
        }
        else
        {
            // BREAK phase
            if (_check.IsLegalBreakAtIndex(_board, slotIndex))
            {
                Vector2Int pos = _check.FromIndex(_board, slotIndex);
                _board.Set(pos.x, pos.y, SlotState.Broken);

                if (breakingSound != null)
                    breakingSound.Play();

                Redraw();

                // check if blue just trapped itself
                if (!_board.AreAdjacent(_board.GetP1Pos(), _board.GetP2Pos()) &&
                    !_check.CurrentHasMove(_board, true))
                {
                    EndGame(Player.P2, "Blue trapped itself");
                    return;
                }

                // switch turn to red
                current = Player.P2;
                phase = Phase.Move;

                ShowTurnText("RED TURN");


                // check if red has any moves
                if (!_check.CurrentHasMove(_board, false))
                {
                    EndGame(Player.P1, "Red stuck");
                    return;
                }

                // start AI
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
        if (!_gameOver && current == Player.P2 && !_aiBusy)
            StartCoroutine(RedTurn());
    }

    private IEnumerator RedTurn()
    {
        _aiBusy = true;

        // wait a bit before making the AI move
        yield return new WaitForSeconds(_aiDelayMove);

        // if red cannot move, blue wins
        if (!_check.CurrentHasMove(_board, false))
        {
            EndGame(Player.P1, "Red stuck");
            _aiBusy = false;
            yield break;
        }

        // MOVE
        var moves = _board.GetLegalMovesFrom(_board.GetP2Pos());
        Vector2Int move = moves[Random.Range(0, moves.Count)];
        Vector2Int old = _board.GetP2Pos();

        _board.Set(old.x, old.y, SlotState.Empty);
        _board.Set(move.x, move.y, SlotState.P2);
        _board.SetP2Pos(move);

        Redraw();

        if (stepSound != null)
            stepSound.Play();

        // optional: short delay after move too
        yield return new WaitForSeconds(_aiDelayMove);

        // BREAK (pick random empty cell)
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

        // check if red trapped itself
        if (!_board.AreAdjacent(_board.GetP1Pos(), _board.GetP2Pos()) &&
            !_check.CurrentHasMove(_board, false))
        {
            EndGame(Player.P1, "Red trapped itself");
            _aiBusy = false;
            yield break;
        }

        // switch back to blue
        current = Player.P1;
        phase = Phase.Move;

        ShowTurnText("BLUE TURN");

        // if blue is stuck now, red wins
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
        // draw all board cells
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
        if (gameoverSound != null)
            gameoverSound.Play();

        if (_gameOverPopup != null)
            _gameOverPopup.SetActive(true);

        if (_gameOverText != null)
            _gameOverText.text = $"Winner: {NameOf(winner)}";
    }

    private bool CheckImmediateGameOver()
    {
        bool currentIsP1 = (current == Player.P1);
        if (!_check.CurrentHasMove(_board, currentIsP1))
        {
            EndGame(Other(current), $"{NameOf(current)} stuck");
            return true;
        }
        return false;
    }

    // ---------------- SMALL HELPERS ----------------
    private Player Other(Player p) => (p == Player.P1) ? Player.P2 : Player.P1;
    private string NameOf(Player p) => (p == Player.P1) ? "BLUE" : "RED";
}
