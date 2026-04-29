using UnityEngine;

public class CameraFollow : MonoBehaviour
{
	[Header("Target")]
	public Transform target;        // 追いかける対象（プレイヤー）
	public Vector3 offset = new Vector3(0, 0, -10); // プレイヤーとの距離（Zは-10推奨）

	[Header("Settings")]
	public float smoothTime = 0.3f; // 追う速さのなめらかさ（小さいほど速く追う）
	public float maxSpeed = 10.0f;  // 追う速度の限界値

	private bool isFirstFrame = true; // 最初のフレームかどうかのフラグ
	private Vector3 currentVelocity; // SmoothDamp計算用の変数（いじらなくてOK）


	void Update()
	{
		//updateの1フレーム目のみPlayerの位置にカメラを瞬間移動させる（ティアリング対策）
		if (target != null && currentVelocity == Vector3.zero && isFirstFrame)
		{
			Vector3 targetPosition = target.position + offset;
			targetPosition.z = -10f;
			transform.position = targetPosition;
			isFirstFrame = false;
		}
	}
	// カメラの移動は LateUpdate に書くのが鉄則（プレイヤーが動いた後に移動するため）
	void LateUpdate()
	{
		if (target == null) return;

		// 1. 目標地点を決める
		Vector3 targetPosition = target.position + offset;

		// 2. 2DゲームなのでZ軸（奥行き）は固定する（カメラが埋まらないように）
		// もしoffsetのZを使いたくない場合は、transform.position.z を代入すれば今の位置を維持する
		targetPosition.z = -10f;

		// 3. ぬるりと移動させる（SmoothDamp）
		// 第4引数が「遅延時間」、第5引数が「最大速度」
		transform.position = Vector3.SmoothDamp(
			transform.position,
			targetPosition,
			ref currentVelocity,
			smoothTime,
			maxSpeed
		);
	}
}