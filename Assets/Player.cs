using UnityEngine;
using UnityEngine.InputSystem;

public class HairController : MonoBehaviour
{
	// =====================
	// 設定
	// =====================

	[Header("Hair Specs")]
	public float minLength = 0.5f;
	public float maxLength = 2.0f;
	public float extendDuration = 0.1f;
	public float rootOffset = 0.5f;

	[Header("Kick")]
	public float kickRange = 1.2f;
	public float kickForce = 20f;
	public float kickCooldown = 1.0f;
	public float kickFlashTime = 0.1f;

	[Header("Grapple")]
	public float swingForce = 20f;

	[Header("Colors")]
	public Color colorNormal = Color.black;
	public Color colorKick = Color.red;
	public Color colorGrapple = Color.blue;
	public Color colorExtending = Color.yellow;

	[Header("References")]
	public LineRenderer hairRenderer;
	public Animator faceAnimator;

	[Header("Face Settings (Random Range)")]
	// 伸ばし中・グラップル中の顔の範囲（例：1～2）
	public int strainFaceMin = 1;
	public int strainFaceMax = 3;

	// キック時の顔の範囲（例：3～4）
	public int kickFaceMin = 3;
	public int kickFaceMax = 4;

	// =====================
	// 内部
	// =====================

	private Rigidbody2D rb;
	private Camera mainCam;
	private Vector2 mousePos;

	private float currentLength;
	private bool isExtending;
	private bool isMaxExtended;
	private bool isGrappling;

	private float kickCooldownTimer;
	private float kickFlashTimer;
	private bool isKickFlashing;

	private Vector2 grapplePoint;
	private DistanceJoint2D joint;

	// ランダムに決まった顔を保存しておく変数
	private int currentStrainFaceIndex = -1;
	private int currentKickFaceIndex = -1;

	// =====================
	// Unity
	// =====================

	void Start()
	{
		rb = GetComponent<Rigidbody2D>();
		mainCam = Camera.main;

		hairRenderer.positionCount = 2;
		hairRenderer.useWorldSpace = true;

		currentLength = minLength;
	}

	void Update()
	{
		if (Mouse.current == null || Keyboard.current == null) return;

		if (kickCooldownTimer > 0)
			kickCooldownTimer -= Time.deltaTime;

		HandleRotation();
		HandleInput();
		UpdateVisuals();
	}

	void FixedUpdate()
	{
		if (isGrappling)
			HandleSwing();
	}

	// =====================
	// 入力
	// =====================

	void HandleInput()
	{
		// --- キック ---
		if (Keyboard.current.spaceKey.wasPressedThisFrame)
		{
			TryKick();
		}

		// --- グラップル（髪伸ばし） ---
		// キーを押した瞬間に、今回の「頑張る顔」をランダムで決める
		if (Keyboard.current.fKey.wasPressedThisFrame)
		{
			// Random.Rangeのint版は、Maxが含まれないので +1 する
			currentStrainFaceIndex = Random.Range(strainFaceMin, strainFaceMax + 1);
		}

		if (Keyboard.current.fKey.isPressed)
		{
			if (!isGrappling)
				ProcessGrappleExtension();
		}
		else
		{
			ResetGrapple();
		}
	}

	// =====================
	// キック
	// =====================

	void TryKick()
	{
		if (kickCooldownTimer > 0) return;

		Vector2 root = GetRootPos();
		Vector2 dir = transform.up;

		RaycastHit2D hit = Physics2D.Raycast(root, dir, kickRange);

		if (hit.collider == null || hit.collider.gameObject == gameObject)
			return;

		// 反動ジャンプ
		rb.linearVelocity = Vector2.zero;
		rb.AddForce(-dir * kickForce, ForceMode2D.Impulse);

		// キックした瞬間に、今回の「怒り顔」をランダムで決める
		currentKickFaceIndex = Random.Range(kickFaceMin, kickFaceMax + 1);

		isKickFlashing = true;
		kickFlashTimer = kickFlashTime;
		kickCooldownTimer = kickCooldown;
	}

	// =====================
	// グラップル
	// =====================

	void ProcessGrappleExtension()
	{
		isExtending = true;

		if (!isMaxExtended)
		{
			float speed = (maxLength - minLength) / Mathf.Max(extendDuration, 0.001f);
			currentLength += speed * Time.deltaTime;

			if (currentLength >= maxLength)
			{
				currentLength = maxLength;
				isMaxExtended = true;
			}
		}

		Vector2 root = GetRootPos();
		Vector2 dir = transform.up;

		RaycastHit2D hit = Physics2D.Raycast(root, dir, currentLength);

		if (hit.collider == null || hit.collider.gameObject == gameObject)
			return;

		if (isMaxExtended && !isGrappling)
			StartGrapple(hit.point, hit.collider.gameObject);
	}

	void StartGrapple(Vector2 point, GameObject hitObject)
	{
		isGrappling = true;
		isExtending = false;
		grapplePoint = point;

		joint = gameObject.AddComponent<DistanceJoint2D>();
		joint.autoConfigureConnectedAnchor = false;

		Rigidbody2D hitRb = hitObject.GetComponent<Rigidbody2D>();
		if (hitRb != null)
		{
			joint.connectedBody = hitRb;
			joint.connectedAnchor = hitObject.transform.InverseTransformPoint(point);
		}
		else
		{
			joint.connectedAnchor = point;
		}

		joint.anchor = Vector2.zero;
		joint.maxDistanceOnly = true;
		joint.distance = Vector2.Distance(transform.position, point);
		joint.enableCollision = true;
	}

	void HandleSwing()
	{
		Vector2 dir = (mousePos - (Vector2)transform.position).normalized;
		rb.AddForce(dir * swingForce);
	}

	void ResetGrapple()
	{
		isExtending = false;
		isMaxExtended = false;
		currentLength = minLength;

		if (joint != null)
			Destroy(joint);

		foreach (var j in GetComponents<DistanceJoint2D>())
			Destroy(j);

		isGrappling = false;
	}

	// =====================
	// 補助
	// =====================

	void HandleRotation()
	{
		mousePos = mainCam.ScreenToWorldPoint(Mouse.current.position.ReadValue());

		Vector2 dir = mousePos - (Vector2)transform.position;
		float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
		rb.MoveRotation(angle);
	}

	Vector2 GetRootPos()
	{
		return (Vector2)transform.position + (Vector2)transform.up * rootOffset;
	}

	// =====================
	// 表示
	// =====================

	void UpdateVisuals()
	{
		// --- アニメーター制御 ---
		if (faceAnimator != null)
		{
			int targetFace = -1; // デフォルト（真顔）

			if (isKickFlashing)
			{
				// キック中：キック時に決定したランダムな顔
				targetFace = currentKickFaceIndex;
			}
			else if (isExtending || isGrappling)
			{
				// 伸ばし中 or 掴まり中：キーを押した時に決定したランダムな顔
				// ※長さによる変化は削除しました
				targetFace = currentStrainFaceIndex;
			}

			faceAnimator.SetInteger("Face", targetFace);
		}

		// --- 描画処理 ---
		Vector2 root = GetRootPos();

		if (isKickFlashing)
		{
			kickFlashTimer -= Time.deltaTime;
			if (kickFlashTimer <= 0)
				isKickFlashing = false;
		}

		Vector2 tip =
			isGrappling ? grapplePoint :
			root + (Vector2)transform.up * currentLength;

		hairRenderer.SetPosition(0, root);
		hairRenderer.SetPosition(1, tip);

		if (isKickFlashing) SetColor(colorKick);
		else if (isGrappling) SetColor(colorGrapple);
		else if (isExtending) SetColor(colorExtending);
		else SetColor(colorNormal);
	}

	void SetColor(Color c)
	{
		hairRenderer.startColor = c;
		hairRenderer.endColor = c;
	}
}