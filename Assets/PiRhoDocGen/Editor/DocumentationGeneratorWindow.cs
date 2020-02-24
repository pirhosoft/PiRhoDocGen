using PiRhoSoft.Utilities.Editor;
using System;
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
		private DocumentationGenerator _generator;
		private GenerationState _state = GenerationState.Waiting;
		private string _message = string.Empty;
		private float _progress = 0.0f;
		private Thread _thread = null;
		private string _applicationPath;

		[MenuItem("Window/PiRho DocGen/Documentation Generator")]
		public static void Open()
		{
			GetWindow<DocumentationGeneratorWindow>("Documentation Generator").Show();
		}

		void OnEnable()
		{
			_applicationPath = Application.dataPath;
			_state = GenerationState.Waiting;

			LoadGenerator();
			CreateElements();
		}

		void OnDisable()
		{
			SaveGenerator();
			UnloadGenerator();
		}

		void OnInspectorUpdate()
		{
			if (_state != GenerationState.Waiting)
				Repaint();
		}

		void CreateElements()
		{
			var serializedObject = new SerializedObject(_generator);

			var generateButton = new Button(StartGeneration) { text = "Generate" };
			var outputDirectoryProperty = serializedObject.FindProperty(nameof(DocumentationGenerator.OutputDirectory));
			var categoriesProperty = serializedObject.FindProperty(nameof(DocumentationGenerator.Categories));
			var tableOfContentsProperty = serializedObject.FindProperty(nameof(DocumentationGenerator.TableOfContents));
			var logDescriptionsProperty = serializedObject.FindProperty(nameof(DocumentationGenerator.LogDescriptions));
			var helpUrlsProperty = serializedObject.FindProperty(nameof(DocumentationGenerator.HelpUrls));

			var scrollContainer = new ScrollView(ScrollViewMode.Vertical);

			scrollContainer.Add(generateButton);
			scrollContainer.Add(new PropertyField(outputDirectoryProperty));
			scrollContainer.Add(new PropertyField(categoriesProperty));
			scrollContainer.Add(new PropertyField(tableOfContentsProperty));
			scrollContainer.Add(new PropertyField(logDescriptionsProperty));
			scrollContainer.Add(new PropertyField(helpUrlsProperty));

			rootVisualElement.Add(scrollContainer);
			rootVisualElement.Bind(serializedObject);
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

		#region Generation

		private enum GenerationState
		{
			Waiting,
			Starting,
			Categories,
			TableOfContents,
			Log,
			Help,
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
				var steps = _generator.Categories.Count + 3.0f;
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

		private void LoadGenerator()
		{
			var path = GetSettingsPath();
			var content = File.ReadAllText(path);

			_generator = CreateInstance<DocumentationGenerator>();

			JsonUtility.FromJsonOverwrite(content, _generator);
		}

		private void UnloadGenerator()
		{
			DestroyImmediate(_generator);
			_generator = null;
		}

		private void SaveGenerator()
		{
			var path = GetSettingsPath();
			var content = JsonUtility.ToJson(_generator, true);
			var outputFile = new FileInfo(path);

			Directory.CreateDirectory(outputFile.Directory.FullName);
			File.WriteAllText(outputFile.FullName, content);
		}

		private string GetSettingsPath()
		{
			return AssetHelper.GetScriptPath() + "Settings.json";
		}

		#endregion
	}
}
