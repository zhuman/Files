// Copyright (c) Files Community
// Licensed under the MIT License.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Storage.Streams;

namespace Files.App.UserControls
{
	public sealed partial class FileIcon : UserControl
	{
		private SelectedItemsPropertiesViewModel viewModel;

		public SelectedItemsPropertiesViewModel ViewModel
		{
			get => viewModel;
			set
			{
				viewModel = value;

				if (value is null)
				{
					return;
				}

				if (ViewModel?.CustomIconSource is not null)
				{
					CustomIconImageSource = new SvgImageSource(ViewModel.CustomIconSource);
				}
			}
		}

		private double itemSize;

		public double ItemSize
		{
			get => itemSize;
			set
			{
				itemSize = value;
				LargerItemSize = itemSize + 2.0;
			}
		}

		private double LargerItemSize { get; set; }

		private static DependencyProperty FileIconImageSourceProperty { get; } = DependencyProperty.Register(nameof(FileIconImageSource), typeof(ImageSource), typeof(FileIcon), null);

		private ImageSource FileIconImageSource
		{
			get => GetValue(FileIconImageSourceProperty) as ImageSource;
			set => SetValue(FileIconImageSourceProperty, value);
		}

		public static DependencyProperty FileIconImageDataProperty { get; } = DependencyProperty.Register(nameof(FileIconImageData), typeof(byte[]), typeof(FileIcon), null);

		public byte[] FileIconImageData
		{
			get => GetValue(FileIconImageDataProperty) as byte[];
			set
			{
				SetValue(FileIconImageDataProperty, value);
				if (value is not null)
				{
					UpdateImageSourceAsync();
				}
			}
		}

		public static DependencyProperty FileIconBitmapProperty { get; } = DependencyProperty.Register(nameof(FileIconBitmap), typeof(MaterializableBitmap), typeof(FileIcon), null);

		public MaterializableBitmap FileIconBitmap
		{
			get => GetValue(FileIconBitmapProperty) as MaterializableBitmap;
			set
			{
				SetValue(FileIconBitmapProperty, value);
				if (value is not null)
				{
					UpdateImageSourceAsync();
				}
			}
		}

		private SvgImageSource CustomIconImageSource { get; set; }

		public FileIcon()
		{
			InitializeComponent();
		}

		public async Task UpdateImageSourceAsync()
		{
			if (FileIconBitmap is not null)
			{
				FileIconImageSource = await FileIconBitmap.MaterializeOnUiThreadAsync();
			}
			if (FileIconImageData is not null)
			{
				var newBitmap = new BitmapImage();
				FileIconImageSource = FileIconImageSource;
				using InMemoryRandomAccessStream stream = new InMemoryRandomAccessStream();
				await stream.WriteAsync(FileIconImageData.AsBuffer());
				stream.Seek(0);
				await newBitmap.SetSourceAsync(stream);
			}
		}
	}
}