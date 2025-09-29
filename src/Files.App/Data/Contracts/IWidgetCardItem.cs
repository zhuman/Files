// Copyright (c) Files Community
// Licensed under the MIT License.

using Microsoft.UI.Xaml.Media;

namespace Files.App.Data.Contracts
{
	public interface IWidgetCardItem<T>
	{
		T Item { get; }

		ImageSource Thumbnail { get; }

		Task LoadCardThumbnailAsync();
	}
}
