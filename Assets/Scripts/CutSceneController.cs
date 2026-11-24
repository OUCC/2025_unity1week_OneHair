using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.InputSystem;

public class CutSceneController : MonoBehaviour
{
	[Header("設定")]
	[Tooltip("満タンになるまでの長押し時間（秒）")]
	[SerializeField] private float holdDuration = 2.0f;

	[Tooltip("キーを離した時にゲージを減らす速度（1秒あたりの減少量割合）")]
	[SerializeField] private float decaySpeed = 2.0f;

	[Header("UI参照")]
	[Tooltip("Image TypeをFilledに設定した円ゲージ用Image")]
	[SerializeField] private Image gaugeImage;

	[Header("完了時イベント")]
	[Tooltip("ゲージが満タンになった時に実行する処理")]
	public UnityEvent onHoldCompleted;

	private float currentHoldTime = 0f;
	private bool isCompleted = false;

	private void Start()
	{
		if (gaugeImage != null)
		{
			gaugeImage.fillAmount = 0f;
		}
	}

	private void Update()
	{
		// キーボードが接続されていない場合は処理しない
		if (Keyboard.current == null) return;

		// Sキーが押されている間
		if (Keyboard.current.sKey.isPressed)
		{
			// すでに完了済みの場合は重複実行を防ぐ（再入力したい場合は一度キーを離す）
			if (isCompleted) return;

			currentHoldTime += Time.deltaTime;

			if (currentHoldTime >= holdDuration)
			{
				currentHoldTime = holdDuration;
				isCompleted = true;
				UpdateGaugeUI();
				ExecuteTargetAction();
			}
			else
			{
				UpdateGaugeUI();
			}
		}
		// Sキーが離されている間
		else
		{
			isCompleted = false;

			if (currentHoldTime > 0f)
			{
				// 徐々にゲージを0に戻す（即座に0にしたい場合は currentHoldTime = 0f; に変更）
				currentHoldTime -= Time.deltaTime * holdDuration * decaySpeed;
				if (currentHoldTime < 0f)
				{
					currentHoldTime = 0f;
				}
				UpdateGaugeUI();
			}
		}
	}

	private void UpdateGaugeUI()
	{
		if (gaugeImage != null)
		{
			gaugeImage.fillAmount = Mathf.Clamp01(currentHoldTime / holdDuration);
		}
	}

	private void ExecuteTargetAction()
	{
		// Inspectorから登録したイベントを実行
		onHoldCompleted?.Invoke();

		// スクリプト内に直接処理を記述する場合は以下に書く
		Debug.Log("Sキーの長押しが完了し、特定の処理が実行された。");
	}
}
