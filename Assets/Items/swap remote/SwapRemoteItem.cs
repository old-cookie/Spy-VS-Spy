using UnityEngine;

/// <summary>
/// Swap Remote item: swaps positions with the nearest enemy within range.
/// Actual swap logic is handled in ItemEffectHandler via item type.
/// </summary>
public class SwapRemoteItem : Item
{
    public override string ItemType => "swap remote";
}
