using UnityEngine;

public class SaveDifficulty : MonoBehaviour
{

	public void Save(int dif)
	{
		PlayerPrefs.SetInt("Difficulty", dif);
		PlayerPrefs.Save();
	}
}

