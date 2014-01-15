using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using System.Drawing;
using Microsoft.Kinect;
using Microsoft.Speech.AudioFormat;
using Microsoft.Speech.Recognition;
using System.Diagnostics;
using System.Threading;
using System.Runtime.InteropServices;

/*
- TODO -
-----
Move code with color frames and skeleton frames + Infrared + everything in main method.
If statement handler with voice control and gesture control.
timing counter for joints to allow for gestures. -DONE?
touch arc, x bounds.
fix on uncheck events 
 * gesture control = DONE
 * voice control = DONE
 * ir mode = DONE
 * direct feed = DONE
 * touch mode = DONE
Features based on PID - DONE
Refactor Gesture controls
 */


namespace KinectApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// Icon for tray notifications.
        /// </summary>
        private NotifyIcon notifyIcon;

        /// <summary>
        /// Speech engine initialization.
        /// </summary>
        private SpeechRecognitionEngine sre;

        /// <summary>
        /// Commands for the speech engine.
        /// </summary>
        private commands cmd = new commands();

        /// <summary>
        /// Active Kinect sensor.
        /// </summary>
        private KinectSensor sensor;

        /// <summary> 
        /// Bitmap that will hold color information.
        /// </summary>
        private WriteableBitmap colorBitmap;

        /// <summary>
        /// Intermediate storage for the bitmap data received from the camera
        /// </summary>
        private byte[] colorPixels;

        /// <summary>
        /// Array of data for skeleton joints.
        /// Data for Gestured motions.
        /// The stopwatch times skeleton joint movements.
        /// flagrun determines how joint comparisons should be made.
        /// Old values are initialized before data is measured.
        /// </summary>
        private Skeleton[] skeletonData;
        private Stopwatch stopWatch = new Stopwatch();
        private bool flagrun = true;
        private float oldlY = float.NaN, oldlX = float.NaN, oldrY = float.NaN, oldrX = float.NaN;

        /// <summary>
        /// Values for Touch Controls.
        /// XRes indicates width resolution, YRes is Height Resolution.
        /// The Dll imports data for setting the mouse cursor to the corresponding hand.
        /// </summary>
        int XRes = (int)System.Windows.SystemParameters.PrimaryScreenWidth;
        int YRes = (int)System.Windows.SystemParameters.PrimaryScreenHeight;
        [DllImport("user32.dll")]
        static extern bool SetCursorPos(int X, int Y);
        
        /// <summary>
        /// Values for IR Controls
        /// pixcount shows the corresponding pixels in the array of pixel values.
        /// IRflagrun will find and add/remove values.
        /// Executionflag will determine if we find values or sending commands to method handler.
        /// totcompfram will hold total frames to run, and half second will be for changing IRflagrun type (removing or adding values to pixels).
        /// </summary>
        bool[] pixels = new bool[(640 * 480)];
        int totcompfram = 7;
        int deletframerun = 2;
        int pixcount = 0;
        private bool IRflagrun = true;
        private bool Executionflag = false;

        /// <summary>
        /// Main initializer window, draws icons to the screen and starts components.
        /// </summary>
        public MainWindow()
        {
            notifyIcon = new NotifyIcon();
            notifyIcon.Icon = new System.Drawing.Icon("Kinect.ico");
            notifyIcon.Text = "Kinect App";

            InitializeComponent();
        }

        /// <summary>
        /// When the main window is loaded perform these actions.
        /// </summary>
        private void Kinect_Main_Loaded(object sender, RoutedEventArgs e)
        {
            // Look through all sensors and start the first connected one.
            foreach (var potentialSensor in KinectSensor.KinectSensors)
            {
                if (potentialSensor.Status == KinectStatus.Connected)
                {
                    this.sensor = potentialSensor;
                    break;
                }
            }

            //Try to start sensor
            try
            {
                this.sensor.Start();
            }
            catch (NullReferenceException)  //If fails, then set sensor to null
            {
                this.sensor = null;
            }

            if (this.sensor == null)        //If sensor is set to null, show "NOTREADY" image
            {
                var uriSource = new Uri(@"/KinectApp;component/NOTREADY.png", UriKind.Relative);
                Image.Source = new BitmapImage(uriSource);
            }

            X_Res.Text = XRes.ToString();   //Shows users X resolution
            Y_Res.Text = YRes.ToString();   //Shows users Y resolution
        }

        /// <summary>
        /// Disposal Actions for closing the Kinect.
        /// </summary>
        private void Kinect_Main_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (this.sensor != null)
            {
                this.sensor.Stop();         //Stops the sensor from preforming actions
                this.sensor.Dispose();      //Disposes of sensor for later use for another program
            }

            notifyIcon.Dispose();           //Gets rid of notification icon in taskbar
        }


        /***************************************************** TRAY NOTIFICATION *********************************************************************/

        /// <summary>
        /// When tray box is checked, let the user know tray notifcations are enabled
        /// </summary>
        private void trayBox_Checked(object sender, RoutedEventArgs e)
        {
            boxEnable("Tray notifications");
        }

        /// <summary>
        /// When tray box is unchecked, let the user know tray notifcations are disabled
        /// </summary>
        private void trayBox_Unchecked(object sender, RoutedEventArgs e)
        {
            closehandlermethod("Tray notifications");
        }

        /// <summary>
        /// checks if traybox is checked, and if so send a message.
        /// </summary>
        /// <param name="boxtype">
        /// Parent caller name.
        /// </param>
        private void boxEnable(string boxtype)
        {
            if (trayBox.IsChecked == true)
            {
                notifyIcon.BalloonTipText = boxtype + " enabled";
                notifyIcon.Visible = true;
                notifyIcon.ShowBalloonTip(100);
            }
        }

        /// <summary>
        /// Method to fix screen and enable tooltip message.
        /// </summary>
        /// <param name="boxtype">
        /// The string specificing the box that was closed.
        /// </param>
        private void closehandlermethod(string boxtype)
        {
            if (boxtype.Equals("IR control") || boxtype.Equals("Direct feed"))
            {
                var uriSource = new Uri(@"/KinectApp;component/NO FEED.png", UriKind.Relative);
                Image.Source = new BitmapImage(uriSource);
            }

            if (trayBox.IsChecked == true)
            {
                notifyIcon.BalloonTipText = boxtype + " disabled";
                notifyIcon.Visible = true;
                notifyIcon.ShowBalloonTip(100);
            }
        }

        /****************************************************** DIRECT FEED *************************************************************************/


        /// <summary>
        /// Show a direct feed from the sensor to the user in Main Window
        /// Enable Color Stream as Blue/Green/Red32 bit bitmap and send to screen for user to see
        /// Check Color Frames thirty times a second
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void feedBox_Checked(object sender, RoutedEventArgs e)
        {
            IRbox.IsChecked = false;
            if (null != this.sensor)
            {
                // Turn on the color stream to receive color frames
                this.sensor.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30);

                // Allocate space to put the pixels we'll receive
                this.colorPixels = new byte[this.sensor.ColorStream.FramePixelDataLength];

                // This is the bitmap we'll display on-screen
                this.colorBitmap = new WriteableBitmap(this.sensor.ColorStream.FrameWidth, this.sensor.ColorStream.FrameHeight, 96.0, 96.0, PixelFormats.Bgr32, null);

                 //Set the image we display to point to the bitmap where we'll put the image data
                this.Image.Source = this.colorBitmap;

                // Add an event handler to be called whenever there is new color frame data
                this.sensor.ColorFrameReady += this.SensorColorFrameReady;
            }

            boxEnable("Direct feed");
        }

        /// <summary>
        /// Disables color stream and calls closing handler method
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void feedBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (null != this.sensor)
            {
                this.sensor.ColorStream.Disable();
            }

            closehandlermethod("Direct feed");
        }


        /// <summary>
        /// Event handler for Kinect sensor's ColorFrameReady event
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void SensorColorFrameReady(object sender, ColorImageFrameReadyEventArgs e)
        {
            using (ColorImageFrame colorFrame = e.OpenColorImageFrame())
            {
                if (colorFrame != null)
                {
                    // Copy the pixel data from the image to a temporary array
                    colorFrame.CopyPixelDataTo(this.colorPixels);

                    // Write the pixel data into our bitmap
                    this.colorBitmap.WritePixels(
                        new Int32Rect(0, 0, this.colorBitmap.PixelWidth, this.colorBitmap.PixelHeight),
                        this.colorPixels,
                        this.colorBitmap.PixelWidth * colorFrame.BytesPerPixel,
                        0);
                }
            }
        }


        /*********************************************** VOICE RECOGNITION ********************************************************************************/


        /// <summary>
        /// Gets the metadata for the speech recognizer (acoustic model) most suitable to
        /// process audio from Kinect device.
        /// </summary>
        /// <returns>
        /// RecognizerInfo if found, <code>null</code> otherwise.
        /// </returns>
        private static RecognizerInfo GetKinectRecognizer()
        {
            foreach (RecognizerInfo recognizer in SpeechRecognitionEngine.InstalledRecognizers())
            {
                string value;
                recognizer.AdditionalInfo.TryGetValue("Kinect", out value);
                if ("True".Equals(value, StringComparison.OrdinalIgnoreCase) && "en-US".Equals(recognizer.Culture.Name, StringComparison.OrdinalIgnoreCase))
                {
                    return recognizer;
                }
            }

            return null;
        }

        /// <summary>
        /// Enables Voice Control balloon tip
        /// Starts speech recognizer
        /// Creates simple grammer database using string arrays
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void voiceBox_Checked(object sender, RoutedEventArgs e)
        {
            boxEnable("Voice control");                     //Enables balloon tips

            try
            {
                RecognizerInfo ri = GetKinectRecognizer();  //Initilizes Kinect Recognizer for speech
                sre = new SpeechRecognitionEngine(ri.Id);   //Initilizes Speech Recognition Engine
            }
            catch (NullReferenceException) // Catch to make sure if no Kinect is found program does not crash
            {
                test.Text = "No kinect ready";
                voiceBox.IsChecked = false;
                return;
            }

            //Create simple string array that contains speech recognition data and interpreted values
            string[] valuesHeard = { "scroll down", "scroll up", "volume up", "volume down", "launch internet explorer", "launch chrome", "launch firefox", "launch zalevski i d e" };
            string[] valuesInterpreted = { "DOWN", "UP", "UP", "DOWN", "launch internet explorer", "launch chrome", "launch firefox", "launch notepad" };

            var commands = new Choices();       //Initilizes Choices for engine

            //Adds all values in string arrays to commands for engine
            for (int i = 0; i < valuesHeard.Length; i++)
            {
                commands.Add(new SemanticResultValue(valuesHeard[i], valuesInterpreted[i]));
            }

            //Submits commands to Grammar Builder for engine
            Grammar g = new Grammar(commands.ToGrammarBuilder());
            this.sre.LoadGrammar(g);

            //Constantly try to recognize speech
            sre.SpeechRecognized += SpeechRecognized;

            // Tells the speech engine where to find the audio stream
            sre.SetInputToAudioStream(sensor.AudioSource.Start(), new SpeechAudioFormatInfo(EncodingFormat.Pcm, 16000, 16, 1, 32000, 2, null));
            sre.RecognizeAsync(RecognizeMode.Multiple);
        }

        /// <summary>
        /// Disposes of Speech Recognition Engine and calls close handler method
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void voiceBox_Unchecked(object sender, RoutedEventArgs e)
        {
            sre.Dispose();
            closehandlermethod("Voice control");
        }

        /// <summary>
        /// Determines confidence level of voice command and launches the voice command.
        /// Also puts debugging text into the main window.
        /// </summary>
        /// <param name="sender"> Encapsulated calling method sending the event. </param>
        /// <param name="e"> The recognized argument. </param>
        private void SpeechRecognized(object sender, SpeechRecognizedEventArgs e)
        {
            if (e.Result.Confidence > 0.6) //If confidence of recognized speech is greater than 60%
            {
                // Debug output, tells what phrase was recongnized and the confidence
                test.Text = "Recognized: " + e.Result.Text + " \nConfidence: " + e.Result.Confidence;

                // Call command, if it returns -1 then there was an issue processing the request 
                int flag = cmd.inputCommand(e.Result.Semantics.Value.ToString());
                
                if (flag == -1) // if there was an error inform the user
                {
                    test.Text += "\nError occurred processing command.";
                }
            }
            else // Else say that it was rejected and confidence
            {
                test.Text = "Rejected " + " \nConfidence: " + e.Result.Confidence;
            }
        }


        /************************************************** GESTURES ***********************************************************************/

        /// <summary>
        /// Enable Skeleton Stream
        /// Calls Skeleton Frame event handler
        /// Enables Gesture Control balloon tip
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void gestureBox_Checked(object sender, RoutedEventArgs e)
        {
            this.sensor.SkeletonStream.Enable(); // Enable skeletal tracking

            skeletonData = new Skeleton[this.sensor.SkeletonStream.FrameSkeletonArrayLength]; // Allocate Skeleton Stream data

            this.sensor.SkeletonFrameReady += new EventHandler<SkeletonFrameReadyEventArgs>(kinect_SkeletonFrameReady); // Get Ready for Skeleton Ready Events

            boxEnable("Gesture control"); // Tooltip informing gesture control is enabled
        }
        
        /// <summary>
        /// Disables Skeleton Stream and calls close handler method
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void gestureBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (null != this.sensor)
            {
                this.sensor.SkeletonStream.Disable();
            }

            closehandlermethod("Gesture control");
        }

        /// <summary>
        /// Open Skeleton Frame for use
        /// Creates points for all relevant joints and old joint positions
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void kinect_SkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            using (SkeletonFrame skeletonFrame = e.OpenSkeletonFrame()) // Open the Skeleton frame
            {
                if (skeletonFrame != null && this.skeletonData != null) // check that a frame is available
                {
                    skeletonFrame.CopySkeletonDataTo(this.skeletonData); // get the skeletal information in this frame
                }
            }

            foreach (Skeleton skeleton in this.skeletonData)
            {
                // if there is a skeleton currently being tracked
                if (skeleton.TrackingState == SkeletonTrackingState.Tracked)
                {
                    // get X and Y coordinates of the left and right wrists
                    float lY = skeleton.Joints[JointType.WristLeft].Position.Y;
                    float lX = skeleton.Joints[JointType.WristLeft].Position.X;
                    float rY = skeleton.Joints[JointType.WristRight].Position.Y;
                    float rX = skeleton.Joints[JointType.WristRight].Position.X;

                    // debug output, displays x and y coordinates
                    test2.Text = "Left X: " + lX;
                    test2.Text += "\nLeft Y: " + lY;
                    test2.Text += "\nRight X: " + rX;
                    test2.Text += "\nRight Y: " + rY;
                    test2.Text += "\nTime: " + stopWatch.ElapsedMilliseconds;

                    //testing if stopwatch has started and if old values were ever kept.
                    if (!stopWatch.IsRunning || float.IsNaN(oldrY)) 
                    {
                        oldlY = skeleton.Joints[JointType.WristLeft].Position.Y;
                        oldlX = skeleton.Joints[JointType.WristLeft].Position.X;
                        oldrY = skeleton.Joints[JointType.WristRight].Position.Y;
                        oldrX = skeleton.Joints[JointType.WristRight].Position.X;
                        stopWatch.Start();
                    }

                    // After one second has elapsed check current XY vs old XY 
                    if (stopWatch.ElapsedMilliseconds > 1000 && flagrun)
                    {
                        if ((lY > (oldlY + .1)) && (lY > 0.3))              //Gesture Up
                        {
                            cmd.inputCommand("scroll up");
                            flagrun = false;
                        }
                        else if ((lY < (oldlY - .1)) && (lY < -0.3))        //Gesture Down
                        {
                            cmd.inputCommand("scroll down");
                            flagrun = false;
                        }
                        else if ((lX < (oldlX - .1)) && (lX < -0.5))        //Gesture Left
                        {
                            cmd.inputCommand("launch internet explorer");
                            flagrun = false;
                        }
                        else if ((lX > (oldlX + .1)) && (lX > 0.2))        //Gesture Right
                        {
                            cmd.inputCommand("launch notepad");
                            flagrun = false;
                        }
                    }
                    if (!flagrun)
                    {
                        oldlY = skeleton.Joints[JointType.WristLeft].Position.Y;
                        oldlX = skeleton.Joints[JointType.WristLeft].Position.X;
                        oldrY = skeleton.Joints[JointType.WristRight].Position.Y;
                        oldrX = skeleton.Joints[JointType.WristRight].Position.X;
                        stopWatch.Restart();
                        flagrun = true;
                    }
                }
            }
        }


        /************************************************** TOUCH *******************************************************************/

        /// <summary>
        /// Starts the depth stream and sets the camera to near mode
        /// informs the user that touch control has started
        /// </summary>
        /// <param name="sender"> object sending event </param>
        /// <param name="e"> event arguments </param>
        private void Touch_Checked(object sender, RoutedEventArgs e)
        {
            // sets camera to near mode
            this.sensor.DepthStream.Range = DepthRange.Near;

            // starts the depthstream at a resolution of 640x480 (maximum resolution)
            this.sensor.DepthStream.Enable(DepthImageFormat.Resolution640x480Fps30);

            // when a depth frame is ready calls the depth frame event handler
            this.sensor.DepthFrameReady += this.DepthFrameReady;

            // shows the tooltip
            boxEnable("Touch control");
        }

        /// <summary>
        /// Disables any unneeded features when touch mode is turned off
        /// </summary>
        /// <param name="sender"> object that send the command </param>
        /// <param name="e"> event arguments </param>
        private void touch_Unchecked(object sender, RoutedEventArgs e)
        {
            if (null != this.sensor)
            {
                this.sensor.DepthStream.Disable(); // disable the depth stream
                this.sensor.DepthStream.Range = DepthRange.Default; // set camera mode to default
            }

            closehandlermethod("Touch control"); // show tooltip
        }

        /// <summary>
        /// Event handler for new depth frames
        /// </summary>
        /// <param name="sender"> object sending the event </param>
        /// <param name="e"> event arguments </param>
        private void DepthFrameReady(object sender, DepthImageFrameReadyEventArgs e)
        {
            // Gets the depth data and stores it in imageFrame
            DepthImageFrame imageFrame = e.OpenDepthImageFrame();

            // if data is in imageFrame
            if (imageFrame != null)
            {
                // store data in temp array
                short[] pixelData = new short[imageFrame.PixelDataLength];
                imageFrame.CopyPixelDataTo(pixelData);
                
                // array to store final depth values in
                int[] depth = new int[imageFrame.Width];

                // only need to check one row of pixels for depth data
                int j = (int)slider.Value;

                // loops through the depth frame and extracts the depth values we want
                for (int i = 0; i < imageFrame.Width; i++)
                {
                    int d = (ushort)pixelData[i + j * imageFrame.Width];
                    d = d >> 3;
                    depth[i] = d;             
                }

                // sends the depth data to the checkDepth method
                checkDepth(depth);
            }
        }

        /// <summary>
        /// First checks for the distance between the bottom of the monitor and 
        /// the top of the monitor to the kinect this gives us an area to check
        /// for objects in.
        /// 
        /// Then checks if any objects are in the area we are looking
        /// if we find an object we send its X coordinate (the location in the array)
        /// and the Y coordinate (the depth) to mouse update
        /// </summary>
        /// <param name="depth"> integer array that has the depth information </param>
        private void checkDepth(int[] depth)
        {
            // array used to pass XY coordinates
            int[] arr = new int[2];

            // values to hold distance from monitor to Kinect
            int ttk = 0;
            int btk = 0;

            // values are user inputed, try to convert them to integers
            try 
            {
                ttk = Convert.ToInt32(topToKinect.Text);
                btk = Convert.ToInt32(botToKinect.Text);
            }
            // If we can't convert them, inform the user to check their input
            catch (System.FormatException)
            {
                topToKinect.Text = "";
                touch.IsChecked = false;
                test3.Text = "Distance to the kinect must be a number";
            }

            // Scan for objects within the bounds of our depth area
            for (int i = 0; i < 640; i++)
            {
                if (depth[i] < btk && depth[i] > ttk)
                {
                    arr[0] = i;
                    arr[1] = depth[i];
                    mouseUpdate(arr, ttk, btk);
                    return;
                }
            }
        }

        /// <summary>
        /// This method updates the mouse cursor's X and Y coordinates
        /// </summary>
        /// <param name="arr"> array containing x and y coordinates </param>
        /// <param name="ttk"> distance from top of monitor to Kinect </param>
        /// <param name="btk"> distance from bottom of monitor to Kinect </param>
        private void mouseUpdate(int[] arr, int ttk, int btk)
        {
            // Since max X coordinate is 640, and max Y coordiante is disance from top of monitor
            // to bottom of monitor, we need multipliers for the touch control to
            // be able to move the mouse across the entire screen
            float xMulti = XRes / (float)(rightAdjust.Value-leftAdjust.Value);
            float yMulti = YRes / (btk - ttk);

            // If X is within our left and right bounds, move the mouse cursor to the position
            if (arr[0] > leftAdjust.Value && arr[0] < rightAdjust.Value)
            {
                SetCursorPos((int)(arr[0] * xMulti), (int)((arr[1] - ttk) * yMulti));
            }
        }

        // Shows the value of the Y slider in the debug output box 
        private void slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (touch.IsChecked == true)
            {
                test3.Text = "Y Scan: " + slider.Value.ToString();
                test3.Text += "\nLeft offset: " + leftAdjust.Value.ToString();
                test3.Text += "\nRight offset: " + rightAdjust.Value.ToString();
            }
        }

        // Shows the value of the rightAdjust slider in the debug output box 
        private void rightAdjust_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (touch.IsChecked == true)
            {
                test3.Text = "Y Scan: " + slider.Value.ToString();
                test3.Text += "\nLeft offset: " + leftAdjust.Value.ToString();
                test3.Text += "\nRight offset: " + rightAdjust.Value.ToString();
            }
        }

        // Shows the value of the leftAdjust slider in the debug output box 
        private void leftAdjust_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (touch.IsChecked == true)
            {
                test3.Text = "Y Scan: " + slider.Value.ToString();
                test3.Text += "\nLeft offset: " + leftAdjust.Value.ToString();
                test3.Text += "\nRight offset: " + rightAdjust.Value.ToString();
            }
        }


        /************************************************** INFRARED SENSOR ***********************************************************************/
        /// <summary>
        /// When the IR Box is checked, we make sure the Direct Feed is off so the streams don't crash against each other
        /// Call the ballon tips to tell user IR Control is enabled
        /// Force the Infrared Emitter to off so the only pixels that will be lit are from LEDs and other Infrared objects
        /// Enable Color Stream as Monochrome bitmap and send to screen for user to see
        /// Check Infrared Frames thirty times a second
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void IRbox_Checked(object sender, RoutedEventArgs e)
        {
            feedBox.IsChecked = false;                      //Disable Direct Feed 

            boxEnable("IR control");                        //Calls function to show balloon tip

            this.sensor.ForceInfraredEmitterOff = true;     //Force Infrared Emitter off 

            //Set entire array of pixels to false for reset
            for(int i = 0; i < (640*480); i++)
            {
                pixels[i] = false;
            }

            //////////////////////////////
            this.sensor.ColorStream.Enable(ColorImageFormat.InfraredResolution640x480Fps30);

            // Allocate space to put the pixels we'll receive
            this.colorPixels = new byte[this.sensor.ColorStream.FramePixelDataLength];

            // This is the bitmap we'll display on-screen
            this.colorBitmap = new WriteableBitmap(this.sensor.ColorStream.FrameWidth, this.sensor.ColorStream.FrameHeight, 96.0, 96.0, PixelFormats.Gray16, null);

            //Set the image we display to point to the bitmap where we'll put the image data
            this.Image.Source = this.colorBitmap;

            // Add an event handler to be called whenever there is new color frame data
            this.sensor.ColorFrameReady += this.IRFrameReady;
        }

        /// <summary>
        /// IRFrameReady runs thirty times a second and checks for lit pixels
        /// Check to make sure that the IRBox is still checked
        /// Parse through colorFrame for lit pixels above a threshold
        /// Make sure those pixels stay lit, otherwise get rid of them (checking whether lit pixels were noise)
        /// Check position of lit pixel and reletive to that, send a command
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void IRFrameReady(object sender, ColorImageFrameReadyEventArgs e)
        {
            using (ColorImageFrame colorFrame = e.OpenColorImageFrame())
            {
                if (colorFrame != null)
                {
                    // Copy the pixel data from the image to a temporary array
                    colorFrame.CopyPixelDataTo(this.colorPixels);

                    // Write the pixel data into our bitmap
                    this.colorBitmap.WritePixels(
                        new Int32Rect(0, 0, this.colorBitmap.PixelWidth, this.colorBitmap.PixelHeight),
                        this.colorPixels,
                        this.colorBitmap.PixelWidth * colorFrame.BytesPerPixel,
                        0);
                }

                //Checks if IRBox is checked or not dude to so many instances being run at a time
                if (IRbox.IsChecked == false)
                    return;

                //Checks if ready to go through find pixels or sending command
                if (!Executionflag)         //Finding pixel
                {
                    //Iterate through array of frames
                    pixcount = 0;
                    if (totcompfram <= deletframerun)           //If frame counter is less than
                    {                                           //or equal to deletframerun
                        IRflagrun = false;                      //Then set IRflagrun to false
                    }
                    for (int m = 1; m < 614399; m = m + 2)      //Every second pixel starting with index '1' has values needed
                    {
                        if (IRflagrun && colorPixels[m] > 150)  //If pixel has this intensity and flag is true
                        {
                            pixels[pixcount] = true;            //Set corresponding element in pixel array to true
                        }
                        if (!IRflagrun && pixels[pixcount] == true && colorPixels[m] < 150) //If pixel loses that intensity and flag is false
                        {
                            pixels[pixcount] = false;           //Set to false
                        }
                        pixcount++;                             //Increase count of parsed pixel
                    }
                    totcompfram--;                              //Decrease frame counter
                }
                else                                            //Done finding pixels
                {                                               //Now it's time to parse through them
                    for (int i = 0; i < pixels.Length; i++)     //Go through pixel array of lit pixels
                    {
                        if (pixels[i])                          //If pixel is lit
                        {
                            //location of truth!!
                            if (i > 150000)                     //If lit pixel is in the upper half of the screen
                            {
                                //send commands
                                cmd.inputCommand("launch internet explorer");   //Send launch command
                            }
                            else                                //If lit pixel is in the lower half of the screen
                            {
                                cmd.inputCommand("launch notepad");             //Send launch command
                            }
                            IRbox.IsChecked = false;            //Set checkbox to false/off
                            return;                             //Return to get out of function call
                        }
                    }
                }

                if (totcompfram == 0)                           //If total comparison frame counter hits 0
                {
                    Executionflag = true;                       //Set execution flag to true
                }

            }
        }

        /// <summary>
        /// When IR Box is unchecked, Color stream needs to be disabled and the Infrared Emitter
        /// needs to be turned back on for future use
        /// All flags, counters, and the pixel array needs to be reset for next use
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void IRbox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (null != this.sensor)
            {
                this.sensor.ColorStream.Disable();              //Turn off color stream
                this.sensor.ForceInfraredEmitterOff = false;    //Turn Infrared Emitter back on for depth stream
            }
            
            //Reset all flags, counters, and the pixel array for next use
            Executionflag = false;
            IRflagrun = true;
            totcompfram = 7;
            deletframerun = 2;
            pixcount = 0;

            for (int i = 0; i < (640 * 480); i++)
            {
                pixels[i] = false;
            }

            closehandlermethod("IR control");               //Calls function to show balloon tip and show "NO FEED" bitmap image
        }
    }
}