using UnityEngine;
using System.Collections.Generic;
using ScreenPocket;

public enum BackgroundType
{
	RangeSpawn,     // Type1: 特定の高さでのみ生成
	HorizontalLoop  // Type2: 横ループ移動
}

[System.Serializable]
public class BackgroundObject
{
	[Header("General Settings")]
	public BackgroundType type;
	public GameObject prefab;
	public float targetHeight; // 生成基準の高さ（ゲーム内メートル）

	// ★ 変更点: rangeThreshold を削除しました
	// public float rangeThreshold = 6f; 

	[Header("Parallax Settings")]
	[Tooltip("画面高さに対する移動倍率。1.0なら通常の追従。0.5なら遠景としてゆっくり動く")]
	public float scrollScale = 1.0f;

	[Header("Type 1: Position Settings")]
	public float ratioX;

	// 内部変数
	private GameObject activeInstance;
	private List<GameObject> loopInstances;
	private bool isInitializedLoop = false;

	// Update引数でCameraを受け取るようにします
	public void Update(Transform parent, Camera camera, float playerHeight)
	{
		if (prefab == null || camera == null) return;

		switch (type)
		{
			case BackgroundType.RangeSpawn:
				UpdateRangeSpawn(parent, camera, playerHeight);
				break;
			case BackgroundType.HorizontalLoop:
				UpdateHorizontalLoop(parent, camera, playerHeight);
				break;
		}
	}

	// --- 共通計算ロジック ---

	// ★ 変更点: カメラのサイズを基に、ワールド座標(m)をスクリーン座標(px)に変換します
	private float CalculateScreenY(float currentHeight, Camera camera)
	{
		// 1. プレイヤーとオブジェクトの距離（メートル）
		float heightDiff = targetHeight - currentHeight;

		// 2. カメラのOrthographicSize(画面の縦半分メートル)から、1メートルあたりのピクセル数を計算
		//    WorldHeight = size * 2
		float worldScreenHeight = camera.orthographicSize * 2f;
		float pixelsPerMeter = Screen.height / worldScreenHeight;

		// 3. 画面上のY座標オフセットを計算 (scrollScaleで視差効果を適用)
		float screenY = heightDiff * pixelsPerMeter * scrollScale;

		return screenY;
	}
	private float GetEstimatedPixelWidth()
	{
		if (activeInstance != null)
		{
			var rt = activeInstance.GetComponent<RectTransform>();
			if (rt != null) return rt.rect.width * rt.lossyScale.x * activeInstance.transform.lossyScale.x;
		}
		if (prefab != null)
		{
			var rt = prefab.GetComponent<RectTransform>();
			if (rt != null) return rt.rect.width * rt.lossyScale.x * prefab.transform.lossyScale.x;
		}
		return Screen.width * 0.2f; // フォールバック
	}

	// 背景オブジェクトの見た目縦サイズ(ピクセル)を推定
	private float GetEstimatedPixelHeight()
	{
		if (activeInstance != null)
		{
			var rt = activeInstance.GetComponent<RectTransform>();
			if (rt != null) return rt.rect.height * rt.lossyScale.y * activeInstance.transform.lossyScale.y;
		}
		if (prefab != null)
		{
			var rt = prefab.GetComponent<RectTransform>();
			if (rt != null) return rt.rect.height * rt.lossyScale.y * prefab.transform.lossyScale.y;
		}
		return Screen.height * 0.2f; // フォールバック
	}

	// ★ 変更点: 判定にもCameraが必要です
	private bool IsVerticallyVisible(float currentHeight, Camera camera, float marginPixels)
	{
		float y = CalculateScreenY(currentHeight, camera);
		float ObjectHight = GetEstimatedPixelHeight();
		float Screenhight = Screen.height;

		// 画面内に入っているか（上下マージン込み）
		return (y + ObjectHight) >= (-Screenhight - marginPixels) && (y - ObjectHight) <= (Screenhight + marginPixels);
	}

	// --- Type 1: 区間生成ロジック ---
	private void UpdateRangeSpawn(Transform parent, Camera camera, float currentHeight)
	{
		// ★ 修正ポイント: マージンに差をつける
		// 生成時: 画面端ギリギリ（または少し余裕を持つ程度）で判定
		float spawnMargin = 10f;

		bool visibleForSpawn = IsVerticallyVisible(currentHeight, camera, spawnMargin);

		if (visibleForSpawn)
		{
			float screenY = CalculateScreenY(currentHeight, camera);
			if (activeInstance == null)
			{
				activeInstance = Object.Instantiate(prefab, parent);
				activeInstance.SetActive(true);
				// ... (座標設定はそのまま) ...
				activeInstance.transform.localPosition = new Vector3((ratioX) * Screen.width, screenY, 0f);
			}
			else
			{
				// ... (座標更新はそのまま) ...
				Vector3 pos = activeInstance.transform.localPosition;
				pos.x = (ratioX) * Screen.width;
				pos.y = screenY;
				activeInstance.transform.localPosition = pos;
			}
		}
		else
		{
			if (activeInstance != null)
			{
				Object.Destroy(activeInstance);
				activeInstance = null;
			}
		}
	}
	// --- Type 2: 横ループロジック ---
	private void UpdateHorizontalLoop(Transform parent, Camera camera, float playerHeight)
	{
		float spawnMargin = GetEstimatedPixelWidth();

		bool visibleForSpawn = IsVerticallyVisible(playerHeight, camera, spawnMargin);

		if (visibleForSpawn)
		{
			// まだ生成されていなければ生成そしてsetactive
			if (!isInitializedLoop)
			{
				InitializeLoop(parent);
				foreach (var obj in loopInstances)
				{
					if (obj != null) obj.SetActive(true);
				}
			}

			// ★ 修正ポイント: 常に位置を更新（縦移動 ＋ 横ループ）
			UpdateLoopPositions(camera, playerHeight);
		}
		else
		{
			if (isInitializedLoop)
			{
				ClearLoopInstances();
			}
		}
	}


	private void UpdateLoopPositions(Camera camera, float currentHeight)
	{
		if (loopInstances == null || loopInstances.Count < 3) return;

		// 1. 縦位置（Y）の計算（Type 1と同じロジック）
		float screenY = CalculateScreenY(currentHeight, camera);

		// 2. 横位置（X）の計算（無限ループ）
		// カメラの現在X座標を取得
		float camX = camera.transform.position.x;
		float bgWidth = 18; // 背景オブジェクトの幅（ワールド単位）

		float snapCenterX = -camX / bgWidth * Screen.width * scrollScale;
		//snapCenterを画面名に留めるように Screen.width の倍数で調整
		snapCenterX = snapCenterX - Mathf.Floor(snapCenterX / Screen.width) * Screen.width;

		// 左
		SetLoopInstancePosition(loopInstances[0], snapCenterX - Screen.width, screenY);
		// 中央
		SetLoopInstancePosition(loopInstances[1], snapCenterX, screenY);
		// 右
		SetLoopInstancePosition(loopInstances[2], snapCenterX + Screen.width, screenY);
	}

	// ヘルパー: 個別の座標設定
	private void SetLoopInstancePosition(GameObject obj, float x, float y)
	{
		if (obj == null) return;
		Vector3 pos = obj.transform.localPosition;
		pos.x = x;
		pos.y = y;
		obj.transform.localPosition = pos;
	}

	// ... (InitializeLoop, ClearLoopInstances, Cleanup は変更なし) ...
	private void InitializeLoop(Transform parent)
	{
		loopInstances = new List<GameObject>();
		float width = Screen.width;
		for (int i = -1; i <= 1; i++)
		{
			GameObject bg = Object.Instantiate(prefab, parent);
			bg.transform.localPosition = new Vector3(i * width, 0f, 0f);
			loopInstances.Add(bg);
		}
		isInitializedLoop = true;
	}



	private void ClearLoopInstances()
	{
		if (loopInstances != null)
		{
			foreach (var obj in loopInstances) if (obj != null) Object.Destroy(obj);
			loopInstances.Clear();
		}
		isInitializedLoop = false;
	}
	public void Cleanup()
	{
		if (activeInstance != null) Object.Destroy(activeInstance);
		ClearLoopInstances();
	}
}

// =========================================================
// Main Class Update
// =========================================================
public class BackGround : MonoBehaviour
{
	[Header("Sky Settings")]
	public Color SkyTopColor;
	public Color SkyBottomColor;
	public RawImage4Color skyImage;

	[Header("Camera")]
	public Camera mainCamera; // TransformではなくCamera型に変更推奨

	[Header("Background Objects List")]
	public List<BackgroundObject> backgroundObjects = new List<BackgroundObject>();

	private GameManager gameManager;

	void Start()
	{
		var gmTransform = transform.Find("GameManager");
		if (gmTransform != null) gameManager = gmTransform.GetComponent<GameManager>();
		else gameManager = FindObjectOfType<GameManager>();

		// Cameraコンポーネント取得の保険
		if (mainCamera == null) mainCamera = Camera.main;
	}

	void Update()
	{
		if (gameManager == null || mainCamera == null) return;

		UpdateSky();

		float playerHeight = mainCamera.transform.position.y; // Y座標取得

		foreach (var bgObject in backgroundObjects)
		{
			// ★ 変更点: Cameraコンポーネント自体を渡す
			bgObject.Update(transform, mainCamera, playerHeight);
		}
	}

	// ... (UpdateSky, OnDestroy は変更なし) ...
	void UpdateSky()
	{
		float currentMaxHeight = gameManager.maxHeight;
		float height = Mathf.Clamp(mainCamera.transform.position.y + 5f, 0f, currentMaxHeight);
		float t1 = height / currentMaxHeight;
		height = Mathf.Clamp(mainCamera.transform.position.y - 5f, 0f, currentMaxHeight);
		float t2 = height / currentMaxHeight;
		Color currentColor1 = Color.Lerp(SkyBottomColor, SkyTopColor, t1);
		Color currentColor2 = Color.Lerp(SkyBottomColor, SkyTopColor, t2);

		if (skyImage != null)
		{
			skyImage.leftTopColor = currentColor1;
			skyImage.rightTopColor = currentColor1;
			skyImage.leftBottomColor = currentColor2;
			skyImage.rightBottomColor = currentColor2;
		}
	}

	void OnDestroy()
	{
		foreach (var bgObject in backgroundObjects) bgObject.Cleanup();
	}

	public void AddBackgroundObject(GameObject targetobject, BackgroundObject bgObject)
	{
		if (bgObject != null && !backgroundObjects.Contains(bgObject))
		{
			Destroy(bgObject.prefab.GetComponent<BackGroundComponent>());
			targetobject.SetActive(false);
			backgroundObjects.Add(bgObject);
		}
	}
}