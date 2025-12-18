using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Teleport item that teleports the player back to their spawn position when consumed.
/// </summary>
public class TeleportItem : Item
{
    /// <summary>
    /// Consumes the teleport item and sends the player back to spawn.
    /// </summary>
    public override void Consume()
    {
        // Consume the item normally (this will despawn it)
        base.Consume();
    }
}
