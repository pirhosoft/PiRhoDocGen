using PiRhoSoft.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace PiRhoSoft.DocGen.Editor
{
	[Flags]
	public enum DocumentationTypeCategory
	{
		Behaviour = 0x1,
		Asset = 0x2,
		Class = 0x4,
		Enum = 0x8,
		Abstract = 0x10,
		All = ~0
	}

	[Serializable] public class DocumentationNamespaceList : SerializedList<string> { }
	[Serializable] public class DocumentationCategoryList : SerializedList<DocumentationCategory> { }
	[Serializable] public class DocumentationSectionList : SerializedList<DocumentationSection> { }

	public class DocumentationGenerator : ScriptableObject
	{
		public string OutputDirectory = "docs/Generated";
		[List] public DocumentationCategoryList Categories = new DocumentationCategoryList();
		[Frame] public TableOfContents TableOfContents = new TableOfContents();
		[Frame] public LogDescriptions LogDescriptions = new LogDescriptions();
		[Frame] public HelpUrlValidator HelpUrls = new HelpUrlValidator();

		void OnEnable()
		{
			_rootPath = new DirectoryInfo(Application.dataPath).Parent.FullName;
		}

		public void GenerateProcessorTemplates()
		{
			var allFile = new StringBuilder(
@":multipage - level: 1
:icons: font
:source - highlighter: highlightjs
:experimental:
:example - caption!:
:figure - caption!:
:table - caption!:
");

			foreach (var category in Categories)
				allFile.AppendLine($"include::{category.Id}.adoc[]");

			WriteFile(OutputDirectory, "all.adoc", allFile.ToString());
		}

		#region Tags

		public const string CategoryNameTag = "{CategoryName}";
		public const string CategoryNiceNameTag = "{CategoryNiceName}";
		public const string CategoryIdTag = "{CategoryId}";

		public const string TypeNameTag = "{TypeName}";
		public const string TypeRawNameTag = "{TypeRawName}";
		public const string TypeNiceNameTag = "{TypeNiceName}";
		public const string TypeIdTag = "{TypeId}";
		public const string TypeNamespaceTag = "{TypeNamespace}";
		public const string TypeFilenameTag = "{TypeFilename}";

		public const string SectionNameTag = "{SectionName}";
		public const string SectionNiceNameTag = "{SectionNiceName}";
		public const string SectionIdTag = "{SectionId}";

		#endregion

		#region Names

		private static readonly Dictionary<string, string> _typeNameMap = new Dictionary<string, string>()
		{
			{ "Void", "void" },
			{ "Boolean", "bool" },
			{ "Int32", "int" },
			{ "Single", "float" },
			{ "String", "string" }
		};

		public static string GetCleanName(Type type)
		{
			var index = type.Name.IndexOf('`'); // generics have this tilde appended followed by the generic list
			var name = index < 0 ? type.Name : type.Name.Substring(0, index);

			name = name.TrimEnd('&'); // refs and outs have this appended

			return _typeNameMap.TryGetValue(name, out string mappedName) ? mappedName : name; // system types use the full name (Int32, Single, etc)
		}

		public static string GetNiceName(string name)
		{
			// for some reason NicifyVariableName can't be called from a background thread, so this is more or less a
			// re-implementation of that

			var nice = name[0].ToString();
			var space = true;

			for (var i = 1; i < name.Length; i++)
			{
				if (space && char.IsUpper(name[i]))
					nice += " ";

				nice += name[i];

				space = name[i] != ' ' && name[i] != '<';
			}

			return nice;
		}

		public static string GetTypeId(Type type)
		{
			var id = GetId(type.Name);

			if (type.IsNested)
				id = GetTypeId(type.DeclaringType) + "-" + id;

			return id;
		}

		public static string GetId(string name)
		{
			var id = char.ToLowerInvariant(name[0]).ToString();

			for (var i = 1; i < name.Length; i++)
			{
				if (name[i] == '[' || name[i] == ']')
					continue;

				if (char.IsUpper(name[i]))
					id += "-";

				if (name[i] == '`')
					id += '-';
				else
					id += char.ToLowerInvariant(name[i]);
			}

			return id;
		}

		#endregion

		#region Type Inclusion

		public static IEnumerable<Type> FindTypes(Func<Type, bool> predicate)
		{
			return AppDomain.CurrentDomain.GetAssemblies()
				.Where(assembly => !assembly.IsDynamic) // GetExportedTypes throws an exception when called on dynamic assemblies
				.SelectMany(t => t.GetExportedTypes())
				.Where(predicate);
		}

		public static bool IsTypeIncluded(Type type, DocumentationTypeCategory includedTypes, IList<string> includedNamespaces)
		{
			var includeAbstract = includedTypes.HasFlag(DocumentationTypeCategory.Abstract);
			var includeClasses = includedTypes.HasFlag(DocumentationTypeCategory.Class);
			var includeEnums = includedTypes.HasFlag(DocumentationTypeCategory.Enum);
			var includeBehaviours = includedTypes.HasFlag(DocumentationTypeCategory.Behaviour);
			var includeAssets = includedTypes.HasFlag(DocumentationTypeCategory.Asset);

			var isBehavior = typeof(MonoBehaviour).IsAssignableFrom(type);
			var isAsset = typeof(ScriptableObject).IsAssignableFrom(type);

			if (!type.IsVisible || (type.IsAbstract && !includeAbstract) || (type.IsEnum && !includeEnums) || (!includeBehaviours && isBehavior) || (!includeAssets && isAsset))
				return false;

			if (!includeClasses && !isBehavior && !isAsset && !type.IsEnum)
				return false;

			return IsTypeIncluded(type, includedNamespaces);
		}

		public static bool IsTypeIncluded(Type type, IList<string> namespaces)
		{
			foreach (var ns in namespaces)
			{
				if (type.Namespace != null && type.Namespace.StartsWith(ns))
					return true;
			}

			return false;
		}

		#endregion

		#region File I/O

		private static string _rootPath;

		public static bool WriteFile(string folder, string filename, string content)
		{
			var outputFile = new FileInfo(Path.Combine(_rootPath, folder, filename));

			try
			{
				Directory.CreateDirectory(outputFile.Directory.FullName);
				File.WriteAllText(outputFile.FullName, content);
				return true;
			}
			catch (Exception exception)
			{
				Debug.LogException(exception);
				return false;
			}
		}

		#endregion
	}
}
