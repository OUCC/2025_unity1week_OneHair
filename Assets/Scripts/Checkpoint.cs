using UnityEngine;

public class CheckPoint : MonoBehaviour
{
	[SerializeField] private GameManager GM;
	private void Start()
	{
		if (GM == null)
		{
			GM = GameManager.Instance;
		}
	}
	private void OnTriggerEnter2D(Collider2D other)
	{
		if (GM == null) return;

		if (other.CompareTag("Player"))
		{
			GM.CheckPointPos = transform.position;
		}
	}
}
