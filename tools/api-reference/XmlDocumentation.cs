using System.Reflection;
using System.Xml.Linq;

namespace Latiprimere.ApiReference;

/// <summary>
/// The packages' XML documentation, by documentation ID, with <c>&lt;inheritdoc/&gt;</c> filled in from the base type
/// or interface member it refers to.
/// </summary>
internal sealed class XmlDocumentation
{
	private readonly Dictionary<string, XElement> members; // Each <member> element, by its documentation ID

	/// <summary>
	/// Initialises a new instance of the <see cref="XmlDocumentation"/> class.
	/// </summary>
	/// <param name="members">Each <c>&lt;member&gt;</c> element, by its documentation ID.</param>
	private XmlDocumentation(Dictionary<string, XElement> members)
	{
		this.members = members;
	}

	/// <summary>
	/// Reads the XML documentation file that sits beside each package's assembly.
	/// </summary>
	/// <param name="packages">The packages.</param>
	/// <returns>The documentation.</returns>
	/// <exception cref="FileNotFoundException">A package has no documentation file beside its assembly.</exception>
	public static XmlDocumentation Load(IEnumerable<ApiPackage> packages)
	{
		ArgumentNullException.ThrowIfNull(packages);

		Dictionary<string, XElement> members = new Dictionary<string, XElement>(StringComparer.Ordinal);

		foreach (ApiPackage package in packages)
		{
			string path = Path.ChangeExtension(package.Assembly.Location, ".xml");
			XDocument document = XDocument.Load(path, LoadOptions.PreserveWhitespace);

			foreach (XElement member in document.Descendants("member"))
			{
				members[(string)member.Attribute("name")!] = member;
			}
		}

		return new XmlDocumentation(members);
	}

	/// <summary>
	/// Checks whether the documentation has an entry with an ID, without inheriting any.
	/// </summary>
	/// <param name="documentationId">The documentation ID.</param>
	/// <returns>Whether there is one.</returns>
	public bool Contains(string documentationId)
	{
		return this.members.ContainsKey(documentationId);
	}

	/// <summary>
	/// Finds a type's documentation.
	/// </summary>
	/// <param name="type">The type.</param>
	/// <returns>Its <c>&lt;member&gt;</c> element, with anything inherited filled in; or <see langword="null"/>.</returns>
	public XElement? ForType(Type type)
	{
		ArgumentNullException.ThrowIfNull(type);

		XElement? own = this.members.GetValueOrDefault(DocumentationIds.ForType(type));

		return XmlDocumentation.Inherit(own, () => this.FindInherited(type));
	}

	/// <summary>
	/// Finds a member's documentation.
	/// </summary>
	/// <param name="member">The member.</param>
	/// <returns>Its <c>&lt;member&gt;</c> element, with anything inherited filled in; or <see langword="null"/>.</returns>
	public XElement? ForMember(MemberInfo member)
	{
		ArgumentNullException.ThrowIfNull(member);

		XElement? own = this.members.GetValueOrDefault(DocumentationIds.ForMember(member));

		return XmlDocumentation.Inherit(own, () => this.FindInherited(member));
	}

	/// <summary>
	/// Fills in <c>&lt;inheritdoc/&gt;</c>: the inherited documentation, with the entry's own elements in place of
	/// the inherited ones of the same kind, such as its own <c>&lt;remarks&gt;</c>.
	/// </summary>
	/// <param name="own">The entry, or <see langword="null"/>.</param>
	/// <param name="findInherited">Finds the documentation it inherits.</param>
	/// <returns>The documentation to show.</returns>
	private static XElement? Inherit(XElement? own, Func<XElement?> findInherited)
	{
		if (own?.Element("inheritdoc") is null)
		{
			return own;
		}

		XElement merged = new XElement(findInherited() ?? new XElement("member"));

		foreach (XElement child in own.Elements().Where(element => element.Name != "inheritdoc"))
		{
			string? key = (string?)child.Attribute("name") ?? (string?)child.Attribute("cref");
			XElement? same = key is null
				? merged.Element(child.Name)
				: merged.Elements(child.Name).FirstOrDefault(element => ((string?)element.Attribute("name") ?? (string?)element.Attribute("cref")) == key);

			same?.Remove();
			merged.Add(new XElement(child));
		}

		return merged;
	}

	/// <summary>
	/// Finds the documentation a type inherits: its base type's, or else its first interface's that has some.
	/// </summary>
	/// <param name="type">The type.</param>
	/// <returns>The documentation, or <see langword="null"/>.</returns>
	private XElement? FindInherited(Type type)
	{
		IEnumerable<Type> candidates = type.BaseType is null ? type.GetInterfaces() : type.GetInterfaces().Prepend(type.BaseType);

		return candidates.Select(this.ForType).FirstOrDefault(documentation => documentation is not null);
	}

	/// <summary>
	/// Finds the documentation a member inherits: from the member it overrides, or the interface member it
	/// implements.
	/// </summary>
	/// <param name="member">The member.</param>
	/// <returns>The documentation, or <see langword="null"/>.</returns>
	private XElement? FindInherited(MemberInfo member)
	{
		switch (member)
		{
			case MethodInfo method:
				MemberInfo? baseMethod = XmlDocumentation.FindBaseMethod(method);

				return baseMethod is null ? null : this.ForMember(baseMethod);

			case PropertyInfo property:
				MethodInfo? accessor = property.GetGetMethod() ?? property.GetSetMethod();
				MethodInfo? baseAccessor = accessor is null ? null : XmlDocumentation.FindBaseMethod(accessor);
				PropertyInfo? baseProperty = baseAccessor?.DeclaringType?
					.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
					.FirstOrDefault(candidate => candidate.GetGetMethod() == baseAccessor || candidate.GetSetMethod() == baseAccessor);

				return baseProperty is null ? null : this.ForMember(baseProperty);

			case ConstructorInfo constructor:
				Type[] parameterTypes = constructor.GetParameters().Select(parameter => parameter.ParameterType).ToArray();
				ConstructorInfo? baseConstructor = constructor.DeclaringType?.BaseType?.GetConstructor(parameterTypes);

				return baseConstructor is null ? null : this.ForMember(baseConstructor);

			default:
				return null;
		}
	}

	/// <summary>
	/// Finds the method another overrides or implements.
	/// </summary>
	/// <param name="method">The method.</param>
	/// <returns>The overridden method, else the interface method it implements; or <see langword="null"/>.</returns>
	private static MethodInfo? FindBaseMethod(MethodInfo method)
	{
		MethodInfo baseDefinition = method.GetBaseDefinition();

		if (baseDefinition.DeclaringType != method.DeclaringType)
		{
			return baseDefinition;
		}

		Type? declaringType = method.DeclaringType;

		if (declaringType is null || declaringType.IsInterface)
		{
			return null;
		}

		foreach (Type implemented in declaringType.GetInterfaces())
		{
			InterfaceMapping map = declaringType.GetInterfaceMap(implemented);
			int index = Array.IndexOf(map.TargetMethods, method);

			if (index >= 0)
			{
				return map.InterfaceMethods[index];
			}
		}

		return null;
	}
}
