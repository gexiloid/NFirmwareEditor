﻿using System.Collections.Generic;
using System.Xml.Serialization;

namespace NFirmwareEditor.Models
{
	public class Configuration
	{
		public Configuration()
		{
			MainWindowWidth = 800;
			MainWindowHeight = 600;
			GridSize = 16;
			ShowGid = true;
			ImageEditorMouseMode = ImageEditorMouseMode.LeftSetRightUnset;
			BackupCreationMode = BackupCreationMode.Extended;
			MostRecentlyUsed = new List<string>();
		}

		public int MainWindowWidth { get; set; }

		public int MainWindowHeight { get; set; }

		public bool MainWindowMaximaged { get; set; }

		public bool ShowGid { get; set; }

		public int GridSize { get; set; }

		public ImageEditorMouseMode ImageEditorMouseMode { get; set; }

		public BackupCreationMode BackupCreationMode { get; set; }

		[XmlArrayItem("Item")]
		public List<string> MostRecentlyUsed { get; set; }
	}

	public enum ImageEditorMouseMode
	{
		LeftSetRightUnset,
		LeftSetUnset
	}

	public enum BackupCreationMode
	{
		Disabled,
		Simple,
		Extended
	}
}
