﻿using Microsoft.Toolkit.Uwp.UI.Controls;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using UniversalSoundboard.Common;
using UniversalSoundboard.DataAccess;
using UniversalSoundboard.Models;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;

namespace UniversalSoundboard.Controllers
{
    public class PlayingSoundsAnimationController
    {
        private bool playingSoundsLoaded = false;
        private bool playingSoundItemsLoaded = false;
        private bool snapBottomPlayingSoundsBarAnimationRunning = false;
        private bool isManipulatingBottomPlayingSoundsBar = false;
        private double maxBottomPlayingSoundsBarHeight = 500;
        private int initialBottomPlayingSoundsCount = 0;
        private int loadedBottomPlayingSoundItemCount = 0;
        private BottomPlayingSoundsBarVerticalPosition bottomPlayingSoundsBarPosition = BottomPlayingSoundsBarVerticalPosition.Bottom;

        RelativePanel ContentRoot;
        GridView SoundGridView;
        GridView FavouriteSoundGridView;
        ListView SoundListView;
        ListView FavouriteSoundListView;
        GridView SoundGridView2;
        ListView SoundListView2;

        ListView PlayingSoundsBarListView;
        ListView BottomPlayingSoundsBarListView;

        ColumnDefinition GridSplitterColDef;
        ColumnDefinition PlayingSoundsBarColDef;

        RelativePanel BottomPlayingSoundsBar;
        Grid GridSplitterGrid;
        RowDefinition GridSplitterGridBottomRowDef;
        GridSplitter BottomPlayingSoundsBarGridSplitter;
        Grid BottomPseudoContentGrid;

        Storyboard SnapBottomPlayingSoundsBarStoryboard;
        DoubleAnimation SnapBottomPlayingSoundsBarStoryboardAnimation;

        public bool IsMobile { get; set; }

        public ObservableCollection<PlayingSoundItemContainer> PlayingSoundItemContainers { get; private set; }
        public ObservableCollection<PlayingSoundItemContainer> ReversedPlayingSoundItemContainers { get; private set; }

        public PlayingSoundsAnimationController(
            RelativePanel contentRoot,
            GridView soundGridView,
            GridView favouriteSoundGridView,
            ListView soundListView,
            ListView favouriteSoundListView,
            GridView soundGridView2,
            ListView soundListView2,

            ListView playingSoundsBarListView,
            ListView bottomPlayingSoundsBarListView,

            ColumnDefinition gridSplitterColDef,
            ColumnDefinition playingSoundsBarColDef,

            RelativePanel bottomPlayingSoundsBar,
            Grid gridSplitterGrid,
            RowDefinition gridSplitterGridBottomRowDef,
            GridSplitter bottomPlayingSoundsBarGridSplitter,
            Grid bottomPseudoContentGrid,

            Storyboard snapBottomPlayingSoundsBarStoryboard,
            DoubleAnimation snapBottomPlayingSoundsBarStoryboardAnimation
        )
        {
            ContentRoot = contentRoot;
            SoundGridView = soundGridView;
            FavouriteSoundGridView = favouriteSoundGridView;
            SoundListView = soundListView;
            FavouriteSoundListView = favouriteSoundListView;
            SoundGridView2 = soundGridView2;
            SoundListView2 = soundListView2;

            PlayingSoundsBarListView = playingSoundsBarListView;
            BottomPlayingSoundsBarListView = bottomPlayingSoundsBarListView;

            GridSplitterColDef = gridSplitterColDef;
            PlayingSoundsBarColDef = playingSoundsBarColDef;

            BottomPlayingSoundsBar = bottomPlayingSoundsBar;
            GridSplitterGrid = gridSplitterGrid;
            GridSplitterGridBottomRowDef = gridSplitterGridBottomRowDef;
            BottomPlayingSoundsBarGridSplitter = bottomPlayingSoundsBarGridSplitter;
            BottomPseudoContentGrid = bottomPseudoContentGrid;

            SnapBottomPlayingSoundsBarStoryboard = snapBottomPlayingSoundsBarStoryboard;
            SnapBottomPlayingSoundsBarStoryboardAnimation = snapBottomPlayingSoundsBarStoryboardAnimation;

            IsMobile = false;

            PlayingSoundItemContainers = new ObservableCollection<PlayingSoundItemContainer>();
            ReversedPlayingSoundItemContainers = new ObservableCollection<PlayingSoundItemContainer>();

            ContentRoot.SizeChanged += ContentRoot_SizeChanged;
            BottomPseudoContentGrid.SizeChanged += BottomPseudoContentGrid_SizeChanged;
            BottomPlayingSoundsBarListView.SizeChanged += BottomPlayingSoundsBarListView_SizeChanged;
            BottomPlayingSoundsBarGridSplitter.ManipulationStarted += BottomPlayingSoundsBarGridSplitter_ManipulationStarted;
            BottomPlayingSoundsBarGridSplitter.ManipulationDelta += BottomPlayingSoundsBarGridSplitter_ManipulationDelta;
            BottomPlayingSoundsBarGridSplitter.ManipulationCompleted += BottomPlayingSoundsBarGridSplitter_ManipulationCompleted;
            SnapBottomPlayingSoundsBarStoryboard.Completed += SnapBottomPlayingSoundsBarStoryboard_Completed;
            FileManager.itemViewHolder.PlayingSoundsLoaded += ItemViewHolder_PlayingSoundsLoaded;
            FileManager.itemViewHolder.RemovePlayingSoundItem += ItemViewHolder_RemovePlayingSoundItem;
            FileManager.itemViewHolder.HideBottomPlayingSoundsBar += ItemViewHolder_HideBottomPlayingSoundsBar;
            FileManager.itemViewHolder.PlayingSounds.CollectionChanged += ItemViewHolder_PlayingSounds_CollectionChanged;
        }

        #region Event handlers
        private async void ContentRoot_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            bool oldIsBottomPlayingSoundsBarVisible = IsMobile;

            maxBottomPlayingSoundsBarHeight = Window.Current.Bounds.Height * 0.6;
            IsMobile = Window.Current.Bounds.Width < FileManager.mobileMaxWidth;

            if (
                playingSoundItemsLoaded
                && IsMobile != oldIsBottomPlayingSoundsBarVisible
            )
            {
                if (IsMobile)
                {
                    GridSplitterGrid.Opacity = 0;
                    await Task.Delay(800);

                    // Show the bottom playing sounds bar
                    UpdatePlayingSoundsList();
                    InitBottomPlayingSoundsBarHeight();

                    await ShowBottomPlayingSoundsBar();
                }
                else
                {
                    GridSplitterGrid.Visibility = Visibility.Collapsed;
                    BottomPlayingSoundsBar.Translation = new Vector3(-10000, 0, 0);
                    BottomPlayingSoundsBar.Height = double.NaN;
                }
            }

            if (!playingSoundItemsLoaded || !IsMobile)
                UpdatePlayingSoundsList();
        }

        private void BottomPseudoContentGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Update the Paddings of the GridViews and ListViews
            AdaptSoundListScrollViewerForBottomPlayingSoundsBar();
        }

        private void BottomPlayingSoundsBarListView_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (isManipulatingBottomPlayingSoundsBar) return;
            GridSplitterGridBottomRowDef.Height = new GridLength(BottomPlayingSoundsBarListView.ActualHeight);
        }

        private void BottomPlayingSoundsBarGridSplitter_ManipulationStarted(object sender, ManipulationStartedRoutedEventArgs e)
        {
            isManipulatingBottomPlayingSoundsBar = true;
        }

        private void BottomPlayingSoundsBarGridSplitter_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
        {
            BottomPlayingSoundsBar.Height = GridSplitterGridBottomRowDef.ActualHeight;
        }

        private void BottomPlayingSoundsBarGridSplitter_ManipulationCompleted(object sender, ManipulationCompletedRoutedEventArgs e)
        {
            isManipulatingBottomPlayingSoundsBar = false;
            SnapBottomPlayingSoundsBar();
        }

        private void SnapBottomPlayingSoundsBarStoryboard_Completed(object sender, object e)
        {
            snapBottomPlayingSoundsBarAnimationRunning = false;
            UpdateGridSplitterRange();
        }

        private void ItemViewHolder_PlayingSoundsLoaded(object sender, EventArgs e)
        {
            LoadPlayingSoundItems();
            playingSoundsLoaded = true;
        }

        private void ItemViewHolder_RemovePlayingSoundItem(object sender, RemovePlayingSoundItemEventArgs args)
        {
            // Find the item container with the uuid and set IsVisible to false
            // so that the item is not rendered when the BottomPlayingSoundsBar is made visible 
            var itemContainer = ReversedPlayingSoundItemContainers.ToList().Find(item => item.PlayingSound.Uuid.Equals(args.Uuid));
            if (itemContainer != null) itemContainer.IsVisible = false;
        }

        private async void ItemViewHolder_HideBottomPlayingSoundsBar(object sender, EventArgs e)
        {
            if (IsMobile)
            {
                await HideBottomPlayingSoundsBar();
                AdaptSoundListScrollViewerForBottomPlayingSoundsBar(0);
            }
        }

        private void ItemViewHolder_PlayingSounds_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (!playingSoundsLoaded) return;

            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                bool onlyPlayingSound = FileManager.itemViewHolder.PlayingSounds.Count == 1;

                if (onlyPlayingSound)
                {
                    // Reset the BottomPlayingSoundsBar to enable loading PlayingSoundItems
                    BottomPlayingSoundsBar.Translation = new Vector3(-10000, 0, 0);
                    BottomPlayingSoundsBar.Height = double.NaN;
                }
                else if (FileManager.itemViewHolder.PlayingSounds.Count == 2)
                {
                    // Show the GridSplitter
                    GridSplitterGrid.Opacity = 0;
                    BottomPlayingSoundsBarGridSplitter.Visibility = Visibility.Visible;

                    UpdateGridSplitterRange();
                    ShowGridSplitter();
                }

                var playingSound = e.NewItems[0] as PlayingSound;

                var item1 = new PlayingSoundItemContainer(PlayingSoundsBarListView.Items.Count, playingSound, false);
                item1.Show += PlayingSoundItemContainer_Show;
                item1.Hide += PlayingSoundItemContainer_Hide;

                var item2 = new PlayingSoundItemContainer(PlayingSoundsBarListView.Items.Count, playingSound, true, !onlyPlayingSound);
                item2.Show += PlayingSoundItemContainer_Show;
                item2.Hide += PlayingSoundItemContainer_Hide;
                item2.Loaded += PlayingSoundItemContainer2_Loaded;

                PlayingSoundItemContainers.Add(item1);
                ReversedPlayingSoundItemContainers.Insert(0, item2);
            }
            else if (
                e.Action == NotifyCollectionChangedAction.Remove
                && FileManager.itemViewHolder.PlayingSounds.Count == 1
            )
            {
                //await HideGridSplitter();
            }
            else if (e.Action == NotifyCollectionChangedAction.Remove && IsMobile)
            {
                //SnapBottomPlayingSoundsBar();
                //UpdateGridSplitterRange();
            }
        }

        private void PlayingSoundItemContainer_Show(object sender, EventArgs e)
        {
            // Update the animation with the actual PlayingSoundItem height
            //PlayingSoundItemContainer itemContainer = sender as PlayingSoundItemContainer;
            //AnimateIncreasingBottomPlayingSoundBar(itemContainer.ContentHeight);

            //await Task.Delay(200);

            //UpdateGridSplitterRange();
            //SnapBottomPlayingSoundsBar();
        }

        private void PlayingSoundItemContainer_Hide(object sender, EventArgs e)
        {
            //AnimateIncreasingBottomPlayingSoundBar();
            /*
            if (isBottomPlayingSoundsBarVisible)
            {
                if (!FileManager.itemViewHolder.OpenMultipleSounds || FileManager.itemViewHolder.PlayingSounds.Count == 1)
                {
                    // Start the animation for hiding the BottomPlayingSoundsBar background / BottomPseudoContentGrid
                    GridSplitterGridBottomRowDef.MinHeight = 0;
                    StartSnapBottomPlayingSoundsBarAnimation(GridSplitterGridBottomRowDef.ActualHeight, 0);
                }
                else
                {
                    PlayingSoundItemContainer itemContainer = sender as PlayingSoundItemContainer;
                    AnimateIncreasingBottomPlayingSoundBar(-itemContainer.ContentHeight);
                }
            }

            await Task.Delay(200);

            if (nextSinglePlayingSoundToOpen != null)
            {
                FileManager.itemViewHolder.PlayingSounds.Add(nextSinglePlayingSoundToOpen);
                nextSinglePlayingSoundToOpen = null;
            }
            else if (FileManager.itemViewHolder.OpenMultipleSounds)
            {
                // Snap the BottomPlayingSoundsBar; for the case that the height of the removed PlayingSound was different
                UpdateGridSplitterRange();
                SnapBottomPlayingSoundsBar();
            }
            */
        }

        private async void PlayingSoundItemContainer_Loaded(object sender, EventArgs e)
        {
            loadedBottomPlayingSoundItemCount++;

            // Wait for all initial items to be loaded
            if (loadedBottomPlayingSoundItemCount == initialBottomPlayingSoundsCount)
            {
                if (
                    FileManager.itemViewHolder.PlayingSoundsListVisible
                    && (
                        !FileManager.itemViewHolder.OpenMultipleSounds
                        || IsMobile
                    )
                )
                {
                    // Show the bottom playing sounds bar
                    // Wait for all animations to have ended
                    await Task.Delay(200);

                    UpdatePlayingSoundsList();
                    InitBottomPlayingSoundsBarHeight();

                    await ShowBottomPlayingSoundsBar();
                }
            }
        }

        private async void PlayingSoundItemContainer2_Loaded(object sender, EventArgs e)
        {
            if (
                FileManager.itemViewHolder.PlayingSounds.Count == 1
                && FileManager.itemViewHolder.PlayingSoundsListVisible
                && (
                    !FileManager.itemViewHolder.OpenMultipleSounds
                    || IsMobile
                )
            )
            {
                await Task.Delay(200);

                // Show the bottom playing sounds bar
                UpdatePlayingSoundsList();
                InitBottomPlayingSoundsBarHeight();

                await ShowBottomPlayingSoundsBar();
            }
        }
        #endregion

        private void LoadPlayingSoundItems()
        {
            playingSoundItemsLoaded = true;
            initialBottomPlayingSoundsCount = FileManager.itemViewHolder.PlayingSounds.Count;

            foreach (var playingSound in FileManager.itemViewHolder.PlayingSounds)
            {
                var item1 = new PlayingSoundItemContainer(PlayingSoundsBarListView.Items.Count, playingSound, false);
                item1.Show += PlayingSoundItemContainer_Show;
                item1.Hide += PlayingSoundItemContainer_Hide;

                var item2 = new PlayingSoundItemContainer(PlayingSoundsBarListView.Items.Count, playingSound, true, false);
                item2.Show += PlayingSoundItemContainer_Show;
                item2.Hide += PlayingSoundItemContainer_Hide;
                item2.Loaded += PlayingSoundItemContainer_Loaded;

                PlayingSoundItemContainers.Add(item1);
                ReversedPlayingSoundItemContainers.Insert(0, item2);
            }
        }

        private void InitBottomPlayingSoundsBarHeight()
        {
            // Update the min and max height of the bottom row def
            UpdateGridSplitterRange();

            if (BottomPlayingSoundsBarListView.Items.Count > 0)
            {
                // Get the height of the first PlayingSound item and set the height of the BottomPlayingSoundsBar
                double firstItemHeight = GetFirstBottomPlayingSoundItemContentHeight();
                BottomPlayingSoundsBar.Height = firstItemHeight;
                GridSplitterGridBottomRowDef.MinHeight = firstItemHeight;
                GridSplitterGridBottomRowDef.Height = new GridLength(firstItemHeight);
            }
        }

        private void UpdateGridSplitterRange()
        {
            // Set the max height of the bottom row def
            double totalHeight = GetTotalBottomPlayingSoundListContentHeight();

            if (totalHeight == 0)
                return;

            if (totalHeight >= maxBottomPlayingSoundsBarHeight)
                totalHeight = maxBottomPlayingSoundsBarHeight;

            GridSplitterGridBottomRowDef.MaxHeight = totalHeight;

            // Set the min height of the bottom row def
            double firstItemHeight = GetFirstBottomPlayingSoundItemContentHeight();
            if (firstItemHeight >= maxBottomPlayingSoundsBarHeight) firstItemHeight = GridSplitterGridBottomRowDef.ActualHeight;

            GridSplitterGridBottomRowDef.MinHeight = firstItemHeight;
        }

        private double GetTotalBottomPlayingSoundListContentHeight()
        {
            double totalHeight = 0;

            foreach (var item in ReversedPlayingSoundItemContainers)
                totalHeight += item.ContentHeight;

            return totalHeight;
        }

        private double GetFirstBottomPlayingSoundItemContentHeight()
        {
            foreach (var item in ReversedPlayingSoundItemContainers)
                if (item.IsVisible)
                    return item.ContentHeight;

            return 0;
        }

        private int GetNumberOfVisibleItemsInReversedPlayingSoundItemContainers()
        {
            int visibleItems = 0;

            foreach (var item in ReversedPlayingSoundItemContainers)
                if (item.IsVisible)
                    visibleItems++;

            return visibleItems;
        }

        private void UpdatePlayingSoundsList()
        {
            if (FileManager.itemViewHolder.PlayingSoundsListVisible)
            {
                // Set the max width of the sounds list and playing sounds list columns
                PlayingSoundsBarColDef.MaxWidth = ContentRoot.ActualWidth / 2;

                if (!FileManager.itemViewHolder.OpenMultipleSounds || IsMobile)
                {
                    // Hide the PlayingSoundsBar and the GridSplitter
                    PlayingSoundsBarColDef.MinWidth = 0;
                    PlayingSoundsBarColDef.Width = new GridLength(0);
                    GridSplitterColDef.Width = new GridLength(0);

                    int playingSoundItemsCount = GetNumberOfVisibleItemsInReversedPlayingSoundItemContainers();

                    // Update the visibility of the BottomPlayingSoundsBar
                    if (playingSoundItemsCount == 0)
                    {
                        BottomPlayingSoundsBar.Translation = new Vector3(-10000, 0, 0);
                        GridSplitterGrid.Visibility = Visibility.Collapsed;
                    }
                    else if (playingSoundItemsCount == 1)
                    {
                        BottomPlayingSoundsBar.Translation = new Vector3(0);
                        GridSplitterGrid.Visibility = Visibility.Visible;

                        // Set the height of the bottom row def, but hide the GridSplitter
                        BottomPlayingSoundsBarGridSplitter.Visibility = Visibility.Collapsed;
                        GridSplitterGridBottomRowDef.Height = new GridLength(GetFirstBottomPlayingSoundItemContentHeight());
                    }
                    else
                    {
                        BottomPlayingSoundsBar.Translation = new Vector3(0);
                        GridSplitterGrid.Visibility = Visibility.Visible;
                        BottomPlayingSoundsBarGridSplitter.Visibility = FileManager.itemViewHolder.OpenMultipleSounds ? Visibility.Visible : Visibility.Collapsed;
                    }
                }
                else
                {
                    // Show the PlayingSoundsBar and the GridSplitter
                    PlayingSoundsBarColDef.MinWidth = ContentRoot.ActualWidth / 3.8;
                    PlayingSoundsBarColDef.Width = new GridLength(ContentRoot.ActualWidth * FileManager.itemViewHolder.PlayingSoundsBarWidth);
                    GridSplitterColDef.Width = new GridLength(12);

                    // Hide the BottomPlayingSoundsBar
                    BottomPlayingSoundsBar.Translation = new Vector3(-10000, 0, 0);
                    GridSplitterGrid.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                // Hide the PlayingSoundsBar and the GridSplitter
                PlayingSoundsBarColDef.MinWidth = 0;
                PlayingSoundsBarColDef.Width = new GridLength(0);
                GridSplitterColDef.Width = new GridLength(0);

                // Hide the BottomPlayingSoundsBar
                BottomPlayingSoundsBar.Translation = new Vector3(-10000, 0, 0);
                GridSplitterGrid.Visibility = Visibility.Collapsed;
            }

            AdaptSoundListScrollViewerForBottomPlayingSoundsBar();
        }

        private void AdaptSoundListScrollViewerForBottomPlayingSoundsBar()
        {
            double bottomPlayingSoundsBarHeight = 0;

            if (IsMobile && FileManager.itemViewHolder.PlayingSounds.Count > 0)
                bottomPlayingSoundsBarHeight = GridSplitterGridBottomRowDef.ActualHeight + (FileManager.itemViewHolder.PlayingSounds.Count <= 1 ? 0 : 16);

            AdaptSoundListScrollViewerForBottomPlayingSoundsBar(bottomPlayingSoundsBarHeight);
        }

        private void AdaptSoundListScrollViewerForBottomPlayingSoundsBar(double height)
        {
            // Set the padding of the sound GridViews and ListViews, so that the ScrollViewer ends at the bottom bar and the list continues behind the bottom bar
            SoundGridView.Padding = new Thickness(
                SoundGridView.Padding.Left,
                SoundGridView.Padding.Top,
                SoundGridView.Padding.Right,
                height
            );
            FavouriteSoundGridView.Padding = new Thickness(
                FavouriteSoundGridView.Padding.Left,
                FavouriteSoundGridView.Padding.Top,
                FavouriteSoundGridView.Padding.Right,
                height
            );
            SoundListView.Padding = new Thickness(
                SoundListView.Padding.Left,
                SoundListView.Padding.Top,
                SoundListView.Padding.Right,
                height
            );
            FavouriteSoundListView.Padding = new Thickness(
                FavouriteSoundListView.Padding.Left,
                FavouriteSoundListView.Padding.Top,
                FavouriteSoundListView.Padding.Right,
                height
            );
            SoundGridView2.Padding = new Thickness(
                SoundGridView2.Padding.Left,
                SoundGridView2.Padding.Top,
                SoundGridView2.Padding.Right,
                height
            );
            SoundListView2.Padding = new Thickness(
                SoundListView2.Padding.Left,
                SoundListView2.Padding.Top,
                SoundListView2.Padding.Right,
                height
            );
        }

        private async Task ShowBottomPlayingSoundsBar()
        {
            double firstItemHeight = GetFirstBottomPlayingSoundItemContentHeight();
            if (firstItemHeight == 0) return;

            BottomPlayingSoundsBar.Background = Application.Current.Resources["NavigationViewHeaderBackgroundBrush"] as AcrylicBrush;
            BottomPseudoContentGrid.Background = new SolidColorBrush(Colors.Transparent);

            BottomPlayingSoundsBar.Translation = new Vector3(0, (float)firstItemHeight, 0);
            BottomPlayingSoundsBar.Opacity = 1;

            // Animate showing the BottomPlayingSoundsBar
            var translationAnimation = Window.Current.Compositor.CreateVector3KeyFrameAnimation();
            translationAnimation.InsertKeyFrame(1.0f, new Vector3(0));
            translationAnimation.Duration = TimeSpan.FromMilliseconds(300);
            translationAnimation.Target = "Translation";

            BottomPlayingSoundsBar.StartAnimation(translationAnimation);

            await Task.Delay(300);

            // Animate showing the grid splitter
            ShowGridSplitter();

            await Task.Delay(300);

            BottomPlayingSoundsBar.Background = new SolidColorBrush(Colors.Transparent);
            BottomPseudoContentGrid.Background = Application.Current.Resources["NavigationViewHeaderBackgroundBrush"] as AcrylicBrush;
        }

        private async Task HideBottomPlayingSoundsBar()
        {
            BottomPlayingSoundsBar.Background = Application.Current.Resources["NavigationViewHeaderBackgroundBrush"] as AcrylicBrush;
            BottomPseudoContentGrid.Background = new SolidColorBrush(Colors.Transparent);

            double firstItemHeight = GetFirstBottomPlayingSoundItemContentHeight();

            // Animate showing the BottomPlayingSoundsBar
            var translationAnimation = Window.Current.Compositor.CreateVector3KeyFrameAnimation();
            translationAnimation.InsertKeyFrame(1.0f, new Vector3(0, (float)firstItemHeight, 0));
            translationAnimation.Duration = TimeSpan.FromMilliseconds(300);
            translationAnimation.Target = "Translation";

            BottomPlayingSoundsBar.StartAnimation(translationAnimation);

            await Task.Delay(300);

            GridSplitterGrid.Visibility = Visibility.Collapsed;
            BottomPlayingSoundsBar.Background = new SolidColorBrush(Colors.Transparent);
            BottomPseudoContentGrid.Background = Application.Current.Resources["NavigationViewHeaderBackgroundBrush"] as AcrylicBrush;
            BottomPlayingSoundsBar.Translation = new Vector3(-10000, 0, 0);
            BottomPlayingSoundsBar.Height = double.NaN;
        }

        private void ShowGridSplitter()
        {
            var opacityAnimation = Window.Current.Compositor.CreateScalarKeyFrameAnimation();
            opacityAnimation.InsertKeyFrame(1.0f, 1);
            opacityAnimation.Duration = TimeSpan.FromMilliseconds(300);
            opacityAnimation.Target = "Opacity";

            GridSplitterGrid.StartAnimation(opacityAnimation);

            AdaptSoundListScrollViewerForBottomPlayingSoundsBar();
        }

        private async Task HideGridSplitter()
        {
            var opacityAnimation = Window.Current.Compositor.CreateScalarKeyFrameAnimation();
            opacityAnimation.InsertKeyFrame(1.0f, 0);
            opacityAnimation.Duration = TimeSpan.FromMilliseconds(300);
            opacityAnimation.Target = "Opacity";

            BottomPlayingSoundsBarGridSplitter.StartAnimation(opacityAnimation);

            await Task.Delay(300);

            BottomPlayingSoundsBarGridSplitter.Visibility = Visibility.Collapsed;
            BottomPlayingSoundsBarGridSplitter.Opacity = 1;
            AdaptSoundListScrollViewerForBottomPlayingSoundsBar();
        }

        private void SnapBottomPlayingSoundsBar()
        {
            double currentPosition = BottomPlayingSoundsBar.ActualHeight - GridSplitterGridBottomRowDef.MinHeight;
            double maxPosition = GridSplitterGridBottomRowDef.MaxHeight - GridSplitterGridBottomRowDef.MinHeight;

            if (currentPosition < maxPosition / 2)
            {
                StartSnapBottomPlayingSoundsBarAnimation(BottomPlayingSoundsBar.ActualHeight, GridSplitterGridBottomRowDef.MinHeight);
                bottomPlayingSoundsBarPosition = BottomPlayingSoundsBarVerticalPosition.Bottom;
            }
            else
            {
                StartSnapBottomPlayingSoundsBarAnimation(BottomPlayingSoundsBar.ActualHeight, GridSplitterGridBottomRowDef.MaxHeight);
                bottomPlayingSoundsBarPosition = BottomPlayingSoundsBarVerticalPosition.Top;
            }
        }

        private void StartSnapBottomPlayingSoundsBarAnimation(double start, double end)
        {
            if (!playingSoundsLoaded)
                return;

            if (end >= maxBottomPlayingSoundsBarHeight)
                end = maxBottomPlayingSoundsBarHeight;

            if (snapBottomPlayingSoundsBarAnimationRunning)
            {
                SnapBottomPlayingSoundsBarStoryboardAnimation.To = end;
            }
            else
            {
                SnapBottomPlayingSoundsBarStoryboardAnimation.From = start;
                SnapBottomPlayingSoundsBarStoryboardAnimation.To = end;
                SnapBottomPlayingSoundsBarStoryboard.Begin();
                snapBottomPlayingSoundsBarAnimationRunning = true;
            }
        }
    }
}
