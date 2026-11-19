using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Latiprimere.ApiReference;

/// <summary>
/// Writes the C# declarations of types and members, as they would appear in the source with their bodies left out.
/// </summary>
internal sealed class Signatures
{
	private readonly NullabilityInfoContext nullability = new NullabilityInfoContext(); // Reads nullable annotations

	/// <summary>
	/// Names what kind of type a type is.
	/// </summary>
	/// <param name="type">The type.</param>
	/// <returns><c>class</c>, <c>static class</c>, <c>record</c>, <c>struct</c>, <c>record struct</c>, <c>enum</c> or
	/// <c>interface</c>.</returns>
	public static string Kind(Type type)
	{
		ArgumentNullException.ThrowIfNull(type);

		if (type.IsEnum)
		{
			return "enum";
		}

		if (type.IsInterface)
		{
			return "interface";
		}

		if (type.IsValueType)
		{
			return Signatures.IsRecord(type) ? "record struct" : "struct";
		}

		if (type.IsAbstract && type.IsSealed)
		{
			return "static class";
		}

		return Signatures.IsRecord(type) ? "record" : "class";
	}

	/// <summary>
	/// Checks whether the compiler made a member, such as a record's <c>Equals</c>, rather than the source declaring it.
	/// </summary>
	/// <param name="member">The member.</param>
	/// <returns>Whether it did.</returns>
	public static bool IsCompilerGenerated(MemberInfo member)
	{
		ArgumentNullException.ThrowIfNull(member);

		return member.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false);
	}

	/// <summary>
	/// Writes a type's declaration.
	/// </summary>
	/// <param name="type">The type.</param>
	/// <returns>The declaration, such as <c>public sealed class PrinterCatalog : IPrinterCatalog</c>.</returns>
	public static string DeclareType(Type type)
	{
		ArgumentNullException.ThrowIfNull(type);

		string kind = Signatures.Kind(type);
		List<string> modifiers = ["public"];

		if (type.IsValueType && !type.IsEnum && type.IsDefined(typeof(IsReadOnlyAttribute), inherit: false))
		{
			modifiers.Add("readonly");
		}
		else if (kind is "class" or "record" && type.IsAbstract)
		{
			modifiers.Add("abstract");
		}
		else if (kind is "class" or "record" && type.IsSealed)
		{
			modifiers.Add("sealed");
		}

		List<string> bases = [];

		if (type.IsEnum && Enum.GetUnderlyingType(type) != typeof(int))
		{
			bases.Add(TypeNames.Display(Enum.GetUnderlyingType(type), null));
		}
		else if (!type.IsValueType && !type.IsInterface && type.BaseType is not null && type.BaseType != typeof(object))
		{
			bases.Add(TypeNames.Display(type.BaseType, null));
		}

		bases.AddRange(Signatures.DirectInterfaces(type).Select(implemented => TypeNames.Display(implemented, null)));

		string declaration = $"{string.Join(' ', modifiers)} {kind} {type.Name}";

		return bases.Count == 0 ? declaration : $"{declaration} : {string.Join(", ", bases)}";
	}

	/// <summary>
	/// Writes a member's declaration.
	/// </summary>
	/// <param name="member">A constructor, method, property, field or event.</param>
	/// <returns>The declaration, such as <c>Task&lt;IPrinter&gt; GetPrinterAsync(string printerId, CancellationToken cancellationToken = default)</c>.</returns>
	/// <exception cref="ArgumentException">The member is of another kind.</exception>
	public string Declare(MemberInfo member)
	{
		ArgumentNullException.ThrowIfNull(member);

		return member switch
		{
			ConstructorInfo constructor => $"public {constructor.DeclaringType!.Name}({this.Parameters(constructor)})",
			MethodInfo method => this.DeclareMethod(method),
			PropertyInfo property => this.DeclareProperty(property),
			FieldInfo field => this.DeclareField(field),
			EventInfo eventInfo => $"public event {TypeNames.Display(eventInfo.EventHandlerType!, this.nullability.Create(eventInfo))} {eventInfo.Name}",
			_ => throw new ArgumentException($"{member.MemberType} members aren't declared here.", nameof(member)),
		};
	}

	/// <summary>
	/// Writes a method's declaration.
	/// </summary>
	/// <param name="method">The method.</param>
	/// <returns>The declaration.</returns>
	private string DeclareMethod(MethodInfo method)
	{
		string returnType = TypeNames.Display(method.ReturnType, this.nullability.Create(method.ReturnParameter));
		string typeParameters = method.IsGenericMethodDefinition
			? "<" + string.Join(", ", method.GetGenericArguments().Select(argument => argument.Name)) + ">"
			: string.Empty;

		return $"{Signatures.Modifiers(method)}{returnType} {method.Name}{typeParameters}({this.Parameters(method)})";
	}

	/// <summary>
	/// Writes a property's declaration, with its public accessors.
	/// </summary>
	/// <param name="property">The property.</param>
	/// <returns>The declaration.</returns>
	private string DeclareProperty(PropertyInfo property)
	{
		MethodInfo? getter = property.GetGetMethod();
		MethodInfo? setter = property.GetSetMethod();
		bool isInitOnly = setter is not null
						  && setter.ReturnParameter.GetRequiredCustomModifiers().Any(modifier => modifier == typeof(IsExternalInit));

		List<string> accessors = [];

		if (getter is not null)
		{
			accessors.Add("get;");
		}

		if (setter is not null)
		{
			accessors.Add(isInitOnly ? "init;" : "set;");
		}

		string required = property.IsDefined(typeof(RequiredMemberAttribute), inherit: false) ? "required " : string.Empty;
		string type = TypeNames.Display(property.PropertyType, this.nullability.Create(property));

		return $"{Signatures.Modifiers((getter ?? setter)!)}{required}{type} {property.Name} {{ {string.Join(' ', accessors)} }}";
	}

	/// <summary>
	/// Writes a field's declaration, with a constant's value.
	/// </summary>
	/// <param name="field">The field.</param>
	/// <returns>The declaration.</returns>
	private string DeclareField(FieldInfo field)
	{
		string type = TypeNames.Display(field.FieldType, this.nullability.Create(field));

		if (field.IsLiteral)
		{
			return $"public const {type} {field.Name} = {Signatures.Literal(field.FieldType, field.GetRawConstantValue())}";
		}

		string modifiers = "public" + (field.IsStatic ? " static" : string.Empty) + (field.IsInitOnly ? " readonly" : string.Empty);

		return $"{modifiers} {type} {field.Name}";
	}

	/// <summary>
	/// Writes a method's parameters.
	/// </summary>
	/// <param name="method">The method or constructor.</param>
	/// <returns>The parameters, separated by commas.</returns>
	private string Parameters(MethodBase method)
	{
		bool isExtension = method.IsDefined(typeof(ExtensionAttribute), inherit: false);

		return string.Join(", ", method.GetParameters().Select((parameter, index) => this.Parameter(parameter, isExtension && index == 0)));
	}

	/// <summary>
	/// Writes one parameter: its modifiers, type, name and default value.
	/// </summary>
	/// <param name="parameter">The parameter.</param>
	/// <param name="isThis">Whether it is an extension method's first parameter.</param>
	/// <returns>The parameter.</returns>
	private string Parameter(ParameterInfo parameter, bool isThis)
	{
		string modifier = isThis ? "this "
			: parameter.IsOut ? "out "
			: parameter.ParameterType.IsByRef ? (parameter.IsIn ? "in " : "ref ")
			: parameter.IsDefined(typeof(ParamArrayAttribute), inherit: false) ? "params "
			: string.Empty;

		string type = TypeNames.Display(parameter.ParameterType, this.nullability.Create(parameter));
		string defaultValue = parameter.HasDefaultValue ? " = " + Signatures.Literal(parameter.ParameterType, parameter.RawDefaultValue) : string.Empty;

		return $"{modifier}{type} {parameter.Name}{defaultValue}";
	}

	/// <summary>
	/// Writes a constant or default value as C# does.
	/// </summary>
	/// <param name="type">The type it has.</param>
	/// <param name="value">The value.</param>
	/// <returns>The literal, such as <c>null</c>, <c>default</c>, <c>"ipp:"</c> or <c>DuplexMode.OneSided</c>.</returns>
	private static string Literal(Type type, object? value)
	{
		Type valueType = Nullable.GetUnderlyingType(type) ?? type;

		return value switch
		{
			null when type.IsValueType && Nullable.GetUnderlyingType(type) is null => "default",
			null => "null",
			string text => "\"" + text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"",
			bool flag => flag ? "true" : "false",
			char character => "'" + character + "'",
			_ when valueType.IsEnum => valueType.Name + "." + (Enum.GetName(valueType, value) ?? Convert.ToString(value, CultureInfo.InvariantCulture)),
			IFormattable number => number.ToString(null, CultureInfo.InvariantCulture),
			_ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
		};
	}

	/// <summary>
	/// Writes a method's modifiers, such as <c>public static </c>, with a space after them. Interface members have
	/// none.
	/// </summary>
	/// <param name="method">The method, or a property's accessor.</param>
	/// <returns>The modifiers.</returns>
	private static string Modifiers(MethodInfo method)
	{
		if (method.DeclaringType!.IsInterface)
		{
			return method.IsStatic ? "static " : string.Empty;
		}

		List<string> modifiers = ["public"];
		bool isOverride = method.GetBaseDefinition().DeclaringType != method.DeclaringType;

		if (method.IsStatic)
		{
			modifiers.Add("static");
		}
		else if (method.IsAbstract)
		{
			modifiers.Add("abstract");
		}
		else if (isOverride)
		{
			modifiers.Add("override");
		}
		else if (method.IsVirtual && !method.IsFinal)
		{
			modifiers.Add("virtual");
		}

		return string.Join(' ', modifiers) + " ";
	}

	/// <summary>
	/// Finds the interfaces a type declares itself: not those its base type brings, those another of its interfaces
	/// brings, or a record's <c>IEquatable</c> of itself.
	/// </summary>
	/// <param name="type">The type.</param>
	/// <returns>The interfaces, by name.</returns>
	public static IEnumerable<Type> DirectInterfaces(Type type)
	{
		ArgumentNullException.ThrowIfNull(type);

		Type[] all = type.GetInterfaces();
		HashSet<Type> inherited = new HashSet<Type>(type.BaseType?.GetInterfaces() ?? []);

		foreach (Type implemented in all)
		{
			inherited.UnionWith(implemented.GetInterfaces());
		}

		if (Signatures.IsRecord(type))
		{
			inherited.Add(typeof(IEquatable<>).MakeGenericType(type));
		}

		return all.Where(implemented => !inherited.Contains(implemented)).OrderBy(implemented => implemented.Name, StringComparer.Ordinal);
	}

	/// <summary>
	/// Checks whether a type is a record, by the <c>PrintMembers</c> method the compiler gives every record.
	/// </summary>
	/// <param name="type">The type.</param>
	/// <returns>Whether it is one.</returns>
	private static bool IsRecord(Type type)
	{
		MethodInfo? printMembers = type.GetMethod("PrintMembers", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);

		return printMembers is not null && Signatures.IsCompilerGenerated(printMembers);
	}
}
