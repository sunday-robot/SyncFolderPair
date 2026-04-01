namespace SyncFolderPair.Core.Utils;

public static class PairsEnumerator
{
    public static IEnumerable<(T? Left, T? Right)> Enumerate<T>(IEnumerable<T> left, IEnumerable<T> right, Comparison<T> comparison)
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
                yield return (leftArray[li++], default);
            else if (cmp == 0)
                yield return (leftArray[li++], rightArray[ri++]);
            else
                yield return (default, rightArray[ri++]);
        }
        while (li < leftArray.Length)
            yield return (leftArray[li++], default);
        while (ri < rightArray.Length)
            yield return (default, rightArray[ri++]);
    }
}
