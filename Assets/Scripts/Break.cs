using UnityEngine;

public class Break : MonoBehaviour
{
    public Rigidbody rb;
    bool isBroken;
    bool destroyScheduled;

    void Start()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (rb != null)
            rb.isKinematic = true;
    }

    void Update()
    {
        if (!isBroken || rb == null)
            return;

        rb.isKinematic = false;

        if (!destroyScheduled)
        {
            destroyScheduled = true;
            Invoke(nameof(DestroyObject), 5f);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (isBroken)
            return;

        if (other.CompareTag("Bullet"))
            BreakApart();
    }

    void OnCollisionEnter(Collision collision)
    {
        if (isBroken)
            return;

        if (collision.collider.CompareTag("Bullet"))
            BreakApart();
    }

    public void BreakApart()
    {
        if (isBroken)
            return;

        isBroken = true;
        CombatStimulus.EmitBreach(transform.position);
    }

    void DestroyObject()
    {
        Destroy(gameObject);
    }
}
