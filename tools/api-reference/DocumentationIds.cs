using System.Reflection;

namespace Latiprimere.ApiReference;

/// <summary>
/// Makes the IDs the C# compiler gives types and members in XML documentation files, such as
/// <c>M:Latiprimere.IPrinter.SubmitAsync(Latiprimere.PrintContent,Latiprimere.PrintOptions,System.Threading.CancellationToken)</c>.
/// </summary>
internal static class DocumentationIds
{
	/// <summary>
	/// Makes a type's ID.
	/// </summary>
	/// <param name="type">The type.</param>
	/// <returns>The ID, starting <c>T:</c>.</returns>
	public static string ForType(Type type)
	{
		ArgumentNullException.ThrowIfNull(type);

		return "T:" + DocumentationIds.TypeName(type.IsGenericType ? type.GetGenericTypeDefinition() : type);
	}

	/// <summary>
	/// Makes a member's ID.
	/// </summary>
	/// <param name="member">A constructor, method, property, field or event.</param>
	/// <returns>The ID, starting <c>M:</c>, <c>P:</c>, <c>F:</c> or <c>E:</c>.</returns>
	/// <exception cref="ArgumentException">The member is of another kind, such as a nested type.</exception>
	public static string ForMember(MemberInfo member)
	{
		ArgumentNullException.ThrowIfNull(member);

		string declaringType = DocumentationIds.TypeName(member.DeclaringType!);

		return member switch
		{
			ConstructorInfo constructor => $"M:{declaringType}.#ctor{DocumentationIds.Parameters(constructor.GetParameters())}",
			MethodInfo method => $"M:{declaringType}.{method.Name}{DocumentationIds.GenericArity(method)}{DocumentationIds.Parameters(method.GetParameters())}",
			PropertyInfo property => $"P:{declaringType}.{property.Name}{DocumentationIds.Parameters(property.GetIndexParameters())}",
			FieldInfo field => $"F:{declaringType}.{field.Name}",
			EventInfo eventInfo => $"E:{declaringType}.{eventInfo.Name}",
			_ => throw new ArgumentException($"{member.MemberType} members have no documentation ID here.", nameof(member)),
		};
	}

	/// <summary>
	/// Writes a type definition's name as IDs do: its namespace and name, with <c>`</c> and the number of type
	/// parameters for a generic one, and dots between nested types.
	/// </summary>
	/// <param name="type">The type definition.</param>
	/// <returns>The name.</returns>
	private static string TypeName(Type type)
	{
		if (type.IsNested)
		{
			return DocumentationIds.TypeName(type.DeclaringType!) + "." + type.Name;
		}

		return type.Namespace is null ? type.Name : type.Namespace + "." + type.Name;
	}

	/// <summary>
	/// Writes a generic method's number of type parameters as IDs do, after two backticks.
	/// </summary>
	/// <param name="method">The method.</param>
	/// <returns>The arity, or nothing for a method that isn't generic.</returns>
	private static string GenericArity(MethodInfo method)
	{
		return method.IsGenericMethodDefinition ? "``" + method.GetGenericArguments().Length.ToString(System.Globalization.CultureInfo.InvariantCulture) : string.Empty;
	}

	/// <summary>
	/// Writes a parameter list as IDs do: the types in brackets, separated by commas, with nothing for no parameters.
	/// </summary>
	/// <param name="parameters">The parameters.</param>
	/// <returns>The list.</returns>
	private static string Parameters(ParameterInfo[] parameters)
	{
		return parameters.Length == 0
			? string.Empty
			: "(" + string.Join(',', parameters.Select(parameter => DocumentationIds.ParameterType(parameter.ParameterType))) + ")";
	}

	/// <summary>
	/// Writes a parameter's type as IDs do: generic arguments in braces, <c>@</c> for <c>ref</c> and <c>out</c>, and
	/// type parameters by position.
	/// </summary>
	/// <param name="type">The type.</param>
	/// <returns>The type, as written in an ID.</returns>
	private static string ParameterType(Type type)
	{
		if (type.IsByRef)
		{
			return DocumentationIds.ParameterType(type.GetElementType()!) + "@";
		}

		if (type.IsArray)
		{
			int rank = type.GetArrayRank();
			string dimensions = rank == 1 ? "[]" : "[" + string.Join(',', Enumerable.Repeat("0:", rank)) + "]";

			return DocumentationIds.ParameterType(type.GetElementType()!) + dimensions;
		}

		if (type.IsPointer)
		{
			return DocumentationIds.ParameterType(type.GetElementType()!) + "*";
		}

		if (type.IsGenericParameter)
		{
			string prefix = type.DeclaringMethod is null ? "`" : "``";

			return prefix + type.GenericParameterPosition.ToString(System.Globalization.CultureInfo.InvariantCulture);
		}

		if (type.IsGenericType)
		{
			string definition = DocumentationIds.TypeName(type.GetGenericTypeDefinition());
			string withoutArity = definition[..definition.LastIndexOf('`')];

			return withoutArity + "{" + string.Join(',', type.GetGenericArguments().Select(DocumentationIds.ParameterType)) + "}";
		}

		return DocumentationIds.TypeName(type);
	}
}
