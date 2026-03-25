using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
public class Player : MonoBehaviour
{
	[Header("Hair Specs")]
	public float minLength = 0.5f;
	public float maxLength = 2.0f;
	public float extendDuration = 0.1f;
	public float rootOffset = 0.5f;

	[Header("Kick")]

	public KickTrigger kickTrigger;

	public float kickRange = 1.2f;
	public int segmentCount = 5;
	public float segmentWaviness = 0.3f;
	public float kickForce = 20f;
	public float kickUpperForce = 10f;
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
	enum DifficultyLevel { Easy, Normal, Hard }
	float DamageMultiplier()
	{
		switch (currentDifficulty)
		{
			case DifficultyLevel.Easy:
				return 0f;
			case DifficultyLevel.Normal:
				return 0.5f;
			case DifficultyLevel.Hard:
				return 1.0f;
			default:
				return 1.0f;
		}
	}
	private DistanceJoint2D joint;

	private int currentStrainFaceIndex = -1;
	private int currentKickFaceIndex = -1;
	[Header("Difficulty Settings")]
	[SerializeField] private DifficultyLevel currentDifficulty = DifficultyLevel.Easy;
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

	[Header("Hair Fall Settings")]
	[SerializeField] private float hairFallSpeed = 0.5f;
	private bool isDead = false;
	private Vector2 hairFallRoot;
	private Vector2 hairFallTip;

	[Header("Physics Limits")]
	[SerializeField] private float maxLinearSpeed = 15f;

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
		bool isPlaying = GameManager.Instance == null || GameManager.Instance.IsPlaying();

		if (isPlaying && !isDead)
		{
			if (Mouse.current == null || Keyboard.current == null) return;

			if (kickCooldownTimer > 0)
				kickCooldownTimer -= Time.deltaTime;

			HandleRotation();
			HandleInput();
		}
		else if (!isPlaying)
		{
			ForceStop();
		}

		if (isDead)
		{
			UpdateHairFall();
		}

		UpdateVisuals();
	}

	void FixedUpdate()
	{
		if (GameManager.Instance != null && !GameManager.Instance.IsPlaying()) return;
		if (isGrappling)
			HandleSwing();

		ClampLinearSpeed();
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
		{
			TryKick();
		}

		if (Keyboard.current.fKey.wasPressedThisFrame || Mouse.current.leftButton.wasPressedThisFrame)
		{
			currentStrainFaceIndex = Random.Range(strainFaceMin, strainFaceMax + 1);
		}

		if (Keyboard.current.fKey.isPressed || Mouse.current.leftButton.isPressed)
		{
			if (kickCooldownTimer > 0) return;
			if (!isGrappling) ProcessGrappleExtension();
		}
		else if (Keyboard.current.fKey.wasReleasedThisFrame || Mouse.current.leftButton.wasReleasedThisFrame)
		{
			if (isExtending || isGrappling) ResetGrapple();
		}
	}

	// =====================
	// キック（ジャンプ）
	// =====================
	void TryKick()
	{
		//if (kickCooldownTimer > 0) return;


		Vector2 root = GetRootPos();
		Vector2 dir = transform.up;


		if (!kickTrigger.HasTarget)
			return;

		if (isGrappling)
			ResetGrapple();

		GameObject target = kickTrigger.GetAnyTarget();

		Damage(kickDamageAmount);

		rb.linearVelocity = Vector2.zero;
		rb.AddForce(-dir * kickForce + Vector2.up * kickUpperForce, ForceMode2D.Impulse);
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
		// zに-90度足すことで、エフェクトが進行方向を向くようにする
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

		if (!extendOnce)
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
		GameObject Penetrate = Instantiate(extendEffectPrefab, position, Quaternion.Euler(0, 0, angle));
		Penetrate.transform.parent = transform;
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
		if (faceAnimator != null && !isDead)
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

		Vector2 root = isDead ? hairFallRoot : GetRootPos();
		Vector2 defaultTip = root + (Vector2)transform.up * currentLength;

		// グラップル時は最優先で2点描画（他状態より優先）
		if (isGrappling && !isDead)
		{
			hairRenderer.positionCount = 2;
			hairRenderer.SetPosition(0, root);
			hairRenderer.SetPosition(1, grapplePoint);
			return;
		}

		// 死亡時は落下中の根本/先端をそのまま描画
		if (isDead)
		{
			hairRenderer.positionCount = 2;
			hairRenderer.SetPosition(0, hairFallRoot);
			hairRenderer.SetPosition(1, hairFallTip);
			return;
		}

		if (kickTrigger != null && kickTrigger.HasTarget)
		{
			float shortenedLength = currentLength * 0.3f;
			DrawWavyHair(root, transform.up, shortenedLength);
		}
		else
		{
			hairRenderer.positionCount = 2;
			hairRenderer.SetPosition(0, root);
			hairRenderer.SetPosition(1, defaultTip);
		}
	}

	void DrawWavyHair(Vector2 root, Vector2 direction, float length)
	{
		// セグメント数に応じて節のある髪の毛を描画
		int positionCount = segmentCount * 2 + 1;
		hairRenderer.positionCount = positionCount;

		//directionから右90度のベクトル
		Vector2 secondaryAngle = new Vector2(-direction.y, direction.x);
		Vector2 currentPos = root + secondaryAngle * -0.2f;
		float segmentLength = length * 2 / segmentCount;

		hairRenderer.SetPosition(0, currentPos);

		for (int i = 0; i < segmentCount; i++)
		{
			// ギザギザ効果のため、左右に振動させる
			float waveOffset = i % 2 == 0 ? segmentWaviness : -segmentWaviness;
			Vector2 perpendicular = new Vector2(-direction.y, direction.x);
			Vector2 wavePos = currentPos + direction * segmentLength + perpendicular * waveOffset;

			hairRenderer.SetPosition(i * 2 + 1, wavePos);
			currentPos = wavePos;

			// 次のセグメントの開始点
			Vector2 nextPos = currentPos + direction * segmentLength * 0.1f;
			hairRenderer.SetPosition(i * 2 + 2, nextPos);
			currentPos = nextPos;
		}
	}

	void UpdateHairFall()
	{
		// 髪の毛を下に落としていく（根本と先端の両方）
		hairFallRoot.y -= hairFallSpeed * Time.deltaTime;
		hairFallTip.y -= hairFallSpeed * Time.deltaTime;
	}

	void ClampLinearSpeed()
	{
		float max = Mathf.Max(0f, maxLinearSpeed);
		if (max <= 0f) return;
		Vector2 v = rb.linearVelocity;
		if (v.sqrMagnitude > max * max)
		{
			rb.linearVelocity = v.normalized * max;
		}
	}

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
		{
			GameManager.Instance.GameOver();
		}
	}

	public void Damage(float amount)
	{
		if (amount <= 0) return;
		currentHP = Mathf.Max(0, currentHP - amount);
		UpdateHP();

		if (currentHP <= 0 && !isDead)
		{
			OnPlayerDeath();
		}
	}

	void OnPlayerDeath()
	{
		isDead = true;
		currentLength = 1f;
		Vector2 root = GetRootPos();
		hairFallRoot = root;
		hairFallTip = root + (Vector2)transform.up * currentLength;
		ForceStop();
	}

	public void Heal(float amount)
	{
		if (amount <= 0) return;
		currentHP = Mathf.Min(maxHP, currentHP + amount);
		UpdateHP();
	}
}
