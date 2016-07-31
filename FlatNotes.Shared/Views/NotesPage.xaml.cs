﻿using FlatNotes.Common;
using FlatNotes.Events;
using FlatNotes.Models;
using FlatNotes.Utils;
using FlatNotes.ViewModels;
using System;
using System.Threading.Tasks;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace FlatNotes.Views
{
    public partial class NotesPage : Page
    {
        public MainViewModel viewModel { get { return _viewModel; } }
        private static MainViewModel _viewModel = MainViewModel.Instance;

        public NavigationHelper NavigationHelper { get { return this.navigationHelper; } }
        private NavigationHelper navigationHelper;

        public event EventHandler NoteOpening;
        public event EventHandler NoteOpened;
        public event EventHandler NoteClosed;

        public NotesPage()
        {
            this.InitializeComponent();

            viewModel.ShowNote = ShowNoteContent;

            Loading += OnLoading;

#if WINDOWS_PHONE_APP
            Loaded += (s, e) => EnableReorderFeature();
            Unloaded += (s, e) => DisableReorderFeature();
#endif
        }

#if WINDOWS_PHONE_APP
        partial void EnableReorderFeature();
        partial void DisableReorderFeature();
#endif
        
        private void OnLoading(FrameworkElement sender, object args)
        {
            viewModel.ReorderMode = ListViewReorderMode.Disabled;
            if (viewModel.Notes == null || viewModel.Notes.Count <= 0) viewModel.Notes = AppData.Notes;
        }

        private async void OnNoteClick(object sender, ItemClickEventArgs e)
        {
            Note note = e.ClickedItem as Note;
            if (note == null) return;

            //this dispatcher fixes crash error (access violation on wp preview for developers)
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                ShowNoteContent(note);
            });
        }

        private void OnItemsReordered(object sender, Events.ItemsReorderedEventArgs e)
        {
            //System.Diagnostics.Debug.WriteLine("OnItemsReordered from {0} to {1}", e.OldItemIndex, e.NewItemIndex);
            if (e.OldItemIndex < 0 || e.NewItemIndex < 0) return;
            if (e.OldItemIndex > viewModel.Notes.Count || e.NewItemIndex > viewModel.Notes.Count) return;

            viewModel.Notes.Move(e.OldItemIndex, e.NewItemIndex);

            int pos = viewModel.Notes.Count - 1;
            foreach (var note in viewModel.Notes)
            {
                note.Order = pos;
                pos--;
            }

            AppData.LocalDB.UpdateAll(viewModel.Notes);
            AppData.RoamingDB.UpdateAll(viewModel.Notes);
        }

        public void ShowNoteContent(object parameter)
        {
            NoteOpening?.Invoke(this, EventArgs.Empty);

            NoteFrame.Navigate(typeof(NoteEditPage), parameter);
            
            NotePopup.IsOpen = true;
            NoteOpened?.Invoke(this, new GenericEventArgs(parameter));
        }

        public void HideNoteContent(object parameter)
        {
            NotePopup.IsOpen = false;
        }

        private void NotePopup_Opened(object sender, object e)
        {
            NoteOpened?.Invoke(this, EventArgs.Empty);
        }

        private void NotePopup_Closed(object sender, object e)
        {
            //while (NoteFrame.CanGoBack) NoteFrame.GoBack();

            NoteClosed?.Invoke(this, EventArgs.Empty);
        }

        // needed because the ActualWidth property does not post updates when it changes
        // see: https://msdn.microsoft.com/en-us/library/windows/apps/windows.ui.xaml.frameworkelement.actualwidth
        private void NoteQuickBox_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            NoteFrame.Width = e.NewSize.Width;
        }

#if WINDOWS_PHONE_APP
        private void OnCreateNoteTapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            viewModel.CreateTextNote();
        }
#endif
    }
}
