using System.Reflection;

namespace Latiprimere.ApiReference;

/// <summary>
/// One public type's page in the reference: where it is, and the members it shows.
/// </summary>
internal sealed class TypePage
{
	/// <summary>
	/// Initialises a new instance of the <see cref="TypePage"/> class.
	/// </summary>
	/// <param name="package">The package the type is in.</param>
	/// <param name="type">The type.</param>
	public TypePage(ApiPackage package, Type type)
	{
		ArgumentNullException.ThrowIfNull(package);
		ArgumentNullException.ThrowIfNull(type);

		this.Package = package;
		this.Type = type;
		this.Members = TypePage.FindMembers(type);
	}

	/// <summary>Gets the package the type is in.</summary>
	public ApiPackage Package { get; }

	/// <summary>Gets the type.</summary>
	public Type Type { get; }

	/// <summary>Gets the public members the type declares, as the page shows them.</summary>
	public IReadOnlyList<MemberInfo> Members { get; }

	/// <summary>Gets the page's file name, without the folder: the type's name in lower case.</summary>
	public string FileName
	{
		get
		{
			return this.Type.Name.ToLowerInvariant() + ".md";
		}
	}

	/// <summary>Gets the page's URL on the website.</summary>
	public string Url
	{
		get
		{
			return $"/docs/reference/{this.Package.Slug}/{this.Type.Name.ToLowerInvariant()}/";
		}
	}

	/// <summary>
	/// Makes a member's anchor on its type's page, as the website makes it from the member's heading:
	/// <c>constructors</c> for a constructor, <c>values</c> for an enum's value, which is listed rather than given a
	/// heading, or its name in lower case.
	/// </summary>
	/// <param name="member">The member.</param>
	/// <returns>The anchor, without <c>#</c>.</returns>
	public static string Anchor(MemberInfo member)
	{
		ArgumentNullException.ThrowIfNull(member);

		return member switch
		{
			ConstructorInfo => "constructors",
			FieldInfo { DeclaringType.IsEnum: true } => "values",
			_ => member.Name.ToLowerInvariant(),
		};
	}

	/// <summary>
	/// Finds the public types a package documents: every public type it exports, by name.
	/// </summary>
	/// <param name="package">The package.</param>
	/// <returns>The types.</returns>
	public static IEnumerable<Type> FindTypes(ApiPackage package)
	{
		ArgumentNullException.ThrowIfNull(package);

		return package.Assembly
			.GetExportedTypes()
			.Where(type => !Signatures.IsCompilerGenerated(type))
			.OrderBy(type => type.Name, StringComparer.OrdinalIgnoreCase);
	}

	/// <summary>
	/// Finds the public members a type declares, leaving out what the compiler adds, such as a record's
	/// <c>Equals</c> and property accessors, and an enum's hidden <c>value__</c>.
	/// </summary>
	/// <param name="type">The type.</param>
	/// <returns>The members: constructors, then the rest by name, overloads by their number of parameters.</returns>
	private static List<MemberInfo> FindMembers(Type type)
	{
		const BindingFlags declaredPublic = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

		return type.GetMembers(declaredPublic)
			.Where(member => member is ConstructorInfo or MethodInfo or PropertyInfo or FieldInfo or EventInfo)
			.Where(member => !Signatures.IsCompilerGenerated(member))
			.Where(member => member is not MethodInfo { IsSpecialName: true })
			.Where(member => member is not FieldInfo { IsSpecialName: true })
			.Where(member => !(type.IsValueType && member is ConstructorInfo constructor && constructor.GetParameters().Length == 0))
			.OrderBy(member => member is ConstructorInfo ? 0 : 1)
			.ThenBy(member => member.Name, StringComparer.OrdinalIgnoreCase)
			.ThenBy(member => member is MethodBase method ? method.GetParameters().Length : 0)
			.ThenBy(DocumentationIds.ForMember, StringComparer.Ordinal)
			.ToList();
	}
}
