using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;

public class GameManager : MonoBehaviour
{
	public static GameManager Instance;

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
	public float maxHeight;

	[Header("UI")]
	[SerializeField] private GameObject FinUI;
	[SerializeField] private GameObject clearUI;
	[SerializeField] private GameObject gameOverUI;
	[SerializeField] private TextMeshProUGUI timeText;
	[SerializeField] private TextMeshProUGUI scoreText;
	[SerializeField] private TextMeshProUGUI heightText;

	[SerializeField] private float heightOffset = 0f;

	private Transform playerTransform;

	private void Awake()
	{
		// シーン内限定シングルトン
		if (Instance != null && Instance != this)
		{
			Destroy(gameObject);
			return;
		}
		Instance = this;
	}

	private void Update()
	{
		if (currentState != GameState.Playing) return;

		if (Keyboard.current.gKey.isPressed) { GameOver(); }
		if (Keyboard.current.hKey.isPressed) { ClearGame(); }

		time += Time.deltaTime;

		if (playerTransform == null)
			FindPlayer();

		if (playerTransform != null)
		{
			float currentHeight = playerTransform.position.y + heightOffset;
			score = Mathf.FloorToInt(currentHeight);

			if (currentHeight > maxHeight)
				score = Mathf.FloorToInt(maxHeight);

			if (heightText != null)
				heightText.text = "高さ：" + currentHeight.ToString("F2") + " m";
		}

		if (timeText != null)
			timeText.text = "時間：" + time.ToString("F2") + " 秒";

		if (scoreText != null)
			scoreText.text = "スコア：" + score + "m";
	}

	void FindPlayer()
	{
		GameObject player = GameObject.FindGameObjectWithTag("Player");
		if (player != null)
			playerTransform = player.transform;
	}

	public void ClearGame()
	{
		currentState = GameState.Clear;
		if (FinUI != null) FinUI.SetActive(true);
		if (clearUI != null) clearUI.SetActive(true);
	}

	public void GameOver()
	{
		currentState = GameState.GameOver;
		if (FinUI != null) FinUI.SetActive(true);
		if (gameOverUI != null) gameOverUI.SetActive(true);
	}

	public void ResetGame()
	{
		time = 0f;
		score = 0;
		maxHeight = 0f;
		currentState = GameState.Playing;
	}

	public bool IsPlaying()
	{
		return currentState == GameState.Playing;
	}
}
