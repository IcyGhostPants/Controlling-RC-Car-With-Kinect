//------------------------------------------------------------------------------
// This program connects to the RC car via Wi-Fi, renders the user's skeleton
// in real-time with the Kinect camera, and utilizes specific gestures made by
// the user to send specific commands to the RC car.
//------------------------------------------------------------------------------

namespace MotorKinect
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;
    using Microsoft.Kinect;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {
        /// <summary>
        /// Width of output drawing
        /// </summary>
        private const float RenderWidth = 640.0f;

        /// <summary>
        /// Height of our output drawing
        /// </summary>
        private const float RenderHeight = 480.0f;

        /// <summary>
        /// Thickness of drawn joint lines
        /// </summary>
        private const double JointThickness = 3;

        /// <summary>
        /// Thickness of body center ellipse
        /// </summary>
        private const double BodyCenterThickness = 10;

        /// <summary>
        /// Thickness of clip edge rectangles
        /// </summary>
        private const double ClipBoundsThickness = 10;

        /// <summary>
        /// Brush used to draw skeleton center point
        /// </summary>
        private readonly Brush centerPointBrush = Brushes.Blue;

        /// <summary>
        /// Brush used for drawing joints that are currently tracked
        /// </summary>
        private readonly Brush trackedJointBrush = new SolidColorBrush(Color.FromArgb(255, 68, 192, 68));

        /// <summary>
        /// Brush used for drawing joints that are currently inferred
        /// </summary>        
        private readonly Brush inferredJointBrush = Brushes.Yellow;

        /// <summary>
        /// Pen used for drawing bones that are currently tracked
        /// </summary>
        private readonly Pen trackedBonePen = new Pen(Brushes.Green, 6);

        /// <summary>
        /// Pen used for drawing bones that are currently inferred
        /// </summary>        
        private readonly Pen inferredBonePen = new Pen(Brushes.Gray, 1);

        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        private KinectSensor sensor;

        /// <summary>
        /// Drawing group for skeleton rendering output
        /// </summary>
        private DrawingGroup drawingGroup;

        /// <summary>
        /// Drawing image that we will display
        /// </summary>
        private DrawingImage imageSource;

        private String currentMotion = "stopped"; // Current movement of the car
        private String currentTurn = "center"; // Current direction of the car
        private String ipFromESP8266 = "http://192.168.1.13"; // IP address of the ESP8266 board to connect to
                                                          // Change before running on different network!

        /// <summary>
        /// Initializes a new instance of the MainWindow class.
        /// </summary>
        public MainWindow() {
            InitializeComponent();
        }

        // Gets movement commands from Kinect and transmits corresponding movement command to car
        private void controlCar(Skeleton skel) {
            var rightHand = skel.Joints[JointType.WristRight];
            XValueRight.Text = rightHand.Position.X.ToString(CultureInfo.InvariantCulture);
            YValueRight.Text = rightHand.Position.Y.ToString(CultureInfo.InvariantCulture);
            ZValueRight.Text = rightHand.Position.Z.ToString(CultureInfo.InvariantCulture);

            var leftHand = skel.Joints[JointType.WristLeft];
            XValueLeft.Text = leftHand.Position.X.ToString(CultureInfo.InvariantCulture);
            YValueLeft.Text = leftHand.Position.Y.ToString(CultureInfo.InvariantCulture);
            ZValueLeft.Text = leftHand.Position.Z.ToString(CultureInfo.InvariantCulture);
            
            // Both hands raised up = go forward
            if (Convert.ToDouble(rightHand.Position.Y) >= 0.21 && Convert.ToDouble(leftHand.Position.Y) >= 0.21) {
                MotionStatus.Text = "Forward";
                if (currentMotion != "forward") {
                    sendDriveCommand("/forward"); // Sends drive command to car
                    currentMotion = "forward";
                }
            }
            // Both hands lowered = go backward
            else if (Convert.ToDouble(rightHand.Position.Y) <= 0.0 && Convert.ToDouble(leftHand.Position.Y) <= 0.0) {
                MotionStatus.Text = "Backward";
                if (currentMotion != "back") {
                    sendDriveCommand("/back");
                    currentMotion = "back";
                }
            }
            // Both arms out = stop car
            else if ((Convert.ToDouble(rightHand.Position.Y) <= 0.21 && Convert.ToDouble(leftHand.Position.Y) >= 0.0) ||
                (Convert.ToDouble(rightHand.Position.Y) >= 0.0 && Convert.ToDouble(leftHand.Position.Y) <= 0.21)) {
                MotionStatus.Text = "Stopped";
                if (currentMotion != "stopped") {
                    sendDriveCommand("/stopped");
                    currentMotion = "stopped";
                }
            }

            // Left arm up, right arm down = turn left
            if (Convert.ToDouble(rightHand.Position.Y) <= 0.0 && Convert.ToDouble(leftHand.Position.Y) >= 0.21) {
                TurnStatus.Text = "Left";
                if (currentTurn != "left") {
                    sendDriveCommand("/left");
                    currentTurn = "left";
                }
            }
            // Right arm up, left arm down = turn right
            else if (Convert.ToDouble(leftHand.Position.Y) <= 0.0 && Convert.ToDouble(rightHand.Position.Y) >= 0.21) {
                TurnStatus.Text = "Right";
                if (currentTurn != "right") {
                    sendDriveCommand("/right");
                    currentTurn = "right";
                }
            }

            // If not turning left or right, center car
            else {
                TurnStatus.Text = "Center";
                if (currentTurn != "center") {
                    sendDriveCommand("/center");
                    currentTurn = "center";
                }
            }

        }

        // Handles transmitting the drive command via Wi-Fi
        private void sendDriveCommand(String command) {
            StringBuilder MyStringBuilder = new StringBuilder(ipFromESP8266);

            MyStringBuilder.AppendLine(command);
            var Url = MyStringBuilder.ToString();
            Console.WriteLine(Url);
            var webRequest = WebRequest.Create(Url);

            // Send the http request and wait for the response
            var responseStream = webRequest.GetResponse().GetResponseStream();

            // Displays the response stream text
            if (responseStream != null) {
                using (var streamReader = new StreamReader(responseStream)) {
                    // Return next available character or -1 if there are no characters to be read
                    while (streamReader.Peek() > -1) {
                        Console.WriteLine(streamReader.ReadLine());
                    }
                }
            }
        }


        /// <summary>
        /// Draws indicators to show which edges are clipping skeleton data
        /// </summary>
        /// <param name="skeleton">skeleton to draw clipping information for</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private static void RenderClippedEdges(Skeleton skeleton, DrawingContext drawingContext) {
            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Bottom)) {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, RenderHeight - ClipBoundsThickness, RenderWidth, ClipBoundsThickness));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Top)) {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, RenderWidth, ClipBoundsThickness));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Left)) {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, ClipBoundsThickness, RenderHeight));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Right)) {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(RenderWidth - ClipBoundsThickness, 0, ClipBoundsThickness, RenderHeight));
            }
        }

        /// <summary>
        /// Execute startup tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowLoaded(object sender, RoutedEventArgs e) {
            // Create the drawing group we'll use for drawing
            this.drawingGroup = new DrawingGroup();

            // Create an image source that we can use in our image control
            this.imageSource = new DrawingImage(this.drawingGroup);

            // Display the drawing using our image control
            Image.Source = this.imageSource;

            // Look through all sensors and start the first connected one
            // Make sure Kinect is connected first!
            foreach (var potentialSensor in KinectSensor.KinectSensors) {
                if (potentialSensor.Status == KinectStatus.Connected) {
                    this.sensor = potentialSensor;
                    break;
                }
            }

            if (null != this.sensor) {
                // Turn on the skeleton stream to receive skeleton frames
                this.sensor.SkeletonStream.Enable();



                // Add an event handler to be called whenever there is new color frame data
                this.sensor.SkeletonFrameReady += this.SensorSkeletonFrameReady;

                // Start the Kinect sensor
                try {
                    this.sensor.Start();
                }
                catch (IOException) {
                    this.sensor = null;
                }
            }
        }

        /// <summary>
        /// Execute shutdown tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e) {
            if (null != this.sensor) {
                this.sensor.Stop();
            }
        }


        /// <summary>
        /// Event handler for Kinect sensor's SkeletonFrameReady event
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void SensorSkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e) {
            Skeleton[] skeletons = new Skeleton[0];

            using (SkeletonFrame skeletonFrame = e.OpenSkeletonFrame()) {
                if (skeletonFrame != null) {
                    skeletons = new Skeleton[skeletonFrame.SkeletonArrayLength];
                    skeletonFrame.CopySkeletonDataTo(skeletons);
                }
            }

            using (DrawingContext dc = this.drawingGroup.Open()) {
                // Draw a transparent background to set the render size
                dc.DrawRectangle(Brushes.Black, null, new Rect(0.0, 0.0, RenderWidth, RenderHeight));

                if (skeletons.Length != 0) {
                    foreach (Skeleton skel in skeletons) {
                        RenderClippedEdges(skel, dc);

                        if (skel.TrackingState == SkeletonTrackingState.Tracked) {
                            this.DrawBonesAndJoints(skel, dc);
                            this.controlCar(skel);

                        }
                        else if (skel.TrackingState == SkeletonTrackingState.PositionOnly) {
                            dc.DrawEllipse(
                            this.centerPointBrush,
                            null,
                            this.SkeletonPointToScreen(skel.Position),
                            BodyCenterThickness,
                            BodyCenterThickness);
                        }
                    }
                }

                // prevent drawing outside of render area
                this.drawingGroup.ClipGeometry = new RectangleGeometry(new Rect(0.0, 0.0, RenderWidth, RenderHeight));
            }
        }

        /// <summary>
        /// Draws a skeleton's bones and joints
        /// </summary>
        /// <param name="skeleton">skeleton to draw</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private void DrawBonesAndJoints(Skeleton skeleton, DrawingContext drawingContext) {
            // Render Torso
            this.DrawBone(skeleton, drawingContext, JointType.Head, JointType.ShoulderCenter);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.ShoulderLeft);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.ShoulderRight);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.Spine);
            this.DrawBone(skeleton, drawingContext, JointType.Spine, JointType.HipCenter);
            this.DrawBone(skeleton, drawingContext, JointType.HipCenter, JointType.HipLeft);
            this.DrawBone(skeleton, drawingContext, JointType.HipCenter, JointType.HipRight);

            // Left Arm
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderLeft, JointType.ElbowLeft);
            this.DrawBone(skeleton, drawingContext, JointType.ElbowLeft, JointType.WristLeft);
            this.DrawBone(skeleton, drawingContext, JointType.WristLeft, JointType.HandLeft);

            // Right Arm
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderRight, JointType.ElbowRight);
            this.DrawBone(skeleton, drawingContext, JointType.ElbowRight, JointType.WristRight);
            this.DrawBone(skeleton, drawingContext, JointType.WristRight, JointType.HandRight);

            // Left Leg
            this.DrawBone(skeleton, drawingContext, JointType.HipLeft, JointType.KneeLeft);
            this.DrawBone(skeleton, drawingContext, JointType.KneeLeft, JointType.AnkleLeft);
            this.DrawBone(skeleton, drawingContext, JointType.AnkleLeft, JointType.FootLeft);

            // Right Leg
            this.DrawBone(skeleton, drawingContext, JointType.HipRight, JointType.KneeRight);
            this.DrawBone(skeleton, drawingContext, JointType.KneeRight, JointType.AnkleRight);
            this.DrawBone(skeleton, drawingContext, JointType.AnkleRight, JointType.FootRight);

            // Render Joints
            foreach (Joint joint in skeleton.Joints) {
                Brush drawBrush = null;

                if (joint.TrackingState == JointTrackingState.Tracked) {
                    drawBrush = this.trackedJointBrush;
                }
                else if (joint.TrackingState == JointTrackingState.Inferred) {
                    drawBrush = this.inferredJointBrush;
                }

                if (drawBrush != null) {
                    drawingContext.DrawEllipse(drawBrush, null, this.SkeletonPointToScreen(joint.Position), JointThickness, JointThickness);
                }
            }
        }

        /// <summary>
        /// Maps a SkeletonPoint to lie within our render space and converts to Point
        /// </summary>
        /// <param name="skelpoint">point to map</param>
        /// <returns>mapped point</returns>
        private Point SkeletonPointToScreen(SkeletonPoint skelpoint) {
            // Convert point to depth space
            DepthImagePoint depthPoint = this.sensor.CoordinateMapper.MapSkeletonPointToDepthPoint(skelpoint, DepthImageFormat.Resolution640x480Fps30);
            return new Point(depthPoint.X, depthPoint.Y);
        }

        /// <summary>
        /// Draws a bone line between two joints
        /// </summary>
        /// <param name="skeleton">skeleton to draw bones from</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        /// <param name="jointType0">joint to start drawing from</param>
        /// <param name="jointType1">joint to end drawing at</param>
        private void DrawBone(Skeleton skeleton, DrawingContext drawingContext, JointType jointType0, JointType jointType1) {
            Joint joint0 = skeleton.Joints[jointType0];
            Joint joint1 = skeleton.Joints[jointType1];

            // If joins can't be found, exit
            if (joint0.TrackingState == JointTrackingState.NotTracked ||
                joint1.TrackingState == JointTrackingState.NotTracked) {
                return;
            }

            // Don't draw if both points are inferred
            if (joint0.TrackingState == JointTrackingState.Inferred &&
                joint1.TrackingState == JointTrackingState.Inferred) {
                return;
            }

            Pen drawPen = this.inferredBonePen;
            if (joint0.TrackingState == JointTrackingState.Tracked && joint1.TrackingState == JointTrackingState.Tracked) {
                drawPen = this.trackedBonePen;
            }

            drawingContext.DrawLine(drawPen, this.SkeletonPointToScreen(joint0.Position), this.SkeletonPointToScreen(joint1.Position));
        }
    }
}