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

	private int currentStrainFaceIndex = -1;
	private int currentKickFaceIndex = -1;

    [Header("HP Regeneration and Damage Settings")]
    [SerializeField]
    private float GrappleDamageAmount = 1.0f;
    [SerializeField]
    private float GrappleDamageInterval = 0.5f;

    private float GrappleDamageCount = 0f;

    [SerializeField] 
    private float grappleOffDamage = 10f;

    [SerializeField]
    private float kickDamageAmount = 5f;

    private bool isDead = false;

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

        updateHP();
		ApplyHairColor();
	}

	void Update()
	{
		bool isPlaying =
			GameManager.Instance == null ||
			GameManager.Instance.IsPlaying();

		// --- 操作系は Playing のみ ---
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
			// 操作不能時は物理・入力を止めるだけ
			ForceStop();
		}

		// --- 描画更新は常に行う ---
		UpdateVisuals();
	}

	void FixedUpdate()
	{
		if (GameManager.Instance != null && !GameManager.Instance.IsPlaying())
			return;

		if (isGrappling)
			HandleSwing();
	}

	// =====================
	// 外部公開：色変更
	// =====================

	public void SetHairColor(Color color)
	{
		currentHairColor = color;
		ApplyHairColor();
	}

	void ApplyHairColor()
	{
		if (hairRenderer == null) return;
		hairRenderer.startColor = currentHairColor;
		hairRenderer.endColor = currentHairColor;
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
			ResetGrapple();
		}
	}

	// =====================
	// キック
	// =====================

	void TryKick()
	{
		if (kickCooldownTimer > 0) return;

		if (isGrappling)
			ResetGrapple();

		Vector2 root = GetRootPos();
		Vector2 dir = transform.up;

		RaycastHit2D hit = Physics2D.Raycast(root, dir, kickRange);

		if (hit.collider == null || hit.collider.gameObject == gameObject)
			return;

        // キックダメージ
        Damage(kickDamageAmount);

		// 反動ジャンプ
		rb.linearVelocity = Vector2.zero;
		rb.AddForce(-dir * kickForce, ForceMode2D.Impulse);

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
        //継続ダメージ
        if (GrappleDamageCount > GrappleDamageInterval)
        {
            GrappleDamageCount -= GrappleDamageInterval;
            Damage(GrappleDamageAmount);
        }

        //継続ダメージ用カウントアップ
        GrappleDamageCount += Time.deltaTime;

		Vector2 dir = (mousePos - (Vector2)transform.position).normalized;
		rb.AddForce(dir * swingForce);
	}

	void ResetGrapple()
	{
		isExtending = false;
		isMaxExtended = false;
		currentLength = minLength;
        Damage(grappleOffDamage);
        Debug.Log("Grapple Off Damage: " + grappleOffDamage);

		foreach (var j in GetComponents<DistanceJoint2D>())
			Destroy(j);

		isGrappling = false;

        //継続ダメージのカウントをリセット
        GrappleDamageCount = 0f;
	}

	void ForceStop()
	{
		ResetGrapple();
		isKickFlashing = false;
		currentStrainFaceIndex = -1;
		currentKickFaceIndex = -1;

		if (rb != null)
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

	// =====================
	// 表示
	// =====================

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
	}

	void SetColor(Color c)
	{
		hairRenderer.startColor = c;
		hairRenderer.endColor = c;
	}

    private void updateHP()
    {
        hpSlider.maxValue = maxHP;
        hpSlider.value = currentHP;

        if (currentHP <= 0)
        {
            //ゲームオーバー
            GameManager.Instance.GameOver();
        }

        Debug.Log("update HP");
    }

    public void Damage(float amount)
    {
        if (amount <= 0) 
        {
            return;
        }

        currentHP -= amount;

        if (currentHP <= 0) 
        {
            currentHP = 0;
        }

        updateHP();
    }

    public void Heal(float amount)
    {
        if (amount <= 0) 
        {
            return;
        }

        currentHP += amount;

        if (currentHP >= maxHP) 
        {
            currentHP = maxHP;
        }

        updateHP();
    }
}