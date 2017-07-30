using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using GMap.NET;
using GMap.NET.MapProviders;
using GMap.NET.WindowsForms;
using GMap.NET.WindowsForms.Markers;
using log4net;
using MissionPlanner.Controls;
using MissionPlanner.Joystick;
using MissionPlanner.Log;
using MissionPlanner.Utilities;
using MissionPlanner.Warnings;
using OpenTK;
using ZedGraph;
using LogAnalyzer = MissionPlanner.Utilities.LogAnalyzer;

namespace MissionPlanner.GCSViews
{
    public partial class FlightDataCustom : MyUserControl, IActivate, IDeactivate
    {
        //Var Global
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        public static bool threadrun;
        int tickStart;

        internal static GMapOverlay tfrpolygons;
        internal static GMapOverlay kmlpolygons;
        internal static GMapOverlay geofence;
        internal static GMapOverlay rallypointoverlay;
        internal static GMapOverlay poioverlay = new GMapOverlay("POI"); // poi layer

        Dictionary<Guid, Form> formguids = new Dictionary<Guid, Form>();

        Thread thisthread;
        List<PointLatLng> trackPoints = new List<PointLatLng>();

        const float rad2deg = (float)(180 / Math.PI);

        const float deg2rad = (float)(1.0 / rad2deg);

        public static GMapControl mymap;

        bool playingLog;
        double LogPlayBackSpeed = 1.0;

        GMapMarker marker;

        public static FlightDataCustom instance;



        public FlightDataCustom()
        {
            log.Info("Ctor Start");
            InitializeComponent();

            log.Info("Components Done");
            instance = this;

            mymap = myGMAP1;
            //MainHcopy = MainH;

            //log.Info("Tunning Graph Settings");
            MainV2.comPort.MavChanged += comPort_MavChanged;

            //log.Info("HUD Settings");

            List<string> list = new List<string>();

            {
                list.Add("LOITER_UNLIM");
                list.Add("RETURN_TO_LAUNCH");
                list.Add("PREFLIGHT_CALIBRATION");
                list.Add("MISSION_START");
                list.Add("PREFLIGHT_REBOOT_SHUTDOWN");
                //DO_SET_SERVO
                //DO_REPEAT_SERVO
            }

            //combo box

            // config map   
            log.Info("Map Setup");
            myGMAP1.CacheLocation = Path.GetDirectoryName(Application.ExecutablePath) + Path.DirectorySeparatorChar +
                                         "gmapcache" + Path.DirectorySeparatorChar;
            myGMAP1.MapProvider = GMapProviders.GoogleSatelliteMap;
            myGMAP1.MinZoom = 0;
            myGMAP1.MaxZoom = 40;
            myGMAP1.SetPositionByKeywords("Husain Sastranegara, Bandung, Indonesia");
            
            myGMAP1.Zoom = 19;

            //myGMAP1.OnMapZoomChanged += myGMAP1_OnMapZoomChanged;

            myGMAP1.DisableFocusOnMouseEnter = true;

            myGMAP1.OnMarkerEnter += myGMAP1_OnMarkerEnter;
            myGMAP1.OnMarkerLeave += myGMAP1_OnMarkerLeave;

            myGMAP1.RoutesEnabled = true;
            myGMAP1.PolygonsEnabled = true;

            tfrpolygons = new GMapOverlay("tfrpolygons");
            myGMAP1.Overlays.Add(tfrpolygons);

            kmlpolygons = new GMapOverlay("kmlpolygons");
            myGMAP1.Overlays.Add(kmlpolygons);

            geofence = new GMapOverlay("geofence");
            myGMAP1.Overlays.Add(geofence);

            polygons = new GMapOverlay("polygons");
            myGMAP1.Overlays.Add(polygons);

            routes = new GMapOverlay("routes");
            myGMAP1.Overlays.Add(routes);

            rallypointoverlay = new GMapOverlay("rally points");
            myGMAP1.Overlays.Add(rallypointoverlay);

            myGMAP1.Overlays.Add(poioverlay);


            MainV2.comPort.ParamListChanged += FlightData_ParentChanged;

            //resize instrument

            //var instrumenWidth = this.Width / 4;
            //panelInstrumen.Width = instrumenWidth;
            //headingIndicatorInstrumentControl1.Width = instrumenWidth;
            //attitudeIndicatorInstrumentControl1.Width = instrumenWidth;
            //panelInstrumen.Height = Convert.ToInt32(instrumenWidth * 3.2);

            //MainV2.AdvancedChanged += MainV2_AdvancedChanged;

            // first run
            //MainV2_AdvancedChanged(null, null);


        }

        GMapOverlay polygons;
        GMapOverlay routes;
        GMapRoute route;
        
        //methods
        //void NoFly_NoFlyEvent(object sender, NoFly.NoFly.NoFlyEventArgs e)
        //void mymap_Paint(object sender, PaintEventArgs e)
        void comPort_MavChanged(object sender, EventArgs e)
        {
            log.Info("Mav Changed " + MainV2.comPort.MAV.sysid);

            HUD.Custom.src = MainV2.comPort.MAV.cs;

            CustomWarning.defaultsrc = MainV2.comPort.MAV.cs;

            MissionPlanner.Controls.PreFlight.CheckListItem.defaultsrc = MainV2.comPort.MAV.cs;
        }

        internal GMapMarker CurrentGMapMarker;
        void myGMAP1_OnMarkerLeave(GMapMarker item)
        {
            CurrentGMapMarker = null;
        }

        void myGMAP1_OnMarkerEnter(GMapMarker item)
        {
            CurrentGMapMarker = item;
        }
        //void tabStatus_Resize(object sender, EventArgs e)
        //private void MainV2_AdvancedChanged(object sender, EventArgs e)

        public void Activate()
        {
            log.Info("Activate Called");
            OnResize(new EventArgs());
            //setting HUD1
            //Setting QuickView
            //check batteryshow
           

        }
         //public void CheckBatteryShow()
        public void Deactivate()
        {
            //hud 1 setting
            //     hud1.Location = new Point(-1000,-1000);
            //store map pos saat di akhir doni
            //MainV2.config["maplast_lat"] = myGMAP1.Position.Lat;
            //MainV2.config["maplast_lng"] = myGMAP1.Position.Lng;
            //MainV2.config["maplast_zoom"] = myGMAP1.Zoom;

            //ZedGraphTimer.Stop();
        }

        private void FlightData_Load(object sender, EventArgs e)
        {
            POI.POIModified += POI_POIModified;

            tfr.GotTFRs += tfr_GotTFRs;

            NoFly.NoFly.NoFlyEvent += NoFly_NoFlyEvent;

            // map zoom setting

            myGMAP1.EmptyTileColor = Color.Gray;

            //combo box MountMode

            //setting menurut checkbox ada autopan, flightsplitter, russian HUD
            //resize HUD

            thisthread = new Thread(mainloop);
            thisthread.Name = "FD Mainloop";
            thisthread.IsBackground = true;
            thisthread.Start();

        }
        void tfr_GotTFRs(object sender, EventArgs e)
        {
            Invoke((Action)delegate
            {
                foreach (var item in tfr.tfrs)
                {
                    List<List<PointLatLng>> points = item.GetPaths();

                    foreach (var list in points)
                    {
                        GMapPolygon poly = new GMapPolygon(list, item.NAME);

                        poly.Fill = new SolidBrush(Color.FromArgb(30, Color.Blue));

                        tfrpolygons.Polygons.Add(poly);
                    }
                }
                tfrpolygons.IsVisibile = MainV2.ShowTFR;
            });
        }
        void POI_POIModified(object sender, EventArgs e)
        {
            POI.UpdateOverlay(poioverlay);
        }

        void NoFly_NoFlyEvent(object sender, NoFly.NoFly.NoFlyEventArgs e)
        {
            Invoke((Action)delegate
            {
                foreach (var poly in e.NoFlyZones.Polygons)
                {
                    kmlpolygons.Polygons.Add(poly);
                }
            });
        }

        //MAIN LOOP IMPORTANT
        private void mainloop()
        {
            threadrun = true;
            EndPoint Remote = new IPEndPoint(IPAddress.Any, 0);

            DateTime tracklast = DateTime.Now.AddSeconds(0);

            DateTime tunning = DateTime.Now.AddSeconds(0);

            DateTime mapupdate = DateTime.Now.AddSeconds(0);

            DateTime vidrec = DateTime.Now.AddSeconds(0);

            DateTime waypoints = DateTime.Now.AddSeconds(0);

            DateTime updatescreen = DateTime.Now;


            DateTime tsreal = DateTime.Now;
            double taketime = 0;
            double timeerror = 0;

            while (threadrun)
            {
                if (MainV2.comPort.giveComport)
                {
                    Thread.Sleep(50);
                    continue;
                }

                if (!MainV2.comPort.logreadmode)
                    Thread.Sleep(50); // max is only ever 10 hz but we go a little faster to empty the serial queue

                //video setting
                //log playback
                if (MainV2.comPort.logreadmode && MainV2.comPort.logplaybackfile != null)
                {
                    if (MainV2.comPort.BaseStream.IsOpen)
                    {
                        MainV2.comPort.logreadmode = false;
                        try
                        {
                            MainV2.comPort.logplaybackfile.Close();
                        }
                        catch
                        {
                            log.Error("Failed to close logfile");
                        }
                        MainV2.comPort.logplaybackfile = null;
                    }


                    //Console.WriteLine(DateTime.Now.Millisecond);

                    if (updatescreen.AddMilliseconds(300) < DateTime.Now)
                    {
                        try
                        {
                            updatePlayPauseButton(true);
                            updateLogPlayPosition();
                        }
                        catch
                        {
                            log.Error("Failed to update log playback pos");
                        }
                        updatescreen = DateTime.Now;
                    }

                    //Console.WriteLine(DateTime.Now.Millisecond + " done ");

                    DateTime logplayback = MainV2.comPort.lastlogread;
                    try
                    {
                        MainV2.comPort.readPacket();
                    }
                    catch
                    {
                        log.Error("Failed to read log packet");
                    }

                    double act = (MainV2.comPort.lastlogread - logplayback).TotalMilliseconds;

                    if (act > 9999 || act < 0)
                        act = 0;

                    double ts = 0;
                    if (LogPlayBackSpeed == 0)
                        LogPlayBackSpeed = 0.01;
                    try
                    {
                        ts = Math.Min((act / LogPlayBackSpeed), 1000);
                    }
                    catch
                    {
                    }

                    double timetook = (DateTime.Now - tsreal).TotalMilliseconds;
                    if (timetook != 0)
                    {
                        //Console.WriteLine("took: " + timetook + "=" + taketime + " " + (taketime - timetook) + " " + ts);
                        //Console.WriteLine(MainV2.comPort.lastlogread.Second + " " + DateTime.Now.Second + " " + (MainV2.comPort.lastlogread.Second - DateTime.Now.Second));
                        //if ((taketime - timetook) < 0)
                        {
                            timeerror += (taketime - timetook);
                            if (ts != 0)
                            {
                                ts += timeerror;
                                timeerror = 0;
                            }
                        }
                        if (Math.Abs(ts) > 1000)
                            ts = 1000;
                    }

                    taketime = ts;
                    tsreal = DateTime.Now;

                    if (ts > 0 && ts < 1000)
                        Thread.Sleep((int)ts);

                    tracklast = tracklast.AddMilliseconds(ts - act);
                    tunning = tunning.AddMilliseconds(ts - act);

                    if (tracklast.Month != DateTime.Now.Month)
                    {
                        tracklast = DateTime.Now;
                        tunning = DateTime.Now;
                    }

                    try
                    {
                        if (MainV2.comPort.logplaybackfile != null &&
                            MainV2.comPort.logplaybackfile.BaseStream.Position ==
                            MainV2.comPort.logplaybackfile.BaseStream.Length)
                        {
                            MainV2.comPort.logreadmode = false;
                        }
                    }
                    catch
                    {
                        MainV2.comPort.logreadmode = false;
                    }
                }
                else
                {
                    // ensure we know to stop
                    if (MainV2.comPort.logreadmode)
                        MainV2.comPort.logreadmode = false;
                    updatePlayPauseButton(false);

                    if (!playingLog && MainV2.comPort.logplaybackfile != null)
                    {
                        continue;
                    }
                }
                //end log playback

                try
                {
                    CheckAndBindPreFlightData();
                    //doni
                    updateBindingSource();

                    //battery warning
                    float warnvolt = 0;
                    //float.TryParse(MainV2.getConfig("speechbatteryvolt"), out warnvolt);
                    float warnpercent = 0;
                    //float.TryParse(MainV2.getConfig("speechbatterypercent"), out warnpercent);

                    //update hud1 for battery
                    // update opengltest
                    // update opengltest2

                    //update vario info
                    Vario.SetValue(MainV2.comPort.MAV.cs.climbrate);

                    attitudeIndicatorInstrumentControl1.RollAngle = 20;
                    attitudeIndicatorInstrumentControl1._RollAngle = 20;
                    attitudeIndicatorInstrumentControl1.SetAttitudeIndicatorParameters(30, 30);
                    //update tunning tab

                    //update map
                    if (tracklast.AddSeconds(1.2) < DateTime.Now)
                    {
                        //if (MainV2.config["CHK_maprotation"] != null &&
                        //        MainV2.config["CHK_maprotation"].ToString() == "True")
                        //{
                        //    // dont holdinvalidation here
                        //    setMapBearing();
                        //}
                        if (route == null)
                        {
                            route = new GMapRoute(trackPoints, "track");
                            routes.Routes.Add(route);
                        }

                        PointLatLng currentloc = new PointLatLng(MainV2.comPort.MAV.cs.lat, MainV2.comPort.MAV.cs.lng);

                        myGMAP1.HoldInvalidation = true;

                        int cnt = 0;
                        while (myGMAP1.inOnPaint)
                        {
                            Thread.Sleep(1);
                            cnt++;
                        }

                        // maintain route history length
                        //if (route.Points.Count > int.Parse(MainV2.config["NUM_tracklength"].ToString()))
                        //{
                        //    //  trackPoints.RemoveRange(0, trackPoints.Count - int.Parse(MainV2.config["NUM_tracklength"].ToString()));
                        //    route.Points.RemoveRange(0,
                        //        route.Points.Count - int.Parse(MainV2.config["NUM_tracklength"].ToString()));
                        //}
                        // add new route point
                        if (MainV2.comPort.MAV.cs.lat != 0)
                        {
                            // trackPoints.Add(currentloc);
                            route.Points.Add(currentloc);
                        }

                        while (myGMAP1.inOnPaint)
                        {
                            Thread.Sleep(1);
                            cnt++;
                        }
                        //debug route
                        //route = new GMapRoute(route.Points, "track");
                        //track.Stroke = Pens.Red;
                        //route.Stroke = new Pen(Color.FromArgb(144, Color.Red));
                        //route.Stroke.Width = 5;
                        //route.Tag = "track";

                        //updateClearRoutes();
                        myGMAP1.UpdateRouteLocalPosition(route);

                        // update programed wp course
                        if (waypoints.AddSeconds(5) < DateTime.Now)
                        {
                            //Console.WriteLine("Doing FD WP's");
                            updateClearMissionRouteMarkers();

                            float dist = 0;
                            float travdist = 0;
                            distanceBar1.ClearWPDist();
                            MAVLink.mavlink_mission_item_t lastplla = new MAVLink.mavlink_mission_item_t();
                            MAVLink.mavlink_mission_item_t home = new MAVLink.mavlink_mission_item_t();

                            foreach (MAVLink.mavlink_mission_item_t plla in MainV2.comPort.MAV.wps.Values)
                            {
                                if (plla.x == 0 || plla.y == 0)
                                    continue;

                                if (plla.command == (byte)MAVLink.MAV_CMD.DO_SET_ROI)
                                {
                                    addpolygonmarkerred(plla.seq.ToString(), plla.y, plla.x, (int)plla.z, Color.Red,
                                        routes);
                                    continue;
                                }

                                string tag = plla.seq.ToString();
                                if (plla.seq == 0 && plla.current != 2)
                                {
                                    tag = "Home";
                                    home = plla;
                                }
                                if (plla.current == 2)
                                {
                                    continue;
                                }

                                if (lastplla.command == 0)
                                    lastplla = plla;

                                try
                                {
                                    dist =
                                        (float)
                                            new PointLatLngAlt(plla.x, plla.y).GetDistance(new PointLatLngAlt(
                                                lastplla.x, lastplla.y));

                                    distanceBar1.AddWPDist(dist);

                                    if (plla.seq <= MainV2.comPort.MAV.cs.wpno)
                                    {
                                        travdist += dist;
                                    }

                                    lastplla = plla;
                                }
                                catch
                                {
                                }

                                addpolygonmarker(tag, plla.y, plla.x, (int)plla.z, Color.White, polygons);
                            }

                            try
                            {
                                //dist = (float)new PointLatLngAlt(home.x, home.y).GetDistance(new PointLatLngAlt(lastplla.x, lastplla.y));
                                // distanceBar1.AddWPDist(dist);
                            }
                            catch
                            {
                            }

                            travdist -= MainV2.comPort.MAV.cs.wp_dist;

                            if (MainV2.comPort.MAV.cs.mode.ToUpper() == "AUTO")
                                distanceBar1.traveleddist = travdist;

                            RegeneratePolygon();

                            // update rally points

                            rallypointoverlay.Markers.Clear();

                            foreach (var mark in MainV2.comPort.MAV.rallypoints.Values)
                            {
                                rallypointoverlay.Markers.Add(new GMapMarkerRallyPt(mark));
                            }

                            // optional on Flight data
                            if (MainV2.ShowAirports)
                            {
                                // airports
                                foreach (var item in Airports.getAirports(myGMAP1.Position))
                                {
                                    rallypointoverlay.Markers.Add(new GMapMarkerAirport(item)
                                    {
                                        ToolTipText = item.Tag,
                                        ToolTipMode = MarkerTooltipMode.OnMouseOver
                                    });
                                }
                            }
                            waypoints = DateTime.Now;
                        }

                        //debug
                        //routes.Polygons.Add(poly);
                        if (route.Points.Count > 0)
                        {
                            // add primary route icon
                            updateClearRouteMarker(currentloc);

                            // draw guide mode point for only main mav
                            if (MainV2.comPort.MAV.cs.mode.ToLower() == "guided" && MainV2.comPort.MAV.GuidedMode.x != 0)
                            {
                                addpolygonmarker("Guided Mode", MainV2.comPort.MAV.GuidedMode.y,
                                    MainV2.comPort.MAV.GuidedMode.x, (int)MainV2.comPort.MAV.GuidedMode.z, Color.Blue,
                                    routes);
                            }

                            // draw all icons for all connected mavs
                            foreach (var port in MainV2.Comports)
                            {
                                //// draw the mavs seen on this port
                                //foreach (var MAV in port.MAVlist.GetMAVStates())
                                //{
                                //    PointLatLng portlocation = new PointLatLng(MAV.cs.lat, MAV.cs.lng);

                                //    if (MAV.cs.firmware == MainV2.Firmwares.ArduPlane ||
                                //        MAV.cs.firmware == MainV2.Firmwares.Ateryx)
                                //    {
                                //        routes.Markers.Add(new GMapMarkerPlane(portlocation, MAV.cs.yaw,
                                //            MAV.cs.groundcourse, MAV.cs.nav_bearing, MAV.cs.target_bearing)
                                //        {
                                //            ToolTipText = MAV.cs.alt.ToString("0"),
                                //            ToolTipMode = MarkerTooltipMode.Always
                                //        });
                                //    }
                                //    else if (MAV.cs.firmware == MainV2.Firmwares.ArduRover)
                                //    {
                                //        routes.Markers.Add(new GMapMarkerRover(portlocation, MAV.cs.yaw,
                                //            MAV.cs.groundcourse, MAV.cs.nav_bearing, MAV.cs.target_bearing));
                                //    }
                                //    else if (MAV.aptype == MAVLink.MAV_TYPE.HELICOPTER)
                                //    {
                                //        routes.Markers.Add(new GMapMarkerHeli(portlocation, MAV.cs.yaw,
                                //            MAV.cs.groundcourse, MAV.cs.nav_bearing));
                                //    }
                                //    else if (MAV.cs.firmware == MainV2.Firmwares.ArduTracker)
                                //    {
                                //        routes.Markers.Add(new GMapMarkerAntennaTracker(portlocation, MAV.cs.yaw,
                                //            MAV.cs.target_bearing));
                                //    }
                                //    else if (MAV.cs.firmware == MainV2.Firmwares.ArduCopter2)
                                //    {
                                //        routes.Markers.Add(new GMapMarkerQuad(portlocation, MAV.cs.yaw,
                                //            MAV.cs.groundcourse, MAV.cs.nav_bearing, MAV.sysid));
                                //    }
                                //    else
                                //    {
                                //        // unknown type
                                //        routes.Markers.Add(new GMarkerGoogle(portlocation, GMarkerGoogleType.green_dot));
                                //    }
                                //}
                            }

                            if (route.Points[route.Points.Count - 1].Lat != 0 &&
                                (mapupdate.AddSeconds(3) < DateTime.Now) && CHK_autopan.Checked)
                            {
                                updateMapPosition(currentloc);
                                mapupdate = DateTime.Now;
                            }

                            if (route.Points.Count == 1 && myGMAP1.Zoom == 3) // 3 is the default load zoom
                            {
                                updateMapPosition(currentloc);
                                updateMapZoom(17);
                                //gMapControl1.ZoomAndCenterMarkers("routes");// ZoomAndCenterRoutes("routes");
                            }
                        }


                        //add this after the mav icons are drawn


                    }


                }
                catch
                { }

            }


        }

        private void CheckAndBindPreFlightData()
        {
            //this.Invoke((Action) delegate { preFlightChecklist1.BindData(); });
        }
        private void setMapBearing()
        {
            Invoke((MethodInvoker)delegate { myGMAP1.Bearing = (int)MainV2.comPort.MAV.cs.yaw; });
        }
        private void updateClearMissionRouteMarkers()
        {
            // not async
            Invoke((MethodInvoker)delegate
            {
                polygons.Routes.Clear();
                polygons.Markers.Clear();
                routes.Markers.Clear();
            });
        }
        private void addpolygonmarkerred(string tag, double lng, double lat, int alt, Color? color, GMapOverlay overlay)
        {
            try
            {
                PointLatLng point = new PointLatLng(lat, lng);
                GMarkerGoogle m = new GMarkerGoogle(point, GMarkerGoogleType.red);
                m.ToolTipMode = MarkerTooltipMode.Always;
                m.ToolTipText = tag;
                m.Tag = tag;

                GMapMarkerRect mBorders = new GMapMarkerRect(point);
                {
                    mBorders.InnerMarker = m;
                }

                overlay.Markers.Add(m);
                overlay.Markers.Add(mBorders);
            }
            catch (Exception)
            {
            }
        }
        private void addpolygonmarker(string tag, double lng, double lat, int alt, Color? color, GMapOverlay overlay)
        {
            try
            {
                PointLatLng point = new PointLatLng(lat, lng);
                GMarkerGoogle m = new GMarkerGoogle(point, GMarkerGoogleType.green);
                m.ToolTipMode = MarkerTooltipMode.Always;
                m.ToolTipText = tag;
                m.Tag = tag;

                GMapMarkerRect mBorders = new GMapMarkerRect(point);
                {
                    mBorders.InnerMarker = m;
                    try
                    {
                        //mBorders.wprad =
                        //    (int)(float.Parse(MainV2.config["TXT_WPRad"].ToString()) / CurrentState.multiplierdist);
                    }
                    catch
                    {
                    }
                    if (color.HasValue)
                    {
                        mBorders.Color = color.Value;
                    }
                }

                overlay.Markers.Add(m);
                overlay.Markers.Add(mBorders);
            }
            catch (Exception)
            {
            }
        }
        /// <summary>
        /// used to redraw the polygon
        /// </summary>
        void RegeneratePolygon()
        {
            List<PointLatLng> polygonPoints = new List<PointLatLng>();

            if (routes == null)
                return;

            foreach (GMapMarker m in polygons.Markers)
            {
                if (m is GMapMarkerRect)
                {
                    m.Tag = polygonPoints.Count;
                    polygonPoints.Add(m.Position);
                }
            }

            if (polygonPoints.Count < 2)
                return;

            GMapRoute homeroute = new GMapRoute("homepath");
            homeroute.Stroke = new Pen(Color.Yellow, 2);
            homeroute.Stroke.DashStyle = DashStyle.Dash;
            // add first point past home
            homeroute.Points.Add(polygonPoints[1]);
            // add home location
            homeroute.Points.Add(polygonPoints[0]);
            // add last point
            homeroute.Points.Add(polygonPoints[polygonPoints.Count - 1]);

            GMapRoute wppath = new GMapRoute("wp path");
            wppath.Stroke = new Pen(Color.Yellow, 4);
            wppath.Stroke.DashStyle = DashStyle.Custom;

            for (int a = 1; a < polygonPoints.Count; a++)
            {
                wppath.Points.Add(polygonPoints[a]);
            }

            polygons.Routes.Add(homeroute);
            polygons.Routes.Add(wppath);
        }
        private void updateClearRouteMarker(PointLatLng currentloc)
        {
            Invoke((MethodInvoker)delegate
            {
                routes.Markers.Clear();
                //routes.Markers.Add(new GMarkerGoogle(currentloc, GMarkerGoogleType.none));
            });
        }
        /// <summary>
        /// Try to reduce the number of map position changes generated by the code
        /// </summary>
        DateTime lastmapposchange = DateTime.MinValue;

        private void updateMapPosition(PointLatLng currentloc)
        {
            BeginInvoke((MethodInvoker)delegate
            {
                try
                {
                    if (lastmapposchange.Second != DateTime.Now.Second)
                    {
                        myGMAP1.Position = currentloc;
                        lastmapposchange = DateTime.Now;
                    }
                    //hud1.Refresh();
                }
                catch
                {
                }
            });
        }
        private void updateMapZoom(int zoom)
        {
            BeginInvoke((MethodInvoker)delegate
            {
                try
                {
                    myGMAP1.Zoom = zoom;
                }
                catch
                {
                }
            });
        }
        int i = 0;
        private void timer1_Tick(object sender, EventArgs e)
        {
            
            i++;
            if (i > 90)
            { i = 0; }
            attitudeIndicatorInstrumentControl1.SetAttitudeIndicatorParameters(Convert.ToDouble( MainV2.comPort.MAV.cs.pitch),Convert.ToDouble(MainV2.comPort.MAV.cs.roll));
            attitudeIndicatorInstrumentControl1.Refresh();
            headingIndicatorInstrumentControl1.Heading = Convert.ToInt32(MainV2.comPort.MAV.cs.yaw);
            headingIndicatorInstrumentControl1.Refresh();
            lblAltitudeValue.Text = MainV2.comPort.MAV.cs.alt.ToString();
            //attitudeIndicatorInstrumentControl1.RollAngle = attitudeIndicatorInstrumentControl1
           
            //attitudeIndicatorInstrumentControl1.SetAttitudeIndicatorParameters(Convert.ToDouble(i), Convert.ToDouble(i));
        }

        DateTime lastscreenupdate = DateTime.Now;
        object updateBindingSourcelock = new object();
        volatile int updateBindingSourcecount;
        private void updateBindingSource()
        {
            //  run at 25 hz.
            if (lastscreenupdate.AddMilliseconds(40) < DateTime.Now)
            {
                // this is an attempt to prevent an invoke queue on the binding update on slow machines
                if (updateBindingSourcecount > 0)
                    return;

                lock (updateBindingSourcelock)
                {
                    updateBindingSourcecount++;
                }

                // async
                BeginInvoke((MethodInvoker)delegate
                {
                    try
                    {
                        if (this.Visible)
                        {
                            //Console.Write("bindingSource1 ");
                           // MainV2.comPort.MAV.cs.UpdateCurrentSettings(bindingSource1);
                            //Console.Write("bindingSourceHud ");
                            MainV2.comPort.MAV.cs.UpdateCurrentSettings(bindingSourceHud);
                            //Console.WriteLine("DONE ");

                           /* if (tabControlactions.SelectedTab == tabStatus)
                            {
                                MainV2.comPort.MAV.cs.UpdateCurrentSettings(bindingSourceStatusTab);
                            }
                            else if (tabControlactions.SelectedTab == tabQuick)
                            {
                                MainV2.comPort.MAV.cs.UpdateCurrentSettings(bindingSourceQuickTab);
                            }
                            else if (tabControlactions.SelectedTab == tabGauges)
                            {
                                MainV2.comPort.MAV.cs.UpdateCurrentSettings(bindingSourceGaugesTab);
                            }
                            */ 
                        }
                        else
                        {
                            //Console.WriteLine("Null Binding");
                            MainV2.comPort.MAV.cs.UpdateCurrentSettings(bindingSourceHud);
                        }
                        lastscreenupdate = DateTime.Now;
                    }
                    catch
                    {
                    }
                    lock (updateBindingSourcelock)
                    {
                        updateBindingSourcecount--;
                    }
                });
            }
        }

        private void FlightData_ParentChanged(object sender, EventArgs e)
        {
            if (MainV2.cam != null)
            {
                //MainV2.cam.camimage += cam_camimage;
            }

            // QUAD
            if (MainV2.comPort.MAV.param.ContainsKey("WP_SPEED_MAX"))
            {
                //modifyandSetSpeed.Value = (decimal)((float)MainV2.comPort.MAV.param["WP_SPEED_MAX"] / 100.0);
            } // plane with airspeed
            else if (MainV2.comPort.MAV.param.ContainsKey("TRIM_ARSPD_CM") &&
                     MainV2.comPort.MAV.param.ContainsKey("ARSPD_ENABLE")
                     && MainV2.comPort.MAV.param.ContainsKey("ARSPD_USE") &&
                     (float)MainV2.comPort.MAV.param["ARSPD_ENABLE"] == 1
                     && (float)MainV2.comPort.MAV.param["ARSPD_USE"] == 1)
            {
                //modifyandSetSpeed.Value = (decimal)((float)MainV2.comPort.MAV.param["TRIM_ARSPD_CM"] / 100.0);
            } // plane without airspeed
            else if (MainV2.comPort.MAV.param.ContainsKey("TRIM_THROTTLE") &&
                     MainV2.comPort.MAV.param.ContainsKey("ARSPD_USE")
                     && (float)MainV2.comPort.MAV.param["ARSPD_USE"] == 0)
            {
               // modifyandSetSpeed.Value = (decimal)(float)MainV2.comPort.MAV.param["TRIM_THROTTLE"];
                // percent
            //modifyandSetSpeed.ButtonText = Strings.ChangeThrottle;
            }
        }
        private void updatePlayPauseButton(bool playing)
        {//doni
            /*
            if (playing)
            {
                if (BUT_playlog.Text == "Pause")
                    return;

                BeginInvoke((MethodInvoker)delegate
                {
                    try
                    {
                        BUT_playlog.Text = "Pause";
                    }
                    catch
                    {
                    }
                });
            }
            else
            {
                if (BUT_playlog.Text == "Play")
                    return;

                BeginInvoke((MethodInvoker)delegate
                {
                    try
                    {
                        BUT_playlog.Text = "Play";
                    }
                    catch
                    {
                    }
                });
            }
             * */
        }
        private void updateLogPlayPosition()
        {
            BeginInvoke((MethodInvoker)delegate
            {
                try
                {
                   /* if (tracklog.Visible)
                        tracklog.Value =
                            (int)
                                (MainV2.comPort.logplaybackfile.BaseStream.Position /
                                 (double)MainV2.comPort.logplaybackfile.BaseStream.Length * 100);
                    if (lbl_logpercent.Visible)
                        lbl_logpercent.Text =
                            (MainV2.comPort.logplaybackfile.BaseStream.Position /
                             (double)MainV2.comPort.logplaybackfile.BaseStream.Length).ToString("0.00%");
                    */ 
                }
                catch
                {
                }
            });
        }
    }
}
