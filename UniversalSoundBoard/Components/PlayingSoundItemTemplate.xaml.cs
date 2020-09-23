﻿using System;
using System.Linq;
using UniversalSoundboard.Common;
using UniversalSoundboard.Components;
using UniversalSoundboard.Models;
using UniversalSoundBoard.DataAccess;
using UniversalSoundBoard.Pages;
using Windows.ApplicationModel.Resources;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;

namespace UniversalSoundBoard.Components
{
    public sealed partial class PlayingSoundItemTemplate : UserControl
    {
        PlayingSound PlayingSound { get; set; }
        PlayingSoundItem PlayingSoundItem;

        private readonly ResourceLoader loader = new ResourceLoader();
        PlayingSoundItemLayoutType layoutType = PlayingSoundItemLayoutType.Small;
        Guid selectedSoundUuid;
        private bool skipSoundsListViewSelectionChanged;

        public PlayingSoundItemTemplate()
        {
            InitializeComponent();

            ContentRoot.DataContext = FileManager.itemViewHolder;
            DataContextChanged += PlayingSoundTemplate_DataContextChanged;
        }

        private void Init()
        {
            if (PlayingSound == null || PlayingSound.MediaPlayer == null) return;

            // Subscribe to the appropriate PlayingSoundItem
            int i = FileManager.itemViewHolder.PlayingSoundItems.FindIndex(item => item.Uuid.Equals(PlayingSound.Uuid));

            if(i == -1)
            {
                // Create a new PlayingSoundItem
                PlayingSoundItem = new PlayingSoundItem(PlayingSound, CoreWindow.GetForCurrentThread().Dispatcher);
                FileManager.itemViewHolder.PlayingSoundItems.Add(PlayingSoundItem);
            }
            else
                PlayingSoundItem = FileManager.itemViewHolder.PlayingSoundItems.ElementAt(i);

            PlayingSoundItem.PlaybackStateChanged -= PlayingSoundItem_PlaybackStateChanged;
            PlayingSoundItem.PlaybackStateChanged += PlayingSoundItem_PlaybackStateChanged;
            PlayingSoundItem.PositionChanged -= PlayingSoundItem_PositionChanged;
            PlayingSoundItem.PositionChanged += PlayingSoundItem_PositionChanged;
            PlayingSoundItem.DurationChanged -= PlayingSoundItem_DurationChanged;
            PlayingSoundItem.DurationChanged += PlayingSoundItem_DurationChanged;
            PlayingSoundItem.ButtonVisibilityChanged -= PlayingSoundItem_ButtonVisibilityChanged;
            PlayingSoundItem.ButtonVisibilityChanged += PlayingSoundItem_ButtonVisibilityChanged;
            PlayingSoundItem.CurrentSoundChanged -= PlayingSoundItem_CurrentSoundChanged;
            PlayingSoundItem.CurrentSoundChanged += PlayingSoundItem_CurrentSoundChanged;
            PlayingSoundItem.ExpandButtonContentChanged -= PlayingSoundItem_ExpandButtonContentChanged;
            PlayingSoundItem.ExpandButtonContentChanged += PlayingSoundItem_ExpandButtonContentChanged;
            PlayingSoundItem.ShowSoundsList -= PlayingSoundItem_ShowSoundsList;
            PlayingSoundItem.ShowSoundsList += PlayingSoundItem_ShowSoundsList;
            PlayingSoundItem.HideSoundsList -= PlayingSoundItem_HideSoundsList;
            PlayingSoundItem.HideSoundsList += PlayingSoundItem_HideSoundsList;
            PlayingSoundItem.FavouriteChanged -= PlayingSoundItem_FavouriteChanged;
            PlayingSoundItem.FavouriteChanged += PlayingSoundItem_FavouriteChanged;
            PlayingSoundItem.VolumeChanged -= PlayingSoundItem_VolumeChanged;
            PlayingSoundItem.VolumeChanged += PlayingSoundItem_VolumeChanged;
            PlayingSoundItem.MutedChanged -= PlayingSoundItem_MutedChanged;
            PlayingSoundItem.MutedChanged += PlayingSoundItem_MutedChanged;
            PlayingSoundItem.RemovePlayingSound -= PlayingSoundItem_RemovePlayingSound;
            PlayingSoundItem.RemovePlayingSound += PlayingSoundItem_RemovePlayingSound;
            PlayingSoundItem.DownloadStatusChanged -= PlayingSoundItem_DownloadStatusChanged;
            PlayingSoundItem.DownloadStatusChanged += PlayingSoundItem_DownloadStatusChanged;
            PlayingSoundItem.Init();

            // Hide the sounds list
            SoundsListViewStackPanel.Height = 0;
            SoundsListView.ItemsSource = PlayingSound.Sounds;

            UpdateUI();

            if (SoundPage.showPlayingSoundItemAnimation && PlayingSoundNameTextBlock.ActualWidth > 200)
            {
                // Show the animation for appearing PlayingSoundItem
                double contentHeight = 88;  // (88 = standard height of PlayingSoundItem with one row of text)
                if (ContentRoot.ActualHeight > 0) contentHeight = ContentRoot.ActualHeight + ContentRoot.Margin.Top + ContentRoot.Margin.Bottom;

                FileManager.itemViewHolder.TriggerShowPlayingSoundItemStartedEvent(
                    this,
                    new PlayingSoundItemEventArgs(
                        PlayingSound.Uuid,
                        contentHeight
                    )
                );

                ShowPlayingSoundItemStoryboardAnimation.To = contentHeight;
                ShowPlayingSoundItemStoryboard.Begin();
            }
        }

        #region UserControl event handlers
        private void PlayingSoundTemplate_Loaded(object sender, RoutedEventArgs eventArgs)
        {
            PlayingSoundItemTemplateUserControl.Height = double.NaN;

            Init();
            AdjustLayout();
        }

        private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            AdjustLayout();
            UpdateUI();
        }

        private void PlayingSoundTemplate_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            if (DataContext == null) return;

            PlayingSound = DataContext as PlayingSound;
            Init();
        }
        #endregion

        #region PlayingSoundItem events
        private void PlayingSoundItem_PlaybackStateChanged(object sender, PlaybackStateChangedEventArgs e)
        {
            UpdatePlayPauseButton(e.IsPlaying);
        }

        private void PlayingSoundItem_PositionChanged(object sender, PositionChangedEventArgs e)
        {
            if(e.Position.Hours == 0)
                RemainingTimeElement.Text = $"{e.Position.Minutes:D2}:{e.Position.Seconds:D2}";
            else
                RemainingTimeElement.Text = $"{e.Position.Hours:D2}:{e.Position.Minutes:D2}:{e.Position.Seconds:D2}";

            ProgressSlider.Value = e.Position.TotalSeconds;
        }

        private void PlayingSoundItem_DurationChanged(object sender, DurationChangedEventArgs e)
        {
            SetTotalDuration();
        }

        private void PlayingSoundItem_CurrentSoundChanged(object sender, CurrentSoundChangedEventArgs e)
        {
            UpdateUI();
        }

        private void PlayingSoundItem_ButtonVisibilityChanged(object sender, ButtonVisibilityChangedEventArgs e)
        {
            PreviousButton.Visibility = e.PreviousButtonVisibility;
            NextButton.Visibility = e.NextButtonVisibility;
            ExpandButton.Visibility = e.ExpandButtonVisibility;

            UpdatePlayPauseButtonMargin();
        }

        private void PlayingSoundItem_ExpandButtonContentChanged(object sender, ExpandButtonContentChangedEventArgs e)
        {
            if (e.Expanded)
            {
                ExpandButton.Content = "\uE098";
                ExpandButtonToolTip.Text = loader.GetString("CollapseButtonTooltip");
            }
            else
            {
                ExpandButton.Content = "\uE099";
                ExpandButtonToolTip.Text = loader.GetString("ExpandButtonTooltip");
            }
        }

        private void PlayingSoundItem_ShowSoundsList(object sender, EventArgs e)
        {
            // Start the animation
            ShowSoundsListViewStoryboardAnimation.To = SoundsListView.ActualHeight;
            ShowSoundsListViewStoryboard.Begin();
        }

        private void PlayingSoundItem_HideSoundsList(object sender, EventArgs e)
        {
            // Start the animation
            HideSoundsListViewStoryboard.Begin();
        }

        private void PlayingSoundItem_FavouriteChanged(object sender, FavouriteChangedEventArgs e)
        {
            SetFavouriteFlyoutItemText(e.Favourite);
        }

        private void PlayingSoundItem_VolumeChanged(object sender, VolumeChangedEventArgs e)
        {
            VolumeControl2.Value = e.Volume;
        }

        private void PlayingSoundItem_MutedChanged(object sender, MutedChangedEventArgs e)
        {
            VolumeControl2.Muted = e.Muted;
        }

        private void PlayingSoundItem_RemovePlayingSound(object sender, EventArgs e)
        {
            TriggerRemovePlayingSound();
        }

        private void PlayingSoundItem_DownloadStatusChanged(object sender, DownloadStatusChangedEventArgs e)
        {
            if (e.IsDownloading)
            {
                if (e.DownloadProgress < 0)
                {
                    // Show the indeterminate progress bar
                    ProgressSlider.Visibility = Visibility.Collapsed;
                    DownloadProgressBar.Visibility = Visibility.Visible;
                    DownloadProgressBar.IsIndeterminate = true;
                }
                else if (e.DownloadProgress > 100)
                {
                    // Show the progress slider
                    ProgressSlider.Visibility = Visibility.Visible;
                    DownloadProgressBar.Visibility = Visibility.Collapsed;
                }
                else
                {
                    // Show the progress bar with the current progress
                    ProgressSlider.Visibility = Visibility.Collapsed;
                    DownloadProgressBar.Visibility = Visibility.Visible;
                    DownloadProgressBar.IsIndeterminate = false;
                    DownloadProgressBar.Value = e.DownloadProgress;
                }
            }
            else
            {
                // Show the progress slider
                ProgressSlider.Visibility = Visibility.Visible;
                DownloadProgressBar.Visibility = Visibility.Collapsed;
            }
        }
        #endregion

        #region Button events
        private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
        {
            PlayingSoundItem.TogglePlayPause();
        }

        private async void PreviousButton_Click(object sender, RoutedEventArgs e)
        {
            await PlayingSoundItem.MoveToPrevious();
        }

        private async void NextButton_Click(object sender, RoutedEventArgs e)
        {
            await PlayingSoundItem.MoveToNext();
        }

        private void ExpandButton_Click(object sender, RoutedEventArgs e)
        {
            if (PlayingSoundItem.SoundsListVisible)
                PlayingSoundItem.CollapseSoundsList(ContentRoot.ActualHeight + ContentRoot.Margin.Top + ContentRoot.Margin.Bottom - SoundsListView.ActualHeight);
            else
                PlayingSoundItem.ExpandSoundsList(SoundsListView.ActualHeight);
        }

        private void VolumeButton_Click(object sender, RoutedEventArgs e)
        {
            // Set the value of the volume slider
            VolumeControl.Value = PlayingSound.Volume;
            VolumeControl.Muted = PlayingSound.Muted;
        }

        private void VolumeControl_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            double value;
            if (layoutType == PlayingSoundItemLayoutType.Large)
                value = VolumeControl.Value;
            else if (layoutType == PlayingSoundItemLayoutType.SingleSoundLarge)
                value = VolumeControl2.Value;
            else
                value = MoreButtonVolumeFlyoutItem.VolumeControlValue;

            // Apply the new volume
            PlayingSound.MediaPlayer.Volume = value / 100 * FileManager.itemViewHolder.Volume / 100;
        }

        private void VolumeControl_IconChanged(object sender, string newIcon)
        {
            // Update the icon of the Volume button
            VolumeButton.Content = newIcon;
        }

        private async void VolumeControl_LostFocus(object sender, RoutedEventArgs e)
        {
            double value;
            if (layoutType == PlayingSoundItemLayoutType.Large)
                value = VolumeControl.Value;
            else if (layoutType == PlayingSoundItemLayoutType.SingleSoundLarge)
                value = VolumeControl2.Value;
            else
                value = MoreButtonVolumeFlyoutItem.VolumeControlValue;

            int volume = Convert.ToInt32(value);
            await PlayingSoundItem.SetVolume(volume);
        }

        private async void VolumeControl_MuteChanged(object sender, bool muted)
        {
            await PlayingSoundItem.SetMuted(muted);
        }

        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            PlayingSoundItem.StartRemove();
            TriggerRemovePlayingSound();
        }

        private void ProgressSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            double diff = e.NewValue - e.OldValue;
            if (diff > 0.6 || diff < -0.6)
                PlayingSoundItem.SetPosition(Convert.ToInt32(e.NewValue));
        }

        private void MenuFlyout_Opened(object sender, object e)
        {
            if (layoutType != PlayingSoundItemLayoutType.Large)
            {
                // Set the value of the VolumeMenuFlyoutItem
                MoreButtonVolumeFlyoutItem.VolumeControlValue = PlayingSound.Volume;
                MoreButtonVolumeFlyoutItem.VolumeControlMuted = PlayingSound.Muted;
            }
        }

        private async void MoreButton_Repeat_1x_Click(object sender, RoutedEventArgs e)
        {
            await PlayingSoundItem.SetRepetitions(2);
        }

        private async void MoreButton_Repeat_2x_Click(object sender, RoutedEventArgs e)
        {
            await PlayingSoundItem.SetRepetitions(3);
        }

        private async void MoreButton_Repeat_5x_Click(object sender, RoutedEventArgs e)
        {
            await PlayingSoundItem.SetRepetitions(6);
        }

        private async void MoreButton_Repeat_10x_Click(object sender, RoutedEventArgs e)
        {
            await PlayingSoundItem.SetRepetitions(11);
        }

        private async void MoreButton_Repeat_endless_Click(object sender, RoutedEventArgs e)
        {
            await PlayingSoundItem.SetRepetitions(int.MaxValue);
        }

        private async void MoreButtonFavouriteItem_Click(object sender, RoutedEventArgs e)
        {
            if (PlayingSound == null || PlayingSound.MediaPlayer == null) return;
            await PlayingSoundItem.ToggleFavourite();
        }

        private void SoundsListViewRemoveSwipeItem_Invoked(SwipeItem sender, SwipeItemInvokedEventArgs args)
        {
            if (PlayingSound.Sounds.Count > 1)
                PlayingSoundItem.RemoveSound((Guid)args.SwipeControl.Tag);
            else
                TriggerRemovePlayingSound();
        }

        private async void SoundsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (skipSoundsListViewSelectionChanged) return;
            await PlayingSoundItem.MoveToSound(SoundsListView.SelectedIndex);
        }

        private void SwipeControl_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            MenuFlyout flyout = new MenuFlyout();
            selectedSoundUuid = (Guid)(sender as SwipeControl).Tag;

            if (PlayingSound.Sounds.Count > 1)
            {
                MenuFlyoutItem removeFlyoutItem = new MenuFlyoutItem
                {
                    Text = loader.GetString("Remove"),
                    Icon = new FontIcon { Glyph = "\uE106" }
                };
                removeFlyoutItem.Click += RemoveFlyoutItem_Click;
                flyout.Items.Add(removeFlyoutItem);
            }

            if (flyout.Items.Count > 0)
                flyout.ShowAt(sender as UIElement, e.GetPosition(sender as UIElement));
        }

        private void RemoveFlyoutItem_Click(object sender, RoutedEventArgs e)
        {
            // Remove the selected sound
            PlayingSoundItem.RemoveSound(selectedSoundUuid);
        }
        #endregion

        #region Functionality
        private void TriggerRemovePlayingSound()
        {
            // Start the animation for hiding the PlayingSoundItem
            HidePlayingSoundItemStoryboardAnimation.From = PlayingSoundItemTemplateUserControl.ActualHeight;
            HidePlayingSoundItemStoryboard.Begin();

            // Trigger the animation in SoundPage for the BottomPlayingSoundsBar, if necessary
            FileManager.itemViewHolder.TriggerRemovePlayingSoundItemEvent(this, new PlayingSoundItemEventArgs(PlayingSound.Uuid));
        }

        private void SetTotalDuration()
        {
            var totalDuration = PlayingSoundItem.CurrentSoundTotalDuration;

            // Set the total duration text
            if (totalDuration.Hours == 0)
                TotalTimeElement.Text = $"{totalDuration.Minutes:D2}:{totalDuration.Seconds:D2}";
            else
                TotalTimeElement.Text = $"{totalDuration.Hours:D2}:{totalDuration.Minutes:D2}:{totalDuration.Seconds:D2}";

            // Set the maximum of the slider
            ProgressSlider.Maximum = totalDuration.TotalSeconds;
        }
        #endregion

        #region UI methods
        private void AdjustLayout()
        {
            if (PlayingSound == null || PlayingSound.MediaPlayer == null) return;

            // Set the appropriate layout for the PlayingSoundItem
            double windowWidth = Window.Current.Bounds.Width;
            double itemWidth = ContentRoot.ActualWidth;

            if (FileManager.itemViewHolder.PlayingSoundsListVisible && !FileManager.itemViewHolder.OpenMultipleSounds)
            {
                if (windowWidth <= 900)
                    layoutType = PlayingSoundItemLayoutType.SingleSoundSmall;
                else
                    layoutType = PlayingSoundItemLayoutType.SingleSoundLarge;
            }
            else if (windowWidth < FileManager.mobileMaxWidth)
                layoutType = PlayingSoundItemLayoutType.Compact;
            else if (itemWidth <= 210)
                layoutType = PlayingSoundItemLayoutType.Mini;
            else if (itemWidth <= 300)
                layoutType = PlayingSoundItemLayoutType.Small;
            else
                layoutType = PlayingSoundItemLayoutType.Large;

            switch (layoutType)
            {
                case PlayingSoundItemLayoutType.SingleSoundSmall:
                    VisualStateManager.GoToState(this, "LayoutSizeSingleSoundSmall", false);
                    break;
                case PlayingSoundItemLayoutType.SingleSoundLarge:
                    VisualStateManager.GoToState(this, "LayoutSizeSingleSoundLarge", false);
                    break;
                case PlayingSoundItemLayoutType.Compact:
                    VisualStateManager.GoToState(this, "LayoutSizeCompact", false);
                    break;
                case PlayingSoundItemLayoutType.Mini:
                    VisualStateManager.GoToState(this, "LayoutSizeMini", false);
                    break;
                case PlayingSoundItemLayoutType.Small:
                    VisualStateManager.GoToState(this, "LayoutSizeSmall", false);
                    break;
                case PlayingSoundItemLayoutType.Large:
                    VisualStateManager.GoToState(this, "LayoutSizeLarge", false);
                    break;
            }

            // Set the visibility of the time texts in the TransportControls
            SetTimelineLayout(
                layoutType == PlayingSoundItemLayoutType.SingleSoundSmall || layoutType == PlayingSoundItemLayoutType.SingleSoundLarge,
                layoutType != PlayingSoundItemLayoutType.Compact
            );
        }

        private void UpdateUI()
        {
            if (PlayingSound == null) return;

            // Set the name of the current sound and set the favourite flyout item
            var currentSound = PlayingSound.Sounds.ElementAt(PlayingSound.Current);
            PlayingSoundNameTextBlock.Text = currentSound.Name;
            SetFavouriteFlyoutItemText(currentSound.Favourite);

            // Set the selected item of the sounds list
            skipSoundsListViewSelectionChanged = true;
            SoundsListView.SelectedIndex = PlayingSound.Current;
            skipSoundsListViewSelectionChanged = false;

            // Set the volume icon
            if (layoutType == PlayingSoundItemLayoutType.Large)
                VolumeButton.Content = VolumeControl.GetVolumeIcon(PlayingSound.Volume, PlayingSound.Muted);

            // Set the total duration text
            SetTotalDuration();
        }

        private void SetTimelineLayout(bool compact, bool timesVisible)
        {
            if (compact)
            {
                // ProgressSlider
                RelativePanel.SetAlignLeftWithPanel(ProgressSlider, false);
                RelativePanel.SetAlignRightWithPanel(ProgressSlider, false);

                RelativePanel.SetRightOf(ProgressSlider, RemainingTimeElement);
                RelativePanel.SetLeftOf(ProgressSlider, TotalTimeElement);

                ProgressSlider.Margin = new Thickness(8, 0, 8, 0);
                ProgressSlider.Height = 37;

                // BufferingProgressBar
                RelativePanel.SetAlignLeftWithPanel(DownloadProgressBar, false);
                RelativePanel.SetAlignRightWithPanel(DownloadProgressBar, false);
                RelativePanel.SetAlignTopWithPanel(DownloadProgressBar, false);

                RelativePanel.SetRightOf(DownloadProgressBar, RemainingTimeElement);
                RelativePanel.SetLeftOf(DownloadProgressBar, TotalTimeElement);
                RelativePanel.SetAlignVerticalCenterWith(DownloadProgressBar, RemainingTimeElement);

                DownloadProgressBar.Margin = new Thickness(8, 2, 8, 0);

                // TimeElapsedElement
                RelativePanel.SetAlignBottomWith(RemainingTimeElement, null);
                RelativePanel.SetAlignLeftWithPanel(RemainingTimeElement, true);
                RelativePanel.SetAlignVerticalCenterWithPanel(RemainingTimeElement, true);

                RemainingTimeElement.Margin = new Thickness(0);

                // TimeRemainingElement
                RelativePanel.SetAlignBottomWith(TotalTimeElement, null);
                RelativePanel.SetAlignRightWithPanel(TotalTimeElement, true);
                RelativePanel.SetAlignVerticalCenterWithPanel(TotalTimeElement, true);

                TotalTimeElement.Margin = new Thickness(0);
            }
            else
            {
                // ProgressSlider
                RelativePanel.SetAlignLeftWithPanel(ProgressSlider, true);
                RelativePanel.SetAlignRightWithPanel(ProgressSlider, true);

                RelativePanel.SetRightOf(ProgressSlider, null);
                RelativePanel.SetLeftOf(ProgressSlider, null);

                ProgressSlider.Margin = new Thickness(0, 0, 0, timesVisible ? 14 : 0);
                ProgressSlider.Height = 33;

                // BufferingProgressBar
                RelativePanel.SetAlignLeftWithPanel(DownloadProgressBar, true);
                RelativePanel.SetAlignRightWithPanel(DownloadProgressBar, true);
                RelativePanel.SetAlignTopWithPanel(DownloadProgressBar, true);

                RelativePanel.SetRightOf(DownloadProgressBar, null);
                RelativePanel.SetLeftOf(DownloadProgressBar, null);
                RelativePanel.SetAlignVerticalCenterWith(DownloadProgressBar, null);

                DownloadProgressBar.Margin = new Thickness(0, 18, 0, timesVisible ? 14 : 0);

                // TimeElapsedElement
                RelativePanel.SetAlignBottomWith(RemainingTimeElement, ProgressSlider);
                RelativePanel.SetAlignLeftWithPanel(RemainingTimeElement, true);
                RelativePanel.SetAlignVerticalCenterWithPanel(RemainingTimeElement, false);

                RemainingTimeElement.Margin = new Thickness(0);

                // TimeRemainingElement
                RelativePanel.SetAlignBottomWith(TotalTimeElement, ProgressSlider);
                RelativePanel.SetAlignRightWithPanel(TotalTimeElement, true);
                RelativePanel.SetAlignVerticalCenterWithPanel(TotalTimeElement, false);

                TotalTimeElement.Margin = new Thickness(0);
            }

            RemainingTimeElement.Visibility = timesVisible ? Visibility.Visible : Visibility.Collapsed;
            TotalTimeElement.Visibility = timesVisible ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdatePlayPauseButton(bool isPlaying)
        {
            if (isPlaying)
            {
                PlayPauseButton.Content = "\uE103";
                PlayPauseButtonToolTip.Text = loader.GetString("PauseButtonToolTip");
            }
            else
            {
                PlayPauseButton.Content = "\uE102";
                PlayPauseButtonToolTip.Text = loader.GetString("PlayButtonToolTip");
            }
        }

        private void UpdatePlayPauseButtonMargin()
        {
            // If a single sound is allowed, move the PlayPauseButton to the right so that the buttons don't move when the Previous or Next button disappear
            if (
                layoutType == PlayingSoundItemLayoutType.SingleSoundSmall
                && PreviousButton.Visibility == Visibility.Collapsed
            ) PlayPauseButton.Margin = new Thickness(52, 0, 10, 0);
            else if (layoutType == PlayingSoundItemLayoutType.SingleSoundSmall)
                PlayPauseButton.Margin = new Thickness(10, 0, 10, 0);
            else
                PlayPauseButton.Margin = new Thickness(1, 0, 1, 0);
        }

        private void SetFavouriteFlyoutItemText(bool fav)
        {
            MoreButtonFavouriteFlyoutItem.Text = loader.GetString(fav ? "SoundItemOptionsFlyout-UnsetFavourite" : "SoundItemOptionsFlyout-SetFavourite");
            MoreButtonFavouriteFlyoutItem.Icon = new FontIcon { Glyph = fav ? "\uE195" : "\uE113" };
        }
        #endregion

        #region Storyboard event handlers
        private async void HidePlayingSoundItemStoryboard_Completed(object sender, object e)
        {
            await PlayingSoundItem.Remove();
        }

        private void ShowSoundsListViewStoryboard_Completed(object sender, object e)
        {
            FileManager.itemViewHolder.TriggerPlayingSoundItemShowSoundsListAnimationEndedEvent(this, new PlayingSoundItemEventArgs(PlayingSound.Uuid));
        }

        private void HideSoundsListViewStoryboard_Completed(object sender, object e)
        {
            FileManager.itemViewHolder.TriggerPlayingSoundItemHideSoundsListAnimationEndedEvent(this, new PlayingSoundItemEventArgs(PlayingSound.Uuid));
        }

        private void ShowPlayingSoundItemStoryboard_Completed(object sender, object e)
        {
            PlayingSoundItemTemplateUserControl.Height = double.NaN;
            SoundPage.showPlayingSoundItemAnimation = false;
        }
        #endregion
    }

    enum PlayingSoundItemLayoutType
    {
        SingleSoundSmall,
        SingleSoundLarge,
        Compact,
        Mini,
        Small,
        Large
    }
}
