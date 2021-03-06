﻿using FlatNotes.Utils.Migration.Versions.v1.Interfaces;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.Serialization;
using Windows.UI.StartScreen;

namespace FlatNotes.Utils.Migration.Versions.v1.Models
{
    public class Notes : ObservableCollection<Note> { }

    [DataContract]
    public class Note : BaseModel, IIdentifiableModelInterface
    {
        public bool Changed = false;
        public String GetID() { return ID; }
        public DateTime? GetCreatedAt() { return CreatedAt; }
        public DateTime? GetUpdatedAt() { return UpdatedAt; }

        public bool IsPinned { get { isPinned = SecondaryTile.Exists(this.ID); return isPinned; } set { if (isPinned != value) { isPinned = value; NotifyPropertyChanged("IsPinned"); } } }
        private bool isPinned { get { return isPinned_value; } set { if (isPinned_value != value) { isPinned_value = value; NotifyPropertyChanged("IsPinned"); } } }
        private bool isPinned_value;

        public String ID { get { return id; } private set { id = value; } }
        [DataMember(Name = "ID")]
        private String id = Guid.NewGuid().ToString();

        public bool IsChecklist { get { return isChecklist; } set { if (isChecklist != value) { isChecklist = value; if (value) EnableChecklist(); else DisableChecklist(); NotifyPropertyChanged("IsChecklist"); NotifyPropertyChanged("IsText"); } } }
        [DataMember(Name = "IsChecklist")]
        private bool isChecklist;

        [IgnoreDataMember]
        public bool IsText { get { return !IsChecklist; } }

        public String Title { get { return title; } set { if ( title != value ) { title = value; NotifyPropertyChanged( "Title" ); } } }
        [DataMember(Name = "Title")]
        private String title;

        public String Text { get { return text; } set { if (text != value) { text = value; NotifyPropertyChanged("Text"); } } }
        [DataMember(Name = "Text")]
        private String text;

        public NoteImages Images { get { return images; } set { replaceNoteImages(value); NotifyPropertyChanged("Images"); } }
        [DataMember(Name = "Images")]
        private NoteImages images = new NoteImages();

        public Checklist Checklist { get { return checklist; } set { replaceChecklist(value); NotifyPropertyChanged("Checklist"); } }
        [DataMember(Name = "Checklist")]
        private Checklist checklist = new Checklist();

        [IgnoreDataMember]
        public NoteColor Color { get { return color; } set { _colortmp = value is NoteColor ? value : NoteColor.DEFAULT; if ( color.Key != _colortmp.Key ) { color = _colortmp; NotifyPropertyChanged( "Color" ); } } }
        private NoteColor color = NoteColor.DEFAULT;
        private NoteColor _colortmp = NoteColor.DEFAULT;

        [DataMember(Name = "Color")]
        private string _color { get { return Color.Key; } set { color = new NoteColor(value); } }

        public DateTime? CreatedAt { get { return createdAt; } private set { createdAt = value; } }
        [DataMember(Name = "CreatedAt")]
        private DateTime? createdAt;

        public DateTime? UpdatedAt { get { return updatedAt; } set { updatedAt = value; } }
        [DataMember(Name = "UpdatedAt")]
        private DateTime? updatedAt;
        
        public Note() {
            PropertyChanged += Note_PropertyChanged;
            //Checklist.CollectionChanged += (s, e) => NotifyPropertyChanged("Checklist");
        }

        public Note( string title = "", string text = "", NoteColor color = null ) : base()
        {
            this.Title = title;
            this.Text = text;
            this.Color = color;
            this.IsChecklist = false;
        }

        public Note( string title, Checklist checklist, NoteColor color = null ) : base()
        {
            this.Title = title;
            this.Text = "";
            this.Color = color;

            this.IsChecklist = true;
            this.Checklist = checklist;
        }

        internal Note(string id, string title, string text, Checklist checklist, NoteColor color, DateTime? createdAt, DateTime? updatedAt) : this()
        {
            this.id = id;
            this.title = title;
            this.text = text;
            this.checklist = checklist;
            this.color = color is NoteColor ? color : NoteColor.DEFAULT;
            this.createdAt = createdAt;
            this.updatedAt = updatedAt;

            this.isChecklist = checklist != null;
        }

        void Note_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            //Debug.WriteLine("Note_PropertyChanged " + e.PropertyName);
            Changed = true;
        }

        public void Touch()
        {
            UpdatedAt = DateTime.UtcNow;
        }


        public bool ToggleChecklist()
        {
            if ( !this.IsChecklist )
            {
                this.IsChecklist = true;
                return true;
            }
            else
            {
                this.IsChecklist = false;
                return false;
            }
        }

        private void EnableChecklist()
        {
            this.isChecklist = true;

            if ( !( string.IsNullOrEmpty( Text ) ) )
            {
                string[] lines = Text.Split( Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries );

                this.Checklist = new Checklist();
                foreach ( string line in lines )
                    Checklist.Add ( new ChecklistItem( line ) );
            }

            Text = "";
        }

        private void DisableChecklist()
        {
            this.isChecklist = false;

            Text = GetTextFromChecklist();

            Checklist.Clear();
        }

        public bool IsEmpty()
        {
            return String.IsNullOrEmpty(Title) && ((!IsChecklist && String.IsNullOrEmpty(Text)) || (IsChecklist && Checklist.Count <= 0)) && Images.Count <= 0;
        }

        public String GetContent()
        {
            return IsChecklist ? GetTextFromChecklist() : Text;
        }

        public void Trim()
        {
            if (!String.IsNullOrEmpty(Title)) Title = Title.Trim();
            if(!String.IsNullOrEmpty(Text)) Text = Text.Trim();

            if (IsChecklist && Checklist != null)
                for (int i = Checklist.Count - 1; i >= 0; i--)
                {
                    if (!String.IsNullOrEmpty(Checklist[i].Text)) Checklist[i].Text = Checklist[i].Text.Trim();

                    if (String.IsNullOrEmpty(Checklist[i].Text))
                        Checklist.RemoveAt(i);
                }
        }

        public String GetTextFromChecklist()
        {
            string txt = "";

            foreach (ChecklistItem item in Checklist)
                txt += item.Text + Environment.NewLine.ToString();

            return txt.Trim();
        }

        private void replaceNoteImages(NoteImages list)
        {
            Images.Clear();

            if (list == null || list.Count <= 0)
                return;

            foreach (var item in list)
                Images.Add(item);

            return;
        }

        private void replaceChecklist(Checklist list)
        {
            Checklist.Clear();

            if (list == null || list.Count <= 0)
                return;

            foreach (var item in list)
                Checklist.Add(item);

            return;
        }
    }
}