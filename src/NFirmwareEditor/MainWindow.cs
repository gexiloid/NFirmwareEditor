﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using JetBrains.Annotations;
using NFirmware;
using NFirmwareEditor.Core;
using NFirmwareEditor.Firmware;
using NFirmwareEditor.Properties;

namespace NFirmwareEditor
{
	public partial class MainWindow : Form
	{
		private readonly FirmwareLoader m_loader = new FirmwareLoader(new FirmwareEncoder());

		private Configuration m_configuration;
		private NFirmware.Firmware m_firmware;

		public MainWindow()
		{
			InitializeComponent();
			InitializeControls();

			Icon = Paths.ApplicationIcon;
			LoadSettings();
		}

		[NotNull]
		public ListBox ImagesListBox
		{
			get { return Block1CheckBox.Checked ? Block1ImagesListBox : Block2ImagesListBox; }
		}

		[CanBeNull]
		public FirmwareDefinition SelectedDefinition
		{
			get { return DefinitionsComboBox.SelectedItem as FirmwareDefinition; }
		}

		private void InitializeControls()
		{
			PreviewPixelGrid.BlockInnerBorderPen = Pens.Transparent;
			PreviewPixelGrid.BlockOuterBorderPen = Pens.Transparent;
			PreviewPixelGrid.ActiveBlockBrush = Brushes.White;
			PreviewPixelGrid.InactiveBlockBrush = Brushes.Black;
		}

		private void ResetWorkspace()
		{
			Block1ImagesListBox.Items.Clear();
			Block2ImagesListBox.Items.Clear();
			ImagePixelGrid.Data = new bool[5, 5];
			StatusLabel.Text = null;
		}

		private void LoadSettings()
		{
			m_configuration = ConfigurationManager.Load();
			WindowState = m_configuration.MainWindowMaximaged ? FormWindowState.Maximized : FormWindowState.Normal;
			Width = m_configuration.MainWindowWidth;
			Height = m_configuration.MainWindowHeight;

			var definitions = FirmwareDefinitionManager.Load();
			foreach (var definition in definitions)
			{
				DefinitionsComboBox.Items.Add(definition);
			}
			if (definitions.Count > 0)
			{
				var savedDefinition = definitions.FirstOrDefault(x => x.Name.Equals(m_configuration.LastUsedDefinition));
				DefinitionsComboBox.SelectedItem = savedDefinition ?? definitions[0];
			}
		}

		private void OpenDialogAndReadFirmwareOnOk(Func<string, FirmwareDefinition, NFirmware.Firmware> readFirmwareDelegate)
		{
			if (SelectedDefinition == null)
			{
				InfoBox.Show("Select firmware definition first.");
				return;
			}

			string firmwareFile;
			using (var op = new OpenFileDialog { Filter = Consts.FirmwareFilter })
			{
				if (op.ShowDialog() != DialogResult.OK) return;
				firmwareFile = op.FileName;
			}

			ResetWorkspace();
			try
			{
				m_firmware = readFirmwareDelegate(firmwareFile, SelectedDefinition);

				FillImagesListBox(Block2ImagesListBox, m_firmware.Block2Images, true);
				FillImagesListBox(Block1ImagesListBox, m_firmware.Block1Images, true);

				SaveEncryptedMenuItem.Enabled = true;
				SaveDecryptedMenuItem.Enabled = true;
				EditMenuItem.Enabled = true;

				Text = string.Format("{0} - {1}", Consts.ApplicationTitle, firmwareFile);
				StatusLabel.Text = @"Firmware loaded successfully.";
			}
			catch (Exception ex)
			{
				InfoBox.Show("Unable to load firmware.\n{0}", ex.Message);
			}
		}

		private void OpenDialogAndSaveFirmwareOnOk(Action<string, NFirmware.Firmware> writeFirmwareDelegate)
		{
			if (m_firmware == null) return;

			string firmwareFile;
			using (var sf = new SaveFileDialog { Filter = Consts.FirmwareFilter })
			{
				if (sf.ShowDialog() != DialogResult.OK) return;
				firmwareFile = sf.FileName;
			}

			try
			{
				writeFirmwareDelegate(firmwareFile, m_firmware);
				StatusLabel.Text = @"Firmware successfully saved to the file: " + firmwareFile;
			}
			catch (Exception ex)
			{
				InfoBox.Show("Unable to save firmware.\n{0}", ex.Message);
			}
		}

		private void FillImagesListBox(ListBox listBox, IEnumerable<object> items, bool selectFirstItem)
		{
			listBox.Items.Clear();
			listBox.BeginUpdate();
			foreach (var item in items)
			{
				listBox.Items.Add(item);
			}
			listBox.EndUpdate();

			if (selectFirstItem && listBox.Items.Count > 0)
			{
				listBox.SelectedIndex = 0;
			}
		}

		private FirmwareImageMetadata GetSelectedImageMetadata(ListBox listBox)
		{
			return listBox == null || listBox.SelectedIndices.Count == 0
				? null
				: listBox.Items[listBox.SelectedIndices[listBox.SelectedIndices.Count - 1]] as FirmwareImageMetadata;
		}

		private List<FirmwareImageMetadata> GetSelectedImagesMetadata(ListBox listBox)
		{
			if (listBox == null || listBox.SelectedIndices.Count == 0) return new List<FirmwareImageMetadata>();

			var result = new List<FirmwareImageMetadata>();
			foreach (int selectedIndex in listBox.SelectedIndices)
			{
				var metadata = listBox.Items[selectedIndex] as FirmwareImageMetadata;
				if(metadata == null) continue;

				result.Add(metadata);
			}
			return result;
		}

		private bool[,] ProcessImage(Func<bool[,], bool[,]> imageDataProcessor, FirmwareImageMetadata imageMetadata)
		{
			var processedData = imageDataProcessor(ImagePixelGrid.Data);
			m_firmware.WriteImage(processedData, imageMetadata);
			return processedData;
		}

		private void MainWindow_FormClosing(object sender, FormClosingEventArgs e)
		{
			ConfigurationManager.Save(m_configuration);
		}

		private void MainWindow_SizeChanged(object sender, EventArgs e)
		{
			if (WindowState == FormWindowState.Maximized)
			{
				m_configuration.MainWindowMaximaged = true;
			}
			else if (WindowState == FormWindowState.Normal)
			{
				m_configuration.MainWindowMaximaged = false;
				m_configuration.MainWindowWidth = Width;
				m_configuration.MainWindowHeight = Height;
			}
		}

		private void DefinitionsComboBox_SelectedValueChanged(object sender, EventArgs e)
		{
			var definition = DefinitionsComboBox.SelectedItem as FirmwareDefinition;
			if (definition == null) return;

			m_configuration.LastUsedDefinition = definition.Name;
		}

		private void BlockCheckBox_CheckedChanged(object sender, EventArgs e)
		{
			if (sender == Block1CheckBox) Block2CheckBox.Checked = !Block1CheckBox.Checked;
			if (sender == Block2CheckBox) Block1CheckBox.Checked = !Block2CheckBox.Checked;

			Block1ImagesListBox.Visible = Block1CheckBox.Checked;
			Block2ImagesListBox.Visible = Block2CheckBox.Checked;

			ImagesListBox.Focus();
			BlockImagesListBox_SelectedValueChanged(ImagesListBox, EventArgs.Empty);
		}

		private void BlockImagesListBox_SelectedValueChanged(object sender, EventArgs e)
		{
			var listBox = sender as ListBox;
			if (listBox == null) return;

			var metadata = GetSelectedImageMetadata(listBox);
			if (metadata == null) return;

			StatusLabel.Text = string.Format("Image: {0}x{1}", metadata.Width, metadata.Height);
			try
			{
				ImagePixelGrid.Data = PreviewPixelGrid.Data = m_firmware.ReadImage(metadata);
			}
			catch (Exception)
			{
				InfoBox.Show("Invalid image data. Possibly firmware definition is incompatible with loaded firmware.");
			}
		}

		private void GridSizeUpDown_ValueChanged(object sender, EventArgs e)
		{
			ImagePixelGrid.BlockSize = (int)GridSizeUpDown.Value;
		}

		private void ShowGridCheckBox_CheckedChanged(object sender, EventArgs e)
		{
			ImagePixelGrid.ShowGrid = ShowGridCheckBox.Checked;
		}

		private void ImagePixelGrid_DataUpdated(bool[,] data)
		{
			var metadata = ImagesListBox.SelectedItem as FirmwareImageMetadata;
			if (metadata == null) return;

			m_firmware.WriteImage(data, metadata);
			PreviewPixelGrid.Data = data;
		}

		private void ClearAllPixelsButton_Click(object sender, EventArgs e)
		{
			var metadata = ImagesListBox.SelectedItem as FirmwareImageMetadata;
			if (metadata == null) return;

			ImagePixelGrid.Data = PreviewPixelGrid.Data = ProcessImage(FirmwareImageProcessor.Clear, metadata);
		}

		private void InvertButton_Click(object sender, EventArgs e)
		{
			var metadata = ImagesListBox.SelectedItem as FirmwareImageMetadata;
			if (metadata == null) return;

			ImagePixelGrid.Data = PreviewPixelGrid.Data = ProcessImage(FirmwareImageProcessor.Invert, metadata);
		}

		private void ShiftLeftButton_Click(object sender, EventArgs e)
		{
			var metadata = ImagesListBox.SelectedItem as FirmwareImageMetadata;
			if (metadata == null) return;

			ImagePixelGrid.Data = PreviewPixelGrid.Data = ProcessImage(FirmwareImageProcessor.ShiftLeft, metadata);
		}

		private void ShiftRightButton_Click(object sender, EventArgs e)
		{
			var metadata = ImagesListBox.SelectedItem as FirmwareImageMetadata;
			if (metadata == null) return;

			ImagePixelGrid.Data = PreviewPixelGrid.Data = ProcessImage(FirmwareImageProcessor.ShiftRight, metadata);
		}

		private void ShiftUpButton_Click(object sender, EventArgs e)
		{
			var metadata = ImagesListBox.SelectedItem as FirmwareImageMetadata;
			if (metadata == null) return;

			ImagePixelGrid.Data = PreviewPixelGrid.Data = ProcessImage(FirmwareImageProcessor.ShiftUp, metadata);
		}

		private void ShiftDownButton_Click(object sender, EventArgs e)
		{
			var metadata = ImagesListBox.SelectedItem as FirmwareImageMetadata;
			if (metadata == null) return;

			ImagePixelGrid.Data = PreviewPixelGrid.Data = ProcessImage(FirmwareImageProcessor.ShiftDown, metadata);
		}

		private void CopyButton_Click(object sender, EventArgs e)
		{
			Clipboard.SetDataObject(ImagePixelGrid.Data);
		}

		private void PasteButton_Click(object sender, EventArgs e)
		{
			var metadata = ImagesListBox.SelectedItem as FirmwareImageMetadata;
			if (metadata == null) return;

			var dataObject = Clipboard.GetDataObject();
			if (dataObject == null) return;

			var buffer = dataObject.GetData(typeof(bool[,])) as bool[,];
			if (buffer == null) return;

			ImagePixelGrid.Data = PreviewPixelGrid.Data = ProcessImage(data => FirmwareImageProcessor.PasteImage(data, buffer), metadata);
		}

		private void OpenEncryptedMenuItem_Click(object sender, EventArgs e)
		{
			OpenDialogAndReadFirmwareOnOk((fileName, definition) => m_loader.LoadEncrypted(fileName, definition));
		}

		private void OpenDecryptedMenuItem_Click(object sender, EventArgs e)
		{
			OpenDialogAndReadFirmwareOnOk((fileName, definition) => m_loader.LoadDecrypted(fileName, definition));
		}

		private void SaveEncryptedMenuItem_Click(object sender, EventArgs e)
		{
			OpenDialogAndSaveFirmwareOnOk((filePath, firmware) => m_loader.SaveEncrypted(filePath, firmware));
		}

		private void SaveDecryptedMenuItem_Click(object sender, EventArgs e)
		{
			OpenDialogAndSaveFirmwareOnOk((filePath, firmware) => m_loader.SaveDecrypted(filePath, firmware));
		}

		private void ExitMenuItem_Click(object sender, EventArgs e)
		{
			Application.Exit();
		}

		private void ClearAllPixelsMenuItem_Click(object sender, EventArgs e)
		{
			ClearAllPixelsButton_Click(null, null);
		}

		private void InvertMenuItem_Click(object sender, EventArgs e)
		{
			InvertButton_Click(null, null);
		}

		private void CopyMenuItem_Click(object sender, EventArgs e)
		{
			CopyButton_Click(null, null);
		}

		private void PasteMenuItem_Click(object sender, EventArgs e)
		{
			PasteButton_Click(null, null);
		}

		private void ShiftUpMenuItem_Click(object sender, EventArgs e)
		{
			ShiftUpButton_Click(null, null);
		}

		private void ShiftDownMenuItem_Click(object sender, EventArgs e)
		{
			ShiftDownButton_Click(null, null);
		}

		private void ShiftLeftMenuItem_Click(object sender, EventArgs e)
		{
			ShiftLeftButton_Click(null, null);
		}

		private void ShiftRightMenuItem_Click(object sender, EventArgs e)
		{
			ShiftRightButton_Click(null, null);
		}

		private void EncryptDecryptToolStripMenuItem_Click(object sender, EventArgs e)
		{
			using (var decryptionWindow = new DecryptionWindow())
			{
				decryptionWindow.ShowDialog();
			}
		}

		private void AboutMenuItem_Click(object sender, EventArgs e)
		{
			InfoBox.Show(Resources.AboutMessage, Consts.ApplicationVersion);
		}

		protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
		{
			if (!keyData.HasFlag(Keys.Control)) return base.ProcessCmdKey(ref msg, keyData);

			if (keyData.HasFlag(Keys.O))
			{
				OpenEncryptedMenuItem.PerformClick();
				return true;
			}
			if (keyData.HasFlag(Keys.E))
			{
				OpenDecryptedMenuItem.PerformClick();
				return true;
			}
			if (keyData.HasFlag(Keys.Shift) && keyData.HasFlag(Keys.S))
			{
				SaveDecryptedMenuItem.PerformClick();
				return true;
			}
			if (keyData.HasFlag(Keys.S))
			{
				SaveEncryptedMenuItem.PerformClick();
				return true;
			}

			if (keyData.HasFlag(Keys.N))
			{
				ClearAllPixelsMenuItem.PerformClick();
				return true;
			}
			if (keyData.HasFlag(Keys.I))
			{
				InvertMenuItem.PerformClick();
				return true;
			}
			if (keyData.HasFlag(Keys.C))
			{
				CopyMenuItem.PerformClick();
				return true;
			}
			if (keyData.HasFlag(Keys.V))
			{
				PasteMenuItem.PerformClick();
				return true;
			}

			var key = keyData &= ~Keys.Control;
			if (key == Keys.Up)
			{
				ShiftUpMenuItem.PerformClick();
				return true;
			}
			if (key == Keys.Down)
			{
				ShiftDownMenuItem.PerformClick();
				return true;
			}
			if (key == Keys.Left)
			{
				ShiftLeftMenuItem.PerformClick();
				return true;
			}
			if (key == Keys.Right)
			{
				ShiftRightMenuItem.PerformClick();
				return true;
			}

			return base.ProcessCmdKey(ref msg, keyData);
		}

		private void ExportContextMenuItem_Click(object sender, EventArgs e)
		{
			var selectedItems = GetSelectedImagesMetadata(ImagesListBox);
			if (selectedItems.Count == 0) return;

			string fileName;
			using (var sf = new SaveFileDialog { Filter = Consts.ExportImageFilter })
			{
				if (sf.ShowDialog() != DialogResult.OK) return;
				fileName = sf.FileName;
			}

			var images = selectedItems.Select(x =>
			{
				var imageData = m_firmware.ReadImage(x);
				return new ExportedImage(x.Index, imageData);
			}).ToList();
			ImageExporter.Export(fileName, images);
		}

		private void ImportContextMenuItem_Click(object sender, EventArgs e)
		{
			var selectedItems = GetSelectedImagesMetadata(ImagesListBox);
			if (selectedItems.Count == 0) return;

			string fileName;
			using (var op = new OpenFileDialog { Filter = Consts.ExportImageFilter })
			{
				if (op.ShowDialog() != DialogResult.OK) return;
				fileName = op.FileName;
			}

			var exportedImages = ImageExporter.Import(fileName);
			if (exportedImages.Count == 0) return;

			var importedImages = exportedImages.Select(x => x.Data).ToList();
			var originalImages = m_firmware.ReadImages(selectedItems).ToList();

			var minimumImagesCount = Math.Min(originalImages.Count, importedImages.Count);
			using (var importWindow = new ImportImageWindow(originalImages.Take(minimumImagesCount), importedImages.Take(minimumImagesCount)))
			{
				if (importWindow.ShowDialog() != DialogResult.OK) return;
			}

			for (var i = 0; i < minimumImagesCount; i++)
			{
				var index = i;
				ProcessImage(x => FirmwareImageProcessor.PasteImage(originalImages[index], importedImages[index]), selectedItems[index]);
			}

			var lastSelectedItem = GetSelectedImageMetadata(ImagesListBox);
			ImagesListBox.SelectedIndices.Clear();
			ImagesListBox.SelectedItem = lastSelectedItem;
		}
	}
}
