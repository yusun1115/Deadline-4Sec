using UnityEngine;

namespace Deadline4Sec
{
    // Marks an in-game HUD label that shows numbers (timer, score, distance,
    // coins, combo count, score popups). GameFont gives these the hand-lettered
    // number font (CallifontSharpie-Bold, 2026-10-04 request); every other text
    // keeps GFCRedSpirit-Bold.
    [DisallowMultipleComponent]
    public sealed class NumericLabel : MonoBehaviour
    {
    }
}
