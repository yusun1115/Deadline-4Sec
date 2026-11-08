using System;
using UnityEngine;

namespace Deadline4Sec
{
    // A short intensity curve (AreaBiome,Pacing.md, step 18). Each area lists its
    // own; the first one in the list is its signature and opens the area.
    [Serializable]
    public sealed class PacingBlock
    {
        public string blockName = "Flow";
        [Min(1)] public int weight = 1;
        [Range(1, 5)] public int[] targets = { 2, 3, 3, 2 };

        public PacingBlock() { }

        public PacingBlock(string name, int blockWeight, params int[] intensities)
        {
            blockName = name;
            weight = blockWeight;
            targets = intensities;
        }
    }

    // Decides how hard the next pattern should be, so a run rises and falls
    // instead of climbing to Hard and staying there. It only names a target:
    // PatternSpawner finds the pattern. Blocks never repeat back to back.
    public sealed class PacingDirector
    {
        private static readonly PacingBlock[] DefaultBlocks =
        {
            new PacingBlock("BuildUp", 1, 1, 2, 3, 4),
            new PacingBlock("Recovery", 1, 3, 2, 1),
            new PacingBlock("Spike", 1, 2, 4, 5, 2),
            new PacingBlock("Flow", 1, 2, 3, 3, 2),
        };

        private readonly System.Random random;
        private readonly int highFrom;
        private readonly int maxHighInARow;
        private PacingBlock[] blocks = DefaultBlocks;
        private PacingBlock block;
        private PacingBlock previousBlock;
        private int step;
        private bool signatureNext;

        public PacingDirector(System.Random blockRandom, int highIntensityFrom, int maximumHighInARow)
        {
            random = blockRandom;
            highFrom = highIntensityFrom;
            maxHighInARow = maximumHighInARow;
        }

        public int LastIntensity { get; private set; }
        public int HighStreak { get; private set; }

        public string BlockName
        {
            get
            {
                EnsureBlock();
                return block.blockName;
            }
        }

        public int TargetIntensity
        {
            get
            {
                EnsureBlock();
                return block.targets[step];
            }
        }

        // A new area: its own blocks, starting with its signature.
        public void UseBlocks(PacingBlock[] areaBlocks)
        {
            bool usable = false;
            if (areaBlocks != null)
                foreach (PacingBlock candidate in areaBlocks)
                    usable |= IsUsable(candidate);
            blocks = usable ? areaBlocks : DefaultBlocks;
            block = null;
            previousBlock = null;
            step = 0;
            signatureNext = true;
        }

        // Never 5 → 5, and never more than a few hard patterns in a row, whatever
        // the blocks and the special sections add up to.
        public bool Allows(int intensity)
        {
            if (intensity >= highFrom && HighStreak >= maxHighInARow)
                return false;
            return intensity < 5 || LastIntensity < 5;
        }

        // The upper deck and the transition don't take a step of the curve.
        public void Record(int actualIntensity, bool takesStep)
        {
            LastIntensity = actualIntensity;
            HighStreak = actualIntensity >= highFrom ? HighStreak + 1 : 0;
            if (!takesStep)
                return;
            EnsureBlock();
            step++;
            if (step < block.targets.Length)
                return;
            previousBlock = block;
            block = null;
            step = 0;
        }

        private void EnsureBlock()
        {
            if (block != null)
                return;
            step = 0;
            if (signatureNext)
            {
                signatureNext = false;
                foreach (PacingBlock candidate in blocks)
                {
                    if (!IsUsable(candidate))
                        continue;
                    block = candidate;
                    return;
                }
            }
            int total = 0;
            foreach (PacingBlock candidate in blocks)
                if (IsUsable(candidate) && candidate != previousBlock)
                    total += Mathf.Max(1, candidate.weight);
            if (total == 0)
            {
                block = previousBlock; // an area with a single block
                return;
            }
            int roll = random.Next(total);
            foreach (PacingBlock candidate in blocks)
            {
                if (!IsUsable(candidate) || candidate == previousBlock)
                    continue;
                roll -= Mathf.Max(1, candidate.weight);
                if (roll < 0)
                {
                    block = candidate;
                    return;
                }
            }
        }

        private static bool IsUsable(PacingBlock candidate) =>
            candidate != null && candidate.targets != null && candidate.targets.Length > 0;
    }
}
