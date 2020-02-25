using PiRhoSoft.Utilities.Editor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace PiRhoSoft.DocGen.Editor
{
	public class DocumentationGeneratorWindow : EditorWindow
	{
		private static readonly StringPreference _settingsFilePreference = new StringPreference("PiRho.DocGen.SettingsFile", "");

		private static string _templatesFolder;
		private static string _applicationPath;
		private static string _rootPath;

		private Dictionary<string, string> _templates;
		private GenericMenu _templateMenu;

		private DocumentationGenerator _generator;
		private GenerationState _state = GenerationState.Waiting;
		private string _message = string.Empty;
		private float _progress = 0.0f;
		private Thread _thread = null;

		[MenuItem("Window/PiRho DocGen/Documentation Generator")]
		public static void Open()
		{
			GetWindow<DocumentationGeneratorWindow>("Documentation Generator").Show();
		}

		void OnEnable()
		{
			_applicationPath = Application.dataPath;
			_rootPath = new DirectoryInfo(_applicationPath).Parent.FullName;
			_state = GenerationState.Waiting;
			_templatesFolder = AssetHelper.GetScriptPath() + "/Templates";

			LoadTemplates();
			LoadGenerator(_settingsFilePreference.Value);
		}

		void OnDisable()
		{
			if (_generator)
				SaveGenerator(_generator, _settingsFilePreference.Value);

			UnloadGenerator();
		}

		void OnInspectorUpdate()
		{
			if (_state != GenerationState.Waiting)
				Repaint();
		}

		void OnGUI()
		{
			if (_state == GenerationState.Done || _state == GenerationState.Error)
			{
				FinishGeneration();
				EditorUtility.ClearProgressBar();
			}
			else if (_state != GenerationState.Waiting)
			{
				EditorUtility.DisplayProgressBar("Generating Documentation", _message, _progress);
			}
		}

		void CreateElements()
		{
			rootVisualElement.Clear();

			var scrollContainer = new ScrollView(ScrollViewMode.Vertical);
			scrollContainer.style.flexGrow = 1;

			var loadContainer = new VisualElement();
			loadContainer.style.flexDirection = FlexDirection.Row;

			var newButton = new Button { text = "New" };
			newButton.clicked += () => _templateMenu.DropDown(newButton.worldBound);

			var loadButton = new Button(OpenSettings) { text = "Load" };

			loadContainer.Add(newButton);
			loadContainer.Add(loadButton);
			scrollContainer.Add(loadContainer);
			rootVisualElement.Add(scrollContainer);

			if (_generator)
			{
				var serializedObject = new SerializedObject(_generator);
				var outputDirectoryProperty = serializedObject.FindProperty(nameof(DocumentationGenerator.OutputDirectory));
				var categoriesProperty = serializedObject.FindProperty(nameof(DocumentationGenerator.Categories));
				var tableOfContentsProperty = serializedObject.FindProperty(nameof(DocumentationGenerator.TableOfContents));
				var logDescriptionsProperty = serializedObject.FindProperty(nameof(DocumentationGenerator.LogDescriptions));
				var helpUrlsProperty = serializedObject.FindProperty(nameof(DocumentationGenerator.HelpUrls));

				var generateContainer = new VisualElement();
				generateContainer.style.flexDirection = FlexDirection.Row;

				var generateButton = new Button(StartGeneration) { text = "Generate" };
				var saveButton = new Button(() => SaveGenerator(_generator, _settingsFilePreference.Value)) { text = "Save" };

				generateContainer.Add(generateButton);
				generateContainer.Add(saveButton);

				scrollContainer.Add(generateContainer);
				scrollContainer.Add(new PropertyField(outputDirectoryProperty));
				scrollContainer.Add(new PropertyField(categoriesProperty));
				scrollContainer.Add(new PropertyField(tableOfContentsProperty));
				scrollContainer.Add(new PropertyField(logDescriptionsProperty));
				scrollContainer.Add(new PropertyField(helpUrlsProperty));

				rootVisualElement.Bind(serializedObject);
			}
		}

		#region Generation

		private enum GenerationState
		{
			Waiting,
			Starting,
			Categories,
			TableOfContents,
			Log,
			Help,
			Templates,
			Done,
			Error
		}

		private void SetProgress(GenerationState state, float progress, string message)
		{
			_state = state;
			_progress = progress;
			_message = message;
		}

		private void StartGeneration()
		{
			if (_thread == null)
			{
				SetProgress(GenerationState.Starting, 0.0f, "Setting up generator");
				_thread = new Thread(GenerateAllThread);
				_thread.Start();
			}
		}

		private void FinishGeneration()
		{
			_thread.Join();
			_state = GenerationState.Waiting;
			_thread = null;
		}

		private void GenerateAllThread()
		{
			try
			{
				var steps = _generator.Categories.Count + 4.0f;
				var progress = 1.0f;

				foreach (var category in _generator.Categories)
				{
					SetProgress(GenerationState.Categories, progress / steps, string.Format("Generating {0} category", category.Name));
					category.Generate(_generator.Categories, _generator.OutputDirectory);
					progress += 1.0f;
				}

				SetProgress(GenerationState.TableOfContents, progress / steps, "Generating table of contents");
				_generator.TableOfContents.Generate(_applicationPath, _generator.Categories, _generator.OutputDirectory);
				progress += 1.0f;

				SetProgress(GenerationState.Log, progress / steps, "Generating log descriptions");
				_generator.LogDescriptions.Generate(_generator.OutputDirectory);
				progress += 1.0f;

				SetProgress(GenerationState.Help, progress / steps, "Validating help urls");
				_generator.HelpUrls.Validate();
				progress += 1.0f;

				SetProgress(GenerationState.Templates, progress / steps, "Creating processor templates");
				_generator.GenerateProcessorTemplates();
				progress += 1.0f;

				SetProgress(GenerationState.Done, 1.0f, "Generation complete");
			}
			catch (Exception exception)
			{
				Debug.LogException(exception);
				SetProgress(GenerationState.Error, 1.0f, "Generation error");
			}
		}

		#endregion

		#region File IO

		private void LoadTemplates()
		{
			_templates = new Dictionary<string, string>();
			_templateMenu = new GenericMenu();

			var folder = Path.Combine(_rootPath, _templatesFolder);
			var files = Directory.EnumerateFiles(folder, "*.json");

			foreach (var file in files)
			{
				try
				{
					var content = File.ReadAllText(file);
					var name = Path.GetFileNameWithoutExtension(file);

					_templates.Add(name, content);
					_templateMenu.AddItem(new GUIContent(name), false, CreateNewSettings, name);
				}
				catch (Exception exception)
				{
					Debug.LogException(exception);
				}
			}
		}

		private void CreateNewSettings(object templateName)
		{
			var path = EditorUtility.SaveFilePanel("Create Settings File", _rootPath, "generator", "json");

			if (!string.IsNullOrEmpty(path))
				CreateGenerator(path, templateName.ToString());
		}

		private void OpenSettings()
		{
			var path = EditorUtility.OpenFilePanel("Open Settings File", _rootPath, "json");

			if (!string.IsNullOrEmpty(path))
				LoadGenerator(path);
		}

		private void CreateGenerator(string path, string templateName)
		{
			var generator = CreateInstance<DocumentationGenerator>();

			if (_templates.TryGetValue(templateName, out string template))
				JsonUtility.FromJsonOverwrite(template, generator);

			if (SaveGenerator(generator, path))
				SetGenerator(generator, path);
			else
				DestroyImmediate(generator);
		}

		private void LoadGenerator(string path)
		{
			try
			{
				var content = File.ReadAllText(path);
				var generator = CreateInstance<DocumentationGenerator>();
				JsonUtility.FromJsonOverwrite(content, generator);
				SetGenerator(generator, path);
			}
			catch
			{
				SetGenerator(null, string.Empty);
			}
		}

		private void UnloadGenerator()
		{
			if (_generator != null)
			{
				DestroyImmediate(_generator);
				_generator = null;
			}
		}

		private void SetGenerator(DocumentationGenerator generator, string path)
		{
			UnloadGenerator();

			_settingsFilePreference.Value = path;
			_generator = generator;

			CreateElements();
		}

		private bool SaveGenerator(DocumentationGenerator generator, string path)
		{
			try
			{
				var content = JsonUtility.ToJson(generator, true);
				var outputFile = new FileInfo(path);

				Directory.CreateDirectory(outputFile.Directory.FullName);
				File.WriteAllText(outputFile.FullName, content);
				return true;
			}
			catch
			{
				return false;
			}
		}

		#endregion
	}
}
