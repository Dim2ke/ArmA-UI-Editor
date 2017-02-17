﻿using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Xml.Serialization;
using ArmA.Studio.UI;

namespace ArmA.Studio.SolutionUtil
{   
    public abstract class SolutionFileBase : UI.ViewModel.IPropertyDatatemplateProvider
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public void RaisePropertyChanged([System.Runtime.CompilerServices.CallerMemberName]string callerName = "") { this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(callerName)); }



        [XmlIgnore]
        public string RelativePath
        {
            get
            {
                SolutionFileBase parent = Parent;
                if (parent != null)
                {
                    return string.Concat(parent.RelativePath, '\\', this.FileName);
                }
                else
                {
                    return this.FileName;
                }
            }
        }
        [XmlIgnore]
        public string FullPath { get { return Path.Combine(Workspace.CurrentWorkspace.WorkingDir, RelativePath); } }

        [XmlIgnore]
        public SolutionFileBase Parent { get { SolutionFileBase sfb; if (this._Parent != null && this._Parent.TryGetTarget(out sfb)) return sfb; else return null; } set { if(this.Parent != null) this.PerformMoveInFileSystem(value.RelativePath); this._Parent = new WeakReference<SolutionFileBase>(value); this.RaisePropertyChanged(); } }
        private WeakReference<SolutionFileBase> _Parent;

        [XmlAttribute("name")]
        public string FileName { get { return this._FileName; } set { if (value.IndexOfAny(Path.GetInvalidFileNameChars()) != -1) throw new InvalidOperationException(Properties.Localization.SolutionFile_NameContainsInvalidFiles); this.PerformRenameInFileSystem(value); this._FileName = value; this.RaisePropertyChanged(); } }
        private string _FileName;

        [XmlArray("childs", IsNullable = true)]
        [XmlArrayItem]
        [XmlArrayItem("folder", Type = typeof(SolutionFolder))]
        [XmlArrayItem("file", Type = typeof(SolutionFile))]
        public virtual ObservableCollection<SolutionFileBase> Children { get { return this._Children; } set { this._Children = value; this.RaisePropertyChanged(); } }
        private ObservableCollection<SolutionFileBase> _Children;

        [XmlIgnore]
        public DataTemplate PropertiesTemplate { get { return this.GetPropertiesTemplate(); } }
        public abstract DataTemplate GetPropertiesTemplate();

        [XmlIgnore]
        public ICommand CmdMouseDoubleClick { get { return new UI.Commands.RelayCommand(OnMouseDoubleClick); } }

        [XmlIgnore]
        public ICommand CmdContextMenuOpening { get { return new UI.Commands.RelayCommand(OnContextMenuOpening); } }

        [XmlIgnore]
        public ICommand CmdContextMenu_Rename { get { return new UI.Commands.RelayCommand((o) => this.IsInRenameMode = true); } }

        [XmlIgnore]
        public ICommand CmdContextMenu_Delete { get { return new UI.Commands.RelayCommand(OnDelete); } }

        [XmlIgnore]
        public ICommand CmdTextBoxLostKeyboardFocus { get { return new UI.Commands.RelayCommand((o) => this.IsInRenameMode = false); } }

        [XmlIgnore]
        public abstract ICommand CmdContextMenu_OpenInExplorer { get; }

        [XmlIgnore]
        public bool IsInRenameMode { get { return this._IsInRenameMode; } set { this._IsInRenameMode = value; this.RaisePropertyChanged(); } }
        private bool _IsInRenameMode;

        [XmlIgnore]
        public bool IsSelected { get { return this._IsSelected; } set { this._IsSelected = value; this.RaisePropertyChanged(); } }
        private bool _IsSelected;

        protected virtual void OnMouseDoubleClick(object param)
        {
            if (this.IsInRenameMode)
                return;
            var sfb = param as SolutionFileBase;
            if (sfb != null)
            {
                Workspace.CurrentWorkspace.OpenOrFocusDocument(sfb.RelativePath);

            }
        }
        protected virtual void OnContextMenuOpening(object param)
        {
            if (this.IsInRenameMode)
                return;
            var sfb = param as SolutionFileBase;
            if (sfb != null)
            {
                this.IsSelected = true;

            }
        }
        protected virtual void OnDelete(object param)
        {
            var msgBoxResult = MessageBox.Show(string.Format(Properties.Localization.MessageBoxDeleteFileConfirmation_Body, this.RelativePath), Properties.Localization.MessageBoxDeleteFileConfirmation_Title, MessageBoxButton.YesNo, MessageBoxImage.Information);
            if (msgBoxResult != MessageBoxResult.Yes)
                return;
            try
            {
                if (File.Exists(this.FullPath))
                {
                    File.Delete(this.FullPath);
                }
                this.Parent.Children.Remove(this);
            }
            catch(Exception ex)
            {
                MessageBox.Show(string.Format(Properties.Localization.MessageBoxOperationFailed_Body, ex.Message, ex.GetType().FullName, ex.StackTrace), Properties.Localization.MessageBoxOperationFailed_Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        public SolutionFileBase()
        {
            this._Children = new ObservableCollection<SolutionFileBase>();
        }
        private void PerformRenameInFileSystem(string newFileName)
        {
            if (string.IsNullOrWhiteSpace(this.FileName))
                return;
            var fPath = this.FullPath;
            //ToDo: Catch IOException and notify user
            try
            {
                var docBase = Workspace.CurrentWorkspace.GetDocumentOfSolutionFileBase(this);
                docBase.SaveDocument(docBase.FilePath);
                Workspace.CurrentWorkspace.DocumentsDisplayed.Remove(docBase);
                File.Move(fPath, Path.Combine(Path.GetDirectoryName(fPath), newFileName));
                Workspace.CurrentWorkspace.OpenOrFocusDocument(Path.Combine(Path.GetDirectoryName(fPath), newFileName));
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(Properties.Localization.MessageBoxOperationFailed_Body, ex.Message, ex.GetType().FullName, ex.StackTrace), Properties.Localization.MessageBoxOperationFailed_Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        private void PerformMoveInFileSystem(string newParentFolder)
        {
            if (this.Parent == null)
                return;
            //ToDo: Catch IOException and notify user
            try
            {
                var docBase = Workspace.CurrentWorkspace.GetDocumentOfSolutionFileBase(this);
                docBase.SaveDocument(docBase.FilePath);
                Workspace.CurrentWorkspace.DocumentsDisplayed.Remove(docBase);
                File.Move(this.FullPath, Path.Combine(newParentFolder, FileName));
                Workspace.CurrentWorkspace.OpenOrFocusDocument(Path.Combine(newParentFolder, FileName));
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(Properties.Localization.MessageBoxOperationFailed_Body, ex.Message, ex.GetType().FullName, ex.StackTrace), Properties.Localization.MessageBoxOperationFailed_Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        public override string ToString()
        {
            return this.FileName;
        }

        public static bool WalkThrough(IEnumerable<SolutionFileBase> sfbCol, Func<SolutionFileBase, bool> walkFnc)
        {
            if (sfbCol == null)
                return false;
            foreach (var sfb in sfbCol)
            {
                if (walkFnc(sfb))
                    return true;
                if (WalkThrough(sfb.Children, walkFnc))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
