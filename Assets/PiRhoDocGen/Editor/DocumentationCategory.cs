using PiRhoSoft.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PiRhoSoft.DocGen.Editor
{
	[Serializable]
	public class DocumentationCategory
	{
		[Serializable]
		public class TemplateSet
		{
			[Multiline] public string CategoryFile = "{CategoryName}/{CategoryNiceName}/{CategoryId}\n\n{Types}";
			[Multiline] public string Type = "{TypeName}/{TypeNiceName}/{TypeId}: {TypeFilename}";
			[Multiline] public string TypeSeparator = "\n";

			[Multiline] public string TypeFile = "{CategoryName}/{CategoryNiceName}/{CategoryId} {TypeNiceName}/{TypeId}/{TypeFilename}\n\n{TypeNamespace}{TypeName}{Generics} : {Bases}\n\n{Sections}";

			[Multiline] public string Generic = "{Name}";
			[Multiline] public string GenericOpener = "<";
			[Multiline] public string GenericCloser = ">";
			[Multiline] public string GenericSeparator = ", ";

			[Multiline] public string BaseOpener = " : ";
			[Multiline] public string BaseSeparator = ", ";

			[Multiline] public string Section = "{SectionName}/{SectionNiceName}/{SectionId}\n\n{Members}";
			[Multiline] public string SectionSeparator = "\n\n";

			[Multiline] public string Constructor = "{Name}({Parameters})";
			[Multiline] public string Field = "{Type} {Name}/{NiceName}";
			[Multiline] public string Property = "{Decorators}{Type} {Name}";
			[Multiline] public string Method = "{Decorators}{Type} {Name}{Generics}({Parameters})";
			[Multiline] public string MemberSeparator = "\n\n";

			[Multiline] public string Parameter = "{Decorators}{Type} {Name}";
			[Multiline] public string ParameterSeparator = ", ";

			[Multiline] public string Decorator = "{Name} ";
			[Multiline] public string DecoratorSeparator = "";

			public string InternalLink = "{TypeName}/{TypeNiceName}/{TypeId}: {TypeFilename}";
			public string UnknownLink = "{TypeName}/{TypeNiceName}/{TypeId}";
		}

		[Serializable]
		public class ExternalNamespace
		{
			public string Namespace;
			public string LinkTemplate = "{TypeName}/{TypeNiceName}/{TypeId}: {TypeFilename}";
		}

		[Serializable]
		public class ExternalNamespaceList : SerializedList<ExternalNamespace> { }

		public string Name = "";
		public string CategoryFilename = "Generated/{CategoryId}.txt";
		public string TypeFilename = "Generated/{CategoryId}/{TypeId}.txt";

		public bool IncludeInTableOfContents = true;

		[EnumButtons] public DocumentationTypeCategory IncludedTypes = DocumentationTypeCategory.All;
		[List] public DocumentationNamespaceList IncludedNamespaces = new DocumentationNamespaceList();
		[List] public DocumentationNamespaceList ExcludedNamespaces = new DocumentationNamespaceList();
		[List] [Inline] public ExternalNamespaceList ExternalNamespaces = new ExternalNamespaceList();
		[List] [Inline] public DocumentationSectionList Sections = new DocumentationSectionList();
		[Frame] public TemplateSet Templates = new TemplateSet();

		public string Id { get; private set; }
		public string NiceName { get; private set; }
		public IEnumerable<DocumentationCategory> AllCategories { get; private set; }

		private const string _typesTag = "{Types}";
		private const string _genericNameTag = "{Name}";
		private const string _arraySuffix = "[]";
		private const string _genericOpener = "<";
		private const string _genericCloser = ">";

		public void Generate(IEnumerable<DocumentationCategory> allCategories, string outputFolder)
		{
			Id = DocumentationGenerator.GetId(Name);
			NiceName = DocumentationGenerator.GetNiceName(Name);
			AllCategories = allCategories;

			var types = GetTypes();
			var index = new StringBuilder();
			var files = new List<string>();
			var first = true;

			foreach (var type in types)
			{
				var typeIndex = type.GenerateIndex(this);
				var typeFile = type.GenerateFile(this);

				if (!first)
					index.Append(Templates.TypeSeparator);

				index.Append(typeIndex);
				first = false;

				DocumentationGenerator.WriteFile(outputFolder, type.Filename, typeFile);
			}

			var contents = Templates.CategoryFile
				.Replace(DocumentationGenerator.CategoryNameTag, Name)
				.Replace(DocumentationGenerator.CategoryNiceNameTag, NiceName)
				.Replace(DocumentationGenerator.CategoryIdTag, Id)
				.Replace(_typesTag, index.ToString());

			var filename = CategoryFilename
				.Replace(DocumentationGenerator.CategoryNameTag, Name)
				.Replace(DocumentationGenerator.CategoryNiceNameTag, NiceName)
				.Replace(DocumentationGenerator.CategoryIdTag, Id);

			DocumentationGenerator.WriteFile(outputFolder, filename, contents);
		}

		public IEnumerable<DocumentationType> GetTypes()
		{
			return DocumentationGenerator
				.FindTypes(IsTypeIncluded)
				.Select(type => new DocumentationType(type, this))
				.OrderBy(type => type.Name);
		}

		private bool IsTypeIncluded(Type type)
		{
			return DocumentationGenerator.IsTypeIncluded(type, IncludedTypes, IncludedNamespaces, ExcludedNamespaces);
		}

		public string GetLink(Type type)
		{
			if (type.IsArray)
			{
				return GetLink(type.GetElementType()) + _arraySuffix;
			}
			else if (type.IsGenericParameter)
			{
				return type.Name;
			}
			else
			{
				var link = GetTypeLink(type);
				
				if (type.IsGenericType)
					link += GetGenerics(type);

				return link;
			}
		}

		private string GetTypeLink(Type type)
		{
			if (DocumentationGenerator.IsTypeIncluded(type, IncludedNamespaces, ExcludedNamespaces))
				return GetTypeLink(type, this, Templates.InternalLink);

			foreach (var external in ExternalNamespaces)
			{
				if (DocumentationGenerator.IsTypeIncluded(type, new List<string> { external.Namespace }, ExcludedNamespaces))
					return GetTypeLink(type, this, external.LinkTemplate);
			}

			return GetTypeLink(type, null, Templates.UnknownLink);
		}

		private string GetTypeLink(Type type, DocumentationCategory category, string template)
		{
			var cleanName = DocumentationGenerator.GetCleanName(type);
			var niceName = DocumentationGenerator.GetNiceName(cleanName);
			var id = DocumentationGenerator.GetTypeId(type);

			return template
				.Replace(DocumentationGenerator.CategoryNameTag, category?.Name)
				.Replace(DocumentationGenerator.CategoryNiceNameTag, category?.NiceName)
				.Replace(DocumentationGenerator.CategoryIdTag, category?.Id)
				.Replace(DocumentationGenerator.TypeIdTag, id)
				.Replace(DocumentationGenerator.TypeNameTag, cleanName)
				.Replace(DocumentationGenerator.TypeRawNameTag, type.Name)
				.Replace(DocumentationGenerator.TypeNiceNameTag, niceName)
				.Replace(DocumentationGenerator.TypeNamespaceTag, type.Namespace)
				.Replace('`', '-'); // this is specific to msdn but potentially makes sense for other scenarios as well
		}

		public string GetGenerics(Type type)
		{
			var builder = new StringBuilder();

			var generics = type.IsConstructedGenericType
				? type.GetGenericArguments()
				: type.GetGenericTypeDefinition().GetGenericArguments();

			if (generics.Length > 0)
			{
				builder.Append(_genericOpener);

				for (var i = 0; i < generics.Length; i++)
				{
					if (i != 0)
						builder.Append(Templates.GenericSeparator);

					var generic = type.IsConstructedGenericType ? GetLink(generics[0]) : Templates.Generic.Replace(_genericNameTag, generics[0].Name);
					builder.Append(generic);
				}

				builder.Append(_genericCloser);
			}

			return builder.ToString();
		}
	}
}
