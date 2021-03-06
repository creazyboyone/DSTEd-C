﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows;
using System.IO;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DSTEd.Core;
using DSTEd.Core.Contents;
using DSTEd.Core.IO;
using DSTEd.UI.Components;
using DSTEd.UI.Theme;
using Xceed.Wpf.AvalonDock;
using Xceed.Wpf.AvalonDock.Layout;
using System.Windows.Media;

namespace DSTEd.UI {
    public partial class IDE : Window {
        private Menu menu                           = null;
        private static string CHANGED_CHARACTER     = "*";
        private AvalonDocument lastactivedocument   = null;

        public IDE() {
            InitializeComponent();
            
            this.menu = new Menu(this);
            this.dockManager.Theme = new Dark();
            this.Closing += this.IDE_Closing;
		}

        public void SetSteamProfile(Core.Steam.Workshop profile) {
            string picture  = @"";
            string name     = @"";

            if(profile == null) {
                picture = @"/DSTEd;component/Assets/Icons/warning.png";
                name    = I18N.__("Guest");
            } else {
                picture = profile.GetPicture();
                name    = profile.GetUsername();

            }

            Logger.Info("PICTURE: " + picture);
            BitmapImage image;
            // base64
            if (picture.StartsWith("data:image"))
            {
                String base64 = picture.Substring(picture.IndexOf(",") + 1);
                byte[] arr = Convert.FromBase64String(base64);
                image = new BitmapImage(); 
                using (MemoryStream ms = new MemoryStream(arr))
                {
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.StreamSource = ms;
                    image.EndInit();
                    image.Freeze();
                }
            } else
            {
                image = new BitmapImage(new Uri(picture, UriKind.RelativeOrAbsolute));
            }
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Render, new Action(() => {
                PICTURE.Source  = image;
                STEAM.Header    = name;
            }));
        }

        public void UpdateWelcome(bool state) {
			Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() => VIEW_WELCOME.IsChecked = state)); 
        }

        public void Init() {
            string path = Boot.Core.Steam.GetGame().GetPath();
            WorkspaceTree mods = null, core = null;
            Dispatcher.Invoke(delegate ()
            {
                mods = new WorkspaceTree(new FileSystem(path + "\\" + "mods"));
                core = new WorkspaceTree(new FileSystem(path + "\\" + "data"));
            });

            Dispatcher.Invoke(DispatcherPriority.Normal, new Action( ()=>
			{
                this.workspace_mods.Content = mods;
                this.workspace_core.Content = core;
			}));
			menu.Init();
        }

        public System.Windows.Controls.MenuItem GetTools() {
            return this.tools;
        }

        public System.Windows.Controls.MenuItem GetDebug() {
            return this.debug;
        }

        public Menu GetMenu() {
            return this.menu;
        }

        public Boolean IsMenuAvailable() {
            return this.menu != null;
        }

        private void OnLayoutRootPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
            var activeContent = ((LayoutRoot) sender).ActiveContent;

            if(this.IsMenuAvailable()) {
                this.GetMenu().Update();
            }

            if(e.PropertyName == "ActiveContent") {
                // Logger.Info(string.Format("ActiveContent-> {0}", activeContent));
            }
        }

        private void OnLoadLayout(object sender, RoutedEventArgs e) {}

        private void OnSaveLayout(object sender, RoutedEventArgs e) {}

        private void dockManager_DocumentClosing(object sender, DocumentClosingEventArgs e) {
			bool savedialogfunc(Dialog.Result r) {
				switch(r) {
                    case Dialog.Result.Cancel:
                        e.Cancel = true;
                    break;
					case Dialog.Result.No:
						//not call SaveActiveDocument() and close
					break;
					case Dialog.Result.Yes:
						SaveActiveDocument();
						//Boot.Core.Workspace.RemoveDocument(GetActiveDocument());
					break;
					default:
						e.Cancel = true;
					break;
				}

				return true;
			}

            // Check if the document has changed.
            if((e.Document.Title).Contains(CHANGED_CHARACTER)) {
                Dialog.Open(I18N.__("Save And close?"), "DSTEd", Dialog.Buttons.YesNoCancel, Dialog.Icon.Warning, savedialogfunc);
            } else {
                e.Cancel = true;
            }
        }

        private void OnReloadManager(object sender, RoutedEventArgs e) {}

        private void OnUnloadManager(object sender, RoutedEventArgs e) {
            if(layoutRoot.Children.Contains(dockManager)) {
                layoutRoot.Children.Remove(dockManager);
            }
        }

        private void OnLoadManager(object sender, RoutedEventArgs e) {
            if(!layoutRoot.Children.Contains(dockManager)) {
                layoutRoot.Children.Add(dockManager);
            }
        }

        private void OnToolWindow1Hiding(object sender, System.ComponentModel.CancelEventArgs e) {
            Dialog.Open("Are you sure you want to close this tool?", "DSTEd", Dialog.Buttons.YesNo, Dialog.Icon.Warning, delegate (Dialog.Result result) {
                this.GetMenu().Update();

                if(result == Dialog.Result.Yes) {
                    return true;
                }

                e.Cancel = true;
                return true;
            });
        }

        private void OnShowHeader(object sender, RoutedEventArgs e) {}

        private void OnMenu(object sender, RoutedEventArgs e) {
            var item = sender as System.Windows.Controls.MenuItem;

            if(item != null) {
                this.menu.Handle(item.Name, item);
            }
        }

        public LayoutDocumentPane GetEditors() {
            return this.editors;
        }

        public DSTEd.UI.Components.Debugger GetDebugPanel() {
            return this.debugger;
        }

        public void SetActiveDocument(Document document) {
            foreach(LayoutDocument entry in this.editors.Children) {
                if(entry.GetType() == typeof(AvalonDocument)) {
                    AvalonDocument doc = (AvalonDocument) entry;

                    if(doc.GetDocument().Equals(document)){
                        doc.IsActive        = true;
                        lastactivedocument  = doc;
                        return;
                    }

                    doc.IsActive = false;
                }
            }

            var newdoc          = new AvalonDocument(document);
            newdoc.IsActive     = true;
            lastactivedocument  = newdoc;
            editors.Children.Add(newdoc);
        }

        public void CloseActiveDocument(){
            GetActiveDocument().Close();
        }

        public void CloseAllDocuments() {
            List<AvalonDocument> closeable = new List<AvalonDocument>();

            foreach(LayoutDocument child in editors.Children) {
                if(child.GetType() == typeof(AvalonDocument)) {
                    closeable.Add((AvalonDocument)child);
                }
            }

            foreach(var document in closeable) {
                document.Close();
            }
        }

        public void SaveActiveDocument() {
            var doc = GetActiveDocument();
            doc.GetDocument().SaveDocument();
            doc.Title = doc.Title.TrimEnd(CHANGED_CHARACTER.ToCharArray());

        }

		public void SaveAllDocument() {
			foreach(LayoutDocument child in editors.Children) {
				if(child.GetType() == typeof(AvalonDocument)) {
					((AvalonDocument) child).GetDocument().SaveDocument();
				}
			}
		}

        public AvalonDocument GetActiveDocument() {
            return lastactivedocument;
		}

        internal void OnChanged(Document document, Document.State state) {
            Logger.Info("[IDE] Changed document: " + document.GetName() + " >> " + state);

            switch (state) {
                case Document.State.CREATED:
                    this.editors.Children.Add(new AvalonDocument(document));
                    this.SetActiveDocument(document);
                break;
                case Document.State.CHANGED:
                    foreach(LayoutDocument entry in this.editors.Children) {
                        if(entry.GetType() == typeof(AvalonDocument)) {
                            AvalonDocument doc = (AvalonDocument) entry;

                            if(doc.GetDocument() == document) {
                                doc.Title = document.GetName() + CHANGED_CHARACTER;
                            }
                        }
                    }
                break;
                case Document.State.REMOVED:
                    foreach(LayoutDocument entry in this.editors.Children) {
                        if(entry.GetType() == typeof(AvalonDocument)) {
                            AvalonDocument doc = (AvalonDocument) entry;

                            if(doc.GetDocument() == document) {
                                //Boot.Core.Workspace.RemoveDocument( ? doc.GetDocument() : null);
                                this.editors.Children.Remove(doc);
                                this.GetMenu().Update();
								//state = Document.State.CREATED;
                                break;
                            }
                        }
                    }
                break;
            }

            this.GetMenu().Update();
        }

        private void IDE_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
            Environment.Exit(0);
        }

		private void SaveExecuted(object sender, System.Windows.Input.ExecutedRoutedEventArgs e) {
			SaveActiveDocument();
			menu.Update();
		}
        
        private void SaveAllExecuted(object sender, System.Windows.Input.ExecutedRoutedEventArgs e) {
			SaveAllDocument();
			menu.Update();
		}
	}
}
