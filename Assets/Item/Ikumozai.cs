using UnityEngine;

public class Ikumozai : MonoBehaviour, IItem
{
	[SerializeField]
	private float healAmount = 100f;

	public void OnPickup(GameObject player)
	{
		player.GetComponent<Player>().Heal(healAmount);
		//スプライトとコライダーを無効化してアイテムを消す
		GetComponent<SpriteRenderer>().enabled = false;
		GetComponent<Collider2D>().enabled = false;
		//アイテムを一定時間後に破壊する
		Destroy(gameObject, 1f);
	}
}
