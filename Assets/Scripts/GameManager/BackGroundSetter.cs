using UnityEngine;
using UnityEngine.UI;

public class BackGroundSetter : MonoBehaviour
{
	// Start is called once before the first execution of Update after the MonoBehaviour is created
	BackgroundObject bgobject;
	[SerializeField] float scale = 1;
	[SerializeField] bool isLoop;
	RectTransform rectTransform;
	void Start()
	{
		rectTransform = GetComponent<RectTransform>();
		bgobject = new BackgroundObject();
		bgobject.scrollScale = scale;
		bgobject.prefab = this.gameObject;
		bgobject.targetHeight = rectTransform.anchoredPosition.y / Screen.height * 10f; // 仮の変換
		if (isLoop)
		{
			bgobject.type = BackgroundType.HorizontalLoop;
		}
		else
		{
			bgobject.ratioX = rectTransform.anchoredPosition.x / Screen.width;
			bgobject.type = BackgroundType.RangeSpawn;
		}
		transform.parent.GetComponent<BackGround>().AddBackgroundObject(gameObject, bgobject);
	}

}
