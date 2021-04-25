﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms.Integration;
using System.Windows.Threading;
using LegendaryExplorer.Dialogs.Splash;
using LegendaryExplorer.Misc;
using LegendaryExplorer.SharedUI.PeregrineTreeView;
using LegendaryExplorer.Tools.PackageEditor;
using LegendaryExplorer.UnrealExtensions.Classes;
using ME3ExplorerCore;
using ME3ExplorerCore.Helpers;
using ME3ExplorerCore.Misc;
using ME3ExplorerCore.Packages;

namespace LegendaryExplorer.Startup
{
    /// <summary>
    /// Contains the application bootup code that is shown when loading, during the splash screen
    /// </summary>
    public static class AppBoot
    {
        public static DPIAwareSplashScreen LEXSplashScreen;

        /// <summary>
        /// Invoked during the splash screen sequence for the application
        /// </summary>
        public static void Startup(App app)
        {
            LEXSplashScreen = new DPIAwareSplashScreen();
            LEXSplashScreen.Show();
#if !DEBUG
            //We should only track things like this in release mode so we don't pollute our dataset
            var props = typeof(APIKeys).GetProperties();
            if (APIKeys.HasAppCenterKey)
            {
                Microsoft.AppCenter.AppCenter.Start(APIKeys.AppCenterKey,
                    typeof(Microsoft.AppCenter.Analytics.Analytics), typeof(Microsoft.AppCenter.Crashes.Crashes));
            }
#endif
            //Peregrine's Dispatcher (for WPF Treeview selecting on virtualized lists)
            DispatcherHelper.Initialize();
            initCoreLib();

            // Winforms setup
            System.Windows.Forms.Application.EnableVisualStyles();
            System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
            WindowsFormsHost.EnableWindowsFormsInterop();

            // WPF setup
            ToolTipService.ShowDurationProperty.OverrideMetadata(typeof(DependencyObject), new FrameworkPropertyMetadata(int.MaxValue));


            //set up AppData Folder
            if (!Directory.Exists(AppDirectories.AppDataFolder))
            {
                Directory.CreateDirectory(AppDirectories.AppDataFolder);
            }

            // Todo: Dark mode settings

            // TODO: IMPLEMENT IN LEX - load tools
            //Tools.Initialize();
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            app.Dispatcher.UnhandledException += app.OnDispatcherUnhandledException; //only start handling them after bootup
            
            Task.Run(() =>
            {

                Action actionDelegate = HandleCommandLineJumplistCall(Environment.GetCommandLineArgs(), out int exitCode);
                if (actionDelegate == null)
                {
                    app.Shutdown(exitCode);
                    LEXSplashScreen?.Close();
                }
                else
                {

                    GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                    GC.Collect();
                    //PendingAppLoadedAction = actionDelegate;



                    //close splash after
                    Thread.Sleep(2000);

                    // TODO: IMPLEMENT IN LEX
                    //GameController.InitializeMessageHook(mainWindow);
                    //PendingAppLoadedAction?.Invoke();

#if DEBUG
                    //StandardLibrary.InitializeStandardLib();
#endif
                }
            }).ContinueWithOnUIThread(x =>
            {
                LEXSplashScreen?.Close();
                app.ShutdownMode = ShutdownMode.OnMainWindowClose;
                var mainWindow = new MainWindow();
                mainWindow.Show();
            });
        }

        private static Action HandleCommandLineJumplistCall(string[] args, out int exitCode)
        {
            exitCode = 0;
            if (args.Length < 2)
            {
                return () => { }; //do nothing delgate. Will do nothing when main UI loads
            }

            string arg = args[1];
            if (arg == "JUMPLIST_PACKAGE_EDITOR")
            {
                return () =>
                {
                    PackageEditorWindow editor = new PackageEditorWindow();
                    editor.Show();
                    editor.Activate();
                };
            }

            /*
            if (arg == "JUMPLIST_SEQUENCE_EDITOR")
            {
                return () =>
                {
                    var editor = new SequenceEditorWPF();
                    editor.Show();
                    editor.Activate();
                };
            }

            if (arg == "JUMPLIST_PATHFINDING_EDITOR")
            {
                return () =>
                {
                    PathfindingEditorWPF editor = new PathfindingEditorWPF();
                    editor.Show();
                    editor.Activate();
                };
            }

            if (arg == "JUMPLIST_SOUNDPLORER")
            {
                return () =>
                {
                    SoundplorerWPF soundplorerWpf = new SoundplorerWPF();
                    soundplorerWpf.Show();
                    soundplorerWpf.Activate();

                };
            }

            //Do not remove - used by Mass Effect Mod Manager to boot the tool
            if (arg == "JUMPLIST_ASIMANAGER")
            {
                return () =>
                {
                    ASIManager asiManager = new ASIManager();
                    asiManager.Show();
                    asiManager.Activate();
                };
            }

            //Do not remove - used by Mass Effect Mod Manager to boot the tool
            if (arg == "JUMPLIST_MOUNTEDITOR")
            {
                return () =>
                {
                    MountEditorWPF mountEditorWpf = new MountEditorWPF();
                    mountEditorWpf.Show();
                    mountEditorWpf.Activate();
                };
            }

            //Do not remove - used by Mass Effect Mod Manager to boot the tool
            if (arg == "JUMPLIST_PACKAGEDUMPER")
            {
                return () =>
                {
                    PackageDumper.PackageDumper packageDumper = new PackageDumper.PackageDumper();
                    packageDumper.Show();
                    packageDumper.Activate();
                };
            }

            //Do not remove - used by Mass Effect Mod Manager to boot the tool
            if (arg == "JUMPLIST_DLCUNPACKER")
            {
                return () =>
                {
                    DLCUnpacker.DLCUnpackerUI dlcUnpacker = new DLCUnpacker.DLCUnpackerUI();
                    dlcUnpacker.Show();
                    dlcUnpacker.Activate();
                };
            }

            if (arg == "JUMPLIST_DIALOGUEEDITOR")
            {
                return () =>
                {
                    DialogueEditorWPF editor = new DialogueEditorWPF();
                    editor.Show();
                    editor.Activate();
                };
            }

            if (arg == "JUMPLIST_MESHPLORER")
            {
                return () =>
                {
                    MeshplorerWPF meshplorerWpf = new MeshplorerWPF();
                    meshplorerWpf.Show();
                    meshplorerWpf.Activate();
                };
            }*/


            string ending = Path.GetExtension(args[1]).ToLower();
            switch (ending)
            {
                case ".pcc":
                case ".sfm":
                case ".upk":
                case ".u":
                case ".udk":
                    return () =>
                    {
                        PackageEditorWindow editor = new PackageEditorWindow();
                        editor.Show();
                        editor.LoadFile(args[1]);
                        editor.RestoreAndBringToFront();
                    };
                    //return 2; //Do not signal bring main forward
            }
            exitCode = 0; //is this even used?
            return null;
        }

        private static void initCoreLib()
        {
#if DEBUG
            MemoryAnalyzer.IsTrackingMemory = true;
#endif
            void packageSaveFailed(string message)
            {
                // I'm not sure if this requires ui thread since it's win32 but i'll just make sure
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(message);
                });
            }

            ME3ExplorerCoreLib.InitLib(TaskScheduler.FromCurrentSynchronizationContext(), packageSaveFailed);
            //CoreLibSettingsBridge.MapSettingsIntoBridge(); // TODO: IMPLEMENT THIS IN LEX - this needs rewritten for new system
            PackageSaver.CheckME3Running = () =>
            {
                // TODO: IMPLEMENT IN LEX
                return false;
                //GameController.TryGetMEProcess(MEGame.ME3, out var me3Proc);
                //return me3Proc != null;
            };
            //PackageSaver.NotifyRunningTOCUpdateRequired = GameController.SendME3TOCUpdateMessage;
            PackageSaver.GetPNGForThumbnail = texture2D => texture2D.GetPNG(texture2D.GetTopMip()); // Used for UDK packages
        }

        /// <summary>
        /// Invoked when a second instance of Legendary Explorer is opened
        /// </summary>
        /// <param name="args"></param>
        public static void HandleDuplicateInstanceArgs(string[] args)
        {
            // TODO: IMPLEMENT IN LEX
        }
    }
}
