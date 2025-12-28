using UnityEngine;

public class Ikumozai : MonoBehaviour, IItem
{
    [SerializeField] 
    private float healAmount = 50f;
    
    public void OnPickup(GameObject player)
    {
        player.GetComponent<Player>().Heal(healAmount);
        Destroy(gameObject);
    }
}
