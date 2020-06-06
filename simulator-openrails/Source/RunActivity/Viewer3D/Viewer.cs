﻿// COPYRIGHT 2009, 2010, 2011, 2012, 2013, 2014, 2015, 2016, 2017 by the Open Rails project.
//
// This file is part of Open Rails.
//
// Open Rails is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// Open Rails is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with Open Rails.  If not, see <http://www.gnu.org/licenses/>.

// This file is the responsibility of the 3D & Environment Team.

using GNU.Gettext;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Orts.Common;
using Orts.Formats.Msts;
using Orts.MultiPlayer;
using Orts.Simulation;
using Orts.Simulation.AIs;
using Orts.Simulation.Physics;
using Orts.Simulation.RollingStocks;
using Orts.Viewer3D.Popups;
using Orts.Viewer3D.Processes;
using Orts.Viewer3D.RollingStock;
using ORTS.Common;
using ORTS.Settings;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Management;
using System.Threading;
using System.Windows.Forms;
using Event = Orts.Common.Event;

namespace Orts.Viewer3D
{
    public class Viewer
    {
        public static GettextResourceManager Catalog { get; private set; }
        public static Random Random { get; private set; }
        // User setups.
        public UserSettings Settings { get; private set; }
        // Multi-threaded processes
        public LoaderProcess LoaderProcess { get; private set; }
        public UpdaterProcess UpdaterProcess { get; private set; }
        public RenderProcess RenderProcess { get; private set; }
        public SoundProcess SoundProcess { get; private set; }
        // Access to the XNA Game class
        public GraphicsDevice GraphicsDevice { get; private set; }
        public string ContentPath { get; private set; }
        public SharedTextureManager TextureManager { get; private set; }
        public SharedMaterialManager MaterialManager { get; private set; }
        public SharedShapeManager ShapeManager { get; private set; }
        public Point DisplaySize { get { return RenderProcess.DisplaySize; } }
        // Components
        public Orts.Viewer3D.Processes.Game Game { get; private set; }
        public Simulator Simulator { get; private set; }
        public World World { get; private set; }
        /// <summary>
        /// Monotonically increasing time value (in seconds) for the game/viewer. Starts at 0 and only ever increases, at real-time.
        /// </summary>
        public double RealTime { get; private set; }
        InfoDisplay InfoDisplay;
        public WindowManager WindowManager { get; private set; }
        public MessagesWindow MessagesWindow { get; private set; } // Game message window (special, always visible)
        public NoticeWindow NoticeWindow { get; private set; } // Game notices window (special)
        public PauseWindow PauseWindow { get; private set; } // Game paused window (special)
        public ActivityWindow ActivityWindow { get; private set; } // Activity notices window
        public QuitWindow QuitWindow { get; private set; } // Escape window
        public HelpWindow HelpWindow { get; private set; } // F1 window
        public TrackMonitorWindow TrackMonitorWindow { get; private set; } // F4 window
        public HUDWindow HUDWindow { get; private set; } // F5 hud
        public OSDLocations OSDLocations { get; private set; } // F6 platforms/sidings OSD
        public OSDCars OSDCars { get; private set; } // F7 cars OSD
        public SwitchWindow SwitchWindow { get; private set; } // F8 window
        public TrainOperationsWindow TrainOperationsWindow { get; private set; } // F9 window
        public CarOperationsWindow CarOperationsWindow { get; private set; } // F9 sub-window for car operations
        public NextStationWindow NextStationWindow { get; private set; } // F10 window
        public CompassWindow CompassWindow { get; private set; } // 0 window
        public TracksDebugWindow TracksDebugWindow { get; private set; } // Control-Alt-F6
        public SignallingDebugWindow SignallingDebugWindow { get; private set; } // Control-Alt-F11 window
        public ComposeMessage ComposeMessageWindow { get; private set; } // ??? window
        public TrainListWindow TrainListWindow { get; private set; } // for switching driven train
        public TTDetachWindow TTDetachWindow { get; private set; } // for detaching player train in timetable mode
        // Route Information
        public TileManager Tiles { get; private set; }
        public TileManager LoTiles { get; private set; }
        public EnvironmentFile ENVFile { get; private set; }
        public SignalConfigurationFile SIGCFG { get; private set; }
        public TrackTypesFile TrackTypes { get; private set; }
        public SpeedpostDatFile SpeedpostDatFile;
        public bool MilepostUnitsMetric { get; private set; }
        // Cameras
        public Camera Camera { get; set; } // Current camera
        public Camera AbovegroundCamera { get; private set; } // Previous camera for when automatically switching to cab.
        public CabCamera CabCamera { get; private set; } // Camera 1
        public HeadOutCamera HeadOutForwardCamera { get; private set; } // Camera 1+Up
        public HeadOutCamera HeadOutBackCamera { get; private set; } // Camera 2+Down
        public TrackingCamera FrontCamera { get; private set; } // Camera 2
        public TrackingCamera BackCamera { get; private set; } // Camera 3
        public TracksideCamera TracksideCamera { get; private set; } // Camera 4
        public PassengerCamera PassengerCamera { get; private set; } // Camera 5
        public BrakemanCamera BrakemanCamera { get; private set; } // Camera 6
        public List<FreeRoamCamera> FreeRoamCameraList = new List<FreeRoamCamera>();
        public FreeRoamCamera FreeRoamCamera { get { return FreeRoamCameraList[0]; } } // Camera 8
        public ThreeDimCabCamera ThreeDimCabCamera; //Camera 0

        List<Camera> WellKnownCameras; // Providing Camera save functionality by GeorgeS

        public TrainCarViewer PlayerLocomotiveViewer { get; private set; }  // we are controlling this loco, or null if we aren't controlling any
        MouseState originalMouseState;      // Current mouse coordinates.

        // This is the train we are controlling
        public TrainCar PlayerLocomotive { get { return Simulator.PlayerLocomotive; } set { Simulator.PlayerLocomotive = value; } }
        public Train PlayerTrain { get { if (PlayerLocomotive == null) return null; else return PlayerLocomotive.Train; } }

        // This is the train we are viewing
        public Train SelectedTrain { get; private set; }
        void CameraActivate()
        {
            if (Camera == null || !Camera.IsAvailable) //passenger camera may jump to a train without passenger view
                FrontCamera.Activate();
            else
                Camera.Activate();
        }

        bool ForceMouseVisible;
        double MouseVisibleTillRealTime;
        public Cursor ActualCursor = Cursors.Default;
        public static Viewport DefaultViewport;

        CabViewDiscreteRenderer MouseChangingControl;
        CabViewDiscreteRenderer MousePickedControl;
        CabViewDiscreteRenderer OldMousePickedControl;

        public bool SaveScreenshot { get; set; }
        public bool SaveActivityThumbnail { get; private set; }
        public string SaveActivityFileStem { get; private set; }

        public Vector3 NearPoint { get; private set; }
        public Vector3 FarPoint { get; private set; }

        public bool DebugViewerEnabled { get; set; }
        public bool SoundDebugFormEnabled { get; set; }

        public TRPFile TRP; // Track profile file

        enum VisibilityState
        {
            Visible,
            Hidden,
            ScreenshotPending,
        };

        VisibilityState Visibility = VisibilityState.Visible;

        // MSTS cab views are images with aspect ratio 4:3.
        // OR can use cab views with other aspect ratios where these are available.
        // On screen with other aspect ratios (e.g. 16:9), two approaches are possible:
        //   1) stretch the width to fit the screen. This gives flattened controls, most noticeable with round dials.
        //   2) clip the image losing a slice off top and bottom.
        // Setting.Cab2DStretch controls the amount of stretch and clip. 0 is entirely clipped and 100 is entirely stretched.
        // No difference is seen on screens with 4:3 aspect ratio.
        // This adjustment assumes that the cab view is 4:3. Where the cab view matches the aspect ratio of the screen, use an adjustment of 100.
        public int CabHeightPixels { get; private set; }
        public int CabWidthPixels { get; private set; }
        public int CabYOffsetPixels { get; set; } // Note: Always -ve. Without it, the cab view is fixed to the top of the screen. -ve values pull it up the screen.
        public int CabXOffsetPixels { get; set; }
        public int CabExceedsDisplay; // difference between cabview texture vertical resolution and display vertical resolution
        public int CabExceedsDisplayHorizontally; // difference between cabview texture horizontal resolution and display vertical resolution
        public float CabTextureInverseRatio = 0.75f; // default of inverse of cab texture ratio 

        public CommandLog Log { get { return Simulator.Log; } }

        public bool DontLoadNightTextures; // Checkbox set and time of day allows not to load textures
        public bool DontLoadDayTextures; // Checkbox set and time of day allows not to load textures
        public bool NightTexturesNotLoaded; // At least one night texture hasn't been loaded
        public bool DayTexturesNotLoaded; // At least one day texture hasn't been loaded
        public long LoadMemoryThreshold; // Above this threshold loader doesn't bulk load day or night textures
        public bool tryLoadingNightTextures = false;
        public bool tryLoadingDayTextures = false;

        public int poscounter = 1; // counter for print position info

        public Camera SuspendedCamera { get; private set; }

        UserInputRailDriver RailDriver;

        public static double DbfEvalAutoPilotTimeS = 0;//Debrief eval
        public static double DbfEvalIniAutoPilotTimeS = 0;//Debrief eval  
        public bool DbfEvalAutoPilot = false;//DebriefEval

        /// <summary>
        /// Finds time of last entry to set ReplayEndsAt and provide the Replay started message.
        /// </summary>
        void InitReplay()
        {
            if (Simulator.ReplayCommandList != null)
            {
                // Get time of last entry
                int lastEntry = Simulator.ReplayCommandList.Count - 1;
                if (lastEntry >= 0)
                {
                    double lastTime = Simulator.ReplayCommandList[lastEntry].Time;
                    Log.ReplayEndsAt = lastTime;
                    double duration = lastTime - Simulator.ClockTime;
                    MessagesWindow.AddMessage(String.Format("Replay started: ending at {0} after {1}",
                        FormatStrings.FormatApproximateTime(lastTime),
                        FormatStrings.FormatTime(duration)),
                        3.0);
                }
            }
        }

        /// <summary>
        /// Initializes a new instances of the <see cref="Viewer3D"/> class based on the specified <paramref name="simulator"/> and <paramref name="game"/>.
        /// </summary>
        /// <param name="simulator">The <see cref="Simulator"/> with which the viewer runs.</param>
        /// <param name="game">The <see cref="Game"/> with which the viewer runs.</param>
        [CallOnThread("Loader")]
        public Viewer(Simulator simulator, Orts.Viewer3D.Processes.Game game)
        {
            Catalog = new GettextResourceManager("RunActivity");
            Random = new Random();
            Simulator = simulator;
            Game = game;
            Settings = simulator.Settings;

            RenderProcess = game.RenderProcess;
            UpdaterProcess = game.UpdaterProcess;
            LoaderProcess = game.LoaderProcess;
            SoundProcess = game.SoundProcess;

            WellKnownCameras = new List<Camera>();
            WellKnownCameras.Add(CabCamera = new CabCamera(this));
            WellKnownCameras.Add(FrontCamera = new TrackingCamera(this, TrackingCamera.AttachedTo.Front));
            WellKnownCameras.Add(BackCamera = new TrackingCamera(this, TrackingCamera.AttachedTo.Rear));
            WellKnownCameras.Add(PassengerCamera = new PassengerCamera(this));
            WellKnownCameras.Add(BrakemanCamera = new BrakemanCamera(this));
            WellKnownCameras.Add(HeadOutForwardCamera = new HeadOutCamera(this, HeadOutCamera.HeadDirection.Forward));
            WellKnownCameras.Add(HeadOutBackCamera = new HeadOutCamera(this, HeadOutCamera.HeadDirection.Backward));
            WellKnownCameras.Add(TracksideCamera = new TracksideCamera(this));
            WellKnownCameras.Add(new FreeRoamCamera(this, FrontCamera)); // Any existing camera will suffice to satisfy .Save() and .Restore()
            WellKnownCameras.Add(ThreeDimCabCamera = new ThreeDimCabCamera(this));

            string ORfilepath = System.IO.Path.Combine(Simulator.RoutePath, "OpenRails");
            ContentPath = Game.ContentPath;
            Trace.Write(" ENV");
            ENVFile = new EnvironmentFile(Simulator.RoutePath + @"\ENVFILES\" + Simulator.TRK.Tr_RouteFile.Environment.ENVFileName(Simulator.Season, Simulator.WeatherType));

            Trace.Write(" SIGCFG");
            if (File.Exists(ORfilepath + @"\sigcfg.dat"))
            {
                Trace.Write(" SIGCFG_OR");
                SIGCFG = new SignalConfigurationFile(ORfilepath + @"\sigcfg.dat", true);
            }
            else
            {
                Trace.Write(" SIGCFG");
                SIGCFG = new SignalConfigurationFile(Simulator.RoutePath + @"\sigcfg.dat", false);
            }

            Trace.Write(" TTYPE");
            TrackTypes = new TrackTypesFile(Simulator.RoutePath + @"\TTYPE.DAT");

            Tiles = new TileManager(Simulator.RoutePath + @"\TILES\", false);
            LoTiles = new TileManager(Simulator.RoutePath + @"\LO_TILES\", true);
            MilepostUnitsMetric = Simulator.TRK.Tr_RouteFile.MilepostUnitsMetric;

            RailDriver = new UserInputRailDriver(Simulator.BasePath);

            Simulator.AllowedSpeedRaised += (object sender, EventArgs e) =>
            {
                var train = sender as Train;
                if (!TrackMonitorWindow.Visible && Simulator.Confirmer != null && train != null)
                {
                    var message = Catalog.GetStringFmt("Allowed speed raised to {0}", FormatStrings.FormatSpeedDisplay(train.AllowedMaxSpeedMpS, MilepostUnitsMetric));
                    Simulator.Confirmer.Message(ConfirmLevel.Information, message);
                }
            };

            Simulator.PlayerLocomotiveChanged += PlayerLocomotiveChanged;
            Simulator.PlayerTrainChanged += PlayerTrainChanged;
            Simulator.RequestTTDetachWindow += RequestTTDetachWindow;

            // The speedpost.dat file is needed only to derive the shape names for the temporary speed restriction zones,
            // so it is opened only in activity mode
            if (Simulator.ActivityRun != null && Simulator.Activity.Tr_Activity.Tr_Activity_File.ActivityRestrictedSpeedZones != null)
            {
                var speedpostDatFile = Simulator.RoutePath + @"\speedpost.dat";
                if (File.Exists(speedpostDatFile))
                {
                    Trace.Write(" SPEEDPOST");
                    SpeedpostDatFile = new SpeedpostDatFile(Simulator.RoutePath + @"\speedpost.dat", Simulator.RoutePath + @"\shapes\");
                }
            }

            Initialize();
        }

        [CallOnThread("Updater")]
        public void Save(BinaryWriter outf, string fileStem)
        {
            outf.Write(Simulator.Trains.IndexOf(PlayerTrain));
            outf.Write(PlayerTrain.Cars.IndexOf(PlayerLocomotive));
            outf.Write(Simulator.Trains.IndexOf(SelectedTrain));

            WindowManager.Save(outf);

            outf.Write(WellKnownCameras.IndexOf(Camera));
            foreach (var camera in WellKnownCameras)
                camera.Save(outf);
            Camera.Save(outf);
            outf.Write(CabYOffsetPixels);
            outf.Write(CabXOffsetPixels);

            // Set these so RenderFrame can use them when its thread gets control.
            SaveActivityFileStem = fileStem;
            SaveActivityThumbnail = true;
            outf.Write(NightTexturesNotLoaded);
            outf.Write(DayTexturesNotLoaded);
            World.WeatherControl.SaveWeatherParameters(outf);
        }

        [CallOnThread("Render")]
        public void Restore(BinaryReader inf)
        {
            Train playerTrain = Simulator.Trains[inf.ReadInt32()];
            PlayerLocomotive = playerTrain.Cars[inf.ReadInt32()];
            var selected = inf.ReadInt32();
            if (selected >= 0 && selected < Simulator.Trains.Count)
            {
                SelectedTrain = Simulator.Trains[selected];
            }
            else if (selected < 0)
            {
                SelectedTrain = Simulator.Trains[0];
            }

            WindowManager.Restore(inf);

            var cameraToRestore = inf.ReadInt32();
            foreach (var camera in WellKnownCameras)
                camera.Restore(inf);
            if (cameraToRestore == -1)
                new FreeRoamCamera(this, Camera).Activate();
            else
                WellKnownCameras[cameraToRestore].Activate();
            Camera.Restore(inf);
            CabYOffsetPixels = inf.ReadInt32();
            CabXOffsetPixels = inf.ReadInt32();
            NightTexturesNotLoaded = inf.ReadBoolean();
            DayTexturesNotLoaded = inf.ReadBoolean();
            LoadMemoryThreshold = (long)HUDWindow.GetVirtualAddressLimit() - 512 * 1024 * 1024;
            tryLoadingNightTextures = true;
            tryLoadingDayTextures = true;

            World.WeatherControl.RestoreWeatherParameters(inf);
        }

        /// <summary>
        /// Called once after the graphics device is ready
        /// to load any static graphics content, background
        /// processes haven't started yet.
        /// </summary>
        [CallOnThread("Loader")]
        internal void Initialize()
        {
            GraphicsDevice = RenderProcess.GraphicsDevice;
            UpdateAdapterInformation(GraphicsDevice.CreationParameters.Adapter);
            DefaultViewport = GraphicsDevice.Viewport;

            if (PlayerLocomotive == null) PlayerLocomotive = Simulator.InitialPlayerLocomotive();
            SelectedTrain = PlayerTrain;
            if (PlayerTrain.TrainType == Train.TRAINTYPE.AI_PLAYERHOSTING)
            {
                Simulator.Trains[0].LeadLocomotive = null;
                Simulator.Trains[0].LeadLocomotiveIndex = -1;
            }

            InitializeAutomaticTrackSounds();

            TextureManager = new SharedTextureManager(this, GraphicsDevice);

            AdjustCabHeight(DisplaySize.X, DisplaySize.Y); // needs TextureManager

            MaterialManager = new SharedMaterialManager(this);
            ShapeManager = new SharedShapeManager(this);

            WindowManager = new WindowManager(this);
            MessagesWindow = new MessagesWindow(WindowManager);
            NoticeWindow = new NoticeWindow(WindowManager);
            PauseWindow = new PauseWindow(WindowManager);
            ActivityWindow = new ActivityWindow(WindowManager);
            QuitWindow = new QuitWindow(WindowManager);
            HelpWindow = new HelpWindow(WindowManager);
            TrackMonitorWindow = new TrackMonitorWindow(WindowManager);
            HUDWindow = new HUDWindow(WindowManager);
            OSDLocations = new OSDLocations(WindowManager);
            OSDCars = new OSDCars(WindowManager);
            SwitchWindow = new SwitchWindow(WindowManager);
            TrainOperationsWindow = new TrainOperationsWindow(WindowManager);
            CarOperationsWindow = new CarOperationsWindow(WindowManager);
            NextStationWindow = new NextStationWindow(WindowManager);
            CompassWindow = new CompassWindow(WindowManager);
            TracksDebugWindow = new TracksDebugWindow(WindowManager);
            SignallingDebugWindow = new SignallingDebugWindow(WindowManager);
            ComposeMessageWindow = new ComposeMessage(WindowManager);
            TrainListWindow = new TrainListWindow(WindowManager);
            TTDetachWindow = new TTDetachWindow(WindowManager);
            WindowManager.Initialize();

            InfoDisplay = new InfoDisplay(this);

            World = new World(this, Simulator.ClockTime);

            Simulator.Confirmer.PlayErrorSound += (s, e) =>
            {
                if (World.GameSounds != null)
                    World.GameSounds.HandleEvent(Event.ControlError);
            };
            Simulator.Confirmer.DisplayMessage += (s, e) => MessagesWindow.AddMessage(e.Key, e.Text, e.Duration);

            if (Simulator.PlayerLocomotive.HasFront3DCab || Simulator.PlayerLocomotive.HasRear3DCab) ThreeDimCabCamera.Activate();
            else if (Simulator.PlayerLocomotive.HasFrontCab || Simulator.PlayerLocomotive.HasRearCab) CabCamera.Activate();
            else CameraActivate();

            // Prepare the world to be loaded and then load it from the correct thread for debugging/tracing purposes.
            // This ensures that a) we have all the required objects loaded when the 3D view first appears and b) that
            // all loading is performed on a single thread that we can handle in debugging and tracing.
            World.LoadPrep();
            if (Simulator.Settings.ConditionalLoadOfDayOrNightTextures) // We need to compute sun height only in this case
            {
            MaterialManager.LoadPrep();
            LoadMemoryThreshold = (long)HUDWindow.GetVirtualAddressLimit() - 512 * 1024 * 1024;
            }
            Load();

            // MUST be after loading is done! (Or we try and load shapes on the main thread.)
            PlayerLocomotiveViewer = World.Trains.GetViewer(PlayerLocomotive);

            SetCommandReceivers();
            InitReplay();
        }

        /// <summary>
        /// Each Command needs to know its Receiver so it can call a method of the Receiver to action the command.
        /// The Receiver is a static property as all commands of the same class share the same Receiver
        /// and it needs to be set before the command is used.
        /// </summary>
        public void SetCommandReceivers()
        {
            ReverserCommand.Receiver = (MSTSLocomotive)PlayerLocomotive;
            NotchedThrottleCommand.Receiver = (MSTSLocomotive)PlayerLocomotive;
            ContinuousThrottleCommand.Receiver = (MSTSLocomotive)PlayerLocomotive;
            TrainBrakeCommand.Receiver = (MSTSLocomotive)PlayerLocomotive;
            EngineBrakeCommand.Receiver = (MSTSLocomotive)PlayerLocomotive;
            DynamicBrakeCommand.Receiver = (MSTSLocomotive)PlayerLocomotive;
            InitializeBrakesCommand.Receiver = PlayerLocomotive.Train;
            EmergencyPushButtonCommand.Receiver = (MSTSLocomotive)PlayerLocomotive;
            HandbrakeCommand.Receiver = (MSTSLocomotive)PlayerLocomotive;
            BailOffCommand.Receiver = (MSTSLocomotive)PlayerLocomotive;
            RetainersCommand.Receiver = (MSTSLocomotive)PlayerLocomotive;
            BrakeHoseConnectCommand.Receiver = (MSTSLocomotive)PlayerLocomotive;
            if (PlayerLocomotive is MSTSSteamLocomotive)
            {
                ContinuousReverserCommand.Receiver = (MSTSSteamLocomotive)PlayerLocomotive;
                ContinuousInjectorCommand.Receiver = (MSTSSteamLocomotive)PlayerLocomotive;
                ContinuousSmallEjectorCommand.Receiver = (MSTSSteamLocomotive)PlayerLocomotive;
                ToggleInjectorCommand.Receiver = (MSTSSteamLocomotive)PlayerLocomotive;
                ContinuousBlowerCommand.Receiver = (MSTSSteamLocomotive)PlayerLocomotive;
                ContinuousDamperCommand.Receiver = (MSTSSteamLocomotive)PlayerLocomotive;
                ContinuousFiringRateCommand.Receiver = (MSTSSteamLocomotive)PlayerLocomotive;
                ToggleManualFiringCommand.Receiver = (MSTSSteamLocomotive)PlayerLocomotive;
                ToggleCylinderCocksCommand.Receiver = (MSTSSteamLocomotive)PlayerLocomotive;
                ToggleCylinderCompoundCommand.Receiver = (MSTSSteamLocomotive)PlayerLocomotive;
                FireShovelfullCommand.Receiver = (MSTSSteamLocomotive)PlayerLocomotive;
                AIFireOnCommand.Receiver = (MSTSSteamLocomotive)PlayerLocomotive;
                AIFireOffCommand.Receiver = (MSTSSteamLocomotive)PlayerLocomotive;
                AIFireResetCommand.Receiver = (MSTSSteamLocomotive)PlayerLocomotive;
            }

            PantographCommand.Receiver = (MSTSLocomotive)PlayerLocomotive;
            if (PlayerLocomotive is MSTSElectricLocomotive)
            {
                CircuitBreakerClosingOrderCommand.Receiver = (MSTSElectricLocomotive)PlayerLocomotive;
                CircuitBreakerClosingOrderButtonCommand.Receiver = (MSTSElectricLocomotive)PlayerLocomotive;
                CircuitBreakerOpeningOrderButtonCommand.Receiver = (MSTSElectricLocomotive)PlayerLocomotive;
                CircuitBreakerClosingAuthorizationCommand.Receiver = (MSTSElectricLocomotive)PlayerLocomotive;
            }

            if (PlayerLocomotive is MSTSDieselLocomotive)
            {
                TogglePlayerEngineCommand.Receiver = (MSTSDieselLocomotive)PlayerLocomotive;
            }

            ImmediateRefillCommand.Receiver = (MSTSLocomotiveViewer)PlayerLocomotiveViewer;
            RefillCommand.Receiver = (MSTSLocomotiveViewer)PlayerLocomotiveViewer;
            ToggleOdometerCommand.Receiver = (MSTSLocomotive)PlayerLocomotive;
            ResetOdometerCommand.Receiver = (MSTSLocomotive)PlayerLocomotive;
            ToggleOdometerDirectionCommand.Receiver = (MSTSLocomotive)PlayerLocomotive;
            SanderCommand.Receiver = (MSTSLocomotive)PlayerLocomotive;
            AlerterCommand.Receiver = (MSTSLocomotive)PlayerLocomotive;
            HornCommand.Receiver = (MSTSLocomotive)PlayerLocomotive;
            BellCommand.Receiver = (MSTSLocomotive)PlayerLocomotive;
            ToggleCabLightCommand.Receiver = (MSTSLocomotive)PlayerLocomotive;
            WipersCommand.Receiver = (MSTSLocomotive)PlayerLocomotive;
            HeadlightCommand.Receiver = (MSTSLocomotive)PlayerLocomotive;
            ChangeCabCommand.Receiver = this;
            ToggleDoorsLeftCommand.Receiver = (MSTSLocomotive)PlayerLocomotive;
            ToggleDoorsRightCommand.Receiver = (MSTSLocomotive)PlayerLocomotive;
            ToggleMirrorsCommand.Receiver = (MSTSLocomotive)PlayerLocomotive;
            CabRadioCommand.Receiver = (MSTSLocomotive)PlayerLocomotive;
            ToggleSwitchAheadCommand.Receiver = this;
            ToggleSwitchBehindCommand.Receiver = this;
            ToggleAnySwitchCommand.Receiver = this;
            UncoupleCommand.Receiver = this;
            SaveScreenshotCommand.Receiver = this;
            ActivityCommand.Receiver = ActivityWindow;  // and therefore shared by all sub-classes
            UseCameraCommand.Receiver = this;
            MoveCameraCommand.Receiver = this;
            ToggleHelpersEngineCommand.Receiver = (MSTSLocomotive)PlayerLocomotive;
        }

        public void ChangeToPreviousFreeRoamCamera()
        {
            if (Camera == FreeRoamCamera)
            {
                // If 8 is the current camera, rotate the list and then activate a different camera.
                RotateFreeRoamCameraList();
                FreeRoamCamera.Activate();
            }
            else
            {
                FreeRoamCamera.Activate();
                RotateFreeRoamCameraList();
            }
        }

        void RotateFreeRoamCameraList()
        {
            // Rotate list moving 1 to 0 etc. (by adding 0 to end, then removing 0)
            FreeRoamCameraList.Add(FreeRoamCamera);
            FreeRoamCameraList.RemoveAt(0);
        }


        public void InitializeAutomaticTrackSounds()
        {
            SharedSMSFileManager.AutoTrackSound = false;
            SharedSMSFileManager.SwitchSMSNumber = Simulator.TRK.Tr_RouteFile.SwitchSMSNumber;

            if (SharedSMSFileManager.SwitchSMSNumber < -1 || SharedSMSFileManager.SwitchSMSNumber >= TrackTypes.Count)
            {
                SharedSMSFileManager.SwitchSMSNumber = -1;
                Trace.TraceInformation("Switch SMS Number out of range");
            }
            if (SharedSMSFileManager.SwitchSMSNumber != -1) SharedSMSFileManager.AutoTrackSound = true;

            SharedSMSFileManager.CurveSMSNumber = Simulator.TRK.Tr_RouteFile.CurveSMSNumber;
            if (SharedSMSFileManager.CurveSMSNumber < -1 || SharedSMSFileManager.CurveSMSNumber >= TrackTypes.Count)
            {
                SharedSMSFileManager.CurveSMSNumber = -1;
                Trace.TraceInformation("Curve SMS Number out of range");
            }
            if (SharedSMSFileManager.CurveSMSNumber != -1) SharedSMSFileManager.AutoTrackSound = true;

            SharedSMSFileManager.CurveSwitchSMSNumber = Simulator.TRK.Tr_RouteFile.CurveSwitchSMSNumber;
            if (SharedSMSFileManager.CurveSwitchSMSNumber < -1 || SharedSMSFileManager.CurveSwitchSMSNumber >= TrackTypes.Count)
            {
                SharedSMSFileManager.CurveSwitchSMSNumber = SharedSMSFileManager.CurveSMSNumber;
                Trace.TraceInformation("CurveSwitch SMS Number out of range, replaced with curve SMS number");
            }
            if (SharedSMSFileManager.CurveSwitchSMSNumber != -1) SharedSMSFileManager.AutoTrackSound = true;

        }

        public void ChangeSelectedTrain(Train selectedTrain)
        {
            SelectedTrain = selectedTrain;
        }

        public void AdjustCabHeight(int windowWidth, int windowHeight)
        {
            CabTextureInverseRatio = 0.75f; // start setting it to default
            // MSTS cab views are designed for 4:3 aspect ratio. This is the default. However a check is done with the actual
            // cabview texture. If this has a different aspect ratio, that one is considered
            // For wider screens (e.g. 16:9), the height of the cab view before adjustment exceeds the height of the display.
            // The user can decide how much of this excess to keep. Setting of 0 keeps all the excess and 100 keeps none.

            // <CSComment> If the aspect ratio of the viewing window is greater than the aspect ratio of the cabview texture file
            // it is either possible to stretch the cabview texture file or to leave the proportions unaltered and to vertically pan
            // the screen
            if (CabCamera.IsAvailable)
            {
                var i = ((PlayerLocomotive as MSTSLocomotive).UsingRearCab) ? 1 : 0;
                var cabTextureFileName = (PlayerLocomotive as MSTSLocomotive).CabViewList[i].CVFFile.TwoDViews[0];
                var cabTextureInverseRatio = ComputeCabTextureInverseRatio(cabTextureFileName);
                if (cabTextureInverseRatio != -1) CabTextureInverseRatio = cabTextureInverseRatio;
            }
            int unstretchedCabHeightPixels = (int)(CabTextureInverseRatio * windowWidth);
            int unstretchedCabWidthPixels = (int)(windowHeight / CabTextureInverseRatio);
            if (((float)windowHeight / windowWidth) < CabTextureInverseRatio)
            {
                // screen is wide-screen, so can choose between vertical scroll or horizontal stretch
                CabExceedsDisplay = (int)((unstretchedCabHeightPixels - windowHeight) * ((100 - Settings.Cab2DStretch) / 100f));
                CabExceedsDisplayHorizontally = 0;
            }
            else if (((float)windowHeight / windowWidth) > CabTextureInverseRatio)
            {
                // must scroll horizontally
                CabExceedsDisplay = 0;
                CabExceedsDisplayHorizontally = unstretchedCabWidthPixels - windowWidth;
            }
            else
            {
                // nice, window aspect ratio and cabview aspect ratio are identical
                CabExceedsDisplay = 0;
                CabExceedsDisplayHorizontally = 0;
            }
            CabHeightPixels = windowHeight + CabExceedsDisplay;
            CabYOffsetPixels = -CabExceedsDisplay / 2; // Initial value is halfway. User can adjust with arrow keys.
            CabWidthPixels = windowWidth + CabExceedsDisplayHorizontally;
            CabXOffsetPixels = CabExceedsDisplayHorizontally / 2;
            if (CabCamera.IsAvailable) CabCamera.Initialize();
        }

        public float ComputeCabTextureInverseRatio(string cabTextureFileName)
        {
            float cabTextureInverseRatio = -1;
            bool _isNightTexture;
            var cabTexture = CABTextureManager.GetTexture(cabTextureFileName, false, false, out _isNightTexture, false);
            if (cabTexture != SharedMaterialManager.MissingTexture)
            {
                cabTextureInverseRatio = (float)cabTexture.Height / cabTexture.Width;
                if (cabTextureInverseRatio == 1 && cabTexture.Width == 1024) cabTextureInverseRatio = 0.75f;
            }
            return cabTextureInverseRatio;
        }

        string adapterDescription;
        public string AdapterDescription { get { return adapterDescription; } }

        uint adapterMemory;
        public uint AdapterMemory { get { return adapterMemory; } }

        [CallOnThread("Updater")]
        internal void UpdateAdapterInformation(GraphicsAdapter graphicsAdapter)
        {
            adapterDescription = graphicsAdapter.Description;
            try
            {
                // Note that we might find multiple adapters with the same
                // description; however, the chance of such adapters not having
                // the same amount of video memory is very slim.
                foreach (ManagementObject videoController in new ManagementClass("Win32_VideoController").GetInstances())
                    if (((string)videoController["Description"] == adapterDescription) && (videoController["AdapterRAM"] != null))
                        adapterMemory = (uint)videoController["AdapterRAM"];
            }
            catch (ManagementException error)
            {
                Trace.WriteLine(error);
            }
            catch (UnauthorizedAccessException error)
            {
                Trace.WriteLine(error);
            }
        }

        [CallOnThread("Loader")]
        public void Load()
        {
            World.Load();
            WindowManager.Load();
        }

        [CallOnThread("Updater")]
        public void Update(RenderFrame frame, float elapsedRealTime)
        {
            RealTime += elapsedRealTime;
            var elapsedTime = new ElapsedTime(Simulator.GetElapsedClockSeconds(elapsedRealTime), elapsedRealTime);

            if (ComposeMessageWindow.Visible == true)
            {
                UserInput.Handled();
                ComposeMessageWindow.AppendMessage(UserInput.GetPressedKeys(), UserInput.GetPreviousPressedKeys());
            }

            HandleUserInput(elapsedTime);
            UserInput.Handled();
            Simulator.Update(elapsedTime.ClockSeconds);
            if (PlayerLocomotive.Train.BrakingTime == -2) // We just had a wagon with stuck brakes
            {
                LoadDefectCarSound(PlayerLocomotive.Train.Cars[-(int)PlayerLocomotive.Train.ContinuousBrakingTime], "BrakesStuck.sms");
            }
            if (MPManager.IsMultiPlayer())
            {
                MPManager.Instance().PreUpdate();
                //get key strokes and determine if some messages should be sent
                MultiPlayerViewer.HandleUserInput();
                MPManager.Instance().Update(Simulator.GameTime);
            }

            RailDriver.Update(PlayerLocomotive);

            // This has to be done also for stopped trains
            var cars = World.Trains.Cars;
            foreach (var car in cars)
                car.Value.UpdateSoundPosition();

            if (Simulator.ReplayCommandList != null)
            {
                Log.Update(Simulator.ReplayCommandList);

                if (Log.PauseState == ReplayPauseState.Due)
                {
                    if (Simulator.Settings.ReplayPauseBeforeEnd)
                    {
                        // Reveal Quit Menu
                        QuitWindow.Visible = Simulator.Paused = !QuitWindow.Visible;
                        Log.PauseState = ReplayPauseState.During;
                    }
                    else
                    {
                        Log.PauseState = ReplayPauseState.Done;
                    }
                }
            }
            if (Log.ReplayComplete)
            {
                MessagesWindow.AddMessage("Replay complete", 2);
                Log.ReplayComplete = false;
            }

            if (frame.IsScreenChanged)
                Camera.ScreenChanged();

            // Update camera first...
            Camera.Update(elapsedTime);
            // No above camera means we're allowed to auto-switch to cab view.
            if ((AbovegroundCamera == null) && Camera.IsUnderground)
            {
                AbovegroundCamera = Camera;
                bool ViewingPlayer = true;

                if (Camera.AttachedCar != null) ViewingPlayer = Camera.AttachedCar.Train == Simulator.PlayerLocomotive.Train;

                if ((Simulator.PlayerLocomotive.HasFront3DCab || Simulator.PlayerLocomotive.HasRear3DCab && ViewingPlayer) && !(Camera is CabCamera))
                {
                    ThreeDimCabCamera.Activate();
                }
                else if ((Simulator.PlayerLocomotive.HasFrontCab || Simulator.PlayerLocomotive.HasRearCab) && ViewingPlayer)
                {
                    CabCamera.Activate();
                }
                else
                {
                    Simulator.Confirmer.Warning(Viewer.Catalog.GetString("Cab view not available"));
                }
            }
            else if (AbovegroundCamera != null
                && Camera.AttachedCar != null
                && Camera.AttachedCar.Train == Simulator.PlayerLocomotive.Train)
            {
                // The AbovegroundCamera.Update() has been creating an odd sound issue when the locomotive is in the tunnel.
                // Allowing the update to take place when only in cab view solved the issue.
                if (Camera == CabCamera)
                    AbovegroundCamera.Update(elapsedTime);
                if (!AbovegroundCamera.IsUnderground)
                {
                    // But only if the user hasn't selected another camera!
                    if (Camera == CabCamera)
                        AbovegroundCamera.Activate();
                    AbovegroundCamera = null;
                }
            }

            World.Update(elapsedTime);

            Simulator.ActiveMovingTable = FindActiveMovingTable();

            frame.PrepareFrame(this);
            Camera.PrepareFrame(frame, elapsedTime);
            frame.PrepareFrame(elapsedTime);
            World.PrepareFrame(frame, elapsedTime);
            InfoDisplay.PrepareFrame(frame, elapsedTime);
            // TODO: This is not correct. The ActivityWindow's PrepareFrame is already called by the WindowManager!
            if (Simulator.ActivityRun != null) ActivityWindow.PrepareFrame(elapsedTime, true);

            WindowManager.PrepareFrame(frame, elapsedTime);
        }

        private void LoadDefectCarSound(TrainCar car, string filename)
        {
            var smsFilePath = Simulator.BasePath + @"\sound\" + filename;
            if (!File.Exists(smsFilePath))
            {
                Trace.TraceWarning("Cannot find defect car sound file {0}", filename);
                return;
            }

            try
            {
                SoundProcess.AddSoundSource(this, new SoundSource(this, car as MSTSWagon, smsFilePath));
            }
            catch (Exception error)
            {
                Trace.WriteLine(new FileLoadException(smsFilePath, error));
            }
        }

        [CallOnThread("Updater")]
        void HandleUserInput(ElapsedTime elapsedTime)
        {
            var train = Program.Viewer.PlayerLocomotive.Train;//DebriefEval

            if (UserInput.IsMouseLeftButtonDown || (Camera is ThreeDimCabCamera && RenderProcess.IsMouseVisible))
            {
                Vector3 nearsource = new Vector3((float)UserInput.MouseX, (float)UserInput.MouseY, 0f);
                Vector3 farsource = new Vector3((float)UserInput.MouseX, (float)UserInput.MouseY, 1f);
                Matrix world = Matrix.CreateTranslation(0, 0, 0);
                NearPoint = DefaultViewport.Unproject(nearsource, Camera.XnaProjection, Camera.XnaView, world);
                FarPoint = DefaultViewport.Unproject(farsource, Camera.XnaProjection, Camera.XnaView, world);
            }

            if (UserInput.IsPressed(UserCommands.CameraReset))
                Camera.Reset();

            Camera.HandleUserInput(elapsedTime);

            if (PlayerLocomotiveViewer != null)
                PlayerLocomotiveViewer.HandleUserInput(elapsedTime);

            InfoDisplay.HandleUserInput(elapsedTime);
            WindowManager.HandleUserInput(elapsedTime);

            // Check for game control keys
            if (MPManager.IsMultiPlayer() && UserInput.IsPressed(UserCommands.GameMultiPlayerTexting))
            {
                if (ComposeMessageWindow == null) ComposeMessageWindow = new ComposeMessage(WindowManager);
                ComposeMessageWindow.InitMessage();
            }
            if (!MPManager.IsMultiPlayer() && UserInput.IsPressed(UserCommands.GamePauseMenu)) { QuitWindow.Visible = Simulator.Paused = !QuitWindow.Visible; }
            if (MPManager.IsMultiPlayer() && UserInput.IsPressed(UserCommands.GamePauseMenu)) { if (Simulator.Confirmer != null) Simulator.Confirmer.Information(Viewer.Catalog.GetString("In MP, use Alt-F4 to quit directly")); }

            if (UserInput.IsPressed(UserCommands.GameFullscreen)) { RenderProcess.ToggleFullScreen(); }
            if (!MPManager.IsMultiPlayer() && UserInput.IsPressed(UserCommands.GamePause)) Simulator.Paused = !Simulator.Paused;
            if (!MPManager.IsMultiPlayer() && UserInput.IsPressed(UserCommands.DebugSpeedUp))
            {
                Simulator.GameSpeed *= 1.5f;
                Simulator.Confirmer.ConfirmWithPerCent(CabControl.SimulationSpeed, CabSetting.Increase, Simulator.GameSpeed * 100);
            }
            if (!MPManager.IsMultiPlayer() && UserInput.IsPressed(UserCommands.DebugSpeedDown))
            {
                Simulator.GameSpeed /= 1.5f;
                Simulator.Confirmer.ConfirmWithPerCent(CabControl.SimulationSpeed, CabSetting.Decrease, Simulator.GameSpeed * 100);
            }
            if (UserInput.IsPressed(UserCommands.DebugSpeedReset))
            {
                Simulator.GameSpeed = 1;
                Simulator.Confirmer.ConfirmWithPerCent(CabControl.SimulationSpeed, CabSetting.Off, Simulator.GameSpeed * 100);
            }
            if (UserInput.IsPressed(UserCommands.GameSave)) { GameStateRunActivity.Save(); }
            if (UserInput.IsPressed(UserCommands.DisplayHelpWindow)) if (UserInput.IsDown(UserCommands.DisplayNextWindowTab)) HelpWindow.TabAction(); else HelpWindow.Visible = !HelpWindow.Visible;
            if (UserInput.IsPressed(UserCommands.DisplayTrackMonitorWindow)) if (UserInput.IsDown(UserCommands.DisplayNextWindowTab)) TrackMonitorWindow.TabAction(); else TrackMonitorWindow.Visible = !TrackMonitorWindow.Visible;
            if (UserInput.IsPressed(UserCommands.DisplayHUD)) if (UserInput.IsDown(UserCommands.DisplayNextWindowTab)) HUDWindow.TabAction(); else HUDWindow.Visible = !HUDWindow.Visible;
            if (UserInput.IsPressed(UserCommands.DisplayStationLabels))
            {
                if (UserInput.IsDown(UserCommands.DisplayNextWindowTab)) OSDLocations.TabAction(); else OSDLocations.Visible = !OSDLocations.Visible;
                if (OSDLocations.Visible)
                {
                    switch (OSDLocations.CurrentDisplayState)
                    {
                        case OSDLocations.DisplayState.Auto:
                            MessagesWindow.AddMessage(Catalog.GetString("Automatic platform and siding labels visible."), 5);
                            break;
                        case OSDLocations.DisplayState.All:
                            MessagesWindow.AddMessage(Catalog.GetString("Platform and siding labels visible."), 5);
                            break;
                        case OSDLocations.DisplayState.Platforms:
                            MessagesWindow.AddMessage(Catalog.GetString("Platform labels visible."), 5);
                            break;
                        case OSDLocations.DisplayState.Sidings:
                            MessagesWindow.AddMessage(Catalog.GetString("Siding labels visible."), 5);
                            break;
                    }
                }
                else
                {
                    MessagesWindow.AddMessage(Catalog.GetString("Platform and siding labels hidden."), 5);
                }
            }
            if (UserInput.IsPressed(UserCommands.DisplayCarLabels))
            {
                if (UserInput.IsDown(UserCommands.DisplayNextWindowTab)) OSDCars.TabAction(); else OSDCars.Visible = !OSDCars.Visible;
                if (OSDCars.Visible)
                {
                    switch (OSDCars.CurrentDisplayState)
                    {
                        case OSDCars.DisplayState.Trains:
                            MessagesWindow.AddMessage(Catalog.GetString("Train labels visible."), 5);
                            break;
                        case OSDCars.DisplayState.Cars:
                            MessagesWindow.AddMessage(Catalog.GetString("Car labels visible."), 5);
                            break;
                    }
                }
                else
                {
                    MessagesWindow.AddMessage(Catalog.GetString("Train and car labels hidden."), 5);
                }
            }
            if (UserInput.IsPressed(UserCommands.DisplaySwitchWindow)) if (UserInput.IsDown(UserCommands.DisplayNextWindowTab)) SwitchWindow.TabAction(); else SwitchWindow.Visible = !SwitchWindow.Visible;
            if (UserInput.IsPressed(UserCommands.DisplayTrainOperationsWindow)) if (UserInput.IsDown(UserCommands.DisplayNextWindowTab)) TrainOperationsWindow.TabAction(); else { TrainOperationsWindow.Visible = !TrainOperationsWindow.Visible; if (!TrainOperationsWindow.Visible) CarOperationsWindow.Visible = false; }
            if (UserInput.IsPressed(UserCommands.DisplayNextStationWindow)) if (UserInput.IsDown(UserCommands.DisplayNextWindowTab)) NextStationWindow.TabAction(); else NextStationWindow.Visible = !NextStationWindow.Visible;
            if (UserInput.IsPressed(UserCommands.DisplayCompassWindow)) if (UserInput.IsDown(UserCommands.DisplayNextWindowTab)) CompassWindow.TabAction(); else CompassWindow.Visible = !CompassWindow.Visible;
            if (UserInput.IsPressed(UserCommands.DebugTracks)) if (UserInput.IsDown(UserCommands.DisplayNextWindowTab)) TracksDebugWindow.TabAction(); else TracksDebugWindow.Visible = !TracksDebugWindow.Visible;
            if (UserInput.IsPressed(UserCommands.DebugSignalling)) if (UserInput.IsDown(UserCommands.DisplayNextWindowTab)) SignallingDebugWindow.TabAction(); else SignallingDebugWindow.Visible = !SignallingDebugWindow.Visible;
            if (UserInput.IsPressed(UserCommands.DisplayBasicHUDToggle)) HUDWindow.ToggleBasicHUD();
            if (UserInput.IsPressed(UserCommands.DisplayTrainListWindow)) TrainListWindow.Visible = !TrainListWindow.Visible;


            if (UserInput.IsPressed(UserCommands.GameChangeCab))
            {
                if (PlayerLocomotive.ThrottlePercent >= 1
                    || Math.Abs(PlayerLocomotive.SpeedMpS) > 1
                    || !IsReverserInNeutral(PlayerLocomotive))
                {
                    Simulator.Confirmer.Warning(CabControl.ChangeCab, CabSetting.Warn2);
                }
                else
                {
                    new ChangeCabCommand(Log);
                }
            }

            if (UserInput.IsPressed(UserCommands.CameraCab))
            {
                if (CabCamera.IsAvailable)
                {
                    new UseCabCameraCommand(Log);
                }
                else if (ThreeDimCabCamera.IsAvailable)
                {
                    new Use3DCabCameraCommand(Log);
                }
                else
                {
                    Simulator.Confirmer.Warning(Viewer.Catalog.GetString("Cab view not available"));
                }
            }
            if (UserInput.IsPressed(UserCommands.CameraThreeDimensionalCab))
            {
                if (ThreeDimCabCamera.IsAvailable)
                {
                    new Use3DCabCameraCommand(Log);
                }
                else
                {
                    Simulator.Confirmer.Warning(Viewer.Catalog.GetString("3D Cab view not available"));
                }
            }
            if (UserInput.IsPressed(UserCommands.CameraOutsideFront))
            {
                CheckReplaying();
                new UseFrontCameraCommand(Log);
            }
            if (UserInput.IsPressed(UserCommands.CameraOutsideRear))
            {
                CheckReplaying();
                new UseBackCameraCommand(Log);
            }
            if (UserInput.IsPressed(UserCommands.CameraJumpingTrains)) RandomSelectTrain(); //hit Alt-9 key, random selected train to have 2 and 3 camera attached to

            if (UserInput.IsPressed(UserCommands.CameraVibrate))
            {
                Program.Simulator.CarVibrating = (Program.Simulator.CarVibrating + 1) % 4;
                Simulator.Confirmer.Message(ConfirmLevel.Information, Catalog.GetStringFmt("Vibrating at level {0}", Program.Simulator.CarVibrating));
                Settings.CarVibratingLevel = Program.Simulator.CarVibrating;
                Settings.Save("CarVibratingLevel");
            }

            //hit 9 key, get back to player train
            if (UserInput.IsPressed(UserCommands.CameraJumpBackPlayer))
            {
                SelectedTrain = PlayerTrain;
                CameraActivate();
            }
            if (UserInput.IsPressed(UserCommands.CameraTrackside))
            {
                CheckReplaying();
                new UseTracksideCameraCommand(Log);
            }
            // Could add warning if PassengerCamera not available.
            if (UserInput.IsPressed(UserCommands.CameraPassenger) && PassengerCamera.IsAvailable)
            {
                CheckReplaying();
                new UsePassengerCameraCommand(Log);
            }
            if (UserInput.IsPressed(UserCommands.CameraBrakeman))
            {
                CheckReplaying();
                new UseBrakemanCameraCommand(Log);
            }
            if (UserInput.IsPressed(UserCommands.CameraFree))
            {
                CheckReplaying();
                new UseFreeRoamCameraCommand(Log);
                Simulator.Confirmer.Message(ConfirmLevel.None, Catalog.GetPluralStringFmt(
                    "{0} viewpoint stored. Use Shift+8 to restore viewpoints.", "{0} viewpoints stored. Use Shift+8 to restore viewpoints.", FreeRoamCameraList.Count - 1));
            }
            if (UserInput.IsPressed(UserCommands.CameraPreviousFree))
            {
                if (FreeRoamCameraList.Count > 0)
                {
                    CheckReplaying();
                    new UsePreviousFreeRoamCameraCommand(Log);
                }
            }
            if (UserInput.IsPressed(UserCommands.CameraHeadOutForward) && HeadOutForwardCamera.IsAvailable)
            {
                CheckReplaying();
                new UseHeadOutForwardCameraCommand(Log);
            }
            if (UserInput.IsPressed(UserCommands.CameraHeadOutBackward) && HeadOutBackCamera.IsAvailable)
            {
                CheckReplaying();
                new UseHeadOutBackCameraCommand(Log);
            }
            if (UserInput.IsPressed(UserCommands.GameSwitchAhead))
            {
                if (PlayerTrain.ControlMode == Train.TRAIN_CONTROL.MANUAL || PlayerTrain.ControlMode == Train.TRAIN_CONTROL.EXPLORER)
                    new ToggleSwitchAheadCommand(Log);
                else
                    Simulator.Confirmer.Warning(CabControl.SwitchAhead, CabSetting.Warn1);
            }
            if (UserInput.IsPressed(UserCommands.GameSwitchBehind))
            {
                if (PlayerTrain.ControlMode == Train.TRAIN_CONTROL.MANUAL || PlayerTrain.ControlMode == Train.TRAIN_CONTROL.EXPLORER)
                    new ToggleSwitchBehindCommand(Log);
                else
                    Simulator.Confirmer.Warning(CabControl.SwitchBehind, CabSetting.Warn1);
            }
            if (UserInput.IsPressed(UserCommands.GameClearSignalForward)) PlayerTrain.RequestSignalPermission(Direction.Forward);
            if (UserInput.IsPressed(UserCommands.GameClearSignalBackward)) PlayerTrain.RequestSignalPermission(Direction.Reverse);
            if (UserInput.IsPressed(UserCommands.GameResetSignalForward)) PlayerTrain.RequestResetSignal(Direction.Forward);
            if (UserInput.IsPressed(UserCommands.GameResetSignalBackward)) PlayerTrain.RequestResetSignal(Direction.Reverse);

            if (UserInput.IsPressed(UserCommands.GameSwitchManualMode)) PlayerTrain.RequestToggleManualMode();

            if (UserInput.IsPressed(UserCommands.GameMultiPlayerDispatcher)) { DebugViewerEnabled = !DebugViewerEnabled; return; }
            if (UserInput.IsPressed(UserCommands.DebugSoundForm)) { SoundDebugFormEnabled = !SoundDebugFormEnabled; return; }

            if (UserInput.IsPressed(UserCommands.CameraJumpSeeSwitch))
            {
                if (Program.DebugViewer != null && Program.DebugViewer.Enabled && (Program.DebugViewer.switchPickedItem != null || Program.DebugViewer.signalPickedItem != null))
                {
                    WorldLocation wos;
                    try
                    {
                        if (Program.DebugViewer.switchPickedItem != null)
                        {
                            TrJunctionNode nextSwitchTrack = Program.DebugViewer.switchPickedItem.Item.TrJunctionNode;
                            wos = new WorldLocation(nextSwitchTrack.TN.UiD.TileX, nextSwitchTrack.TN.UiD.TileZ, nextSwitchTrack.TN.UiD.X, nextSwitchTrack.TN.UiD.Y + 8, nextSwitchTrack.TN.UiD.Z);
                        }
                        else
                        {
                            var s = Program.DebugViewer.signalPickedItem.Item;
                            wos = new WorldLocation(s.TileX, s.TileZ, s.X, s.Y + 8, s.Z);
                        }
                        if (FreeRoamCameraList.Count == 0)
                        {
                            new UseFreeRoamCameraCommand(Log);
                        }
                        FreeRoamCamera.SetLocation(wos);
                        //FreeRoamCamera
                        FreeRoamCamera.Activate();
                    }
                    catch { }


                }
            }

            // Turntable commands
            if (Simulator.MovingTables != null)
            {
                if (UserInput.IsPressed(UserCommands.ControlTurntableClockwise))
                {
                    Simulator.ActiveMovingTable = FindActiveMovingTable();
                    if (Simulator.ActiveMovingTable != null)
                    {
                        TurntableClockwiseCommand.Receiver = Simulator.ActiveMovingTable;
                        new TurntableClockwiseCommand(Log);
                    }
                }
                else if (UserInput.IsReleased(UserCommands.ControlTurntableClockwise) && Simulator.ActiveMovingTable != null)
                {
                    TurntableClockwiseTargetCommand.Receiver = Simulator.ActiveMovingTable;
                    new TurntableClockwiseTargetCommand(Log);
                }

                if (UserInput.IsPressed(UserCommands.ControlTurntableCounterclockwise))
                {
                    Simulator.ActiveMovingTable = FindActiveMovingTable();
                    if (Simulator.ActiveMovingTable != null)
                    {
                        TurntableCounterclockwiseCommand.Receiver = Simulator.ActiveMovingTable;
                        new TurntableCounterclockwiseCommand(Log);
                    }
                }

                else if (UserInput.IsReleased(UserCommands.ControlTurntableCounterclockwise) && Simulator.ActiveMovingTable != null)
                {
                    TurntableCounterclockwiseTargetCommand.Receiver = Simulator.ActiveMovingTable;
                    new TurntableCounterclockwiseTargetCommand(Log);
                }
            }

            if (UserInput.IsPressed(UserCommands.GameAutopilotMode))
            {
                if (PlayerLocomotive.Train.TrainType == Train.TRAINTYPE.AI_PLAYERHOSTING)
                {
                    var success = ((AITrain)PlayerLocomotive.Train).SwitchToPlayerControl();
                    if (success)
                    {   
                        Simulator.Confirmer.Message(ConfirmLevel.Information, Viewer.Catalog.GetString("Switched to player control"));
                        DbfEvalAutoPilot = false;//Debrief eval
                    }
                }
                else if (PlayerLocomotive.Train.TrainType == Train.TRAINTYPE.AI_PLAYERDRIVEN)
                {
                    if (PlayerLocomotive.Train.ControlMode == Train.TRAIN_CONTROL.MANUAL)
                        Simulator.Confirmer.Message(ConfirmLevel.Warning, Viewer.Catalog.GetString("You can't switch from manual to autopilot mode"));
                    else
                    {
                        var success = ((AITrain)PlayerLocomotive.Train).SwitchToAutopilotControl();
                        if (success)
                        {
                            Simulator.Confirmer.Message(ConfirmLevel.Information, Viewer.Catalog.GetString("Switched to autopilot"));
                            DbfEvalIniAutoPilotTimeS = Simulator.ClockTime;//Debrief eval
                            DbfEvalAutoPilot = true;//Debrief eval
                        }
                    }
                }
            }

            if (DbfEvalAutoPilot && (Simulator.ClockTime - DbfEvalIniAutoPilotTimeS) > 1.0000 )
            {              
                DbfEvalAutoPilotTimeS = DbfEvalAutoPilotTimeS + (Simulator.ClockTime - DbfEvalIniAutoPilotTimeS);//Debrief eval
                train.DbfEvalValueChanged = true;
                DbfEvalIniAutoPilotTimeS = Simulator.ClockTime;//Debrief eval
            }
            if (UserInput.IsPressed(UserCommands.DebugDumpKeymap))
            {
                var textPath = Path.Combine(Settings.LoggingPath, "OpenRailsKeyboard.txt");
                Settings.Input.DumpToText(textPath);
                MessagesWindow.AddMessage(Catalog.GetStringFmt("Keyboard map list saved to '{0}'.", textPath), 10);

                var graphicPath = Path.Combine(Settings.LoggingPath, "OpenRailsKeyboard.png");
                Settings.Input.DumpToGraphic(graphicPath);
                MessagesWindow.AddMessage(Catalog.GetStringFmt("Keyboard map image saved to '{0}'.", graphicPath), 10);
            }

            // print position command
            // <Rob Roeterdink (roeter)>
            // code not yet activated - requires changes in output file selection
            // TODO : get proper output file path
            //if (UserInput.IsPressed(UserCommands.PrintTrainPosition))
            //{
            //    if (SelectedTrain != null)
            //    {
            //        var sob = new StringBuilder();
            //        sob.Append("Position : ");
            //        sob.AppendFormat("{0} : Tile : {1}, {2} ; Position : {3}, {4} ; Distance Travelled : {5}\n",
            //            poscounter.ToString(),
            //            SelectedTrain.FrontTDBTraveller.TileX.ToString(),
            //            SelectedTrain.FrontTDBTraveller.TileZ.ToString(),
            //            SelectedTrain.FrontTDBTraveller.X.ToString(),
            //            SelectedTrain.FrontTDBTraveller.Z.ToString(),
            //            SelectedTrain.DistanceTravelledM.ToString());
            //        File.AppendAllText(@"C:\temp\TrainPosition.txt", sob.ToString());

            //        if (Simulator.Confirmer != null)
            //        {
            //            Simulator.Confirmer.Information(sob.ToString());
            //        }

            //        poscounter++;
            //    }
            //}

            //in the dispatcher window, when one clicks a train and "See in Game", will jump to see that train
            if (Program.DebugViewer != null && Program.DebugViewer.ClickedTrain == true)
            {
                Program.DebugViewer.ClickedTrain = false;
                if (SelectedTrain != Program.DebugViewer.PickedTrain)
                {
                    SelectedTrain = Program.DebugViewer.PickedTrain;
                    Simulator.AI.aiListChanged = true;

                    if (SelectedTrain.Cars == null || SelectedTrain.Cars.Count == 0) SelectedTrain = PlayerTrain;

                    CameraActivate();
                }
            }

            //in TrainSwitcher, when one clicks a train, Viewer will jump to see that train
            if (Simulator.TrainSwitcher.ClickedTrainFromList == true)
            {
                Simulator.TrainSwitcher.ClickedTrainFromList = false;
                if (SelectedTrain != Simulator.TrainSwitcher.PickedTrainFromList && SelectedTrain.Cars != null || SelectedTrain.Cars.Count != 0)
                {
                    SelectedTrain = Simulator.TrainSwitcher.PickedTrainFromList;
                    Simulator.AI.aiListChanged = true;

                    CameraActivate();
                }
            }

            if (!Simulator.Paused && UserInput.IsDown(UserCommands.GameSwitchWithMouse))
            {
                ForceMouseVisible = true;
                if (UserInput.IsMouseLeftButtonPressed)
                {
                    TryThrowSwitchAt();
                    UserInput.Handled();
                }
            }
            else if (!Simulator.Paused && UserInput.IsDown(UserCommands.GameUncoupleWithMouse))
            {
                ForceMouseVisible = true;
                if (UserInput.IsMouseLeftButtonPressed)
                {
                    TryUncoupleAt();
                    UserInput.Handled();
                }
            }
            else
            {
                ForceMouseVisible = false;
            }

            // reset cursor type when needed

            if (!(Camera is CabCamera) && !(Camera is ThreeDimCabCamera) && ActualCursor != Cursors.Default) ActualCursor = Cursors.Default;

            // Mouse control for 2D cab

            if (Camera is CabCamera && (PlayerLocomotiveViewer as MSTSLocomotiveViewer)._hasCabRenderer)
            {
                if (UserInput.IsMouseLeftButtonPressed)
                {
                    foreach (var controlRenderer in (PlayerLocomotiveViewer as MSTSLocomotiveViewer)._CabRenderer.ControlMap.Values)
                    {
                        CabViewDiscreteRenderer discreteRenderer = controlRenderer as CabViewDiscreteRenderer;
                        if (discreteRenderer != null && discreteRenderer.IsMouseWithin())
                        {
                            MouseChangingControl = discreteRenderer;
                            break;
                        }
                    }
                }

                if (MouseChangingControl != null)
                {
                    MouseChangingControl.HandleUserInput();
                    if (UserInput.IsMouseLeftButtonReleased)
                    {
                        MouseChangingControl = null;
                        UserInput.Handled();
                    }
                }
            }

            // explore 2D cabview controls

            if (Camera is CabCamera && (PlayerLocomotiveViewer as MSTSLocomotiveViewer)._hasCabRenderer && MouseChangingControl == null && 
                RenderProcess.IsMouseVisible)
            {
                if (!UserInput.IsMouseLeftButtonPressed)
                {
                    foreach (var controlRenderer in (PlayerLocomotiveViewer as MSTSLocomotiveViewer)._CabRenderer.ControlMap.Values)
                    {
                        CabViewDiscreteRenderer discreteRenderer = controlRenderer as CabViewDiscreteRenderer;
                        if (discreteRenderer != null && discreteRenderer.IsMouseWithin())
                        {
                            MousePickedControl = discreteRenderer;
                            break;
                        }
                    }
                    if (MousePickedControl != null & MousePickedControl != OldMousePickedControl)
                    {
                        // say what control you have here
                        Simulator.Confirmer.Message(ConfirmLevel.None, MousePickedControl.GetControlType().ToString());
                    }
                    if (MousePickedControl != null) ActualCursor = Cursors.Hand;
                    else if (ActualCursor == Cursors.Hand) ActualCursor = Cursors.Default;
                    OldMousePickedControl = MousePickedControl;
                    MousePickedControl = null;
                }
            }

            // mouse for 3D camera

            if (Camera is ThreeDimCabCamera && (PlayerLocomotiveViewer as MSTSLocomotiveViewer)._has3DCabRenderer)
            {
                if (UserInput.IsMouseLeftButtonPressed)
                {
                    var trainCarShape = (PlayerLocomotiveViewer as MSTSLocomotiveViewer).ThreeDimentionCabViewer.TrainCarShape;
                    var animatedParts = (PlayerLocomotiveViewer as MSTSLocomotiveViewer).ThreeDimentionCabViewer.AnimateParts;
                    var controlMap = (PlayerLocomotiveViewer as MSTSLocomotiveViewer).ThreeDimentionCabRenderer.ControlMap;
                    float bestD = 0.015f;  // 15 cm squared click range
                    CabViewControlRenderer cabRenderer;
                    foreach (var animatedPart in animatedParts)
                    {
                        var key = animatedPart.Value.Key;
                        try
                        {
                            cabRenderer = controlMap[key];
                        }
                        catch
                        {
                            continue;
                        }
                        if (cabRenderer is CabViewDiscreteRenderer)
                        {
                            foreach (var iMatrix in animatedPart.Value.MatrixIndexes)
                            { 
                                var matrix = Matrix.Identity;
                                var hi = iMatrix;
                                while (hi >= 0 && hi < trainCarShape.Hierarchy.Length && trainCarShape.Hierarchy[hi] != -1)
                                {
                                    Matrix.Multiply(ref matrix, ref trainCarShape.XNAMatrices[hi], out matrix);
                                    hi = trainCarShape.Hierarchy[hi];
                                }
                                matrix = Matrix.Multiply(matrix, trainCarShape.Location.XNAMatrix);
                                var matrixWorldLocation = trainCarShape.Location.WorldLocation;
                                matrixWorldLocation.Location.X = matrix.Translation.X;
                                matrixWorldLocation.Location.Y = matrix.Translation.Y;
                                matrixWorldLocation.Location.Z = -matrix.Translation.Z;
                                Vector3 xnaCenter = Camera.XnaLocation(matrixWorldLocation);
                                float d = ORTSMath.LineSegmentDistanceSq(xnaCenter, NearPoint, FarPoint);
                                if (bestD > d)
                                {
                                    MouseChangingControl = cabRenderer as CabViewDiscreteRenderer;
                                    bestD = d;
                                }
                            }
                        }
                    }
                }

                if (MouseChangingControl != null)
                {
                    MouseChangingControl.HandleUserInput();
                    if (UserInput.IsMouseLeftButtonReleased)
                    {
                        MouseChangingControl = null;
                        UserInput.Handled();
                    }
                }
            }

            // explore 3D cabview controls

            if (Camera is ThreeDimCabCamera && (PlayerLocomotiveViewer as MSTSLocomotiveViewer)._has3DCabRenderer && MouseChangingControl == null &&
                RenderProcess.IsMouseVisible)
            {
                if (!UserInput.IsMouseLeftButtonPressed)
                {
                    var trainCarShape = (PlayerLocomotiveViewer as MSTSLocomotiveViewer).ThreeDimentionCabViewer.TrainCarShape;
                    var animatedParts = (PlayerLocomotiveViewer as MSTSLocomotiveViewer).ThreeDimentionCabViewer.AnimateParts;
                    var controlMap = (PlayerLocomotiveViewer as MSTSLocomotiveViewer).ThreeDimentionCabRenderer.ControlMap;
                    float bestD = 0.01f;  // 10 cm squared click range
                    CabViewControlRenderer cabRenderer;
                    foreach (var animatedPart in animatedParts)
                    {
                        var key = animatedPart.Value.Key;
                        try
                        {
                            cabRenderer = controlMap[key];
                        }
                        catch
                        {
                            continue;
                        }
                        if (cabRenderer is CabViewDiscreteRenderer)
                        {
                            foreach (var iMatrix in animatedPart.Value.MatrixIndexes)
                            {
                                var matrix = Matrix.Identity;
                                var hi = iMatrix;
                                while (hi >= 0 && hi < trainCarShape.Hierarchy.Length && trainCarShape.Hierarchy[hi] != -1)
                                {
                                    Matrix.Multiply(ref matrix, ref trainCarShape.XNAMatrices[hi], out matrix);
                                    hi = trainCarShape.Hierarchy[hi];
                                }
                                matrix = Matrix.Multiply(matrix, trainCarShape.Location.XNAMatrix);
                                var matrixWorldLocation = trainCarShape.Location.WorldLocation;
                                matrixWorldLocation.Location.X = matrix.Translation.X;
                                matrixWorldLocation.Location.Y = matrix.Translation.Y;
                                matrixWorldLocation.Location.Z = -matrix.Translation.Z;
                                Vector3 xnaCenter = Camera.XnaLocation(matrixWorldLocation);
                                float d = ORTSMath.LineSegmentDistanceSq(xnaCenter, NearPoint, FarPoint);

                                if (bestD > d)
                                {
                                    MousePickedControl = cabRenderer as CabViewDiscreteRenderer;
                                    bestD = d;
                                }
                            }
                        }
                    }
                    if (MousePickedControl != null & MousePickedControl != OldMousePickedControl)
                    {
                        // say what control you have here
                        Simulator.Confirmer.Message(ConfirmLevel.None, MousePickedControl.GetControlType().ToString());
                    }
                    if (MousePickedControl != null)
                    {
                        ActualCursor = Cursors.Hand;
                    }
                    else if (ActualCursor == Cursors.Hand)
                    {
                        ActualCursor = Cursors.Default;
                    }
                    OldMousePickedControl = MousePickedControl;
                    MousePickedControl = null;
                }
            }

            if (UserInput.RDState != null)
                UserInput.RDState.Handled();

            MouseState currentMouseState = Mouse.GetState();

            if (currentMouseState.X != originalMouseState.X ||
                currentMouseState.Y != originalMouseState.Y)
                MouseVisibleTillRealTime = RealTime + 1;

            RenderProcess.IsMouseVisible = ForceMouseVisible || RealTime < MouseVisibleTillRealTime;
            originalMouseState = currentMouseState;
            RenderProcess.ActualCursor = ActualCursor;
        }

        static bool IsReverserInNeutral(TrainCar car)
        {
            // Diesel and electric locos have a Reverser lever and,
            // in the neutral position, direction == N
            return car.Direction == Direction.N
                // Steam locos never have direction == N, so check for setting close to zero.
            || Math.Abs(car.Train.MUReverserPercent) <= 1;
        }
        /// <summary>
        /// If the player changes the camera during replay, then further replay of the camera is suspended.
        /// The player's camera commands will be recorded instead of the replay camera commands.
        /// Replay and recording of non-camera commands such as controls continues.
        /// </summary>
        public void CheckReplaying()
        {
            if (Simulator.IsReplaying)
            {
                if (!Log.CameraReplaySuspended)
                {
                    Log.CameraReplaySuspended = true;
                    SuspendedCamera = Camera;
                    Simulator.Confirmer.Confirm(CabControl.Replay, CabSetting.Warn1);
                }
            }
        }

        /// <summary>
        /// Replay of the camera is not resumed until the player opens the Quit Menu and then presses Esc to unpause the simulator.
        /// </summary>
        public void ResumeReplaying()
        {
            Log.CameraReplaySuspended = false;
            if (SuspendedCamera != null)
                SuspendedCamera.Activate();
        }

        public void ChangeCab()
        {
            if (!Simulator.PlayerLocomotive.Train.IsChangeCabAvailable()) return;

            Simulator.PlayerLocomotive = Simulator.PlayerLocomotive.Train.GetNextCab();
            PlayerLocomotiveViewer = World.Trains.GetViewer(Simulator.PlayerLocomotive);
            if (PlayerLocomotiveViewer is MSTSLocomotiveViewer && (PlayerLocomotiveViewer as MSTSLocomotiveViewer)._hasCabRenderer)
                AdjustCabHeight(DisplaySize.X, DisplaySize.Y);
            Camera.Activate(); // If you need anything else here the cameras should check for it.        
            SetCommandReceivers();
            ThreeDimCabCamera.ChangeCab(Simulator.PlayerLocomotive);
            HeadOutForwardCamera.ChangeCab(Simulator.PlayerLocomotive);
            HeadOutBackCamera.ChangeCab(Simulator.PlayerLocomotive);
            if (MPManager.IsMultiPlayer())
                MPManager.LocoChange(Simulator.PlayerLocomotive.Train, Simulator.PlayerLocomotive);
            Simulator.Confirmer.Confirm(CabControl.ChangeCab, CabSetting.On);
        }

        /// <summary>
        /// Called when switching player train
        /// </summary>
        void PlayerLocomotiveChanged(object sender, EventArgs e)
        {
            PlayerLocomotiveViewer = World.Trains.GetViewer(Simulator.PlayerLocomotive);
            CabCamera.Activate(); // If you need anything else here the cameras should check for it.
            SetCommandReceivers();
            ThreeDimCabCamera.ChangeCab(Simulator.PlayerLocomotive);
            HeadOutForwardCamera.ChangeCab(Simulator.PlayerLocomotive);
            HeadOutBackCamera.ChangeCab(Simulator.PlayerLocomotive);
        }

        // change reference to player train when switching train in Timetable mode
        void PlayerTrainChanged(object sender, Simulator.PlayerTrainChangedEventArgs e)
        {
            if (SelectedTrain == e.OldTrain)
            {
                SelectedTrain = e.NewTrain;
            }
        }

        // display window for Timetable Player train detach actions
        void RequestTTDetachWindow(object sender, EventArgs e)
        {
            TTDetachWindow.Visible = true;
        }

        // Finds the Turntable or Transfertable nearest to the viewing point
        MovingTable FindActiveMovingTable()
        {
            MovingTable activeMovingTable = null;
            float minDistanceSquared = 1000000f;
            if (Simulator.MovingTables != null)
            {
                foreach (var movingTable in Simulator.MovingTables)
                {

                    if (movingTable.WorldPosition.XNAMatrix.M44 != 100000000)
                    {
                        var distanceSquared = WorldLocation.GetDistanceSquared(movingTable.WorldPosition.WorldLocation, Camera.CameraWorldLocation);
                        if (distanceSquared <= minDistanceSquared && distanceSquared < 160000) //must be the nearest one, but must also be near!
                        {
                            minDistanceSquared = distanceSquared;
                            activeMovingTable = movingTable;
                        }
                    }
                }
            }
            return activeMovingTable;
        }

        [CallOnThread("Loader")]
        public void Mark()
        {
            WindowManager.Mark();
        }

        [CallOnThread("Render")]
        internal void Terminate()
        {
            InfoDisplay.Terminate();
            RailDriver.Shutdown();
        }

        private int trainCount;
        void RandomSelectTrain()
        {
            try
            {
                SortedList<double, Train> users = new SortedList<double, Train>();
                foreach (var t in Simulator.Trains)
                {
                    if (t == null || t.Cars == null || t.Cars.Count == 0) continue;
                    var d = WorldLocation.GetDistanceSquared(t.RearTDBTraveller.WorldLocation, PlayerTrain.RearTDBTraveller.WorldLocation);
                    users.Add(d + Viewer.Random.NextDouble(), t);
                }
                trainCount++;
                if (trainCount >= users.Count) trainCount = 0;

                SelectedTrain = users.ElementAt(trainCount).Value;
                if (SelectedTrain.Cars == null || SelectedTrain.Cars.Count == 0) SelectedTrain = PlayerTrain;

                //if (SelectedTrain.LeadLocomotive == null) SelectedTrain.LeadNextLocomotive();
                //if (SelectedTrain.LeadLocomotive != null) { PlayerLocomotive = SelectedTrain.LeadLocomotive; PlayerLocomotiveViewer = World.Trains.GetViewer(Simulator.PlayerLocomotive); }

            }
            catch
            {
                SelectedTrain = PlayerTrain;
            }
            Simulator.AI.aiListChanged = true;
            CameraActivate();
        }

        /// <summary>
        /// The user has left-clicked with U pressed.
        /// If the mouse was over a coupler, then uncouple the car.
        /// </summary>
        void TryUncoupleAt()
        {
            // Create a ray from the near clip plane to the far clip plane.
            Vector3 direction = FarPoint - NearPoint;
            direction.Normalize();
            Ray pickRay = new Ray(NearPoint, direction);

            // check each car
            Traveller traveller = new Traveller(PlayerTrain.FrontTDBTraveller, Traveller.TravellerDirection.Backward);
            int carNo = 0;
            foreach (TrainCar car in PlayerTrain.Cars)
            {
                float d = (car.CouplerSlackM + car.GetCouplerZeroLengthM()) / 2;
                traveller.Move(car.CarLengthM + d);

                Vector3 xnaCenter = Camera.XnaLocation(traveller.WorldLocation);
                float radius = 2f;  // 2 meter click range
                BoundingSphere boundingSphere = new BoundingSphere(xnaCenter, radius);

                if (null != pickRay.Intersects(boundingSphere))
                {
                    new UncoupleCommand(Log, carNo);
                    break;
                }
                traveller.Move(d);
                carNo++;
            }
        }

        /// <summary>
        /// The user has left-clicked with Alt key pressed.
        /// If the mouse was over a switch, then toggle the switch.
        /// No action if toggling blocks the player loco's path.
        /// </summary>
        void TryThrowSwitchAt()
        {
            TrackNode bestTn = null;
            float bestD = 10;
            // check each switch
            for (int j = 0; j < Simulator.TDB.TrackDB.TrackNodes.Count(); j++)
            {
                TrackNode tn = Simulator.TDB.TrackDB.TrackNodes[j];
                if (tn != null && tn.TrJunctionNode != null)
                {

                    Vector3 xnaCenter = Camera.XnaLocation(new WorldLocation(tn.UiD.TileX, tn.UiD.TileZ, tn.UiD.X, tn.UiD.Y, tn.UiD.Z));
                    float d = ORTSMath.LineSegmentDistanceSq(xnaCenter, NearPoint, FarPoint);

                    if (bestD > d)
                    {
                        bestTn = tn;
                        bestD = d;
                    }
                }
            }
            if (bestTn != null)
            {
                new ToggleAnySwitchCommand(Log, bestTn.TCCrossReference[0].Index);
            }
        }

        public void ToggleAnySwitch(int index)
        {
            Simulator.Signals.RequestSetSwitch(index);
        }
        public void ToggleSwitchAhead()
        {
            if (PlayerTrain.ControlMode == Train.TRAIN_CONTROL.MANUAL)
            {
                PlayerTrain.ProcessRequestManualSetSwitch(Direction.Forward);
            }
            else if (PlayerTrain.ControlMode == Train.TRAIN_CONTROL.EXPLORER)
            {
                PlayerTrain.ProcessRequestExplorerSetSwitch(Direction.Forward);
            }
        }

        public void ToggleSwitchBehind()
        {
            if (PlayerTrain.ControlMode == Train.TRAIN_CONTROL.MANUAL)
            {
                PlayerTrain.ProcessRequestManualSetSwitch(Direction.Reverse);
            }
            else if (PlayerTrain.ControlMode == Train.TRAIN_CONTROL.EXPLORER)
            {
                PlayerTrain.ProcessRequestExplorerSetSwitch(Direction.Reverse);
            }
        }

        internal void UncoupleBehind(int carPosition)
        {
            Simulator.UncoupleBehind(carPosition);
            //make the camera train to be the player train
            if (PlayerLocomotive != null && PlayerLocomotive.Train != null) this.SelectedTrain = PlayerLocomotive.Train;
            CameraActivate();
        }

        internal void BeginRender(RenderFrame frame)
        {
            if (frame.IsScreenChanged)
            {
                WindowManager.ScreenChanged();
                AdjustCabHeight(RenderProcess.GraphicsDeviceManager.PreferredBackBufferWidth, RenderProcess.GraphicsDeviceManager.PreferredBackBufferHeight);
            }

            MaterialManager.UpdateShaders();
        }

        internal void EndRender(RenderFrame frame)
        {
            // VisibilityState is used to delay calling SaveScreenshot() by one render cycle.
            // We want the hiding of the MessageWindow to take effect on the screen before the screen content is saved.
            if (Visibility == VisibilityState.Hidden)  // Test for Hidden state must come before setting Hidden state.
            {
                Visibility = VisibilityState.ScreenshotPending;  // Next state else this path would be taken more than once.
                if (!Directory.Exists(Settings.ScreenshotPath))
                    Directory.CreateDirectory(Settings.ScreenshotPath);
                var fileName = Path.Combine(Settings.ScreenshotPath, System.Windows.Forms.Application.ProductName + " " + DateTime.Now.ToString("yyyy-MM-dd hh-mm-ss")) + ".png";
                SaveScreenshotToFile(Game.GraphicsDevice, fileName, false);
                SaveScreenshot = false; // cancel trigger
            }
            if (SaveScreenshot)
            {
                Visibility = VisibilityState.Hidden;
                // Hide MessageWindow
                MessagesWindow.Visible = false;
                // Audible confirmation that screenshot taken
                if (World.GameSounds != null) World.GameSounds.HandleEvent(Event.ControlError);
            }

            // Use IsDown() not IsPressed() so users can take multiple screenshots as fast as possible by holding down the key.
            if (UserInput.IsDown(UserCommands.GameScreenshot)
                && Visibility == VisibilityState.Visible) // Ensure we only get one screenshot.
                new SaveScreenshotCommand(Log);

            // SaveActivityThumbnail and FileStem set by Viewer3D
            // <CJComment> Intended to save a thumbnail-sized image but can't find a way to do this.
            // Currently saving a full screen image and then showing it in Menu.exe at a thumbnail size.
            // </CJComment>
            if (SaveActivityThumbnail)
            {
                SaveActivityThumbnail = false;
                SaveScreenshotToFile(Game.GraphicsDevice, Path.Combine(UserSettings.UserDataFolder, SaveActivityFileStem + ".png"), true);
                MessagesWindow.AddMessage(Catalog.GetString("Game saved"), 5);
            }
        }

        [CallOnThread("Render")]
        void SaveScreenshotToFile(GraphicsDevice graphicsDevice, string fileName, bool silent)
        {
            var screenshot = new ResolveTexture2D(graphicsDevice, graphicsDevice.PresentationParameters.BackBufferWidth, graphicsDevice.PresentationParameters.BackBufferHeight, 1, SurfaceFormat.Color);
            graphicsDevice.ResolveBackBuffer(screenshot);
            new Thread(() =>
            {
                try
                {
                    // Unfortunately, the back buffer includes an alpha channel. Although saving this might seem okay,
                    // it actually ruins the picture - nothing in the back buffer is seen on-screen according to its
                    // alpha, it's only used for blending (if at all). We'll remove the alpha here.
                    var data = new uint[screenshot.Width * screenshot.Height];
                    screenshot.GetData(data);
                    for (var i = 0; i < data.Length; i++)
                        data[i] |= 0xFF000000;
                    screenshot.SetData(data);

                    // Now save the modified image.
                    screenshot.Save(fileName, ImageFileFormat.Png);
                    screenshot.Dispose();

                    if (!silent)
                        MessagesWindow.AddMessage(String.Format("Saving screenshot to '{0}'.", fileName), 10);

                    Visibility = VisibilityState.Visible;
                    // Reveal MessageWindow
                    MessagesWindow.Visible = true;
                }
                catch { }
            }).Start();
        }
    }
}