using System.Reflection;

namespace Latiprimere.ApiReference;

/// <summary>
/// Writes types as C# code does: keywords for built-in types, generic arguments in angle brackets, and <c>?</c> for
/// nullable value and reference types.
/// </summary>
internal static class TypeNames
{
	private static readonly Dictionary<Type, string> Keywords = new Dictionary<Type, string>
	{
		[typeof(bool)]    = "bool",
		[typeof(byte)]    = "byte",
		[typeof(sbyte)]   = "sbyte",
		[typeof(char)]    = "char",
		[typeof(short)]   = "short",
		[typeof(ushort)]  = "ushort",
		[typeof(int)]     = "int",
		[typeof(uint)]    = "uint",
		[typeof(long)]    = "long",
		[typeof(ulong)]   = "ulong",
		[typeof(float)]   = "float",
		[typeof(double)]  = "double",
		[typeof(decimal)] = "decimal",
		[typeof(string)]  = "string",
		[typeof(object)]  = "object",
		[typeof(void)]    = "void",
	}; // C# keywords for built-in types

	/// <summary>
	/// Writes a type as C# does.
	/// </summary>
	/// <param name="type">The type.</param>
	/// <param name="nullability">Its nullability where it is used, or <see langword="null"/> to write reference types
	/// without <c>?</c>.</param>
	/// <returns>The type, such as <c>Task&lt;IPrinter?&gt;</c>.</returns>
	public static string Display(Type type, NullabilityInfo? nullability)
	{
		ArgumentNullException.ThrowIfNull(type);

		if (type.IsByRef)
		{
			return TypeNames.Display(type.GetElementType()!, nullability);
		}

		Type? underlying = Nullable.GetUnderlyingType(type);

		if (underlying is not null)
		{
			NullabilityInfo? argument = nullability?.GenericTypeArguments.Length == 1 ? nullability.GenericTypeArguments[0] : null;

			return TypeNames.Display(underlying, argument) + "?";
		}

		string name;

		if (type.IsArray)
		{
			name = TypeNames.Display(type.GetElementType()!, nullability?.ElementType) + "[" + new string(',', type.GetArrayRank() - 1) + "]";
		}
		else if (type.IsGenericType)
		{
			Type[] arguments = type.GetGenericArguments();
			IEnumerable<string> argumentNames = arguments.Select
			(
				(argument, index) => TypeNames.Display(argument, nullability is not null && index < nullability.GenericTypeArguments.Length ? nullability.GenericTypeArguments[index] : null)
			);

			name = TypeNames.WithoutArity(type.Name) + "<" + string.Join(", ", argumentNames) + ">";
		}
		else
		{
			name = TypeNames.Keywords.GetValueOrDefault(type) ?? type.Name;
		}

		bool isNullableReference = !type.IsValueType && nullability?.ReadState == NullabilityState.Nullable;

		return isNullableReference ? name + "?" : name;
	}

	/// <summary>
	/// Removes a generic type's number of type parameters from its name, as in <c>IReadOnlyList`1</c>.
	/// </summary>
	/// <param name="name">The name.</param>
	/// <returns>The name without it.</returns>
	public static string WithoutArity(string name)
	{
		ArgumentNullException.ThrowIfNull(name);

		int backtick = name.IndexOf('`', StringComparison.Ordinal);

		return backtick < 0 ? name : name[..backtick];
	}
}
