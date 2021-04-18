using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace DaxStudio.Controls.DataGridFilter.Themes
{
    public static class ResourceKeys
    {
		#region Accent Keys
		/// <summary>
		/// Accent Color Key - This Color key is used to accent elements in the UI
		/// (e.g.: Color of Activated Normal Window Frame, ResizeGrip, Focus or MouseOver input elements)
		/// </summary>
		public static readonly ComponentResourceKey ControlAccentColorKey = new ComponentResourceKey(typeof(ResourceKeys), "ControlAccentColorKey");

		/// <summary>
		/// Accent Brush Key - This Brush key is used to accent elements in the UI
		/// (e.g.: Color of Activated Normal Window Frame, ResizeGrip, Focus or MouseOver input elements)
		/// </summary>
		public static readonly ComponentResourceKey ControlAccentBrushKey = new ComponentResourceKey(typeof(ResourceKeys), "ControlAccentBrushKey");
		#endregion Accent Keys

		#region TextEditor BrushKeys
		public static readonly ComponentResourceKey DataGridHeaderBackground = new ComponentResourceKey(typeof(ResourceKeys), "DataGridHeaderBackground");
		public static readonly ComponentResourceKey DataGridHeaderForeground = new ComponentResourceKey(typeof(ResourceKeys), "DataGridHeaderForeground");
		#endregion
	}
}
