﻿using davClassLibrary;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using UniversalSoundboard.Common;
using UniversalSoundboard.DataAccess;
using UniversalSoundboard.Models;
using Windows.Media;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Xaml;

namespace UniversalSoundboard.Components
{
    /**
     * Base class for PlayingSoundItems that are displayed in the PlayingSoundsBar and BottomPlayingSoundsBar
     * 
     * Each PlayingSoundItemTemplate is assigned to a PlayingSoundItem class, so that all logic runs in the PlayingSoundItem
     * and the PlayingSoundItemTemplate only updates the UI.
     * Each PlayingSoundItem can have multiple (2 for both lists) PlayingSoundItemTemplates.
     * 
     * The PlayingSoundItems are stored as a list in ItemViewHolder. Whenever a new PlayingSoundItemTemplate is initialized,
     * it will check if a PlayingSoundItem already exists for the given PlayingSound in the list.
     * If so, the PlayingSoundItemTemplate will subscribe to the PlayingSoundItem and will receive all UI updates.
     * If not, the PlayingSoundItemTemplate will create a new PlayingSoundItem and subscribe to it.
     */
    public class PlayingSoundItem
    {
        public PlayingSound PlayingSound { get; private set; }
        public Guid Uuid { get => PlayingSound == null ? Guid.Empty : PlayingSound.Uuid; }
        public bool SoundsListVisible { get => soundsListVisible; }
        public bool CurrentSoundIsDownloading { get => currentSoundIsDownloading; }
        public TimeSpan CurrentSoundTotalDuration { get => currentSoundTotalDuration; }

        #region Local variables
        private CoreDispatcher dispatcher;
        private bool initialized = false;
        private bool skipSoundsCollectionChanged = false;
        private bool soundsListVisible = false;
        private TimeSpan currentSoundTotalDuration = TimeSpan.Zero;
        private bool showSoundsListAnimationTriggered = false;
        private bool hideSoundsListAnimationTriggered = false;
        private bool currentSoundIsDownloading = false;
        readonly List<(Guid, int)> DownloadProgressList = new List<(Guid, int)>();
        private bool removed = false;

        Timer fadeOutTimer;
        const int fadeOutTime = 300;
        const int fadeOutFrames = 10;
        int currentFadeOutFrame = 0;
        double fadeOutVolumeDiff;

        private Visibility previousButtonVisibility = Visibility.Visible;
        private Visibility nextButtonVisibility = Visibility.Visible;
        private Visibility expandButtonVisibility = Visibility.Visible;

        private SystemMediaTransportControls systemMediaTransportControls;
        #endregion
        
        #region Events
        public event EventHandler<PlaybackStateChangedEventArgs> PlaybackStateChanged;
        public event EventHandler<PositionChangedEventArgs> PositionChanged;
        public event EventHandler<DurationChangedEventArgs> DurationChanged;
        public event EventHandler<CurrentSoundChangedEventArgs> CurrentSoundChanged;
        public event EventHandler<ButtonVisibilityChangedEventArgs> ButtonVisibilityChanged;
        public event EventHandler<ExpandButtonContentChangedEventArgs> ExpandButtonContentChanged;
        public event EventHandler<EventArgs> ShowSoundsList;
        public event EventHandler<EventArgs> HideSoundsList;
        public event EventHandler<FavouriteChangedEventArgs> FavouriteChanged;
        public event EventHandler<VolumeChangedEventArgs> VolumeChanged;
        public event EventHandler<MutedChangedEventArgs> MutedChanged;
        public event EventHandler<EventArgs> ShowPlayingSound;
        public event EventHandler<EventArgs> RemovePlayingSound;
        public event EventHandler<DownloadStatusChangedEventArgs> DownloadStatusChanged;
        #endregion

        public PlayingSoundItem(PlayingSound playingSound, CoreDispatcher dispatcher)
        {
            PlayingSound = playingSound;
            this.dispatcher = dispatcher;
        }

        public void Init()
        {
            if (!initialized)
            {
                initialized = true;

                if (PlayingSound.Current >= PlayingSound.Sounds.Count)
                    PlayingSound.Current = PlayingSound.Sounds.Count - 1;
                else if (PlayingSound.Current < 0)
                    PlayingSound.Current = 0;

                systemMediaTransportControls = SystemMediaTransportControls.GetForCurrentView();
                systemMediaTransportControls.IsPlayEnabled = true;
                systemMediaTransportControls.IsPauseEnabled = true;

                // Subscribe to ItemViewHolder events
                FileManager.itemViewHolder.PropertyChanged += ItemViewHolder_PropertyChanged;
                FileManager.itemViewHolder.SoundDeleted += ItemViewHolder_SoundDeleted;
                FileManager.itemViewHolder.PlayingSoundItemStartSoundsListAnimation += ItemViewHolder_PlayingSoundItemStartSoundsListAnimation;

                // Subscribe to MediaPlayer events
                PlayingSound.MediaPlayer.MediaEnded += MediaPlayer_MediaEnded;
                PlayingSound.MediaPlayer.PlaybackSession.PositionChanged += PlaybackSession_PositionChanged;

                if (PlayingSound.MediaPlayer.Source != null)
                {
                    var mediaSource = (MediaSource)PlayingSound.MediaPlayer.Source;
                    mediaSource.OpenOperationCompleted += PlayingSoundItem_OpenOperationCompleted;

                    if (mediaSource.IsOpen)
                    {
                        // Set the total duration and call the DurationChanged event
                        TimeSpan duration = TimeSpan.FromMinutes(1);

                        try
                        {
                            duration = mediaSource.Duration.GetValueOrDefault();
                        }
                        catch(Exception e) { }

                        currentSoundTotalDuration = duration;
                        DurationChanged?.Invoke(this, new DurationChangedEventArgs(duration));
                    }
                }

                systemMediaTransportControls.ButtonPressed += SystemMediaTransportControls_ButtonPressed;

                // Subscribe to other event handlers
                PlayingSound.Sounds.CollectionChanged += Sounds_CollectionChanged;

                // Stop all other PlayingSounds if this PlayingSound was just started
                if(PlayingSound.StartPlaying)
                    StopAllOtherPlayingSounds();

                if (Dav.IsLoggedIn)
                {
                    // Add each sound file that is not downloaded to the download queue
                    List<int> fileDownloads = new List<int>();

                    for (int i = 0; i < PlayingSound.Sounds.Count; i++)
                    {
                        // Check the download status of the file
                        if (PlayingSound.Sounds[i].AudioFileTableObject.FileDownloadStatus == TableObjectFileDownloadStatus.Downloaded)
                            continue;

                        if (i == 0)
                        {
                            fileDownloads.Add(PlayingSound.Current);
                            continue;
                        }

                        // First, add the next sound
                        int nextSoundIndex = PlayingSound.Current + i;
                        if (nextSoundIndex < PlayingSound.Sounds.Count)
                            fileDownloads.Add(nextSoundIndex);

                        // Second, add the previous sound
                        int previousSoundIndex = PlayingSound.Current - i;
                        if (previousSoundIndex >= 0)
                            fileDownloads.Add(previousSoundIndex);
                    }

                    fileDownloads.Reverse();
                    foreach (int i in fileDownloads)
                        AddSoundToDownloadQueue(i);

                    if (CheckFileDownload())
                    {
                        PlayingSound.MediaPlayer.Pause();
                        SetPlaybackState(false);
                    }
                    else if (PlayingSound.StartPlaying)
                    {
                        PlayingSound.MediaPlayer.Play();
                        SetPlaybackState(true);
                    }
                }
                else if (PlayingSound.StartPlaying)
                {
                    PlayingSound.MediaPlayer.Play();
                    SetPlaybackState(true);
                }
            }

            ShowPlayingSound?.Invoke(this, new EventArgs());

            // Set the correct visibilities and icons for the buttons
            UpdateButtonVisibility();
            UpdateFavouriteFlyoutItem();
            UpdateVolumeControl();
            ExpandButtonContentChanged?.Invoke(this, new ExpandButtonContentChangedEventArgs(soundsListVisible));
            UpdateSystemMediaTransportControls();
        }

        #region ItemViewHolder event handlers
        private void ItemViewHolder_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (PlayingSound == null || PlayingSound.MediaPlayer == null) return;

            if (e.PropertyName == ItemViewHolder.VolumeKey)
                PlayingSound.MediaPlayer.Volume = (double)PlayingSound.Volume / 100 * FileManager.itemViewHolder.Volume / 100;
            else if (e.PropertyName == ItemViewHolder.MutedKey)
                PlayingSound.MediaPlayer.IsMuted = PlayingSound.Muted || FileManager.itemViewHolder.Muted;
        }

        private void ItemViewHolder_SoundDeleted(object sender, SoundEventArgs e)
        {
            // Find the deleted sound in the Sounds list of the PlayingSound
            int i = PlayingSound.Sounds.ToList().FindIndex(s => s.Uuid.Equals(e.Uuid));
            if (i == -1) return;

            // Remove the sound from the list
            PlayingSound.Sounds.RemoveAt(i);
        }

        private void ItemViewHolder_PlayingSoundItemStartSoundsListAnimation(object sender, EventArgs e)
        {
            if (showSoundsListAnimationTriggered)
            {
                showSoundsListAnimationTriggered = false;
                ShowSoundsList?.Invoke(this, new EventArgs());
            }
            else if (hideSoundsListAnimationTriggered)
            {
                hideSoundsListAnimationTriggered = false;
                HideSoundsList?.Invoke(this, new EventArgs());
            }
        }
        #endregion
        
        #region MediaPlayer event handlers
        private async void MediaPlayer_MediaEnded(MediaPlayer sender, object args)
        {
            await dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                // Check if the end of the sounds list was reached
                if (PlayingSound.Current + 1 < PlayingSound.Sounds.Count)
                {
                    // Move to the next sound
                    await MoveToSound(PlayingSound.Current + 1, true);
                }
                else
                {
                    // Move to the beginning of the sounds list
                    PlayingSound.Repetitions--;

                    if (PlayingSound.Repetitions <= 0)
                    {
                        // Remove the PlayingSound
                        TriggerRemove();
                        return;
                    }

                    // Set the new repetitions
                    await FileManager.SetRepetitionsOfPlayingSoundAsync(PlayingSound.Uuid, PlayingSound.Repetitions);

                    if (PlayingSound.Sounds.Count > 1)
                    {
                        // If randomly is true, shuffle sounds
                        if (PlayingSound.Randomly)
                        {
                            Random random = new Random();
                            skipSoundsCollectionChanged = true;
                            int soundsCount = PlayingSound.Sounds.Count;

                            // Copy the sounds
                            List<Sound> sounds = new List<Sound>();
                            foreach (var sound in PlayingSound.Sounds)
                                sounds.Add(sound);

                            PlayingSound.Sounds.Clear();

                            // Add the sounds in random order
                            for (int i = 0; i < soundsCount; i++)
                            {
                                // Take a random sound from the sounds list and add it to the original sounds list
                                int randomIndex = random.Next(sounds.Count);

                                PlayingSound.Sounds.Add(sounds.ElementAt(randomIndex));
                                sounds.RemoveAt(randomIndex);
                            }

                            // Set the new sound order
                            await FileManager.SetSoundsListOfPlayingSoundAsync(PlayingSound.Uuid, PlayingSound.Sounds.ToList());

                            skipSoundsCollectionChanged = false;
                        }

                        await MoveToSound(0, true);
                    }
                    else
                    {
                        PlayingSound.MediaPlayer.PlaybackSession.Position = TimeSpan.Zero;
                        PlayingSound.MediaPlayer.Play();
                        SetPlaybackState(true);
                    }
                }
            });
        }

        private async void PlaybackSession_PositionChanged(MediaPlaybackSession sender, object args)
        {
            await dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                PositionChanged?.Invoke(this, new PositionChangedEventArgs(sender.Position));
            });
        }

        private async void PlayingSoundItem_OpenOperationCompleted(MediaSource sender, MediaSourceOpenOperationCompletedEventArgs args)
        {
            PlayingSound.MediaPlayer.PlaybackSession.Position = TimeSpan.Zero;

            await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                currentSoundTotalDuration = sender.Duration.GetValueOrDefault();
                DurationChanged?.Invoke(this, new DurationChangedEventArgs(sender.Duration.GetValueOrDefault()));
            });
        }

        private async void SystemMediaTransportControls_ButtonPressed(SystemMediaTransportControls sender, SystemMediaTransportControlsButtonPressedEventArgs args)
        {
            if (!PlayingSound.Uuid.Equals(FileManager.itemViewHolder.ActivePlayingSound)) return;

            await dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                switch (args.Button)
                {
                    case SystemMediaTransportControlsButton.Previous:
                        await MoveToPrevious();
                        break;
                    case SystemMediaTransportControlsButton.Next:
                        await MoveToNext();
                        break;
                    default:
                        SetPlayPause(args.Button == SystemMediaTransportControlsButton.Play);
                        break;
                }
            });
        }
        #endregion

        #region Other event handlers
        private async void Sounds_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (PlayingSound == null || PlayingSound.MediaPlayer == null || skipSoundsCollectionChanged) return;

            if (e.Action == NotifyCollectionChangedAction.Remove)
            {
                if (PlayingSound.Sounds.Count == 0)
                {
                    // Delete the PlayingSound
                    await Remove();
                    return;
                }

                if(e.OldStartingIndex < PlayingSound.Current)
                {
                    PlayingSound.Current--;
                    CurrentSoundChanged?.Invoke(this, new CurrentSoundChangedEventArgs(PlayingSound.Current));
                    await FileManager.SetCurrentOfPlayingSoundAsync(PlayingSound.Uuid, PlayingSound.Current);
                }
                else if(e.OldStartingIndex == PlayingSound.Current)
                {
                    if(PlayingSound.Current == PlayingSound.Sounds.Count)
                    {
                        // Move to the previous sound
                        await MoveToSound(PlayingSound.Current - 1);
                    }
                    else
                    {
                        // Move to the new current sound
                        await MoveToSound(PlayingSound.Current);
                    }
                }
                else if (PlayingSound.Sounds.Count == 1)
                {
                    if (e.OldStartingIndex == 0 && PlayingSound.Current == 0)
                        await MoveToSound(0);
                }

                // Update the PlayingSound
                await FileManager.SetSoundsListOfPlayingSoundAsync(PlayingSound.Uuid, PlayingSound.Sounds.ToList());
            }
        }
        #endregion

        #region Functionality
        public async Task MoveToSound(int index, bool startPlaying = false)
        {
            if (
                PlayingSound.Sounds.Count == 0
                || index >= PlayingSound.Sounds.Count
                || index < 0
            ) return;

            // Update PlayingSound.Current
            PlayingSound.Current = index;

            // Update the text of the current sound
            CurrentSoundChanged?.Invoke(
                this,
                new CurrentSoundChangedEventArgs(index)
            );

            // Update the visibility of the buttons
            UpdateButtonVisibility();
            UpdateFavouriteFlyoutItem();

            // Update the SystemMediaTransportControls
            UpdateSystemMediaTransportControls();

            if (CheckFileDownload()) return;

            // Set the new source of the MediaPlayer
            var audioFile = PlayingSound.Sounds[PlayingSound.Current].AudioFile;
            if (audioFile == null)
            {
                await MoveToNext();
                return;
            }
            var filePath = audioFile.Path;

            var newSource = MediaSource.CreateFromUri(new Uri(filePath));
            newSource.OpenOperationCompleted += PlayingSoundItem_OpenOperationCompleted;

            bool wasPlaying = PlayingSound.MediaPlayer.PlaybackSession.PlaybackState == MediaPlaybackState.Playing;
            PlayingSound.MediaPlayer.PlaybackSession.Position = TimeSpan.Zero;
            PlayingSound.MediaPlayer.Source = newSource;
            if (wasPlaying || startPlaying)
            {
                PlayingSound.MediaPlayer.Play();
                SetPlaybackState(true);
            }

            // Save the new Current
            await FileManager.SetCurrentOfPlayingSoundAsync(PlayingSound.Uuid, index);
        }

        /**
         * Stops all other PlayingSounds if MultiSoundPlayback is disabled
         */
        private void StopAllOtherPlayingSounds()
        {
            if (FileManager.itemViewHolder.MultiSoundPlayback || !FileManager.itemViewHolder.OpenMultipleSounds) return;

            // Cause all other PlayingSounds to stop playback
            foreach (var playingSoundItem in FileManager.itemViewHolder.PlayingSoundItems)
            {
                if (playingSoundItem.PlayingSound.MediaPlayer == null || playingSoundItem.Uuid.Equals(PlayingSound.Uuid)) continue;
                playingSoundItem.PlayingSound.MediaPlayer.Pause();
                playingSoundItem.SetPlaybackState(false);
            }
        }

        private void SetPlaybackState(bool isPlaying)
        {
            PlaybackStateChanged?.Invoke(
                this,
                new PlaybackStateChangedEventArgs(isPlaying)
            );

            systemMediaTransportControls.PlaybackStatus = isPlaying ? MediaPlaybackStatus.Playing : MediaPlaybackStatus.Paused;
        }

        private void UpdateButtonVisibility()
        {
            previousButtonVisibility = PlayingSound.Current > 0 ? Visibility.Visible : Visibility.Collapsed;
            nextButtonVisibility = PlayingSound.Current != PlayingSound.Sounds.Count - 1 ? Visibility.Visible : Visibility.Collapsed;
            expandButtonVisibility = PlayingSound.Sounds.Count > 1 ? Visibility.Visible : Visibility.Collapsed;

            TriggerButtonVisibilityChangedEvent();
        }

        private void UpdateFavouriteFlyoutItem()
        {
            if (PlayingSound == null) return;

            Sound currentSound = PlayingSound.Sounds.ElementAt(PlayingSound.Current);
            FavouriteChanged?.Invoke(this, new FavouriteChangedEventArgs(currentSound.Favourite));
        }

        private void UpdateVolumeControl()
        {
            if (PlayingSound == null) return;

            VolumeChanged?.Invoke(
                this,
                new VolumeChangedEventArgs(PlayingSound.Volume)
            );
            MutedChanged?.Invoke(
                this,
                new MutedChangedEventArgs(PlayingSound.Muted)
            );
        }

        private void TriggerButtonVisibilityChangedEvent()
        {
            ButtonVisibilityChanged?.Invoke(
                this,
                new ButtonVisibilityChangedEventArgs(
                    previousButtonVisibility,
                    nextButtonVisibility,
                    expandButtonVisibility
                )
            );
        }

        private void UpdateSystemMediaTransportControls()
        {
            if (PlayingSound.Current >= PlayingSound.Sounds.Count)
            {
                // Disable the SystemMediaTransportControls
                systemMediaTransportControls.IsEnabled = false;
                return;
            }
            
            Sound currentSound = PlayingSound.Sounds[PlayingSound.Current];
            systemMediaTransportControls.IsEnabled = true;

            var updater = systemMediaTransportControls.DisplayUpdater;
            updater.ClearAll();

            updater.Type = MediaPlaybackType.Music;
            updater.MusicProperties.Title = currentSound.Name;

            if (currentSound.Categories.Count > 0)
                updater.MusicProperties.Artist = currentSound.Categories.First().Name;

            if (currentSound.ImageFileTableObject != null && currentSound.ImageFile != null)
                updater.Thumbnail = RandomAccessStreamReference.CreateFromFile(currentSound.ImageFile);

            updater.Update();

            systemMediaTransportControls.IsPreviousEnabled = PlayingSound.Current != 0 && PlayingSound.Sounds.Count > 1;
            systemMediaTransportControls.IsNextEnabled = PlayingSound.Current != PlayingSound.Sounds.Count - 1;

            FileManager.itemViewHolder.ActivePlayingSound = PlayingSound.Uuid;
        }

        private void AddSoundToDownloadQueue(int i)
        {
            if (PlayingSound.Sounds[i].AudioFileTableObject.FileDownloadStatus != TableObjectFileDownloadStatus.Downloaded)
            {
                DownloadProgressList.Add((PlayingSound.Sounds[i].AudioFileTableObject.Uuid, -2));
                PlayingSound.Sounds[i].AudioFileTableObject.ScheduleFileDownload(new Progress<(Guid, int)>(DownloadProgress));
            }
        }

        /**
         * Checks the current sound file download status and returns true, if the file is currently downloading
         */
        private bool CheckFileDownload()
        {
            Guid currentSoundUuid = PlayingSound.Sounds[PlayingSound.Current].AudioFileTableObject.Uuid;

            // Get the current sound file download progress
            int i = DownloadProgressList.FindIndex(progress => progress.Item1.Equals(currentSoundUuid));
            if (i == -1)
            {
                DownloadStatusChanged?.Invoke(
                    this,
                    new DownloadStatusChangedEventArgs(false, 0)
                );
                currentSoundIsDownloading = false;
                return false;
            }

            int value = DownloadProgressList[i].Item2;

            if (value == -1 || value == 101)
            {
                DownloadStatusChanged?.Invoke(
                    this,
                    new DownloadStatusChangedEventArgs(false, 0)
                );
                currentSoundIsDownloading = false;
                return false;
            }

            // Update the download progress bar for the current sound
            DownloadStatusChanged?.Invoke(
                this,
                new DownloadStatusChangedEventArgs(true, value)
            );
            currentSoundIsDownloading = true;

            return true;
        }

        private async void DownloadProgress((Guid, int) value)
        {
            if (PlayingSound == null || PlayingSound.MediaPlayer == null) return;

            // Find the file in the download progress list
            int i = DownloadProgressList.FindIndex(progress => progress.Item1.Equals(value.Item1));
            if (i == -1) return;

            // Update the progress in the download progress list
            DownloadProgressList[i] = value;

            // Check if the download progress belongs to the current sound
            if (PlayingSound.Sounds[PlayingSound.Current].AudioFileTableObject.Uuid.Equals(value.Item1))
            {
                // Show the download progress bar as the file is still downloading
                DownloadStatusChanged?.Invoke(
                    this,
                    new DownloadStatusChangedEventArgs(true, value.Item2)
                );

                if (value.Item2 == 101)
                {
                    currentSoundIsDownloading = false;
                    await Task.Delay(5);

                    // Set the source of the current sound
                    var audioFile = PlayingSound.Sounds[PlayingSound.Current].AudioFile;
                    if(audioFile != null)
                    {
                        var newSource = MediaSource.CreateFromUri(new Uri(audioFile.Path));
                        newSource.OpenOperationCompleted += PlayingSoundItem_OpenOperationCompleted;
                        PlayingSound.MediaPlayer.Source = newSource;

                        // Start the current sound
                        if (PlayingSound.StartPlaying)
                        {
                            PlayingSound.MediaPlayer.Play();
                            SetPlaybackState(true);
                            StopAllOtherPlayingSounds();
                        }
                    }
                }
                else if (value.Item2 == -1)
                {
                    currentSoundIsDownloading = false;

                    // Move to the next sound
                    await MoveToNext();
                }
                else
                {
                    currentSoundIsDownloading = true;
                    return;
                }
            }
            else if (value.Item2 != 101 && value.Item2 != -1)
                return;

            // The file (not the current file) was successfully downloaded
            // Remove the file download progress from the list
            DownloadProgressList.RemoveAt(i);
        }

        private void StartFadeOut()
        {
            if (PlayingSound == null || PlayingSound.MediaPlayer == null) return;
            if (PlayingSound.MediaPlayer.PlaybackSession.PlaybackState != MediaPlaybackState.Playing) return;

            currentFadeOutFrame = 0;
            double interval = fadeOutTime / (double)fadeOutFrames;
            fadeOutVolumeDiff = PlayingSound.MediaPlayer.Volume / fadeOutFrames;

            fadeOutTimer = new Timer();
            fadeOutTimer.Elapsed += (object sender, ElapsedEventArgs e) => FadeOut();

            fadeOutTimer.Interval = interval;
            fadeOutTimer.Start();

            FadeOut();
        }

        private void FadeOut()
        {
            if (currentFadeOutFrame >= fadeOutFrames || PlayingSound.MediaPlayer == null)
            {
                if (PlayingSound.MediaPlayer != null)
                {
                    PlayingSound.MediaPlayer.Pause();
                    PlayingSound.MediaPlayer.PlaybackSession.Position = TimeSpan.Zero;
                }

                fadeOutTimer.Stop();
            }
            else
            {
                // Decrease the volume
                PlayingSound.MediaPlayer.Volume -= fadeOutVolumeDiff;
                currentFadeOutFrame++;
            }
        }
        #endregion

        #region Public methods
        public async Task ReloadPlayingSound()
        {
            var playingSound = await FileManager.GetPlayingSoundAsync(PlayingSound.Uuid);
            if (playingSound == null) return;
            if (PlayingSound.MediaPlayer.PlaybackSession.PlaybackState == MediaPlaybackState.Playing) return;

            // Update Current
            if(PlayingSound.Current != playingSound.Current)
                await MoveToSound(playingSound.Current);

            // Update Volume
            if (PlayingSound.Volume != playingSound.Volume)
            {
                PlayingSound.Volume = playingSound.Volume;
                VolumeChanged?.Invoke(
                    this,
                    new VolumeChangedEventArgs(PlayingSound.Volume)
                );
            }

            // Update Muted
            if(PlayingSound.Muted != playingSound.Muted)
            {
                PlayingSound.Muted = playingSound.Muted;
                PlayingSound.MediaPlayer.IsMuted = playingSound.Muted || FileManager.itemViewHolder.Muted;
                MutedChanged?.Invoke(
                    this,
                    new MutedChangedEventArgs(playingSound.Muted)
                );
            }

            // Update Repetitions
            if (PlayingSound.Repetitions != playingSound.Repetitions)
                PlayingSound.Repetitions = playingSound.Repetitions;

            // Update Sounds
            // Check if the sounds have changed
            bool soundsChanged = PlayingSound.Sounds.Count != playingSound.Sounds.Count;

            if (!soundsChanged)
            {
                int i = 0;
                foreach(var sound in PlayingSound.Sounds)
                {
                    if (!sound.Uuid.Equals(playingSound.Sounds[i].Uuid))
                    {
                        soundsChanged = true;
                        break;
                    }

                    i++;
                }
            }

            if (soundsChanged)
            {
                // Reload the sounds
                PlayingSound.Sounds.Clear();
                foreach (var sound in playingSound.Sounds)
                    PlayingSound.Sounds.Add(sound);
            }
        }

        /**
         * Toggles the MediaPlayer from Playing -> Paused or from Paused -> Playing
         */
        public void TogglePlayPause()
        {
            if (PlayingSound == null || PlayingSound.MediaPlayer == null) return;
            SetPlayPause(PlayingSound.MediaPlayer.PlaybackSession.PlaybackState == MediaPlaybackState.Paused);
        }

        /**
         * Plays or pauses the MediaPlayer
         */
        public void SetPlayPause(bool play)
        {
            if (PlayingSound == null || PlayingSound.MediaPlayer == null) return;

            // Check if the file is currently downloading
            if (currentSoundIsDownloading)
            {
                PlayingSound.MediaPlayer.Pause();
                PlayingSound.StartPlaying = play;
                PlaybackStateChanged?.Invoke(
                    this,
                    new PlaybackStateChangedEventArgs(PlayingSound.StartPlaying)
                );
                return;
            }

            if (play)
            {
                PlayingSound.MediaPlayer.Play();
                SetPlaybackState(true);
                StopAllOtherPlayingSounds();
            }
            else
            {
                PlayingSound.MediaPlayer.Pause();
                SetPlaybackState(false);
            }

            UpdateSystemMediaTransportControls();
        }

        public async Task MoveToPrevious()
        {
            if (PlayingSound == null || PlayingSound.MediaPlayer == null) return;

            if (PlayingSound.MediaPlayer.PlaybackSession.Position.Seconds >= 5)
            {
                // Move to the start of the sound
                PlayingSound.MediaPlayer.PlaybackSession.Position = TimeSpan.Zero;
            }
            else
            {
                // Move to the previous sound
                await MoveToSound(PlayingSound.Current - 1);
            }
        }

        public async Task MoveToNext()
        {
            if (PlayingSound == null || PlayingSound.MediaPlayer == null) return;
            await MoveToSound(PlayingSound.Current + 1);
        }

        public void ExpandSoundsList(double heightDifference)
        {
            if (soundsListVisible) return;
            soundsListVisible = true;
            showSoundsListAnimationTriggered = true;

            // Trigger the event to start the animation and wait for SoundPage to start the animation
            FileManager.itemViewHolder.TriggerPlayingSoundItemShowSoundsListAnimationStartedEvent(
                this,
                new PlayingSoundItemEventArgs(
                    PlayingSound.Uuid,
                    heightDifference
                )
            );

            // Update the ExpandButton content
            ExpandButtonContentChanged?.Invoke(this, new ExpandButtonContentChangedEventArgs(true));
        }

        public void CollapseSoundsList(double heightDifference)
        {
            if (!soundsListVisible) return;
            soundsListVisible = false;
            hideSoundsListAnimationTriggered = true;

            // Trigger the event to start the animation and wait for SoundPage to start the animation
            FileManager.itemViewHolder.TriggerPlayingSoundItemHideSoundsListAnimationStartedEvent(
                this,
                new PlayingSoundItemEventArgs(
                    PlayingSound.Uuid,
                    heightDifference
                )
            );

            // Update the ExpandButton content
            ExpandButtonContentChanged?.Invoke(this, new ExpandButtonContentChangedEventArgs(false));
        }

        public void SetPosition(int position)
        {
            if (PlayingSound == null || PlayingSound.MediaPlayer == null) return;
            if (position > currentSoundTotalDuration.TotalSeconds - 1) return;

            PlayingSound.MediaPlayer.PlaybackSession.Position = new TimeSpan(0, 0, position);
        }

        public async Task SetVolume(int volume)
        {
            PlayingSound.Volume = volume;
            VolumeChanged?.Invoke(
                this,
                new VolumeChangedEventArgs(volume)
            );

            // Save the new volume
            await FileManager.SetVolumeOfPlayingSoundAsync(PlayingSound.Uuid, volume);
        }

        public async Task SetMuted(bool muted)
        {
            PlayingSound.Muted = muted;
            PlayingSound.MediaPlayer.IsMuted = muted || FileManager.itemViewHolder.Muted;
            MutedChanged?.Invoke(
                this,
                new MutedChangedEventArgs(muted)
            );

            // Save the new muted
            await FileManager.SetMutedOfPlayingSoundAsync(PlayingSound.Uuid, muted);
        }

        public async Task SetRepetitions(int repetitions)
        {
            PlayingSound.Repetitions = repetitions;
            await FileManager.SetRepetitionsOfPlayingSoundAsync(PlayingSound.Uuid, repetitions);
        }

        public async Task ToggleFavourite()
        {
            Sound currentSound = PlayingSound.Sounds.ElementAt(PlayingSound.Current);
            currentSound.Favourite = !currentSound.Favourite;

            // Save the new favourite and reload the sound
            await FileManager.SetSoundAsFavouriteAsync(currentSound.Uuid, currentSound.Favourite);
            await FileManager.ReloadSound(currentSound.Uuid);

            FavouriteChanged?.Invoke(this, new FavouriteChangedEventArgs(currentSound.Favourite));
        }

        public void TriggerRemove()
        {
            // Stop and reset the MediaPlayer
            StartFadeOut();

            // Start the remove animation
            RemovePlayingSound?.Invoke(this, new EventArgs());

            // Trigger the animation in SoundPage for the BottomPlayingSoundsBar
            FileManager.itemViewHolder.TriggerRemovePlayingSoundItemStartedEvent(this, new PlayingSoundItemEventArgs(PlayingSound.Uuid));
        }

        public async Task Remove()
        {
            if (removed) return;
            removed = true;

            // Remove the PlayingSound from the list
            FileManager.itemViewHolder.PlayingSounds.Remove(PlayingSound);

            // Remove this PlayingSoundItem from the PlayingSoundItems list
            FileManager.itemViewHolder.PlayingSoundItems.Remove(this);

            if (PlayingSound.MediaPlayer != null)
            {
                PlayingSound.MediaPlayer.Pause();
                PlayingSound.MediaPlayer.PlaybackSession.Position = TimeSpan.Zero;
            }

            // Remove the sound from the SystemMediaTransportControls
            systemMediaTransportControls.IsEnabled = false;

            // Update the BottomPlayingSoundsBar in SoundPage
            FileManager.itemViewHolder.TriggerRemovePlayingSoundItemEndedEvent(this, new PlayingSoundItemEventArgs(PlayingSound.Uuid));

            // Delete the PlayingSound
            await FileManager.DeletePlayingSoundAsync(PlayingSound.Uuid);
        }

        public void RemoveSound(Guid uuid)
        {
            int index = PlayingSound.Sounds.ToList().FindIndex(s => s.Uuid.Equals(uuid));
            if (index == -1) return;

            PlayingSound.Sounds.RemoveAt(index);
        }
        #endregion
    }
}
