// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.App.Services.PreviewPopupProviders
{
	/// <inheritdoc cref="IPreviewPopupService"/>
	internal sealed partial class PreviewPopupService : ObservableObject, IPreviewPopupService
	{
		bool hasDetectedProvider = false;
		IPreviewPopupProvider? provider = null;

		public async Task<IPreviewPopupProvider?> GetProviderAsync()
		{
			if (hasDetectedProvider)
				return provider;

			hasDetectedProvider = true;
			if (await QuickLookProvider.Instance.DetectAvailability())
				return provider = await Task.FromResult<IPreviewPopupProvider>(QuickLookProvider.Instance);
			if (await SeerProProvider.Instance.DetectAvailability())
				return provider = await Task.FromResult<IPreviewPopupProvider>(SeerProProvider.Instance);
			else
				return provider = null;
		}
	}
}
