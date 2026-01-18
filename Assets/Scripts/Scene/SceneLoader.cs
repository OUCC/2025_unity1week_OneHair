using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
	/// <summary>
	/// シーン名で移動
	/// </summary>
	public void LoadScene(string sceneName)
	{
		SceneManager.LoadScene(sceneName);
	}

	/// <summary>
	/// シーン番号で移動
	/// </summary>
	public void LoadScene(int sceneIndex)
	{
		SceneManager.LoadScene(sceneIndex);
	}

	/// <summary>
	/// 現在のシーンをリロード
	/// </summary>
	public void ReloadScene()
	{
		SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
	}
}
