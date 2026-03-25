using UnityEngine;
using System.Collections.Generic;

public class KickTrigger : MonoBehaviour
{
	private HashSet<GameObject> targets = new HashSet<GameObject>();

	public bool HasTarget => targets.Count > 0;

	public GameObject GetAnyTarget()
	{
		foreach (var t in targets)
			return t;
		return null;
	}

	private void OnTriggerEnter2D(Collider2D other)
	{
		if (other.CompareTag("Player")) return;
		targets.Add(other.gameObject);
	}

	private void OnTriggerExit2D(Collider2D other)
	{
		targets.Remove(other.gameObject);
	}
}
