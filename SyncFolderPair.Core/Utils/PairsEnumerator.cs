using SyncFolderPair.Core.Types;

namespace SyncFolderPair.Core.Utils;

public static class PairsEnumerator
{
    public static IEnumerable<Pair<T>> Enumerate<T>(IEnumerable<T> left, IEnumerable<T> right, Comparison<T> comparison)
    {
        var leftArray = left.ToArray();
        var rightArray = right.ToArray();
        Array.Sort(leftArray, comparison);
        Array.Sort(rightArray, comparison);

        var li = 0;
        var ri = 0;
        while (li < leftArray.Length && ri < rightArray.Length)
        {
            var cmp = comparison(leftArray[li], rightArray[ri]);
            if (cmp < 0)
                yield return new Pair<T>.Left(leftArray[li++]);
            else if (cmp == 0)
                yield return new Pair<T>.Both(leftArray[li++], rightArray[ri++]);
            else
                yield return new Pair<T>.Right(rightArray[ri++]);
        }
        while (li < leftArray.Length)
            yield return new Pair<T>.Left(leftArray[li++]);
        while (ri < rightArray.Length)
            yield return new Pair<T>.Right(rightArray[ri++]);
    }
}
