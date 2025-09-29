// Copyright (c) Files Community
// Licensed under the MIT License.

using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation.Metadata;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using WinRT;
using static Vanara.PInvoke.ComCtl32;

namespace Files.App.Helpers
{
	internal static class BitmapHelper
	{
		public static async Task<BitmapImage?> ToBitmapAsync(this byte[]? data, int decodeSize = -1)
		{
			if (data is null)
			{
				return null;
			}

			try
			{
				using var ms = new MemoryStream(data);
				var image = new BitmapImage();
				if (decodeSize > 0)
				{
					image.DecodePixelWidth = decodeSize;
					image.DecodePixelHeight = decodeSize;
				}
				image.DecodePixelType = DecodePixelType.Logical;
				await image.SetSourceAsync(ms.AsRandomAccessStream());
				return image;
			}
			catch (Exception)
			{
				return null;
			}
		}

		public static async Task<ImageSource?> ToBitmapAsync(this MaterializableBitmap? data, int decodedSize = -1)
		{
			if (data == null)
				return null;

			return await data.MaterializeOnUiThreadAsync(decodedSize);
		}

		/// <summary>
		/// Rotates the image at the specified file path.
		/// </summary>
		/// <param name="filePath">The file path to the image.</param>
		/// <param name="rotation">The rotation direction.</param>
		/// <remarks>
		/// https://learn.microsoft.com/uwp/api/windows.graphics.imaging.bitmapdecoder?view=winrt-22000
		/// https://learn.microsoft.com/uwp/api/windows.graphics.imaging.bitmapencoder?view=winrt-22000
		/// </remarks>
		public static async Task RotateAsync(string filePath, BitmapRotation rotation)
		{
			try
			{
				if (string.IsNullOrEmpty(filePath))
				{
					return;
				}

				var file = await StorageHelpers.ToStorageItem<IStorageFile>(filePath);
				if (file is null)
				{
					return;
				}

				var fileStreamRes = await FilesystemTasks.Wrap(() => file.OpenAsync(FileAccessMode.ReadWrite).AsTask());
				using IRandomAccessStream fileStream = fileStreamRes.Result;
				if (fileStream is null)
				{
					return;
				}

				BitmapDecoder decoder = await BitmapDecoder.CreateAsync(fileStream);
				using var memStream = new InMemoryRandomAccessStream();
				BitmapEncoder encoder = await BitmapEncoder.CreateForTranscodingAsync(memStream, decoder);

				for (int i = 0; i < decoder.FrameCount - 1; i++)
				{
					encoder.BitmapTransform.Rotation = rotation;
					await encoder.GoToNextFrameAsync();
				}

				encoder.BitmapTransform.Rotation = rotation;

				await encoder.FlushAsync();

				memStream.Seek(0);
				fileStream.Seek(0);
				fileStream.Size = 0;

				await RandomAccessStream.CopyAsync(memStream, fileStream);
			}
			catch (Exception ex)
			{
				var errorDialog = new ContentDialog()
				{
					Title = Strings.FailedToRotateImage.GetLocalizedResource(),
					Content = ex.Message,
					PrimaryButtonText = Strings.OK.GetLocalizedResource(),
				};

				if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 8))
					errorDialog.XamlRoot = MainWindow.Instance.Content.XamlRoot;

				await errorDialog.TryShowAsync();
			}
		}

		/// <summary>
		/// This function encodes a software bitmap with the specified encoder and saves it to a file
		/// </summary>
		/// <param name="softwareBitmap"></param>
		/// <param name="outputFile"></param>
		/// <param name="encoderId">The guid of the image encoder type</param>
		/// <returns></returns>
		public static async Task SaveSoftwareBitmapToFileAsync(SoftwareBitmap softwareBitmap, BaseStorageFile outputFile, Guid encoderId)
		{
			using IRandomAccessStream stream = await outputFile.OpenAsync(FileAccessMode.ReadWrite);
			// Create an encoder with the desired format
			BitmapEncoder encoder = await BitmapEncoder.CreateAsync(encoderId, stream);

			// Set the software bitmap
			encoder.SetSoftwareBitmap(softwareBitmap);

			try
			{
				await encoder.FlushAsync();
			}
			catch (Exception err)
			{
				const int WINCODEC_ERR_UNSUPPORTEDOPERATION = unchecked((int)0x88982F81);
				switch (err.HResult)
				{
					case WINCODEC_ERR_UNSUPPORTEDOPERATION:
						// If the encoder does not support writing a thumbnail, then try again
						// but disable thumbnail generation.
						encoder.IsThumbnailGenerated = false;
						break;

					default:
						throw;
				}
			}

			if (encoder.IsThumbnailGenerated == false)
			{
				await encoder.FlushAsync();
			}
		}
	}

	/// <summary>
	/// Implements a store of bitmap data that can be materialized into an ImageSource when needed. Image data can come
	/// from a variety of sources and conversion to XAML objects is optimized for each case.
	/// </summary>
	public class MaterializableBitmap
	{
		ImageSource _materializedSource;

		byte[] _fileData;
		System.Drawing.Bitmap _bitmap;
		Rectangle? _bitmapRect;

		private MaterializableBitmap()
		{ }

		public static MaterializableBitmap CreateFromFileBytes(byte[] fileData)
		{
			return new MaterializableBitmap() { _fileData = fileData };
		}

		public static MaterializableBitmap CreateFromBitmap(System.Drawing.Bitmap bitmap)
		{
			return new MaterializableBitmap() { _bitmap = bitmap };
		}

		public static MaterializableBitmap CreateFromNativeIcon(System.Drawing.Icon icon)
		{
			return CreateFromBitmap(icon.ToBitmap());
		}

		public static MaterializableBitmap CreateFromImageList(Vanara.PInvoke.ComCtl32.IImageList imageList, int imageIndex)
		{
			IMAGEINFO imageInfo = default;
			try
			{
				imageInfo = imageList.GetImageInfo(imageIndex);
			}
			catch { } // GetImageInfo can be unsupported, maybe if the icon is PNG

			if (imageInfo.hbmMask != IntPtr.Zero || imageInfo.hbmImage == IntPtr.Zero)
			{
				// We won't handle masks ourselves, so fallback to using an icon
				using var safeIcon = imageList.GetIcon(imageIndex, Vanara.PInvoke.ComCtl32.IMAGELISTDRAWFLAGS.ILD_TRANSPARENT);
				return CreateFromNativeIcon(Icon.FromHandle(safeIcon.DangerousGetHandle()));
			}

			// We can directly access the bitmap data efficiently from the hbmImage
			var bmp = System.Drawing.Image.FromHbitmap((nint)imageInfo.hbmImage);
			return new MaterializableBitmap() {  _bitmap = bmp, _bitmapRect = imageInfo.rcImage };
		}

		public async Task<ImageSource?> MaterializeOnUiThreadAsync(int decodeSize = -1)
		{
			if (_materializedSource != null)
				return _materializedSource;

			// If we only have a file stream, then we can load the bitmap from a byte stream
			if (_fileData != null)
			{
				_materializedSource = await MaterializeBitmapImageFromBytes(decodeSize);
				return _materializedSource;
			}

			// If we have raw pixel data then the most optimal ImageSource is a SurfaceImageSource

			if (_bitmap != null)
			{
				// Use bitmap data to materialize
				if (_bitmap.LockBits(_bitmapRect ?? new Rectangle(0, 0, _bitmap.Width, _bitmap.Height), System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppPArgb) is System.Drawing.Imaging.BitmapData data)
				{
					try
					{
						_materializedSource = MaterializeSurfaceFromBitmapData(data, decodeSize);
						return _materializedSource;
					}
					finally
					{
						_bitmap.UnlockBits(data);
					}
				}
			}

			return null;
		}

		private async Task<BitmapImage?> MaterializeBitmapImageFromBytes(int decodeSize = -1)
		{
			if (_fileData is null)
			{
				return null;
			}

			try
			{
				using var ms = new MemoryStream(_fileData);
				var image = new BitmapImage();
				if (decodeSize > 0)
				{
					image.DecodePixelWidth = decodeSize;
					image.DecodePixelHeight = decodeSize;
				}
				image.DecodePixelType = DecodePixelType.Logical;
				await image.SetSourceAsync(ms.AsRandomAccessStream());
				return image;
			}
			catch (Exception)
			{
				return null;
			}

		}

		private SurfaceImageSource MaterializeSurfaceFromBitmapData(System.Drawing.Imaging.BitmapData data, int decodeSize = -1)
		{
			var device = CanvasDevice.GetSharedDevice();

			var newSource = new CanvasImageSource(device, data.Width, data.Height, 96);

			// Upload the raw pixel data to the GPU
			CanvasBitmap bitmap;
			unsafe
			{
				CanvasAlphaMode alphaMode = (data.PixelFormat == System.Drawing.Imaging.PixelFormat.Format32bppPArgb || Win32Helper.IsAlphaBitmap(data)) ? CanvasAlphaMode.Premultiplied : CanvasAlphaMode.Ignore;

				if (data.Stride > 0)
				{
					bitmap = CanvasBitmap.CreateFromBytes(device, new UnsafePointerBuffer((void*)data.Scan0, (uint)(data.Stride * data.Height)).As<IBuffer>(), data.Width, data.Height, Windows.Graphics.DirectX.DirectXPixelFormat.B8G8R8A8UIntNormalized, newSource.Dpi, alphaMode);
				}
				else
				{
					// If the stride is negative, we need to copy the data to a new buffer with a positive stride
					var positiveStride = -data.Stride;
					var buffer = new byte[positiveStride * data.Height];
					for (int y = 0; y < data.Height; y++)
					{
						Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), buffer, y * positiveStride, positiveStride);
					}

					bitmap = CanvasBitmap.CreateFromBytes(device, buffer.AsBuffer(), data.Width, data.Height, Windows.Graphics.DirectX.DirectXPixelFormat.R8G8B8A8UInt, newSource.Dpi, alphaMode);
				}
			}

			// Draw the image into the SurfaceImageSource on the GPU
			using (var session = newSource.CreateDrawingSession(Windows.UI.Color.FromArgb(0, 0, 0, 0)))
			{
				session.DrawImage(bitmap);
			}

			return newSource;
		}

	}

	[System.Runtime.InteropServices.Guid("905a0fef-bc53-11df-8c49-001e4fc686da"), ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IBufferByteAccessInternal
	{
		IntPtr Buffer { get; }
	}

	/// <summary>
	/// Wraps an existing (unsafe) raw pointer in a WinRT IBuffer. This is useful when working with Win32 APIs that
	/// return raw pointers and sizes. However note that the lifetime of the underlying data needs to be ensured by the
	/// creating code, as this object does not understand the pointer's backing store lifetime.
	/// </summary>
	public unsafe sealed class UnsafePointerBuffer : IBuffer, IBufferByteAccessInternal, ICustomQueryInterface
	{
		void* _pointer;
		uint _capacity;

		public unsafe UnsafePointerBuffer(void* pointer, uint capacity)
		{
			_pointer = pointer;
			_capacity = capacity;
		}

		public uint Capacity => _capacity;

		public uint Length { get => _capacity; set => throw new NotImplementedException(); }

		public IntPtr Buffer { get => (IntPtr)_pointer; }

		public void GetIids(out int iidCount, out nint iids)
		{
			throw new NotImplementedException();
		}

		public CustomQueryInterfaceResult GetInterface(ref Guid iid, out nint ppv)
		{
			if (iid.Equals(typeof(IBufferByteAccessInternal).GUID))
			{
				ppv = Marshal.GetComInterfaceForObject(this, typeof(IBufferByteAccessInternal), CustomQueryInterfaceMode.Ignore);
				return CustomQueryInterfaceResult.Handled;
			}
			// fall through to projected interfaces...
			ppv = IntPtr.Zero;
			return CustomQueryInterfaceResult.NotHandled;
		}

		public void GetRuntimeClassName(out nint className)
		{
			throw new NotImplementedException();
		}

		public void GetTrustLevel(out TrustLevel trustLevel)
		{
			throw new NotImplementedException();
		}
	}
}