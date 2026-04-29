using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;
using unityroom.Api;
using System.Data.Common;

public class GameManager : MonoBehaviour
{
	public static GameManager Instance;
	public Vector2 CheckPointPos;

	public enum GameState
	{
		Playing,
		Clear,
		GameOver
	}

	[Header("Game State")]
	public GameState currentState = GameState.Playing;
	public float time;
	public int score;

	[Header("Height Score")]
	public float playerHeight;
	private int currentscore = 0;
	public float maxHeight;

	[Header("Checkpoint Data")]
	private Vector3 checkpointPosition;
	private float checkpointTime;
	private bool hasCheckpoint = false;

	[Header("UI")]
	[SerializeField] private GameObject FinUI;
	[SerializeField] private GameObject clearUI;
	[SerializeField] private GameObject gameOverUI;
	[SerializeField] private TextMeshProUGUI timeText;
	[SerializeField] private TextMeshProUGUI scoreText;
	[SerializeField] private TextMeshProUGUI heightText;
	[SerializeField] private Player player;

	[SerializeField] private float heightOffset = 0f;

	private Transform playerTransform;

	private void Awake()
	{
		// DontDestroyOnLoadで保持
		if (Instance != null && Instance != this)
		{
			Destroy(gameObject);
			return;
		}
		Instance = this;

	}

	private void Start()
	{
		// チェックポイントデータがあれば復元
		if (CheckpointData.Load(out Vector3 savedPos, out float savedTime) && player != null)
		{
			player.transform.position = savedPos;
			time = savedTime;
		}
	}

	private void Update()
	{
		if (currentState != GameState.Playing) return;

		time += Time.deltaTime;

		if (playerTransform == null)
			FindPlayer();

		if (playerTransform != null)
		{
			float currentHeight = playerTransform.position.y + heightOffset;
			playerHeight = currentHeight;
			score = Mathf.FloorToInt(currentHeight);
			if (score > currentscore)
			{
				currentscore = score;
			}
			if (currentHeight < -5.0f)
			{
				GameOver();
			}

			if (currentHeight > maxHeight)
				score = Mathf.FloorToInt(maxHeight);

			if (heightText != null)
				heightText.text = "高さ：" + currentHeight.ToString("F2") + " m";
		}

		if (timeText != null)
			timeText.text = "時間：" + time.ToString("F2") + " 秒";

		if (scoreText != null)
			scoreText.text = "スコア：" + currentscore + "m";

		if (Keyboard.current.cKey.wasPressedThisFrame)
		{
			CheckpointData.Clear();
			Debug.Log("Checkpoint data cleared.");
			//シーンを再読み込みしてリセット
			UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
		}
	}

	void FindPlayer()
	{
		GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
		if (playerObj != null)
			playerTransform = playerObj.transform;
	}

	public void ClearGame()
	{
		if (currentState != GameState.Playing) return;
		currentState = GameState.Clear;
		if (FinUI != null) FinUI.SetActive(true);
		if (clearUI != null) clearUI.SetActive(true);
		score += (int)player.currentHP;
		CheckpointData.Clear();
		UnityroomApiClient.Instance.SendScore(1, score, ScoreboardWriteMode.HighScoreDesc);
	}

	public void GameOver()
	{
		if (currentState != GameState.Playing) return;
		currentState = GameState.GameOver;
		if (FinUI != null) FinUI.SetActive(true);
		if (gameOverUI != null) gameOverUI.SetActive(true);
		//UnityroomApiClient.Instance.SendScore(1, score, ScoreboardWriteMode.HighScoreDesc);
	}

	public void ClearGameData()
	{
		CheckpointData.Clear();
	}

	public bool IsPlaying()
	{
		return currentState == GameState.Playing;
	}
}
