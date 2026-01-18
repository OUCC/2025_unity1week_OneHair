using UnityEngine;
using System.Collections.Generic;
using ScreenPocket;
// using ScreenPocket; // GameManagerのnamespaceがあれば有効化

// =========================================================
// 1. 設定用 Enum と クラス定義
// =========================================================

/// <summary>
/// 背景の動きの種類
/// </summary>
public enum BackgroundType
{
	RangeSpawn,     // Type1: 特定の高さでのみ生成
	HorizontalLoop  // Type2: 横方向にループ移動
}

/// <summary>
/// 個別の背景オブジェクト設定
/// [System.Serializable] をつけることで、
/// BackGroundコンポーネントのインスペクター上でリストとして扱えるようになります。
/// </summary>
[System.Serializable]
public class BackgroundObject
{
	[Header("General Settings")]
	public string name = "New Background"; // 管理しやすいように名前をつける
	public BackgroundType type;
	public GameObject prefab;
	public float targetHeight; // 生成基準の高さ

	[Header("Type 1: Range Settings (区間生成用)")]
	public float offsetX;
	public float rangeThreshold = 6f; // 生成判定の範囲

	[Header("Type 2: Loop Settings (横ループ用)")]
	public float scrollSpeedX = 30f;

	// 内部変数（生成した実体などを保持）
	private GameObject activeInstance;
	private List<GameObject> loopInstances;
	private bool isInitializedLoop = false;

	/// <summary>
	/// 更新処理（BackGroundクラスから毎フレーム呼ばれる）
	/// </summary>
	public void Update(Transform parent, Transform camera, float playerHeight)
	{
		if (prefab == null) return;

		switch (type)
		{
			case BackgroundType.RangeSpawn:
				UpdateRangeSpawn(parent, camera, playerHeight);
				break;
			case BackgroundType.HorizontalLoop:
				UpdateHorizontalLoop(parent, camera, playerHeight); // 引数追加
				break;
		}
	}

	// --- Type 1: 区間生成ロジック ---
	private void UpdateRangeSpawn(Transform parent, Transform camera, float currentHeight)
	{
		// 範囲内かどうかの判定
		bool inRange = (currentHeight >= targetHeight - rangeThreshold && currentHeight <= targetHeight + rangeThreshold);

		if (inRange)
		{
			if (activeInstance == null)
			{
				activeInstance = Object.Instantiate(prefab, parent);
				activeInstance.transform.localPosition = new Vector3(offsetX, targetHeight, 0f);
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

		// 縦位置の更新（カメラに合わせて逆スクロール）
		if (activeInstance != null)
		{
			float camY = camera.position.y;
			// ローカル座標で制御（親の動きに依存しないよう計算）
			activeInstance.transform.localPosition = new Vector3(offsetX, targetHeight - camY, 0f);
		}
	}

	// --- Type 2: 横ループロジック ---
	private void UpdateHorizontalLoop(Transform parent, Transform camera, float playerHeight)
	{
		if (!isInitializedLoop) InitializeLoop(parent);

		float camY = camera.position.y;
		float width = Screen.width; // 画面幅基準（必要に応じて固定値に変更可）

		foreach (var bg in loopInstances)
		{
			if (bg == null) continue;

			Vector3 pos = bg.transform.localPosition;

			// 縦：指定高さからカメラ位置を引いて逆スクロールさせる
			pos.y = targetHeight - camY;

			// 横：自動スクロール
			pos.x -= scrollSpeedX * Time.deltaTime;

			// ループ処理
			if (pos.x <= -width)
			{
				pos.x += width * 3f; // 3枚並べているので3枚分戻す
			}

			bg.transform.localPosition = pos;
		}
	}

	private void InitializeLoop(Transform parent)
	{
		loopInstances = new List<GameObject>();
		float width = Screen.width;

		// -1(左), 0(中央), 1(右) の3枚を生成して隙間なくループさせる
		for (int i = -1; i <= 1; i++)
		{
			GameObject bg = Object.Instantiate(prefab, parent);
			bg.transform.localPosition = new Vector3(i * width, targetHeight, 0f);
			loopInstances.Add(bg);
		}
		isInitializedLoop = true;
	}

	/// <summary>
	/// シーン終了時などの掃除
	/// </summary>
	public void Cleanup()
	{
		if (activeInstance != null) Object.Destroy(activeInstance);
		if (loopInstances != null)
		{
			foreach (var obj in loopInstances) if (obj != null) Object.Destroy(obj);
			loopInstances.Clear();
		}
	}
}


// =========================================================
// 2. メインの管理クラス (MonoBehaviour)
// =========================================================

public class BackGround : MonoBehaviour
{
	// =====================
	// Sky 設定
	// =====================
	[Header("Sky Settings")]
	public Color SkyTopColor;
	public Color SkyBottomColor;
	public RawImage4Color skyImage;

	// =====================
	// Camera
	// =====================
	[Header("Camera")]
	public Transform mainCamera;

	// =====================
	// Background Objects (リスト)
	// =====================
	[Header("Background Objects List")]
	// ★ここがリストになっているので、インスペクターで自由に増やせます★
	public List<BackgroundObject> backgroundObjects = new List<BackgroundObject>();

	private GameManager gameManager;

	void Start()
	{
		// GameManagerを探して取得
		var gmTransform = transform.Find("GameManager");
		if (gmTransform != null)
		{
			gameManager = gmTransform.GetComponent<GameManager>();
		}
		else
		{
			gameManager = FindObjectOfType<GameManager>();
		}
	}

	void Update()
	{
		if (gameManager == null) return;

		UpdateSky();
		UpdateBackgroundObjects();
	}

	// Skyグラデーション
	void UpdateSky()
	{
		float currentMaxHeight = gameManager.maxHeight;
		float height = Mathf.Clamp(gameManager.playerHeight + 5f, 0f, currentMaxHeight);
		float t1 = height / currentMaxHeight;
		height = Mathf.Clamp(gameManager.playerHeight - 5f, 0f, currentMaxHeight);
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

	// 全背景オブジェクトの更新
	void UpdateBackgroundObjects()
	{
		// リストに登録された全ての背景設定を実行
		foreach (var bgObject in backgroundObjects)
		{
			bgObject.Update(transform, mainCamera, gameManager.playerHeight);
		}
	}

	void OnDestroy()
	{
		foreach (var bgObject in backgroundObjects)
		{
			bgObject.Cleanup();
		}
	}
}