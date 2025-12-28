using UnityEngine;

public class DestoroySelf : MonoBehaviour
{
	// Start is called once before the first execution of Update after the MonoBehaviour is created
	public void DestroyNow()
	{
		Destroy(gameObject);
	}
}
