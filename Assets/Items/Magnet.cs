using UnityEngine;

/// <summary>
/// A magnet item that pulls other players towards the user when consumed.
/// </summary>
public class Magnet : Item
{
    /// <summary>
    /// The item type identifier for the magnet.
    /// </summary>
    public override string ItemType => "magnet";
}
