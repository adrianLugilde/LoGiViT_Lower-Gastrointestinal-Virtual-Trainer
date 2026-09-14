public interface ITrainingRankingPolicy<T>
{
    // Return <0 if a should rank ABOVE b (meaning a is better)
    int Compare(T a, T b);
}