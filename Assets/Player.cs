using UnityEngine;
using UnityEngine.InputSystem;

public class HairController : MonoBehaviour
{
	[Header("Hair Specs")]
	public float minLength = 0.5f;     // 普段の長さ
	public float maxLength = 2.0f;     // 最大長さ
	public float extendDuration = 0.1f;// ★変更: 伸びきるまでの時間(秒)
	public float rootOffset = 0.5f;    // 頭の半径

	[Header("Actions")]
	public float jumpForce = 20.0f;
	public float swingForce = 20.0f;
	public float grappleCooldown = 1.0f; // キック後の吸着不可時間

	[Header("Colors")]
	public Color colorNormal = Color.black;
	public Color colorExtending = Color.yellow;
	public Color colorJump = Color.red;
	public Color colorGrapple = Color.blue;

	[Header("References")]
	public LineRenderer hairRenderer;

	// 内部変数
	private Rigidbody2D rb;
	private Camera mainCam;
	private Vector2 mousePos;

	// 状態管理
	private float currentLength;
	private bool isExtending = false;
	private bool isGrappling = false;
	private bool isMaxExtended = false;

	// クールダウン・演出用
	private float jumpFlashTimer = 0f;
	private bool isJumpFlashing = false;
	private float currentGrappleCooldown = 0f;

	// 物理用
	private Vector2 grapplePoint;
	private DistanceJoint2D joint;

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

		if (currentGrappleCooldown > 0)
		{
			currentGrappleCooldown -= Time.deltaTime;
		}

		HandleRotation();
		HandleInput();
		UpdateVisuals();
	}

	void FixedUpdate()
	{
		if (isGrappling)
		{
			HandleSwing();
		}
	}

	Vector2 GetRootPos()
	{
		return (Vector2)transform.position + (Vector2)transform.up * rootOffset;
	}

	void HandleRotation()
	{
		Vector2 screenPos = Mouse.current.position.ReadValue();
		mousePos = mainCam.ScreenToWorldPoint(screenPos);

		Vector2 direction = mousePos - (Vector2)transform.position;
		float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;

		rb.MoveRotation(angle);
	}

	void HandleInput()
	{
		if (Keyboard.current.spaceKey.isPressed)
		{
			if (!isGrappling && !isJumpFlashing)
			{
				ProcessExtension();
			}
		}
		else
		{
			ResetHair();
		}
	}

	void ProcessExtension()
	{
		isExtending = true;

		// 1. 時間管理で長さを計算
		if (!isMaxExtended)
		{
			// (目標距離 / 時間) = 速度
			float speed = (maxLength - minLength) / Mathf.Max(extendDuration, 0.001f); // 0除算防止

			currentLength += speed * Time.deltaTime;

			if (currentLength >= maxLength)
			{
				currentLength = maxLength;
				isMaxExtended = true;
			}
		}

		// 2. 当たり判定
		Vector2 root = GetRootPos();
		Vector2 dir = transform.up;

		RaycastHit2D hit = Physics2D.Raycast(root, dir, currentLength);

		if (hit.collider != null && hit.collider.gameObject != gameObject)
		{
			if (!isMaxExtended)
			{
				// 伸びてる最中 = ジャンプ
				PerformJump();
			}
			else
			{
				// 伸びきった後 = くっつく
				if (!isGrappling && currentGrappleCooldown <= 0)
				{
					StartGrapple(hit.point, hit.collider.gameObject);
				}
			}
		}
	}

	void PerformJump()
	{
		rb.linearVelocity = Vector2.zero;
		rb.AddForce(-transform.up * jumpForce, ForceMode2D.Impulse);

		isJumpFlashing = true;
		jumpFlashTimer = 0.1f;
		currentGrappleCooldown = grappleCooldown;

		ResetHairStateOnly();
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
		Vector2 dirToMouse = (mousePos - (Vector2)transform.position).normalized;
		rb.AddForce(dirToMouse * swingForce);
	}

	void ResetHair()
	{
		ResetHairStateOnly();

		if (joint != null) Destroy(joint);
		DistanceJoint2D[] joints = GetComponents<DistanceJoint2D>();
		foreach (var j in joints) Destroy(j);

		isGrappling = false;
	}

	void ResetHairStateOnly()
	{
		isExtending = false;
		isMaxExtended = false;
		currentLength = minLength;
	}

	void UpdateVisuals()
	{
		Vector2 root = GetRootPos();

		if (isJumpFlashing)
		{
			jumpFlashTimer -= Time.deltaTime;
			if (jumpFlashTimer <= 0) isJumpFlashing = false;
		}

		Vector2 tipPos;
		if (isGrappling)
		{
			tipPos = grapplePoint;
		}
		else
		{
			tipPos = root + (Vector2)transform.up * currentLength;
		}

		hairRenderer.SetPosition(0, root);
		hairRenderer.SetPosition(1, tipPos);

		if (isJumpFlashing) SetColor(colorJump);
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