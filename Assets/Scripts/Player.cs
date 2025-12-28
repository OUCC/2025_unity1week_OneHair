using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
public class Player : MonoBehaviour
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

	[Header("HP Settings")]
	public Slider hpSlider;
	public float maxHP = 100f;
	public float currentHP = 100f;

	[Header("Grapple")]
	public float swingForce = 20f;

	[Header("Hair Color")]
	[SerializeField] private Color currentHairColor = Color.black;

	[Header("References")]
	public LineRenderer hairRenderer;
	public Animator faceAnimator;

	[Header("Face Settings (Random Range)")]
	public int strainFaceMin = 1;
	public int strainFaceMax = 3;
	public int kickFaceMin = 3;
	public int kickFaceMax = 4;

	[Header("Effects")]
	[SerializeField] private GameObject jumpEffectPrefab;
	[SerializeField] private GameObject extendEffectPrefab;
	[SerializeField] private GameObject grappleHitEffectPrefab;


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

	private bool extendOnce = false;
	private float kickCooldownTimer;
	private float kickFlashTimer;
	private bool isKickFlashing;

	private Vector2 grapplePoint;
	private DistanceJoint2D joint;

	private int currentStrainFaceIndex = -1;
	private int currentKickFaceIndex = -1;
	[Header("Audio")]
	public AudioSource sfxSource;              // 1発系
	public AudioSource grappleLoopSource;      // 掴んでる間ループ
	public AudioClip kickClip;
	public AudioClip grappleLoopClip;


	[Header("Damage Settings")]
	[SerializeField] private float GrappleDamageAmount = 1.0f;
	[SerializeField] private float GrappleDamageInterval = 0.5f;
	[SerializeField] private float grappleOffDamage = 10f;
	[SerializeField] private float kickDamageAmount = 5f;

	private float GrappleDamageCount = 0f;

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
		UpdateHP();
		ApplyHairColor();
	}

	void Update()
	{
		bool isPlaying =
			GameManager.Instance == null ||
			GameManager.Instance.IsPlaying();

		if (isPlaying)
		{
			if (Mouse.current == null || Keyboard.current == null) return;

			if (kickCooldownTimer > 0)
				kickCooldownTimer -= Time.deltaTime;

			HandleRotation();
			HandleInput();
		}
		else
		{
			ForceStop();
		}

		UpdateVisuals();
	}

	void FixedUpdate()
	{
		if (GameManager.Instance != null && !GameManager.Instance.IsPlaying())
			return;

		if (isGrappling)
			HandleSwing();
	}

    void OnTriggerEnter2D(Collider2D collision)
    {
        IItem item = collision.GetComponent<IItem>();
        if (item != null)
        {
            item.OnPickup(gameObject);
        }
    }
    // =====================
    // 入力
    // =====================

    void HandleInput()
	{
		if (Keyboard.current.spaceKey.wasPressedThisFrame)
			TryKick();

		if (Keyboard.current.fKey.wasPressedThisFrame)
			currentStrainFaceIndex = Random.Range(strainFaceMin, strainFaceMax + 1);

		if (Keyboard.current.fKey.isPressed)
		{
			if (kickCooldownTimer > 0) return;
			if (!isGrappling)
				ProcessGrappleExtension();
		}
		else if (Keyboard.current.fKey.wasReleasedThisFrame)
		{
			if (isExtending || isGrappling) ResetGrapple();
		}
	}

	// =====================
	// キック（ジャンプ）
	// =====================

	void TryKick()
	{
		Debug.Log($"Kick pressed. cooldown={kickCooldownTimer}, sfx={(sfxSource != null)}, clip={(kickClip != null)}");
		if (kickCooldownTimer > 0) return;


		Vector2 root = GetRootPos();
		Vector2 dir = transform.up;

		RaycastHit2D hit = Physics2D.Raycast(root, dir, kickRange);

		if (hit.collider == null || hit.collider.gameObject == gameObject)
			return;

		if (isGrappling)
			ResetGrapple();

		Damage(kickDamageAmount);

		rb.linearVelocity = Vector2.zero;
		rb.AddForce(-dir * kickForce, ForceMode2D.Impulse);
		if (sfxSource != null && kickClip != null)
		{
			sfxSource.PlayOneShot(kickClip);
		}


		SpawnJumpEffect(root, -dir);

		currentKickFaceIndex = Random.Range(kickFaceMin, kickFaceMax + 1);
		isKickFlashing = true;
		kickFlashTimer = kickFlashTime;
		kickCooldownTimer = kickCooldown;
	}

	void SpawnJumpEffect(Vector2 position, Vector2 direction)
	{
		if (jumpEffectPrefab == null) return;

		float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
		//zに)90度足すことで、エフェクトが進行方向を向くようにする

		angle -= 90f;
		Instantiate(jumpEffectPrefab, position, Quaternion.Euler(0, 0, angle));
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

		if (!extendOnce) { extendOnce = true; SpawnExtendEffect(GetRootPos()); }

		Vector2 root = GetRootPos();
		Vector2 dir = transform.up;

		RaycastHit2D hit = Physics2D.Raycast(root, dir, currentLength);

		if (hit.collider == null || hit.collider.gameObject == gameObject || hit.collider.isTrigger)
			return;

		if (isMaxExtended && !isGrappling)
			StartGrapple(hit.point, hit.collider.gameObject);
	}

	void SpawnExtendEffect(Vector2 position)
	{
		if (extendEffectPrefab == null) return;
		//angle は自分の向き+90度
		float angle = transform.eulerAngles.z + 90f;
		Instantiate(extendEffectPrefab, position, Quaternion.Euler(0, 0, angle));
	}

	void StartGrapple(Vector2 point, GameObject hitObject)
	{
		isGrappling = true;
		isExtending = false;
		grapplePoint = point;

		SpawnGrappleHitEffect(point);

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
		if (grappleLoopSource != null && grappleLoopClip != null)
		{
			if (grappleLoopSource.clip != grappleLoopClip)
				grappleLoopSource.clip = grappleLoopClip;

			if (!grappleLoopSource.isPlaying)
				grappleLoopSource.Play();
		}
	}

	void SpawnGrappleHitEffect(Vector2 position)
	{
		if (grappleHitEffectPrefab == null) return;
		Instantiate(grappleHitEffectPrefab, position, Quaternion.identity);
	}

	void HandleSwing()
	{
		if (GrappleDamageCount > GrappleDamageInterval)
		{
			GrappleDamageCount -= GrappleDamageInterval;
			Damage(GrappleDamageAmount);
		}

		GrappleDamageCount += Time.deltaTime;

		Vector2 dir = (mousePos - (Vector2)transform.position).normalized;
		rb.AddForce(dir * swingForce);
	}

	void ResetGrapple()
	{
		if (grappleLoopSource != null && grappleLoopSource.isPlaying)
			grappleLoopSource.Stop();
		isExtending = false;
		extendOnce = false;
		isMaxExtended = false;
		currentLength = minLength;

		Damage(grappleOffDamage);

		foreach (var j in GetComponents<DistanceJoint2D>())
			Destroy(j);

		isGrappling = false;
		GrappleDamageCount = 0f;
	}

	void ForceStop()
	{

		ResetGrapple();
		if (grappleLoopSource != null && grappleLoopSource.isPlaying)
		{
			grappleLoopSource.Stop();
		}

		isKickFlashing = false;
		currentStrainFaceIndex = -1;
		currentKickFaceIndex = -1;

		rb.linearVelocity = Vector2.zero;
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

	void UpdateVisuals()
	{
		if (faceAnimator != null)
		{
			int targetFace = -1;

			if (isKickFlashing)
				targetFace = currentKickFaceIndex;
			else if (isExtending || isGrappling)
				targetFace = currentStrainFaceIndex;

			faceAnimator.SetInteger("Face", targetFace);
		}

		if (isKickFlashing)
		{
			kickFlashTimer -= Time.deltaTime;
			if (kickFlashTimer <= 0)
				isKickFlashing = false;
		}

		Vector2 root = GetRootPos();
		Vector2 tip =
			isGrappling ? grapplePoint :
			root + (Vector2)transform.up * currentLength;

		hairRenderer.SetPosition(0, root);
		hairRenderer.SetPosition(1, tip);
	}

	// =====================
	// HP
	// =====================

	void ApplyHairColor()
	{
		hairRenderer.startColor = currentHairColor;
		hairRenderer.endColor = currentHairColor;
	}

	void UpdateHP()
	{
		hpSlider.maxValue = maxHP;
		hpSlider.value = currentHP;

		if (currentHP <= 0)
			GameManager.Instance.GameOver();
	}

	public void Damage(float amount)
	{
		if (amount <= 0) return;
		currentHP = Mathf.Max(0, currentHP - amount);
		UpdateHP();
	}

	public void Heal(float amount)
	{
		if (amount <= 0) return;
		currentHP = Mathf.Min(maxHP, currentHP + amount);
		UpdateHP();
	}
}
