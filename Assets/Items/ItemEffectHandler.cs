using UnityEngine;
using Unity.Netcode;

public class ItemEffectHandler : NetworkBehaviour
{
    [Header("Speed Boost Settings (Cookie)")]
    [SerializeField, Range(1f, 5f)]
    private float speedBoostMultiplier = 2f;

    [SerializeField, Min(0f)]
    private float speedBoostDuration = 5f;

    [Header("Slow Down Settings (Banana)")]
    [SerializeField, Range(0.1f, 1f)]
    private float slowDownMultiplier = 0.5f;

    [SerializeField, Min(0f)]
    private float slowDownDuration = 3f;

    private float speedBoostTimer;
    private float slowDownTimer;
    private float activeBoostMultiplier = 1f;
    private float activeSlowMultiplier = 1f;

    public float CurrentSpeedMultiplier => activeBoostMultiplier * activeSlowMultiplier;

    private void Update()
    {
        UpdateEffectTimers();
    }

    public void ApplyEffect(string itemType)
    {
        if (string.IsNullOrEmpty(itemType))
        {
            return;
        }

        switch (itemType)
        {
            case "cookie":
                ApplySpeedBoost();
                break;
            case "banana":
                ApplySlowDownToOthersServerRpc();
                break;
            default:
                break;
        }
    }

    private void UpdateEffectTimers()
    {
        if (speedBoostTimer > 0f)
        {
            speedBoostTimer = Mathf.Max(0f, speedBoostTimer - Time.deltaTime);
            if (speedBoostTimer <= 0f)
            {
                activeBoostMultiplier = 1f;
            }
        }

        if (slowDownTimer > 0f)
        {
            slowDownTimer = Mathf.Max(0f, slowDownTimer - Time.deltaTime);
            if (slowDownTimer <= 0f)
            {
                activeSlowMultiplier = 1f;
            }
        }
    }

    private void ApplySpeedBoost()
    {
        activeBoostMultiplier = speedBoostMultiplier;
        speedBoostTimer = speedBoostDuration;
    }

    [ServerRpc]
    private void ApplySlowDownToOthersServerRpc()
    {
        var players = GameObject.FindGameObjectsWithTag("Player");
        foreach (var player in players)
        {
            var handler = player.GetComponent<ItemEffectHandler>();
            if (handler != null && handler != this)
            {
                handler.ApplySlowDownClientRpc();
            }
        }
    }

    [ClientRpc]
    public void ApplySlowDownClientRpc()
    {
        if (!IsLocalPlayer)
        {
            return;
        }

        ApplySlowDown();
    }

    private void ApplySlowDown()
    {
        activeSlowMultiplier = slowDownMultiplier;
        slowDownTimer = slowDownDuration;
    }
}
