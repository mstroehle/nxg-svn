﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NationalInstruments.Composition;
using NationalInstruments.Core;
using Svn.SvnThings;
using NationalInstruments.Shell;
using System.IO;
using NationalInstruments.Design;
using NationalInstruments.ProjectExplorer.Design;
using NationalInstruments.SourceModel.Envoys;
using NationalInstruments.Restricted.Shell;
using SharpSvn;

namespace Svn.Plugin
{
    [Export(typeof(SvnManagerPlugin))]
    //[PartMetadata(ExportIdentifier.RootContainerKey, "")] // This has project afinity so we need to create one per Project
    public class SvnManagerPlugin : IPartImportsSatisfiedNotification
    {
        private SvnManager _svnManager;
        private IDocumentManager _documentManager;
        private Project _currentProject;    

        [Import]
        public ICompositionHost Host { get; set; }

        /// <summary>
        /// Thrown when the status of a file has been updated
        /// </summary>
        public event EventHandler<SvnStatusUpdatedEventArgs> SvnStatusUpdatedEvent;

        public void OnImportsSatisfied()
        {
            _documentManager = Host.GetSharedExportedValue<IDocumentManager>();
            _documentManager.PropertyChanged += HandleDocumentManagerPropertyChanged;
            ConnectToProject();

        }

        private void HandleDocumentManagerPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == DocumentManagerExtensions.ActiveProjectPropertyChangedName)
            {
                ConnectToProject();
            }
        }
       
        private void ConnectToProject()
        {
            _currentProject = _documentManager.ActiveProject;
            _currentProject.PropertyChanged += HandleProjectPropertyChanged;
            ConnectToRepository(_currentProject.StoragePath); 
        }

        

        private void HandleProjectPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == Project.StoragePathPropertyChangedName)
            {
                ConnectToRepository(_currentProject.StoragePath);
            }
        }

        private void ConnectToRepository(string storagePath)
        {
            if (string.IsNullOrEmpty(storagePath))
            {
                // Unsaved project
                return;
            }            
            _svnManager = new SvnManager();
            if (_svnManager.IsWorkingCopy(Path.GetDirectoryName(storagePath))) 
            {
                _svnManager.LoadCurrentSvnItemsInLocalRepository(Path.GetDirectoryName(storagePath));                
            }
            _svnManager.SvnStatusUpdatedEvent += SvnManagerOnSvnStatusUpdatedEvent;

        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="svnStatusUpdatedEventArgs"></param>
        private void SvnManagerOnSvnStatusUpdatedEvent(object sender, SvnStatusUpdatedEventArgs svnStatusUpdatedEventArgs)
        {
            Host.BeginInvoke(() =>
            {
                //GetStudioWindows() is in NationalInstruments.Restricted.Shell and may not always be publicly exposed.
                //This call may need to change at a later date, but should work for now.
                var studioWindows = StudioWindow.GetStudioWindows(Host); //Get all of the StudioWindows
                foreach (StudioWindow studioWindow in studioWindows) // use each StudioWindow's EditSite to refresh the appropriate icon
                {
                    var editSite = studioWindow.GetEditSite();
                    if (null != editSite) // check editSite != null just in case
                    {
                        var projectExplorerViewModel = editSite?.GetProjectExplorerViewModelFromEditSite();
                        var projectItem = projectExplorerViewModel?.FindProjectItemByFullPath(svnStatusUpdatedEventArgs.FullFilePath);
                        projectItem?.RefreshIcon();
                    }
                }
            });
            var handler = SvnStatusUpdatedEvent;
            handler?.Invoke(this, svnStatusUpdatedEventArgs);
        }        

        /// <summary>
        /// Lock the file
        /// </summary>
        /// <param name="filePath">file to lock</param>
        /// <param name="comment">optional lock comment</param>
        /// <returns></returns>
        public bool Lock(string filePath, string comment = "")
        {            
            return _svnManager.Lock(filePath, comment);
        }

        /// <summary>
        /// Release lock
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns>true if success, false otherwise</returns>
        public bool ReleaseLock(string filePath)
        {
            return _svnManager.ReleaseLock(filePath);
        }

        /// <summary>
        /// Revert File
        /// </summary>
        /// <param name="filePath">path to file</param>
        /// <returns></returns>
        public bool Revert(string filePath)
        {
            return _svnManager.Revert(filePath);
        }

        /// <summary>
        /// Status of a single file
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public SvnItem Status(string filePath)
        {
            return _svnManager.GetSingleItemStatus(filePath);
        }

        /// <summary>
        /// History
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public Collection<SvnLogEventArgs> History(string filePath)
        {            
            return _svnManager.GetHistory(filePath);
        }

        /// <summary>
        /// Update to revision
        /// </summary>
        /// <param name="filePath">file path</param>
        /// <param name="revision">revision</param>
        /// <returns></returns>
        public bool UpdateToRevision(string filePath, long revision)
        {
            return _svnManager.UpdateToRevision(filePath, revision);
        }

        /// <summary>
        /// Revert to revsision from history
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="startRevision"></param>
        /// <param name="endRevision"></param>
        /// <returns></returns>
        public bool ReverseMerge(string filePath, long startRevision, long endRevision)
        {
            return _svnManager.ReverseMerge(filePath, startRevision, endRevision);
        }

        /// <summary>
        /// Obtain a file from SVN and place it in this location
        /// </summary>
        /// <param name="tempFilePathOldVersion"></param>
        /// <returns></returns>
        public bool Write(string localFilePath, string tempFilePathOldVersion, long revision)
        {
            return _svnManager.Write(localFilePath, tempFilePathOldVersion, revision);
        }

        /// <summary>
        /// Add file to SVN
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public bool Add(string filePath)
        {
            return _svnManager.Add(filePath);
        }

        /// <summary>
        /// Commit a single file
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="commitMessage"></param>
        /// <returns></returns>
        public bool Commit(string filePath, string commitMessage)
        {
            return _svnManager.Commit(filePath, commitMessage);
        }

        /// <summary>
        /// Commit all files
        /// </summary>
        /// <param name="filePaths"></param>
        /// <param name="commitMessage"></param>
        /// <returns></returns>
        public bool CommitAllFiles(List<string> filePaths, string commitMessage)
        {
            return _svnManager.CommitAllFiles(filePaths, commitMessage);
        }

        /// <summary>
        /// Get all of the SvnItems that we have stored with their statuses
        /// </summary>
        /// <returns></returns>
        public Dictionary<string, SvnItem> GetMappings()
        {
            return _svnManager.GetMappings();
        }

        /// <summary>
        /// Update the svn status cache
        /// </summary>
        public void UpdateCache()
        {
            _svnManager?.UpdateCache();
        }
    }
}
