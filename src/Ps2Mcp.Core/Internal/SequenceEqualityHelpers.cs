using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Ps2Mcp.Core.Internal;

internal static class SequenceEqualityHelpers
{
    public static bool SequenceEqual<T>(ImmutableArray<T> left, ImmutableArray<T> right)
    {
        if (left.Length != right.Length) return false;
        var comparer = EqualityComparer<T>.Default;
        for (int i = 0; i < left.Length; i++)
        {
            if (!comparer.Equals(left[i], right[i])) return false;
        }
        return true;
    }

    public static bool NullableSequenceEqual<T>(ImmutableArray<T>? left, ImmutableArray<T>? right)
    {
        if (!left.HasValue) return !right.HasValue;
        if (!right.HasValue) return false;
        return SequenceEqual(left.Value, right.Value);
    }

    public static void AddToHash<T>(ref HashCode hash, ImmutableArray<T> list)
    {
        hash.Add(list.Length);
        for (int i = 0; i < list.Length; i++) hash.Add(list[i]);
    }

    public static void AddNullableToHash<T>(ref HashCode hash, ImmutableArray<T>? list)
    {
        if (!list.HasValue) { hash.Add(false); return; }
        hash.Add(true);
        AddToHash(ref hash, list.Value);
    }
}
