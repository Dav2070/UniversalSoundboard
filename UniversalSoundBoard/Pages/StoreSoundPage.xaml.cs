﻿using davClassLibrary;
using Sentry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UniversalSoundboard.Common;
using UniversalSoundboard.Components;
using UniversalSoundboard.DataAccess;
using UniversalSoundboard.Dialogs;
using UniversalSoundboard.Models;
using Windows.ApplicationModel.DataTransfer;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

namespace UniversalSoundboard.Pages
{
    public sealed partial class StoreSoundPage : Page
    {
        private SoundResponse soundItem;
        private MediaPlayer mediaPlayer;
        private Uri sourceUri = null;
        private bool isLoading = false;
        private bool isPlaying = false;
        private bool isDownloading = false;
        private int downloadProgress = 0;
        private bool belongsToUser = false;
        private bool isInSoundboard = true;
        private bool promoteButtonVisible = false;
        private bool moreButtonVisible = false;
        private Task<SoundPromotionResponse> createSoundPromotionTask;

        public StoreSoundPage()
        {
            InitializeComponent();

            mediaPlayer = new MediaPlayer
            {
                Volume = (double)FileManager.itemViewHolder.Volume / 100
            };

            FileManager.itemViewHolder.SoundPromotionStarted += ItemViewHolder_SoundPromotionStarted;
            mediaPlayer.MediaEnded += MediaPlayer_MediaEnded;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            SetThemeColors();
            UpdatePlayPauseButtonUI();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.Parameter == null)
                return;

            if (e.Parameter.GetType() == typeof(string))
            {
                isLoading = true;
                Bindings.Update();

                // Get the sound from the API
                soundItem = await ApiManager.RetrieveSound((string)e.Parameter);

                if (soundItem == null)
                {
                    MainPage.NavigateToPage(typeof(StorePage));
                    return;
                }

                mediaPlayer.Source = MediaSource.CreateFromUri(new Uri(soundItem.AudioFileUrl));

                if (soundItem.Source != null)
                    sourceUri = new Uri(soundItem.Source);

                isLoading = false;
            }
            else
            {
                soundItem = e.Parameter as SoundResponse;

                mediaPlayer.Source = MediaSource.CreateFromUri(new Uri(soundItem.AudioFileUrl));
                Bindings.Update();

                // Load the entire sound from the API
                soundItem = await ApiManager.RetrieveSound(soundItem.Uuid);

                if (soundItem == null)
                {
                    MainPage.NavigateToPage(typeof(StorePage));
                    return;
                }

                if (soundItem.Source != null)
                    sourceUri = new Uri(soundItem.Source);
            }

            // Check if the sound is already in the soundboard
            var sound = FileManager.itemViewHolder.AllSounds.FirstOrDefault(s =>
            {
                if (s.Source == null) return false;

                if (soundItem.Source != null)
                    return s.Source.Equals(soundItem.Source);

                return s.Source.Equals(soundItem.Uuid);
            });

            if (soundItem.User != null)
                belongsToUser = soundItem.User.Id == Dav.User.Id;

            isInSoundboard = sound != null;
            promoteButtonVisible = belongsToUser && soundItem.Promotion == null;
            moreButtonVisible = soundItem.Source == null && !belongsToUser;

            Bindings.Update();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            mediaPlayer.Pause();
        }

        private async void ItemViewHolder_SoundPromotionStarted(object sender, EventArgs e)
        {
            isLoading = true;
            Bindings.Update();

            soundItem = await ApiManager.RetrieveSound(soundItem.Uuid, caching: false);

            isLoading = false;
            Bindings.Update();
        }

        private async void MediaPlayer_MediaEnded(MediaPlayer sender, object args)
        {
            isPlaying = false;

            await MainPage.dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                UpdatePlayPauseButtonUI();
            });
        }

        private void SetThemeColors()
        {
            RequestedTheme = FileManager.GetRequestedTheme();
            SolidColorBrush appThemeColorBrush = new SolidColorBrush(FileManager.GetApplicationThemeColor());
            ContentRoot.Background = appThemeColorBrush;
        }

        private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
        {
            if (isPlaying)
                mediaPlayer.Pause();
            else
                mediaPlayer.Play();

            isPlaying = !isPlaying;
            UpdatePlayPauseButtonUI();
        }

        private void UpdatePlayPauseButtonUI()
        {
            if (isPlaying)
            {
                PlayPauseButton.Content = "\uE103";
                PlayPauseButtonToolTip.Text = FileManager.loader.GetString("PauseButtonToolTip");
            }
            else
            {
                PlayPauseButton.Content = "\uE102";
                PlayPauseButtonToolTip.Text = FileManager.loader.GetString("PlayButtonToolTip");
            }
        }

        private async void AddToSoundboardButton_Click(object sender, RoutedEventArgs e)
        {
            if (FileManager.itemViewHolder.Categories.Count > 1)
            {
                // Show the dialog to select categories
                var addToSoundboardDialog = new StoreAddToSoundboardDialog();
                addToSoundboardDialog.PrimaryButtonClick += AddToSoundboardDialog_PrimaryButtonClick;
                await addToSoundboardDialog.ShowAsync();
            }
            else
            {
                await AddSoundToSoundboard(new List<Guid>());
            }
        }

        private async void AddToSoundboardDialog_PrimaryButtonClick(Dialog sender, ContentDialogButtonClickEventArgs args)
        {
            var dialog = sender as StoreAddToSoundboardDialog;

            // Get the selected categories
            List<Guid> categoryUuids = new List<Guid>();

            foreach (var item in dialog.SelectedItems)
                categoryUuids.Add((Guid)((CustomTreeViewNode)item).Tag);

            await AddSoundToSoundboard(categoryUuids);
        }

        public async Task AddSoundToSoundboard(List<Guid> categoryUuids)
        {
            // Start downloading the audio file
            isDownloading = true;
            downloadProgress = 0;

            var progress = new Progress<int>((int value) => DownloadProgress(value));
            var cancellationTokenSource = new CancellationTokenSource();
            bool downloadSuccess = false;

            // Create a file in the cache
            StorageFolder cacheFolder = ApplicationData.Current.LocalCacheFolder;
            StorageFile targetFile = await cacheFolder.CreateFileAsync(string.Format("storedownload.{0}", soundItem.Type), CreationCollisionOption.GenerateUniqueName);

            await Task.Run(async () =>
            {
                downloadSuccess = (
                    await FileManager.DownloadBinaryDataToFile(
                        targetFile,
                        new Uri(soundItem.AudioFileUrl),
                        progress,
                        cancellationTokenSource.Token
                    )
                ).Key;
            });

            isDownloading = false;
            Bindings.Update();

            // Save the sound in the database
            Guid uuid = await FileManager.CreateSoundAsync(
                null,
                soundItem.Name,
                categoryUuids,
                targetFile,
                null,
                soundItem.Source ?? soundItem.Uuid
            );

            // Add the sound to the list
            await FileManager.AddSound(uuid);

            SentrySdk.CaptureMessage("StoreSoundPage-AddToSoundboard", scope =>
            {
                scope.SetTags(new Dictionary<string, string>
                {
                    { "SoundUuid", soundItem.Uuid },
                    { "SoundName", soundItem.Name }
                });
            });

            isInSoundboard = true;
            Bindings.Update();
        }

        private void DownloadProgress(int value)
        {
            downloadProgress = value;
            Bindings.Update();
        }

        private void ProfileButton_Click(object sender, RoutedEventArgs e)
        {
            SentrySdk.CaptureMessage("StoreSoundPage-ProfileButton-Click", scope =>
            {
                scope.SetTag("UserId", soundItem.User.Id.ToString());
            });

            MainPage.NavigateToPage(
                typeof(StoreProfilePage),
                soundItem.User.Id,
                new DrillInNavigationTransitionInfo()
            );
        }

        private void TagsGridView_ItemClick(object sender, ItemClickEventArgs e)
        {
            SentrySdk.CaptureMessage("StoreSoundPage-TagsGridView-ItemClick", scope =>
            {
                scope.SetTag("Tag", (string)e.ClickedItem);
            });

            MainPage.NavigateToPage(
                typeof(StoreSearchPage),
                e.ClickedItem,
                new DrillInNavigationTransitionInfo()
            );
        }

        private async void EditButton_Click(object sender, RoutedEventArgs e)
        {
            var itemTemplate = Resources["TagsTokenizingTextBoxItemTemplate"] as DataTemplate;

            var editSoundDialog = new EditSoundDialog(soundItem, itemTemplate);
            editSoundDialog.PrimaryButtonClick += EditSoundDialog_PrimaryButtonClick;
            await editSoundDialog.ShowAsync();
        }

        private async void EditSoundDialog_PrimaryButtonClick(Dialog sender, ContentDialogButtonClickEventArgs args)
        {
            var editSoundDialog = sender as EditSoundDialog;

            string name = editSoundDialog.Name;
            string description = editSoundDialog.Description;
            List<string> tags = editSoundDialog.SelectedTags;

            FileManager.itemViewHolder.LoadingScreenMessage = FileManager.loader.GetString("StoreSoundPage-UpdateSound");
            FileManager.itemViewHolder.LoadingScreenVisible = true;

            var updateSoundResult = await ApiManager.UpdateSound(soundItem.Uuid, name, description, tags);

            FileManager.itemViewHolder.LoadingScreenVisible = false;
            FileManager.itemViewHolder.LoadingScreenMessage = "";

            if (updateSoundResult != null)
            {
                // Update the UI with the new values
                soundItem.Name = name;
                soundItem.Description = description;
                soundItem.Tags = tags;

                Bindings.Update();
            }
        }

        private async void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            var deleteSoundDialog = new DeleteSoundDialog(soundItem.Name);
            deleteSoundDialog.PrimaryButtonClick += DeleteSoundDialog_PrimaryButtonClick;
            await deleteSoundDialog.ShowAsync();
        }

        private async void DeleteSoundDialog_PrimaryButtonClick(Dialog sender, ContentDialogButtonClickEventArgs args)
        {
            FileManager.itemViewHolder.LoadingScreenMessage = FileManager.loader.GetString("StoreSoundPage-DeleteSound");
            FileManager.itemViewHolder.LoadingScreenVisible = true;

            var deleteSoundResult = await ApiManager.DeleteSound(soundItem.Uuid);

            FileManager.itemViewHolder.LoadingScreenVisible = false;
            FileManager.itemViewHolder.LoadingScreenMessage = "";

            if (deleteSoundResult != null && deleteSoundResult.Uuid != null)
            {
                MainPage.NavigateToPage(
                    typeof(StoreProfilePage),
                    soundItem.User.Id,
                    new DrillInNavigationTransitionInfo()
                );
            }
        }

        private async void PromoteButton_Click(object sender, RoutedEventArgs e)
        {
            createSoundPromotionTask = ApiManager.CreateSoundPromotion(
                soundItem.Uuid,
                FileManager.loader.GetString("StoreSoundPage-CreateSoundPromotionTitle"),
                FileManager.loader.GetString("Currency")
            );

            SentrySdk.CaptureMessage("StoreSoundPage-PromoteButtonClick", scope =>
            {
                scope.SetTags(new Dictionary<string, string>
                {
                    { "SoundUuid", soundItem.Uuid },
                    { "SoundName", soundItem.Name }
                });
            });

            var startSoundPromotionDialog = new StartSoundPromotionDialog();
            startSoundPromotionDialog.PrimaryButtonClick += StartSoundPromotionDialog_PrimaryButtonClick;
            await startSoundPromotionDialog.ShowAsync();
        }

        private async void StartSoundPromotionDialog_PrimaryButtonClick(Dialog sender, ContentDialogButtonClickEventArgs args)
        {
            var soundPromotionResponse = await createSoundPromotionTask;

            if (soundPromotionResponse != null && soundPromotionResponse.SessionUrl != null)
                await Launcher.LaunchUriAsync(new Uri(soundPromotionResponse.SessionUrl));

            SentrySdk.CaptureMessage("StoreSoundPage-StartSoundPromotionDialog-PrimaryButtonClick", scope =>
            {
                scope.SetTags(new Dictionary<string, string>
                {
                    { "SoundUuid", soundItem.Uuid },
                    { "SoundName", soundItem.Name }
                });
            });
        }

        private void ShareButton_Click(object sender, RoutedEventArgs e)
        {
            DataTransferManager dataTransferManager = DataTransferManager.GetForCurrentView();
            dataTransferManager.DataRequested += DataTransferManager_DataRequested;
            DataTransferManager.ShowShareUI();
        }

        private void DataTransferManager_DataRequested(DataTransferManager sender, DataRequestedEventArgs args)
        {
            DataRequest request = args.Request;
            request.Data.Properties.Title = "Share sound";
            request.Data.SetWebLink(new Uri(string.Format("{0}/sound/{1}", Constants.UniversalSoundboardWebsiteBaseUrl, soundItem.Uuid)));
        }

        private async void ReportMenuFlyoutItem_Click(object sender, RoutedEventArgs e)
        {
            var reportSoundDialog = new ReportSoundDialog();
            reportSoundDialog.PrimaryButtonClick += ReportSoundDialog_PrimaryButtonClick;
            await reportSoundDialog.ShowAsync();
        }

        private async void ReportSoundDialog_PrimaryButtonClick(Dialog sender, ContentDialogButtonClickEventArgs args)
        {
            ReportSoundDialog dialog = sender as ReportSoundDialog;
            await ApiManager.CreateSoundReport(soundItem.Uuid, dialog.Description);
        }
    }
}
