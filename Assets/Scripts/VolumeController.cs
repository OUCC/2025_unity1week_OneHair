using UnityEngine;
using UnityEngine.UI;

public class VolumeController : MonoBehaviour
{
	[SerializeField] private AudioSource kickSource; // 制御したい音源
	[SerializeField] private AudioSource grapleSource; // 制御したい音源
	[SerializeField] private AudioSource cheackpointSource; // 制御したい音源
	[SerializeField] private AudioSource goalSource; // 制御したい音源
	[SerializeField] private AudioSource bgmSource; // 制御したい音源	
	[SerializeField] private float defaultVolume = 0.5f; // デフォルトの音量
	void Start()
	{
		// 1. 保存されている音量を読み込む（デフォルトは 0.5f）
		float savedVolume = PlayerPrefs.GetFloat("BGM_Volume", 0.5f);

		// 2. 音量とスライダーに反映
		kickSource.volume = defaultVolume;
		grapleSource.volume = defaultVolume;
		cheackpointSource.volume = defaultVolume;
		goalSource.volume = defaultVolume;
		bgmSource.volume = defaultVolume;


	}

}