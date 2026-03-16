namespace SyncFolderPair.Utils
{
    public static class PairEnumerator
    {
        public enum Existance
        {
            OnlyLeft,
            OnlyRight,
            Both,
        }

        public static IEnumerable<(T, Existance)> Enumerate<T>(T[] leftArray, T[] rightArray, IComparer<T> comparator)
        {
            leftArray = [.. leftArray.OrderBy(x => x, comparator)];
            rightArray = [.. rightArray.OrderBy(x => x, comparator)];

            int li = 0;
            int ri = 0;
            while (li < leftArray.Length && ri < rightArray.Length)
            {
                int cmp = comparator.Compare(leftArray[li], rightArray[ri]);
                if (cmp < 0)
                {
                    yield return (leftArray[li], Existance.OnlyLeft);
                    li++;
                }
                else if (cmp == 0)
                {
                    yield return (leftArray[li], Existance.Both);
                    li++;
                    ri++;
                }
                else
                {
                    yield return (rightArray[ri], Existance.OnlyRight);
                    ri++;
                }
            }
            for (; li < leftArray.Length; li++)
            {
                yield return (leftArray[li], Existance.OnlyLeft);
            }
            for (; ri < rightArray.Length; ri++)
            {
                yield return (rightArray[ri], Existance.OnlyRight);
            }
        }
    }
}
