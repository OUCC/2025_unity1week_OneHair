using UnityEngine;

public class Goal : MonoBehaviour
{
	[SerializeField] private string playerTag = "Player";

	private bool isCleared = false;

	private void OnCollisionEnter2D(Collision2D other)
	{
		if (isCleared) return;
		if (!other.gameObject.CompareTag(playerTag)) return;

		isCleared = true;

		if (GameManager.Instance != null)
		{
			GameManager.Instance.ClearGame();
		}
		else
		{
			Debug.LogWarning("GameManager.Instance が存在しません");
		}
	}
}
