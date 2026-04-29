using UnityEngine;

public class CheckPoint : MonoBehaviour
{
	[SerializeField] private GameManager GM;
	[SerializeField] private AudioSource cheackpointSource;
	[SerializeField] private GameObject CheackEffect;

	void Start()
	{
		GM = GameManager.Instance;
	}

	private void OnTriggerEnter2D(Collider2D other)
	{
		if (GM == null) return;

		if (other.CompareTag("Player"))
		{
			CheckpointData.Save(transform.position, GM.time, out bool isNewCheckpoint);

			if (isNewCheckpoint)
			{
				cheackpointSource.Play();
				CheackEffect.SetActive(true);
			}
		}
	}
}

public static class CheckpointData
{
	private const string CHECKPOINT_X = "CheckpointX";
	private const string CHECKPOINT_Y = "CheckpointY";
	private const string CHECKPOINT_Z = "CheckpointZ";
	private const string CHECKPOINT_TIME = "CheckpointTime";
	private const string HAS_CHECKPOINT = "HasCheckpoint";

	public static void Save(Vector3 position, float time, out bool isNewCheckpoint)
	{
		isNewCheckpoint = false;

		// 既存のチェックポイントをロード
		bool hasExisting = Load(out Vector3 existingPos, out float existingTime);

		// 既存チェックポイントがあり、新しい位置が低い場合は更新しない
		if (hasExisting && position.y < existingPos.y)
		{
			isNewCheckpoint = false;
			return;
		}

		// 新しいチェックポイントを保存
		PlayerPrefs.SetFloat(CHECKPOINT_X, position.x);
		PlayerPrefs.SetFloat(CHECKPOINT_Y, position.y);
		PlayerPrefs.SetFloat(CHECKPOINT_Z, position.z);
		PlayerPrefs.SetFloat(CHECKPOINT_TIME, time);
		PlayerPrefs.SetInt(HAS_CHECKPOINT, 1);
		PlayerPrefs.Save();

		isNewCheckpoint = true;
	}

	public static bool Load(out Vector3 position, out float time)
	{
		if (PlayerPrefs.GetInt(HAS_CHECKPOINT, 0) == 0)
		{
			position = Vector3.zero;
			time = 0f;
			return false;
		}

		position = new Vector3(
			PlayerPrefs.GetFloat(CHECKPOINT_X, 0f),
			PlayerPrefs.GetFloat(CHECKPOINT_Y, 0f),
			PlayerPrefs.GetFloat(CHECKPOINT_Z, 0f)
		);
		time = PlayerPrefs.GetFloat(CHECKPOINT_TIME, 0f);
		return true;
	}

	public static void Clear()
	{
		PlayerPrefs.DeleteKey(CHECKPOINT_X);
		PlayerPrefs.DeleteKey(CHECKPOINT_Y);
		PlayerPrefs.DeleteKey(CHECKPOINT_Z);
		PlayerPrefs.DeleteKey(CHECKPOINT_TIME);
		PlayerPrefs.DeleteKey(HAS_CHECKPOINT);
		PlayerPrefs.Save();
	}
}