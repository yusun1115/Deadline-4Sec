using UnityEngine;

namespace Deadline4Sec
{
    // Marks an in-game HUD label that shows numbers (timer, score, distance,
    // coins, combo count, score popups). GameFont gives these the hand-lettered
    // number font (HiKR-ExtraBold, italic — 2026-10-11 request; was
    // CallifontSharpie-Bold from 2026-10-04); every other text
    // keeps GFCRedSpirit-Bold.
    [DisallowMultipleComponent]
    public sealed class NumericLabel : MonoBehaviour
    {
    }
}
