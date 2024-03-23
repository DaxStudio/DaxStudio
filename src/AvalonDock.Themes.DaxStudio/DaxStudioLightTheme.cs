﻿/************************************************************************
   AvalonDock

   Copyright (C) 2007-2013 Xceed Software Inc.

   This program is provided to you under the terms of the Microsoft Public
   License (Ms-PL) as published at https://opensource.org/licenses/MS-PL
 ************************************************************************/

using System;

namespace AvalonDock.Themes
{
	/// <inheritdoc/>
	public class DaxStudioLightTheme : Theme
	{
		/// <inheritdoc/>
		public override Uri GetResourceUri()
		{
			return new Uri(
				"/DaxStudio.AvalonDock.Themes;component/LightTheme.xaml",
				UriKind.Relative);
		}
	}
}
