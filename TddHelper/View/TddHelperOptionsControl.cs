﻿using System;
using System.Windows.Forms;
using DreamWorks.TddHelper.Model;
using DreamWorks.TddHelper.Resources;
using DreamWorks.TddHelper.ViewModel;
using GalaSoft.MvvmLight.Messaging;
using MvvmFx.Windows.Data;
using Newtonsoft.Json;

namespace DreamWorks.TddHelper.View
{
	public partial class TddHelperOptionsControl : UserControl
	{
		private readonly BindingManager _bindingManager;
		private readonly OptionsViewModel _optionsViewModel;
		private static bool _bindingsAdded;

		public TddHelperOptionsControl()
		{
			InitializeComponent();
			_optionsViewModel = new OptionsViewModel();
			_bindingManager = new BindingManager();
		}

		public OptionsPageCustom OptionsPage { get; set; }

		public void OnLoad(object sender, EventArgs e)
		{
			if (!_bindingsAdded)
			{
				AddBindings();
				_bindingsAdded = true;
			}
			UpdateUI();
		}

		public OptionsViewModel OptionsViewModel
		{
			get { return _optionsViewModel; }
		}

		public void Save()
		{
			TddSettings.Default.Settings = JsonConvert.SerializeObject(_optionsViewModel);
			TddSettings.Default.Save();
			StaticOptions.MainOptions = _optionsViewModel;
		}

		private void UpdateUI()
		{
			// deserialization bypasses the property sets, thats why we have to update the UI
			if (!string.IsNullOrEmpty(TddSettings.Default.Settings))
			{
				var fromDisk =
					JsonConvert.DeserializeObject<OptionsViewModel>(TddSettings.Default.Settings);
				_optionsViewModel.Clone(fromDisk);
			}
			_optionsViewModel.UpdateUI();
		}

		private void AddBindings()
		{
			BindTextControls();
			BindRadioButtons();
			BindCheckboxes();
			ClearcacheButton.Click += ClearcacheButton_Click;
			clearProjectCacheButton.Click += ClearProjectcacheButton_Click;
		}

		private void ClearcacheButton_Click(object sender, EventArgs e)
		{
			var result = MessageBox.Show(OptionsPage,
				Strings.TddHelperOptionsControl_ConfirmClearCacheMessage,
				Strings.TddHelper_App_Name, MessageBoxButtons.YesNo);
			if (result == DialogResult.Yes)
				Messenger.Default.Send(new OptionsClearFileAssociationsCache());
		}

		private void ClearProjectcacheButton_Click(object sender, EventArgs e)
		{
			var result = MessageBox.Show(OptionsPage,
				Strings.TddHelperOptionsControl_ConfirmClearCacheProjectMessage,
				Strings.TddHelper_App_Name, MessageBoxButtons.YesNo);
			if (result == DialogResult.Yes)
				Messenger.Default.Send(new OptionsClearProjectAssociationsCache());
		}

		private void BindCheckboxes()
		{
			_bindingManager.Bindings.Add(
				new TypedBinding<CheckBox, OptionsViewModel>
					(AutoCreateFileCheckbox, c => c.Checked, _optionsViewModel,
						o => o.AutoCreateTestFile));
			_bindingManager.Bindings.Add(
				new TypedBinding<CheckBox, OptionsViewModel>
					(MirrorProjectFoldersCheckbox, c => c.Checked, _optionsViewModel,
						o => o.MirrorProjectFolders));
			_bindingManager.Bindings.Add(
				new TypedBinding<CheckBox, OptionsViewModel>
					(CreateReferenceCheckbox, c => c.Checked, _optionsViewModel,
						o => o.CreateReference));
			_bindingManager.Bindings.Add(
				new TypedBinding<CheckBox, OptionsViewModel>
					(MakeFriendAssemblyCheckbox, c => c.Checked, _optionsViewModel,
						o => o.MakeFriendAssembly));
			_bindingManager.Bindings.Add(
				new TypedBinding<CheckBox, OptionsViewModel>
					(CleanCheckbox, c => c.Checked, _optionsViewModel,
						o => o.Clean));
		}

		private void BindRadioButtons()
		{
			_bindingManager.Bindings.Add(
				new TypedBinding<RadioButton, OptionsViewModel>
					(UnitTestLeftRadio, r => r.Checked, _optionsViewModel,
						o => o.UnitTestLeft));
			_bindingManager.Bindings.Add(
				new TypedBinding<RadioButton, OptionsViewModel>
					(UnitTestRightRadio, r => r.Checked, _optionsViewModel,
						o => o.UnitTestRight));
			_bindingManager.Bindings.Add(
				new TypedBinding<RadioButton, OptionsViewModel>
					(NoSplitRadio, r => r.Checked, _optionsViewModel,
						o => o.NoSplit));
		}

		private void BindTextControls()
		{
			_bindingManager.Bindings.Add(
				new TypedBinding<TextBox, OptionsViewModel>
					(TestFileSuffixEdit, t => t.Text, _optionsViewModel,
						o => o.TestFileSuffix));
			_bindingManager.Bindings.Add(
				new TypedBinding<TextBox, OptionsViewModel>
					(ProjectSuffixEdit, t => t.Text, _optionsViewModel,
						o => o.ProjectSuffix));
		}
	}
}