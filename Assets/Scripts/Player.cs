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
	public float kickUpperForce = 10f;
	public float kickCooldown = 1.0f;
	public float kickFlashTime = 0.1f;
	public KickTrigger kickTrigger;

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

	[Header("Audio")]
	public AudioSource sfxSource;
	public AudioSource grappleLoopSource;
	public AudioClip kickClip;
	public AudioClip grappleLoopClip;

	[Header("Damage Settings")]
	[SerializeField] private float GrappleDamageAmount = 1.0f;
	[SerializeField] private float GrappleDamageInterval = 0.5f;
	[SerializeField] private float grappleOffDamage = 10f;
	[SerializeField] private float kickDamageAmount = 5f;

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

	private bool extendOnce;
	private float kickCooldownTimer;
	private float kickFlashTimer;
	private bool isKickFlashing;

	private Vector2 grapplePoint;
	private DistanceJoint2D joint;

	private int currentStrainFaceIndex = -1;
	private int currentKickFaceIndex = -1;

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
			if (isExtending || isGrappling)
				ResetGrapple();
		}
	}

	// =====================
	// キック
	// =====================

	void TryKick()
	{
		if (kickCooldownTimer > 0) return;
		if (kickTrigger == null || !kickTrigger.HasTarget) return;

		if (isGrappling)
			ResetGrapple();

		Damage(kickDamageAmount);

		Vector2 dir = transform.up;
		rb.linearVelocity = Vector2.zero;
		rb.AddForce(-dir * kickForce + Vector2.up * kickUpperForce, ForceMode2D.Impulse);

		if (sfxSource != null && kickClip != null)
			sfxSource.PlayOneShot(kickClip);

		SpawnJumpEffect(GetRootPos(), -dir);

		currentKickFaceIndex = Random.Range(kickFaceMin, kickFaceMax + 1);
		isKickFlashing = true;
		kickFlashTimer = kickFlashTime;
		kickCooldownTimer = kickCooldown;
	}

	void SpawnJumpEffect(Vector2 position, Vector2 direction)
	{
		if (jumpEffectPrefab == null) return;
		float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
		Instantiate(jumpEffectPrefab, position, Quaternion.Euler(0, 0, angle));
	}

	// =====================
	// グラップル
	// =====================

	void ProcessGrappleExtension()
	{
		isExtending = true;

		float speed = (maxLength - minLength) / Mathf.Max(extendDuration, 0.001f);
		currentLength = Mathf.Min(maxLength, currentLength + speed * Time.deltaTime);
		isMaxExtended = currentLength >= maxLength;

		if (!extendOnce)
		{
			extendOnce = true;
			SpawnExtendEffect(GetRootPos());
		}
	}

	void SpawnExtendEffect(Vector2 position)
	{
		if (extendEffectPrefab == null) return;
		float angle = transform.eulerAngles.z + 90f;
		Instantiate(extendEffectPrefab, position, Quaternion.Euler(0, 0, angle));
	}

	void HandleSwing()
	{
		GrappleDamageCount += Time.deltaTime;
		if (GrappleDamageCount >= GrappleDamageInterval)
		{
			GrappleDamageCount = 0f;
			Damage(GrappleDamageAmount);
		}

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
		isGrappling = false;
		GrappleDamageCount = 0f;

		Damage(grappleOffDamage);

		foreach (var j in GetComponents<DistanceJoint2D>())
			Destroy(j);
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
		int face = -1;
		if (isKickFlashing) face = currentKickFaceIndex;
		else if (isExtending || isGrappling) face = currentStrainFaceIndex;

		if (faceAnimator != null)
			faceAnimator.SetInteger("Face", face);

		if (isKickFlashing)
		{
			kickFlashTimer -= Time.deltaTime;
			if (kickFlashTimer <= 0)
				isKickFlashing = false;
		}

		Vector2 root = GetRootPos();
		Vector2 tip = root + (Vector2)transform.up * currentLength;
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
	}

	public void Damage(float amount)
	{
		if (amount <= 0) return;
		currentHP = Mathf.Max(0, currentHP - amount);
		UpdateHP();
	}

	public void Heal(float amount) { if (amount <= 0) return; currentHP = Mathf.Min(maxHP, currentHP + amount); UpdateHP(); }
}
