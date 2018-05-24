﻿using davClassLibrary;
using davClassLibrary.Common;
using davClassLibrary.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using UniversalSoundboard.Common;
using UniversalSoundBoard.Common;
using UniversalSoundBoard.DataAccess;
using UniversalSoundBoard.Models;
using UniversalSoundBoard.Pages;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.DataTransfer;
using Windows.ApplicationModel.DataTransfer.ShareTarget;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace UniversalSoundBoard
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    sealed partial class App : Application
    {

        public ItemViewHolder _itemViewHolder = new ItemViewHolder {
            title = (new Windows.ApplicationModel.Resources.ResourceLoader()).GetString("AllSounds"),
            progressRingIsActive = false,
            sounds = new ObservableCollection<Sound>(),
            favouriteSounds = new ObservableCollection<Sound>(),
            allSounds = new ObservableCollection<Sound>(),
            allSoundsChanged = true,
            categories = new ObservableCollection<Category>(),
            searchQuery = "",
            editButtonVisibility = Visibility.Collapsed,
            playAllButtonVisibility = Visibility.Collapsed,
            selectionMode = ListViewSelectionMode.None,
            normalOptionsVisibility = true,
            selectedSounds = new List<Sound>(),
            playingSounds = new ObservableCollection<PlayingSound>(),
            playingSoundsListVisibility = Visibility.Visible,
            playOneSoundAtOnce = FileManager.playOneSoundAtOnce,
            showCategoryIcon = FileManager.showCategoryIcon,
            showSoundsPivot = FileManager.showSoundsPivot,
            savePlayingSounds = FileManager.savePlayingSounds,
            isExporting = false,
            exported = false,
            isImporting = false,
            imported = false,
            areExportAndImportButtonsEnabled = true,
            exportMessage = "",
            importMessage = "",
            soundboardSize = "",
            windowTitleMargin = new Thickness(12, 7, 0, 0),
            searchAutoSuggestBoxVisibility = true,
            volumeButtonVisibility = true,
            addButtonVisibility = true,
            selectButtonVisibility = true,
            searchButtonVisibility = false,
            cancelButtonVisibility = false,
            shareButtonVisibility = false,
            moreButtonVisibility = true,
            topButtonsCollapsed = false,
            areSelectButtonsEnabled = false,
            selectedCategory = 0,
            upgradeDataStatusText = "Preparing...",
            user = null,
            loginMenuItemVisibility = true
        };
        
        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            Microsoft.ApplicationInsights.WindowsAppInitializer.InitializeAsync(
                Microsoft.ApplicationInsights.WindowsCollectors.Metadata |
                Microsoft.ApplicationInsights.WindowsCollectors.Session);
            InitializeComponent();
            Suspending += OnSuspending;

            // Set dark theme
            var localSettings = ApplicationData.Current.LocalSettings;
            
            if(localSettings.Values["theme"] != null)
            {
                switch ((string)localSettings.Values["theme"])
                {
                    case "dark":
                        RequestedTheme = ApplicationTheme.Dark;
                        break;
                    case "light":
                        RequestedTheme = ApplicationTheme.Light;
                        break;
                }
            }
            else
            {
                localSettings.Values["theme"] = FileManager.theme;
            }

            // Initialize Dav settings
            ProjectInterface.RetrieveConstants = new RetrieveConstants();
            ProjectInterface.LocalDataSettings = new LocalDataSettings();
            (App.Current as App)._itemViewHolder.user = new DavUser();

            FileManager.CreateCategoriesObservableCollection();
        }

        /// <summary>
        /// Invoked when the application is launched normally by the end user.  Other entry points
        /// will be used such as when the application is launched to open a specific file.
        /// </summary>
        /// <param name="e">Details about the launch request and process.</param>
        protected override async void OnLaunched(LaunchActivatedEventArgs e)
        {
            Frame rootFrame = Window.Current.Content as Frame;

            // Do not repeat app initialization when the Window already has content,
            // just ensure that the window is active
            if (rootFrame == null)
            {
                // Create a Frame to act as the navigation context and navigate to the first page
                rootFrame = new Frame();

                rootFrame.NavigationFailed += OnNavigationFailed;

                if (e.PreviousExecutionState == ApplicationExecutionState.Terminated)
                {
                    //TODO: Load state from previously suspended application
                }

                // Place the frame in the current Window
                Window.Current.Content = rootFrame;
            }

            if (e.PrelaunchActivated == false)
            {
                if (rootFrame.Content == null)
                {
                    // When the navigation stack isn't restored navigate to the first page,
                    // configuring the new page by passing required information as a navigation
                    // parameter
                    rootFrame.Navigate(typeof(MainPage), e.Arguments);
                }
                // Ensure the current window is active
                Window.Current.Activate();
            }

            if (e.PreviousExecutionState != ApplicationExecutionState.Running)
            {
                if(await FileManager.UsesOldDataModel())
                {
                    bool loadState = (e.PreviousExecutionState == ApplicationExecutionState.Terminated);
                    UpgradeDataSplashScreen upgradeDataSplashScreen = new UpgradeDataSplashScreen(e.SplashScreen, loadState);
                    Window.Current.Content = upgradeDataSplashScreen;
                }
            }
            
            // Check if app was launched from a secondary tile
            if (!String.IsNullOrEmpty(e.Arguments))
            {
                Guid soundUuid = FileManager.ConvertStringToGuid(e.Arguments);
                SoundPage.PlaySound(await FileManager.GetSound(soundUuid));
            }

            Window.Current.Activate();
        }

        private Frame CreateRootFrame(ApplicationExecutionState previousExecutionState, string arguments, Type page)
        {
            Frame rootFrame = Window.Current.Content as Frame;

            // Do not repeat app initialization when the Window already has content,
            // just ensure that the window is active
            if (rootFrame == null)
            {
                Debug.WriteLine("CreateFrame: Initializing root frame ...");

                // Create a Frame to act as the navigation context and navigate to the first page
                rootFrame = new Frame();

                // Set the default language
                rootFrame.Language = Windows.Globalization.ApplicationLanguages.Languages[0];

                rootFrame.NavigationFailed += OnNavigationFailed;

                if (previousExecutionState == ApplicationExecutionState.Terminated)
                {
                    //TODO: Load state from previously suspended application
                }

                // Place the frame in the current Window
                Window.Current.Content = rootFrame;
            }

            return rootFrame;
        }

        /// <summary>
        /// Invoked when Navigation to a certain page fails
        /// </summary>
        /// <param name="sender">The Frame which failed navigation</param>
        /// <param name="e">Details about the navigation failure</param>
        void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }

        /// <summary>
        /// Invoked when application execution is being suspended.  Application state is saved
        /// without knowing whether the application will be terminated or resumed with the contents
        /// of memory still intact.
        /// </summary>
        /// <param name="sender">The source of the suspend request.</param>
        /// <param name="e">Details about the suspend request.</param>
        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            //TODO: Save application state and stop any background activity
            deferral.Complete();
        }

        protected override void OnShareTargetActivated(ShareTargetActivatedEventArgs args)
        {
            //var rootFrame = CreateRootFrame(args.PreviousExecutionState, "");
            //Frame rootFrame = Window.Current.Content as Frame;

            if (args.PreviousExecutionState == ApplicationExecutionState.NotRunning)
            {
                ShareOperation shareOperation = args.ShareOperation;
                if (shareOperation.Data.Contains(StandardDataFormats.StorageItems))
                {
                    shareOperation.ReportDataRetrieved();

                    Frame frame = new Frame();
                    frame.Navigate(typeof(ShareTargetPage), shareOperation);

                    Window.Current.Content = frame;
                    Window.Current.Activate();
                }
                else
                {
                    shareOperation.ReportError("An error occured.");
                }
            }
            else
            {
                // App is running
                ShareOperation shareOperation = args.ShareOperation;
                if (shareOperation.Data.Contains(StandardDataFormats.StorageItems))
                {
                    shareOperation.ReportDataRetrieved();

                    var rootFrame = CreateRootFrame(args.PreviousExecutionState, "", typeof(ShareTargetPage));
                    rootFrame.Navigate(typeof(ShareTargetPage), shareOperation);
                    Window.Current.Activate();
                }
                else
                {
                    shareOperation.ReportError("An error occured.");
                }
            }
        }
    }
}
