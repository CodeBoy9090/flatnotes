using Keep.Common;
using Keep.Models;
using Keep.Utils;
using Keep.Views;
using System;
using System.Diagnostics;
using System.Linq;
using Windows.UI.Xaml.Controls;

namespace Keep.ViewModels
{
    public class NoteEditViewModel : ViewModelBase
    {
        public event EventHandler ReorderModeEnabled;
        public event EventHandler ReorderModeDisabled;

        public RelayCommand ToggleChecklistCommand { get; private set; }
        public RelayCommand ArchiveNoteCommand { get; private set; }
        public RelayCommand RestoreNoteCommand { get; private set; }
        public RelayCommand DeleteNoteCommand { get; private set; }

        public NoteEditViewModel()
        {
            ToggleChecklistCommand = new RelayCommand(ToggleChecklist);
            ArchiveNoteCommand = new RelayCommand(ArchiveNote); //CanArchiveNote
            RestoreNoteCommand = new RelayCommand(RestoreNote); //CanRestoreNote
            DeleteNoteCommand = new RelayCommand(DeleteNote);

            AppData.NoteArchived += (s, e) => { ArchiveNoteCommand.RaiseCanExecuteChanged(); RestoreNoteCommand.RaiseCanExecuteChanged(); };
            AppData.NoteRestored += (s, e) => { ArchiveNoteCommand.RaiseCanExecuteChanged(); RestoreNoteCommand.RaiseCanExecuteChanged(); };
            PropertyChanged += OnPropertyChanged;
        }

        private void OnPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Note")
                IsArchived = AppData.ArchivedNotes.Where<Note>(x => x.ID == note.ID).FirstOrDefault<Note>() != null;
        }

        public Note Note { get { return note; } set { note = value; NotifyPropertyChanged("Note"); } }
        private Note note = new Note();

        public bool IsArchived { get { return isArchived; } set { isArchived = value; NotifyPropertyChanged("IsArchived"); } }
        private bool isArchived;

        public ListViewReorderMode ReorderMode
        {
            get { return reorderMode; }
            set
            {
                if (reorderMode == value) return;

                reorderMode = value;
                NotifyPropertyChanged("ReorderMode");

                var handler = value == ListViewReorderMode.Enabled ? ReorderModeEnabled : ReorderModeDisabled;
                if (handler != null) handler(this, EventArgs.Empty);
            }
        }
        public ListViewReorderMode reorderMode = ListViewReorderMode.Disabled;

        #region COMMANDS_ACTIONS

        private void ToggleChecklist()
        {
            Note.ToggleChecklist();
        }

        private bool CanArchiveNote()
        {
            return Note != null && !IsArchived;
        }

        private async void ArchiveNote()
        {
            await AppData.ArchiveNote(Note);
            note = null;

            if (App.RootFrame.CanGoBack)
                App.RootFrame.GoBack();
            else
                App.RootFrame.Navigate(typeof(MainPage));
        }

        private bool CanRestoreNote()
        {
            Debug.WriteLine("CanRestoreNote " + IsArchived);
            return IsArchived;
        }

        private async void RestoreNote()
        {
            await AppData.RestoreNote(Note);
            note = null;

            if (App.RootFrame.CanGoBack)
                App.RootFrame.GoBack();
            else
                App.RootFrame.Navigate(typeof(ArchivedNotesPage));
        }

        private async void DeleteNote()
        {
            bool success = IsArchived ? await AppData.RemoveArchivedNote(Note) :  await AppData.RemoveNote(Note);
            if (!success) return;

            note = null;

            if (App.RootFrame.CanGoBack)
                App.RootFrame.GoBack();
            else if(IsArchived)
                App.RootFrame.Navigate(typeof(ArchivedNotesPage));
            else
                App.RootFrame.Navigate(typeof(MainPage));
        }

        #endregion
    }
}