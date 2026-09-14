using System.Collections.Generic;
using System.Linq;

public static class TrainingsRankingLogic
{
    public static bool TryInsertBounded<T>(
        IList<T> list,
        T candidate,
        ITrainingRankingPolicy<T> policy,
        int capacity)
    {
        // Ensure sorted before operating (optional if you always keep it sorted)
        var sorted = list.OrderBy(x => x, new PolicyComparer<T>(policy)).ToList();

        if (sorted.Count < capacity)
        {
            sorted.Add(candidate);
            sorted.Sort(new PolicyComparer<T>(policy));
            Overwrite(list, sorted);
            return true;
        }

        // Compare against the worst (last)
        var worst = sorted[^1];
        if (policy.Compare(candidate, worst) < 0)
        {
            sorted.Add(candidate);
            sorted.Sort(new PolicyComparer<T>(policy));
            if (sorted.Count > capacity) sorted.RemoveAt(sorted.Count - 1);
            Overwrite(list, sorted);
            return true;
        }

        return false;
    }

    private static void Overwrite<T>(IList<T> target, IList<T> source)
    {
        target.Clear();
        for (int i = 0; i < source.Count; i++) target.Add(source[i]);
    }

    private sealed class PolicyComparer<T> : IComparer<T>
    {
        private readonly ITrainingRankingPolicy<T> _policy;
        public PolicyComparer(ITrainingRankingPolicy<T> policy) => _policy = policy;
        public int Compare(T x, T y) => _policy.Compare(x, y);
    }
}
