﻿using FlatNotes.Common;
using FlatNotes.Models;
using FlatNotes.Utils;
using FlatNotes.ViewModels;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.Resources;
using Windows.Foundation;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace FlatNotes.Views
{
    public sealed partial class NoteEditPage : Page
    {
        public NavigationHelper NavigationHelper { get { return this.navigationHelper; } }
        private NavigationHelper navigationHelper;

        public NoteEditViewModel viewModel { get { return (NoteEditViewModel)DataContext; } }
        private static Brush previousBackground;

        private bool checklistChanged = false;

        public NoteEditPage()
        {
            this.InitializeComponent();

            //Navigation Helper
            this.navigationHelper = new NavigationHelper(this);
            this.navigationHelper.LoadState += this.NavigationHelper_LoadState;
            this.navigationHelper.SaveState += this.NavigationHelper_SaveState;

            //Color Picker
            NoteColorPicker.NoteColorChanged += (s, _e) => { viewModel.Note.Color = _e.NoteColor; };

            this.Loaded += OnLoaded;
            viewModel.PropertyChanged += OnViewModelPropertyChanged;
            viewModel.Note.PropertyChanged += OnNotePropertyChanged;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            UpdateStatusBarColor();

            if(viewModel.Note.IsNewNote && AppData.Notes.Count > 0)
                NoteTitleTextBox.Focus(FocusState.Programmatic);
        }

        private void OnViewModelPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if(e.PropertyName == "Note" && viewModel.Note != null)
            {
                viewModel.Note.PropertyChanged += OnNotePropertyChanged;
                UpdateStatusBarColor();
                UpdateIsPinnedStatus();
            }
        }

        private void OnNotePropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "IsPinned")
                UpdateIsPinnedStatus();
            else if(e.PropertyName == "Color")
                UpdateStatusBarColor();
        }

        private void UpdateStatusBarColor()
        {
            if (viewModel.Note == null) return;
            App.ChangeStatusBarColor(new Color().FromHex(viewModel.Note.Color.DarkColor2), Colors.White);
        }

        private void UpdateIsPinnedStatus()
        {
            if (viewModel.Note == null) return;

            if (viewModel.Note.IsPinned)
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
            }
        }

        partial void EnableSwipeFeature(FrameworkElement element, FrameworkElement referenceFrame);
        partial void DisableSwipeFeature(FrameworkElement element);

        private void NavigationHelper_LoadState(object sender, LoadStateEventArgs e)
        {
            GoogleAnalytics.EasyTracker.GetTracker().SendView("NoteEditPage");

            if (e.NavigationParameter != null && e.NavigationParameter is Note)
                viewModel.Note = e.NavigationParameter as Note;
            else
                viewModel.Note = new Note();

            viewModel.Note.Changed = false;
            viewModel.Note.Images.CollectionChanged += Images_CollectionChanged;
            viewModel.Note.Checklist.CollectionChanged += Checklist_CollectionChanged;
            viewModel.Note.Checklist.CollectionItemChanged += Checklist_CollectionItemChanged;

            previousBackground = App.RootFrame.Background;
            App.RootFrame.Background = new SolidColorBrush().fromHex(viewModel.Note.Color.Color);

            NoteColorPicker.SelectedNoteColor = viewModel.Note.Color;
        }

        private async void NavigationHelper_SaveState(object sender, SaveStateEventArgs e)
        {
            App.RootFrame.Background = previousBackground;
            CommandBar.IsOpen = false;

            //deleted
            if (viewModel.Note == null) return;

            //remove change binding
            viewModel.Note.Images.CollectionChanged -= Images_CollectionChanged;
            viewModel.Note.Checklist.CollectionChanged -= Checklist_CollectionChanged;
            viewModel.Note.Checklist.CollectionItemChanged -= Checklist_CollectionItemChanged;

            //trim
            viewModel.Note.Trim();

            //update tile
            if (viewModel.Note.Changed) TileManager.UpdateNoteTileIfExists(viewModel.Note, AppSettings.Instance.TransparentNoteTile);

            //save or remove if empty
            if (viewModel.Note.Changed || viewModel.Note.IsEmpty())
                await AppData.CreateOrUpdateNote(viewModel.Note);

            //checklist changed (fix cache problem with converter)
            if (checklistChanged) viewModel.Note.NotifyChanges();

            //await Task.Delay(0300);
            //viewModel.Note = null;
            NoteEditViewModel.CurrentNoteBeingEdited = null;
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

        private void Images_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            Debug.WriteLine("Images_CollectionChanged");
            viewModel.Note.Touch();
        }

        private void Checklist_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            Debug.WriteLine("Checklist_CollectionChanged");
            checklistChanged = true;
            viewModel.Note.Touch();
        }

        private void Checklist_CollectionItemChanged(object sender, EventArgs e)
        {
            Debug.WriteLine("Checklist_CollectionItemChanged");
            checklistChanged = true;
            viewModel.Note.Touch();
        }

        private void NoteChecklistListView_Holding(object sender, HoldingRoutedEventArgs e)
        {
#if WINDOWS_PHONE_APP
            NoteChecklistListView.ReorderMode = ListViewReorderMode.Enabled;
#endif
        }

        //swipe feature
        private void OnChecklistItemLoaded(object sender, RoutedEventArgs e)
        {
#if WINDOWS_PHONE_APP
            FrameworkElement element = sender as FrameworkElement;
            FrameworkElement referenceFrame = NoteChecklistListView;

            //workaround to fix a bug
            element.Opacity = 1;

            if(viewModel.ReorderMode != ListViewReorderMode.Enabled)
                EnableSwipeFeature(element, referenceFrame);

            enableSwipeEventHandlers[element] = (s, _e) => { EnableSwipeFeature(element, referenceFrame); };
            disableSwipeEventHandlers[element] = (s, _e) => { DisableSwipeFeature(element); };

            viewModel.ReorderModeDisabled += enableSwipeEventHandlers[element];
            viewModel.ReorderModeEnabled += disableSwipeEventHandlers[element];
#endif
        }

        private void OnChecklistItemUnloaded(object sender, RoutedEventArgs e)
        {
#if WINDOWS_PHONE_APP
            FrameworkElement element = sender as FrameworkElement;

            viewModel.ReorderModeDisabled -= enableSwipeEventHandlers[element];
            viewModel.ReorderModeEnabled -= disableSwipeEventHandlers[element];

            DisableSwipeFeature(element);
#endif
        }
        
        private void NoteImageContainer_Holding(object sender, HoldingRoutedEventArgs e)
        {
            FlyoutBase.ShowAttachedFlyout((FrameworkElement)sender);
        }

        private void DeleteNoteImageMenuFlyoutItem_Click(object sender, RoutedEventArgs e)
        {
            viewModel.TempNoteImage = (e.OriginalSource as FrameworkElement).DataContext as NoteImage;
            viewModel.DeleteNoteImageCommand.Execute(viewModel.TempNoteImage);
        }
    }
}
