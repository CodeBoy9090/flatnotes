﻿using FlatNotes.Common;
using FlatNotes.Models;
using FlatNotes.Utils;
using FlatNotes.ViewModels;
using System;
using System.Collections.Specialized;
using System.Linq;
using Windows.ApplicationModel.Resources;
using Windows.Foundation;
using Windows.Foundation.Metadata;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace FlatNotes.Views
{
#if WINDOWS_PHONE_APP
    public sealed partial class NoteEditPage : Page, IFileOpenPickerContinuable
#else
    public sealed partial class NoteEditPage : Page
#endif
    {
        public static event EventHandler NoteUnloaded;

        public NoteEditViewModel viewModel { get { return _viewModel; } }
        private static NoteEditViewModel _viewModel = NoteEditViewModel.Instance;

        public NavigationHelper NavigationHelper { get { return this.navigationHelper; } }
        private NavigationHelper navigationHelper;

        private static Brush previousBackground;
        private bool checklistChanged = false;
        private bool openImagePicker = false;
        private bool clickedOnReminderSaveOrCancelButtons = false;

#if WINDOWS_PHONE_APP
        partial void EnableSwipeFeature(FrameworkElement element, FrameworkElement referenceFrame);
        partial void DisableSwipeFeature(FrameworkElement element);
#endif

        public NoteEditPage()
        {
            this.InitializeComponent();

            App.RootFrame.SizeChanged += (s, e) => OnWindowSizeChanged(e.NewSize);
            OnWindowSizeChanged(App.RootFrame.RenderSize);

            //Navigation Helper
            this.navigationHelper = new NavigationHelper(this);
            this.navigationHelper.LoadState += this.OnLoadState;
            this.navigationHelper.SaveState += (s, e) => this.OnSaveState();

            this.Loaded += (s, e) => OnLoaded();
            this.Unloaded += (s, e) => OnUnloaded();

#if WINDOWS_PHONE_APP
            //Color Picker WP81
            ColorPickerAppBarToggleButton.Checked += (s, _e) => NoteColorPicker.Open();
            ColorPickerAppBarToggleButton.Unchecked += (s, _e) => NoteColorPicker.Close();
            NoteColorPicker.Opened += (s, _e) => { ColorPickerAppBarToggleButton.IsChecked = true; };
            NoteColorPicker.Closed += (s, _e) => { ColorPickerAppBarToggleButton.IsChecked = false; };
#endif
        }

        private void OnLoaded()
        {
            if (openImagePicker)
                viewModel.OpenImagePicker();

            if (viewModel.Note.IsEmpty() && !openImagePicker)
                NoteTitleTextBox.Focus(FocusState.Programmatic);

            if (ApiInformation.IsTypePresent("FlyoutBaseClosingEventArgs"))
            {
                Flyout.GetAttachedFlyout(ReminderAppBarButton).Closing += Flyout_Closing;
                Flyout.GetAttachedFlyout(ReminderAppBarButton).Closed += Flyout_Closed;
            }
        }

        private void OnUnloaded()
        {
            while (Frame.CanGoBack) Frame.GoBack();
            Frame.BackStack.Clear();
            NoteUnloaded?.Invoke(this, EventArgs.Empty);
            OnSaveState();

            if (ApiInformation.IsTypePresent("FlyoutBaseClosingEventArgs"))
            {
                Flyout.GetAttachedFlyout(ReminderAppBarButton).Closing -= Flyout_Closing;
                Flyout.GetAttachedFlyout(ReminderAppBarButton).Closed -= Flyout_Closed;
            }
        }

        private void OnLoadState(object sender, LoadStateEventArgs e)
        {
            OnColorChanged();
            openImagePicker = false;

            viewModel.PropertyChanged += OnViewModelPropertyChanged;
            
            if (e.NavigationParameter != null && e.NavigationParameter is Note)
            {
                viewModel.Note = e.NavigationParameter as Note;
            }
            else if (e.NavigationParameter != null && e.NavigationParameter is NoteImage)
            {
                openImagePicker = true;
                viewModel.Note = new Note();
            }
            else
            {
                viewModel.Note = new Note();
            }

            viewModel.Note.Changed = false;
            viewModel.Note.Images.CollectionChanged += Images_CollectionChanged;
            viewModel.Note.Checklist.CollectionChanged += Checklist_CollectionChanged;
            viewModel.Note.Checklist.CollectionItemChanged += Checklist_CollectionItemChanged;
            viewModel.Note.PropertyChanged += OnNotePropertyChanged;

            previousBackground = App.RootFrame.Background;
            App.RootFrame.Background = viewModel.Note.Color.Color;

            //Color Picker
            NoteColorPicker.SelectedNoteColor = viewModel.Note.Color;
            NoteColorPicker.NoteColorChanged += (s, _e) => { viewModel.Note.Color = _e.NoteColor; };
        }

        private void OnSaveState()
        {
            //App.RootFrame.Background = previousBackground;

            viewModel.PropertyChanged -= OnViewModelPropertyChanged;

            var note = viewModel.Note;
            viewModel.Note = null;

            //deleted
            if (note == null) return;

            //remove change binding
            note.Images.CollectionChanged -= Images_CollectionChanged;
            note.Checklist.CollectionChanged -= Checklist_CollectionChanged;
            note.Checklist.CollectionItemChanged -= Checklist_CollectionItemChanged;
            note.PropertyChanged -= OnNotePropertyChanged;

            //prevent from losing changes when navigating with textbox focused
            //this.CommandBar.Focus(FocusState.Programmatic);
            //this.CommandBar.IsOpen = false;
            //await Task.Delay(0200);

            //trim
            note.Trim();

            //always update live tile
            NotificationsManager.UpdateNoteTileIfExists(note, AppSettings.Instance.TransparentNoteTile);

            //save or remove if empty
            if (note.Changed || note.IsEmpty())
                AppData.CreateOrUpdateNote(note);

            //checklist changed (fix cache problem with converter)
            if (checklistChanged) note.NotifyChanges();

            NoteEditViewModel.CurrentNoteBeingEdited = null;
            note = null;
        }

        #region NavigationHelper registration

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            this.navigationHelper.OnNavigatedTo(e);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            this.navigationHelper.OnNavigatedFrom(e);
        }

        #endregion


        private void OnViewModelPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Note" && viewModel.Note != null)
            {
                //viewModel.Note.PropertyChanged -= OnNotePropertyChanged;
                //viewModel.Note.PropertyChanged += OnNotePropertyChanged;

                OnColorChanged();
                UpdateIsPinnedStatus();
            }
        }

        private void OnNotePropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "IsPinned" || e.PropertyName == "CanPin")
                UpdateIsPinnedStatus();
            else if (e.PropertyName == "Color")
                OnColorChanged();
        }

        private void OnColorChanged()
        {
            if (viewModel.Note == null)
            {
                //App.ResetStatusBar();
                return;
            }

            if (App.RootFrame.ActualWidth < 768)
            {
                var statusBarColor = viewModel.Note.Color.Color.Color;//.Add(Color.FromArgb(0x10, 0, 0, 0));
                App.ChangeStatusBarColor(statusBarColor, null, ElementTheme.Light);
            } else {
                //App.ResetStatusBar();
            }
           

//#if WINDOWS_UWP
//            try
//            {
//                //this.Resources["FlyoutBackgroundThemeBrush"] = viewModel.Note.Color.DarkColor;
//                this.Resources["MenuFlyoutItemBackground"] = viewModel.Note.Color.DarkColor;
//                this.Resources["MenuFlyoutPresenterBackground"] = viewModel.Note.Color.DarkColor;

//                var style = new Style(typeof(FlyoutPresenter));
//                style.Setters.Add(new Setter(FlyoutPresenter.BackgroundProperty, viewModel.Note.Color.DarkColor));
//                style.Setters.Add(new Setter(FlyoutPresenter.BorderBrushProperty, viewModel.Note.Color.Color));
//                style.Setters.Add(new Setter(FlyoutPresenter.BorderThicknessProperty, new Thickness(1)));
//                ColorPickerAppBarToggleButton.Flyout.SetValue(Flyout.FlyoutPresenterStyleProperty, style);

//                var menuFlyoutStyle = new Style(typeof(MenuFlyoutPresenter));
//                menuFlyoutStyle.Setters.Add(new Setter(MenuFlyoutPresenter.BackgroundProperty, viewModel.Note.Color.DarkColor));
//                menuFlyoutStyle.Setters.Add(new Setter(MenuFlyoutPresenter.BorderBrushProperty, viewModel.Note.Color.Color));
//                menuFlyoutStyle.Setters.Add(new Setter(MenuFlyoutPresenter.BorderThicknessProperty, new Thickness(1)));
//                (this.Resources["NoteImageMenuFlyout"] as MenuFlyout).SetValue(MenuFlyout.MenuFlyoutPresenterStyleProperty, menuFlyoutStyle);
//            }
//            catch (Exception)
//            {
//            }
//#endif
        }

        private void UpdateIsPinnedStatus(bool? forceStatus = null)
        {
            if (viewModel.Note == null) return;
            bool isPinned = forceStatus != null ? (bool)forceStatus : viewModel.Note.IsPinned;

            if (isPinned)
            {
                TogglePinAppBarButton.Icon = new SymbolIcon(Symbol.UnPin);
                TogglePinAppBarButton.Command = viewModel.UnpinCommand;
                TogglePinAppBarButton.Label = ResourceLoader.GetForCurrentView().GetString("Unpin");
            }
            else
            {
                TogglePinAppBarButton.Icon = new SymbolIcon(Symbol.Pin);
                TogglePinAppBarButton.Command = viewModel.PinCommand;
                TogglePinAppBarButton.Label = ResourceLoader.GetForCurrentView().GetString("Pin");
                TogglePinAppBarButton.IsEnabled = viewModel.Note.CanPin;
            }
        }

        private void Images_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            viewModel.Note.Touch();
            viewModel.Note.NotifyPropertyChanged("Images");
        }

        private void Checklist_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            checklistChanged = true;
            viewModel.Note.Touch();
            viewModel.Note.NotifyPropertyChanged("Checklist");

            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove)
            {
                try
                {
                    var list = (object[])e.OldItems.SyncRoot;
                    var checklistItem = list[0] as ChecklistItem;

                    if (checklistItem != null)
                    {
                        AppData.LocalDB.Delete<ChecklistItem>(checklistItem.ID);
                        AppData.RoamingDB.Delete<ChecklistItem>(checklistItem.ID);
                    }
                }
                catch (Exception)
                {
                }
            }
        }

        private void Checklist_CollectionItemChanged(object sender, EventArgs e)
        {
            checklistChanged = true;
            viewModel.Note.Touch();
        }

        private void NoteChecklistListView_Holding(object sender, HoldingRoutedEventArgs e)
        {
            NoteChecklistListView.ReorderMode = ListViewReorderMode.Enabled;
        }

#if WINDOWS_PHONE_APP
        //swipe feature
        private void OnChecklistItemLoaded(object sender, RoutedEventArgs e)
        {
            FrameworkElement element = sender as FrameworkElement;
            FrameworkElement referenceFrame = NoteChecklistListView;

            //workaround to fix a bug
            element.Opacity = 1;

            if (viewModel.ReorderMode != ListViewReorderMode.Enabled)
                EnableSwipeFeature(element, referenceFrame);

            enableSwipeEventHandlers[element] = (s, _e) => { EnableSwipeFeature(element, referenceFrame); };
            disableSwipeEventHandlers[element] = (s, _e) => { DisableSwipeFeature(element); };

            viewModel.ReorderModeDisabled += enableSwipeEventHandlers[element];
            viewModel.ReorderModeEnabled += disableSwipeEventHandlers[element];
        }

        private void OnChecklistItemUnloaded(object sender, RoutedEventArgs e)
        {
            FrameworkElement element = sender as FrameworkElement;

            viewModel.ReorderModeDisabled -= enableSwipeEventHandlers[element];
            viewModel.ReorderModeEnabled -= disableSwipeEventHandlers[element];

            DisableSwipeFeature(element);
        }
#endif

        private void DeleteNoteImageMenuFlyoutItem_Click(object sender, RoutedEventArgs e)
        {
            viewModel.TempNoteImage = (e.OriginalSource as FrameworkElement).DataContext as NoteImage;
            viewModel.DeleteNoteImageCommand.Execute(viewModel.TempNoteImage);
        }

        private void NoteImageContainer_Holding(object sender, HoldingRoutedEventArgs e)
        {
            ShowNoteImageFlyout((FrameworkElement)sender);
        }

#if WINDOWS_UWP
        private void NoteImageContainer_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            ShowNoteImageFlyout((FrameworkElement)sender);
        }
#endif

        private void ShowNoteImageFlyout(FrameworkElement element)
        {
            if (element == null) return;
            Flyout.ShowAttachedFlyout(element);
        }

        // double tap to zoom
        private async void ScrollViewer_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            var scrollViewer = sender as ScrollViewer;

            var dispatcher = Window.Current.CoreWindow.Dispatcher;
            await dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.High, () =>
            {
                var doubleTapPoint = e.GetPosition(scrollViewer);

                if (scrollViewer.ZoomFactor > 1)
                {
                    scrollViewer.ChangeView(0, 0, 1.0F);
                }
                else
                {
                    //Debug.WriteLine(doubleTapPoint.X + "x" + doubleTapPoint.Y);
                    scrollViewer.ChangeView(doubleTapPoint.X, doubleTapPoint.Y, 2.0F);
                    scrollViewer.ChangeView(doubleTapPoint.X, doubleTapPoint.Y, 2.0F);
                }
            });
        }

        // disable flipview from going to next item with mouse scroll
        private void NoteImagesFlipView_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            //e.Handled = true;
        }

        private void onImagesCollectionChanges(object sender, NotifyCollectionChangedEventArgs e)
        {
            var lastIndex = (NoteImagesFlipView.DataContext as NoteImages).Count - 1;

            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                NoteImagesFlipView.SelectedIndex = lastIndex;
            }
            else if ((e.Action == NotifyCollectionChangedAction.Remove || e.Action == NotifyCollectionChangedAction.Reset))
            {
                var nextIndex = NoteImagesFlipView.SelectedIndex + 1;
                if (nextIndex > lastIndex) nextIndex = lastIndex;
                NoteImagesFlipView.SelectedIndex = nextIndex;
            }
        }

        private void NoteImagesFlipView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var flipView = sender as FlipView;

            if (flipView.SelectedIndex < 0 || e.AddedItems.Count <= 0 || e.RemovedItems.Count <= 0) return;

            var images = (NoteImages)flipView.DataContext;
            for (var i = 0; i < images.Count; i++) images[i].IsSelected = flipView.SelectedIndex == i;

            viewModel.Note.NotifyPropertyChanged("Images");
        }

        private void NoteImagesFlipView_Loaded(object sender, RoutedEventArgs e)
        {
            var flipView = sender as FlipView;
            flipView.SelectionChanged += NoteImagesFlipView_SelectionChanged;
            (flipView.DataContext as NoteImages).CollectionChanged += onImagesCollectionChanges;

            loadSelectedNoteImage();
            NoteImagesFlipView.Focus(FocusState.Pointer);
        }

        private void NoteImagesFlipView_Unloaded(object sender, RoutedEventArgs e)
        {
            var flipView = sender as FlipView;
            flipView.SelectionChanged -= NoteImagesFlipView_SelectionChanged;
            (flipView.DataContext as NoteImages).CollectionChanged -= onImagesCollectionChanges;
        }

        private void loadSelectedNoteImage()
        {
            var images = (NoteImages)NoteImagesFlipView.DataContext;
            var selectedImage = images.FirstOrDefault((i) => i.IsSelected);
            NoteImagesFlipView.SelectedIndex = images.Count > 0 ? images.IndexOf(selectedImage) : -1;
        }

        private void ReminderPicker_Saved(object sender, EventArgs e)
        {
            clickedOnReminderSaveOrCancelButtons = true;
            ReminderAppBarButton.Flyout.Hide();
            viewModel.CreateNoteReminderCommand.Execute(sender);
        }

        private void ReminderPicker_Canceled(object sender, EventArgs e)
        {
            clickedOnReminderSaveOrCancelButtons = true;
            ReminderAppBarButton.Flyout.Hide();
        }

        private void OnWindowSizeChanged(Size NewSize)
        {
            //Debug.WriteLine("NoteEditPage OnWindowSizeChanged " + NewSize.Width);
            if (NewSize.Width >= 768)
            {
                Grid.SetRow(this.CommandBar, 0);
                this.CommandBar.HorizontalAlignment = HorizontalAlignment.Right;
            }
            else
            {
                Grid.SetRow(this.CommandBar, 2);
                this.CommandBar.HorizontalAlignment = HorizontalAlignment.Stretch;
            }
        }

        // apenas a partir da build 14393
        private void Flyout_Closing(FlyoutBase sender, FlyoutBaseClosingEventArgs args)
        {
            // gambiarra
            if (!clickedOnReminderSaveOrCancelButtons) args.Cancel = true;
        }

        private void Flyout_Closed(object sender, object e)
        {
            // reset the gambiarra
            clickedOnReminderSaveOrCancelButtons = false;
        }


#if WINDOWS_PHONE_APP
        public void ContinueFileOpenPicker(FileOpenPickerContinuationEventArgs args)
        {
            viewModel.HandleImageFromFilePicker(args.Files);
        }
#endif
    }
}
