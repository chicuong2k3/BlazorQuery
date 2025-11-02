using System.Collections;
using System.Reflection;

namespace BlazorQuery.Core.BuildingBlocks;

public class QueryKey : IEquatable<QueryKey>
{
    public IReadOnlyList<object?> Parts { get; }
    private readonly int _hashCode;
    public QueryKey(params object?[] parts)
    {
        Parts = parts ?? Array.Empty<object>();
        _hashCode = ComputeHash(Parts);
    }

    public override bool Equals(object? obj)
        => Equals(obj as QueryKey);

    public bool Equals(QueryKey? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (Parts.Count != other.Parts.Count) return false;

        for (int i = 0; i < Parts.Count; i++)
        {
            if (!PartEquals(Parts[i], other.Parts[i])) return false;
        }
        return true;
    }

    public override int GetHashCode() => _hashCode;

    public override string ToString() 
        => $"QueryKey({string.Join(", ", Parts.Select(PartToString))})";

    private static int ComputeHash(IReadOnlyList<object?> parts)
    {
        unchecked
        {
            int hash = 17;
            foreach (var part in parts)
            {
                hash = hash * 31 + PartHash(part);
            }
            return hash;
        }
    }

    private static bool PartEquals(object? a, object? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;

        var typeA = a.GetType();
        var typeB = b.GetType();

        // Anonymous or record types
        if ((typeA.IsAnonymous() || typeA.IsRecord()) &&
            (typeB.IsAnonymous() || typeB.IsRecord()))
        {
            return AnonymousEquals(a, b);
        }

        // IEnumerable (excluding string)
        if (a is IEnumerable enumA && b is IEnumerable enumB &&
            typeA != typeof(string) && typeB != typeof(string))
            return EnumerableEquals(enumA, enumB);

        return a.Equals(b);
    }

    private static int PartHash(object? part)
    {
        if (part is null) return 0;

        var type = part.GetType();

        if (type.IsAnonymous() || type.IsRecord())
            return AnonymousHash(part);

        if (part is IEnumerable e && type != typeof(string))
            return EnumerableHash(e);

        return part.GetHashCode();
    }

    private static bool EnumerableEquals(IEnumerable a, IEnumerable b)
    {
        var enumA = a.Cast<object?>().ToList();
        var enumB = b.Cast<object?>().ToList();
        if (enumA.Count != enumB.Count) return false;
        for (int i = 0; i < enumA.Count; i++)
        {
            if (!PartEquals(enumA[i], enumB[i])) return false;
        }
        return true;
    }

    private static int EnumerableHash(IEnumerable e)
    {
        unchecked
        {
            int hash = 17;
            foreach (var item in e)
            {
                hash = hash * 31 + PartHash(item);
            }
            return hash;
        }
    }

    private static bool AnonymousEquals(object a, object b)
    {
        var propsA = a.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .OrderBy(p => p.Name);
        var propsB = b.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .OrderBy(p => p.Name);

        if (propsA.Count() != propsB.Count()) return false;

        foreach (var (pa, pb) in propsA.Zip(propsB, (x, y) => (x, y)))
        {
            var valA = pa.GetValue(a);
            var valB = pb.GetValue(b);
            if (!PartEquals(valA, valB)) return false;
        }
        return true;
    }

    private static int AnonymousHash(object a)
    {
        unchecked
        {
            int hash = 17;
            var props = a.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public)
             .OrderBy(p => p.Name);
            foreach (var prop in props)
            {
                hash = hash * 31 + PartHash(prop.GetValue(a));
            }
            return hash;
        }
    }

    private static string PartToString(object? part)
    {
        if (part is null) return "null";
        if (part is IEnumerable e && part.GetType() != typeof(string))
            return "[" + string.Join(",", e.Cast<object?>().Select(PartToString)) + "]";
        return part.ToString() ?? "null";
    }

}

internal static class TypeExtensions
{
    public static bool IsAnonymous(this Type type)
        => Attribute.IsDefined(type, typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), false)
           && type.IsGenericType && type.Name.Contains("AnonymousType");

    public static bool IsRecord(this Type type)
    => type.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), false)
       && type.GetMethod("<Clone>$") != null;
}