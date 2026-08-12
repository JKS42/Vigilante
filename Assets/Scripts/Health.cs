using System;
using UnityEngine;

public class Health : MonoBehaviour
{
    [SerializeField] float maxHealth = 100f;

    public float MaxHealth => maxHealth;
    public float CurrentHealth { get; private set; }
    public bool IsDead => CurrentHealth <= 0f;

    public event Action<float, Vector3, GameObject> OnDamaged;
    public event Action OnDied;

    void Awake()
    {
        CurrentHealth = maxHealth;
    }

    public void TakeDamage(float amount, Vector3 hitPoint, GameObject instigator)
    {
        if (IsDead || amount <= 0f)
            return;

        CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
        OnDamaged?.Invoke(amount, hitPoint, instigator);

        if (CurrentHealth <= 0f)
            Die();
    }

    public void TakeDamage(float amount)
    {
        TakeDamage(amount, transform.position, null);
    }

    public void Heal(float amount)
    {
        if (IsDead || amount <= 0f)
            return;

        CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
    }

    public void ResetHealth()
    {
        CurrentHealth = maxHealth;
    }

    void Die()
    {
        OnDied?.Invoke();
    }
}
