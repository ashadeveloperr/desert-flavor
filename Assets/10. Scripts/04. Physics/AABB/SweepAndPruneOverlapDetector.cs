using System;
using System.Collections.Generic;
using UnityEngine;

namespace Study3D.ThreeDPhysics
{
    public sealed class SweepAndPruneOverlapDetector
    {
        private readonly List<int> sortedIndices = new();
        private readonly List<int> activeIndices = new();
        private readonly List<(int A, int B)> pairs = new();
        private readonly Comparison<int> compareByMinX;

        private IReadOnlyList<Bounds> currentBounds;

        public SweepAndPruneOverlapDetector() => compareByMinX = CompareByMinX;

        public IReadOnlyList<(int A, int B)> Pairs => pairs;

        public void Detect(IReadOnlyList<Bounds> bounds)
        {
            currentBounds = bounds;
            pairs.Clear();
            sortedIndices.Clear();
            activeIndices.Clear();

            for (var i = 0; i < bounds.Count; i++) sortedIndices.Add(i);
            sortedIndices.Sort(compareByMinX);

            foreach (var current in sortedIndices)
            {
                var currentBox = bounds[current];
                PruneInactive(currentBox.min.x);

                foreach (var other in activeIndices)
                {
                    if (currentBox.Intersects(bounds[other]))
                        pairs.Add(current < other ? (current, other) : (other, current));
                }

                activeIndices.Add(current);
            }

            currentBounds = null;
        }

        private void PruneInactive(float sweepX)
        {
            for (var i = activeIndices.Count - 1; i >= 0; i--)
            {
                if (currentBounds[activeIndices[i]].max.x < sweepX) activeIndices.RemoveAt(i);
            }
        }

        private int CompareByMinX(int a, int b) => currentBounds[a].min.x.CompareTo(currentBounds[b].min.x);
    }
}