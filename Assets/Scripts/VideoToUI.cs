using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

public class VideoToUI : MonoBehaviour
{
	void Start()
	{
		var videoPlayer = GetComponent<VideoPlayer>();
		var rawImage = GetComponent<RawImage>();

		// 動画のテクスチャをUIの画像としてセットする
		videoPlayer.sendFrameReadyEvents = true;
		videoPlayer.frameReady += (source, frameIdx) =>
		{
			rawImage.texture = source.texture;
		};
	}
}