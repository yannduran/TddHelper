﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using DreamWorks.TddHelper.Model;
using DreamWorks.TddHelper.Utility;
using DreamWorks.TddHelper.View;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell.Interop;
using NuGet.VisualStudio;

namespace DreamWorks.TddHelper.Implementation
{
	internal class TestLocator
	{
		private const string Document = "Document";
		private const string OpenFileCommand = "File.OpenFile";
		private const string NewVerticalTabGroupCommand = "Window.NewVerticalTabGroup";
		private const string WindowMoveToNextTabGroupCommand = "Window.MoveToNextTabGroup";
		private const string FileSaveAll = "File.SaveAll";
		private const string WindowCloseAllDocuments = "Window.CloseAllDocuments";
		private static readonly log4net.ILog Logger = log4net.LogManager.
			GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

		public TestLocator(DTE2 dte, IVsUIShell shell, IVsPackageInstaller packageInstaller, IProjectModel project)
		{
			Access.Dte = dte;
			Access.Shell = shell;
			Access.ProjectModel = project;
			Access.PackageInstaller = packageInstaller;
		}

		internal void OpenTestOrImplementation(object sender, EventArgs e)
		{
			Logger.Info("Executing: JumpToTestOrImplmentation");
			if (Access.Dte.ActiveWindow == null || Access.Dte.ActiveDocument == null ||
			    Access.Dte.ActiveWindow.Document == null)
			{
				Console.Beep();
				Logger.Warn("JumpToTestOrImplmentation: No Active Window - aborting");
				return;
			}

			Access.ProjectModel.UpdateSolutionId();
			SourceTargetInfo.Clear();
			SourceTargetInfo.SourcePath = Access.Dte.ActiveWindow.Document.FullName;
			if (!SourceTargetInfo.IsSourcePathCsFile)
			{
				Logger.Warn("JumpToTestOrImplmentation: Attemting to jump on non CSharp File - aborting");
				return;
			}

			Access.ProjectModel.GetCSharpFilesFromSolution();

			SourceTargetInfo.TargetPath = FindExistingFileWithConflictResolution();
			if (SourceTargetInfo.TargetPath == null) // dialog cancelled
			{
				Logger.Warn("JumpToTestOrImplmentation: User Cancelled Resolution Dialog - aborting");
				return;
			}
			if (SourceTargetInfo.TargetPath == string.Empty) // not found
			{
				if (!CreateClassHelper.TryToCreateNewTargetClass())
				{
					Logger.Warn("JumpToTestOrImplmentation: TryToCreateNewTargetClass returns false - aborting");
					return;
				}
			}
			Load();
		}

		private void Load()
		{
			if (string.IsNullOrEmpty(SourceTargetInfo.TargetPath))
			{
				Logger.Warn("JumpToTestOrImplmentation: No SourceTargetInfo.TargetPath, load cancelled");
				return;
			}

			SaveAndUnloadDocuments();

			if (StaticOptions.MainOptions.NoSplit)
				LoadDocumentsIntoOneTabWell();
			else
				LoadAndPlaceImplementationAndTest();
		}

		private void LoadAndPlaceImplementationAndTest()
		{
			// Make sure the active window is in the first tab well
			// as we will load our douments there
			ActivateFirstDocument();

			try
			{
				if (StaticOptions.MainOptions.UnitTestLeft ^ SourceTargetInfo.IsSourcePathTest)
				{
					Access.Dte.ExecuteCommand(OpenFileCommand, SourceTargetInfo.QuotedTargetPath);
					Access.Dte.ExecuteCommand(OpenFileCommand, SourceTargetInfo.QuotedSourcePath);
				}
				else
				{
					Access.Dte.ExecuteCommand(OpenFileCommand, SourceTargetInfo.QuotedSourcePath);
					Access.Dte.ExecuteCommand(OpenFileCommand, SourceTargetInfo.QuotedTargetPath);
				}

				if (ViewUtil.IsMoreThanOneTabWellShown())
				{
					Access.Dte.ExecuteCommand(WindowMoveToNextTabGroupCommand);
				}
				else
				{
					Access.Dte.ExecuteCommand(NewVerticalTabGroupCommand);
				}
			}
			catch (COMException e)
			{
				Logger.Warn("JumpToTestOrImplmentation: LoadAndPlaceImplementationAndTest(), got COM Exception");
				ExceptionLogHelper.LogException(e);
			}
			catch (Exception e)
			{
				Logger.Warn("JumpToTestOrImplmentation: LoadAndPlaceImplementationAndTest(), got other exception");
				ExceptionLogHelper.LogException(e);
			}
		}

		private void LoadDocumentsIntoOneTabWell()
		{
			Access.Dte.ExecuteCommand(OpenFileCommand, SourceTargetInfo.QuotedSourcePath);
			Access.Dte.ExecuteCommand(OpenFileCommand, SourceTargetInfo.QuotedTargetPath);
		}

		private void SaveAndUnloadDocuments()
		{
			var sourceDocument = GetDocumentForPath(SourceTargetInfo.SourcePath);
			var targetDocument = GetDocumentForPath(SourceTargetInfo.TargetPath);

			if (StaticOptions.MainOptions.Clean)
			{
				Access.Dte.ExecuteCommand(FileSaveAll);
				Access.Dte.ExecuteCommand(WindowCloseAllDocuments);
			}
			else
			{
				SaveAndCloseIfOpen(sourceDocument);
				SaveAndCloseIfOpen(targetDocument);
			}
		}

		private static void SaveAndCloseIfOpen(Document document)
		{
			if (document == null) 
				return;
			document.Save();
			document.Close();
		}

		private Document GetDocumentForPath(string targetPath)
		{
			foreach (Document document in Access.Dte.Documents)
			{
				if (string.Equals(document.FullName, targetPath,
					StringComparison.CurrentCultureIgnoreCase))
					return document;
			}
			return null;
		}

		internal bool ActivateFirstDocument()
		{
			foreach (Window window in Access.Dte.Windows)
			{
				// document in the first tab well has Left==32
				if (window.Kind == Document && window.Left == 32)
				{
					window.Activate();
					window.Document.Activate();
					return true;
				}
			}
			return false;
		}

		private string FindExistingFileWithConflictResolution()
		{
			var candidateList = new List<string>();
			foreach (var fullPathToFile in Access.ProjectModel.CsharpFilesInProject)
			{
				var fileName = Path.GetFileName(fullPathToFile);
				if (String.Equals(fileName, SourceTargetInfo.TargetFileName, StringComparison.OrdinalIgnoreCase))
				{
					if (File.Exists(fullPathToFile))
						candidateList.Add(fullPathToFile);
				}
			}
			if (candidateList.Count == 0)
				return string.Empty;
			if (candidateList.Count == 1)
				return candidateList[0];
			if (candidateList.Count > 1)
				return ResolveConflicts(candidateList);
			return string.Empty;
		}

		private string ResolveConflicts(IEnumerable<string> candidateList)
		{
			var correspondingFile = Access.ProjectModel.FindTargetFileInCache(SourceTargetInfo.SourcePath);

			if (!string.IsNullOrEmpty(correspondingFile))
				return correspondingFile;

			var resolveFileConflictDialog = new ResolveFileConflictDialog(candidateList);
			ViewUtil.SetModalDialogOwner(resolveFileConflictDialog);

			var dlgResult = resolveFileConflictDialog.ShowDialog();
			if (!dlgResult.HasValue || dlgResult != true)
				return null;

			var selectedFilePath = resolveFileConflictDialog.ViewModel.SelectedFile.Path;
			if (SourceTargetInfo.IsSourcePathTest)
				Access.ProjectModel.AddFileAssociationToCache(selectedFilePath, SourceTargetInfo.TargetPath);
			else
				Access.ProjectModel.AddFileAssociationToCache(SourceTargetInfo.SourcePath, selectedFilePath);

			return selectedFilePath;
		}
	}
}