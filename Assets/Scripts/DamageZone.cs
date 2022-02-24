using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DamageZone : MonoBehaviour
{
    [SerializeField] float DamageRate = 5f;

    protected List<IDamageable> ObjectsInZone = new List<IDamageable>();

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        foreach(var damageable in ObjectsInZone)
        {
            damageable.OnTakeDamage(gameObject, DamageRate * Time.deltaTime);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        IDamageable damageable;
        if (other.TryGetComponent<IDamageable>(out damageable))
        {
            ObjectsInZone.Add(damageable);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        IDamageable damageable;
        if (other.TryGetComponent<IDamageable>(out damageable))
        {
            ObjectsInZone.Remove(damageable);
        }
    }
}
