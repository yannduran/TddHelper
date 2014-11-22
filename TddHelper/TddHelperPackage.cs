﻿// Copyright 2014 Friedrich Brunzema [brunzefb AT gmail DOT com]
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//    http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using EnvDTE;
using EnvDTE80;

using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;


namespace DreamWorks.TddHelper
{
	[PackageRegistration(UseManagedResourcesOnly = true)]
	[InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)]
	[ProvideMenuResource("Menus.ctmenu", 1)]
	[Guid(GuidList.guidTddHelperPkgString)]
	public sealed class TddHelperPackage : Package
	{
		private const string Document = "Document";
		private const string OpenFileCommand = "File.OpenFile";
		private const string CsharpFileExtension = ".cs";
		private const string TestDotCs = "test.cs";

		public TddHelperPackage()
		{
		}

		protected override void Initialize()
		{
			base.Initialize();

			var menuCommandService = GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
			if (null == menuCommandService)
				return;

			var cmdIdJumpRight = new CommandID(GuidList.guidTddHelperCmdSet, (int)PkgCmdIDList.cmdIdJumpRight);
			var menuItemJumpRight= new MenuCommand(JumpRight, cmdIdJumpRight);
			menuCommandService.AddCommand(menuItemJumpRight);

			var cmdIdJumpLeft = new CommandID(GuidList.guidTddHelperCmdSet, (int)PkgCmdIDList.cmdIdJumpLeft);
			var menuItemJumpLeft = new MenuCommand(JumpLeft, cmdIdJumpLeft);
			menuCommandService.AddCommand(menuItemJumpLeft);

			var cmdIdLocateTest = new CommandID(GuidList.guidTddHelperCmdSet, (int)PkgCmdIDList.cmdIdLocateTest);
			var menuItemLocateTest = new MenuCommand(OpenTestOrImplementation, cmdIdLocateTest);
			menuCommandService.AddCommand(menuItemLocateTest);
			
		}
		
		private void OpenTestOrImplementation(object sender, EventArgs e)
		{
			var dte = (DTE2)GetService(typeof(DTE));
			if (dte.ActiveWindow == null || dte.ActiveDocument == null || dte.ActiveWindow.Document == null)
				return;

			var fullName = dte.ActiveWindow.Document.FullName;
			var isTest = fullName.ToLower().EndsWith(TestDotCs);
			var isCs = fullName.ToLower().EndsWith(CsharpFileExtension);

			if (!isCs)
				return;

			var helper = new SolutionHelper(dte);
			helper.GetCSharpFilesFromSolution();

			var fileName = Path.GetFileName(fullName);
			string targetToActivate;
			if (!isTest)
				targetToActivate = helper.FindPathToTestFile(fileName);
			else
				targetToActivate = helper.FindPathImplementationFile(fileName);

			if (!dte.IsOpenFile[EnvDTE.Constants.vsViewKindTextView, targetToActivate])
				dte.ExecuteCommand(OpenFileCommand, targetToActivate);
			
		}

		

		
		
		private void JumpRight(object sender, EventArgs e)
		{
			ExecuteJump(true);
		}

		private void JumpLeft(object sender, EventArgs e)
		{
			ExecuteJump(false);
		}

		private void ExecuteJump(bool jumpRight)
		{
			var dte = (DTE2)GetService(typeof(DTE));
			var topLevelWindows = GetSortedTopLevelWindows(dte);
			if (topLevelWindows.Count < 2)
				return;
			var activeIndex = FindActiveWindowIndex(topLevelWindows, dte);
			if (jumpRight) 
				activeIndex--; 
			else 
				activeIndex++;
			activeIndex = (activeIndex < 0 ? activeIndex + topLevelWindows.Count : activeIndex) % topLevelWindows.Count;
			topLevelWindows[activeIndex].Activate();
		}

		private static int FindActiveWindowIndex(IReadOnlyList<Window> topLevelWindows, DTE2 dte)
		{
			for (var i = 0; i < topLevelWindows.Count; ++i)
			{
				if (topLevelWindows[i].Document != dte.ActiveDocument)
					continue;
				return i;
			}
			return 0;
		}

		private static List<Window> GetSortedTopLevelWindows(DTE2 dte)
		{
			// Documents with a "left" or "top" value > 0 are the focused ones in each group, 
			// so we only need to collect those
			var topLevelWindows = new List<Window>();
			foreach (Window window in dte.Windows)
			{
				
				if (window.Kind == Document && (window.Left > 0 || window.Top > 0))
					topLevelWindows.Add(window);
			}
			topLevelWindows.Sort((a, b) => a.Left < b.Left ? -1 : 1);
			return topLevelWindows;
		}

		private void VsShowMessageBox(string message)
		{
			var uiShell = (IVsUIShell)GetService(typeof(SVsUIShell));
			var clsid = Guid.Empty;
			int result;
			ErrorHandler.ThrowOnFailure(uiShell.ShowMessageBox(
				0,
				ref clsid,
				Resources.AppTitle,
				message,
				string.Empty,
				0,
				OLEMSGBUTTON.OLEMSGBUTTON_OK,
				OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST,
				OLEMSGICON.OLEMSGICON_INFO,
				0,
				out result));
		}
	}

}