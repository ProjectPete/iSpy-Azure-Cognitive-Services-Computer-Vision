using DigitalEyes.iSpy.DetectAnalyse;
using DigitalEyes.iSpy.DetectAnalyse.Helpers;
using DigitalEyes.iSpy.DetectAnalyse.Model;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Media;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Plugins
{
    public class Main : IDisposable
    {
        // **************************************************
        // *** Update or verify the following two values. ***
        // **************************************************

        // Replace the subscriptionKey string value with your valid subscription key.
        const string subscriptionKey = "<YOUR KEY>";

        // Replace or verify the region.
        //
        // You must use the same region in your REST API call as you used to obtain your subscription keys.
        // For example, if you obtained your subscription keys from the westus region, replace 
        // "westcentralus" in the URI below with "westus".
        //
        // NOTE: Free trial subscription keys are generated in the westcentralus region, so if you are using
        // a free trial subscription key, you should not need to change this region.
        const string uriBase = "https://westcentralus.api.cognitive.microsoft.com/vision/v1.0/analyze";

        public string Alert;            // <-- Used by iSpy

        public string Configuration     // <-- Used by iSpy
        {
            get { return _config; }
            set
            {
                _config = value;
                InitConfig();
            }
        }
        
        static List<AnalysisReport> analysisResult = new List<AnalysisReport>();
        static object analysisResultLockObject = new object();
        static DateTime lastAnalysis = DateTime.MinValue;
        static object lastAnalysisLockObject = new object();
        static Bitmap lastActionWindow = null;

        internal int Sensitivity = 10;
        internal int MinAlartPercent = 10;
        internal int MaxAlartPercent = 90;
        internal int MaxPixelsDetail = 30;
        internal bool ShowPixels = false;

        string _config = "";
        bool _disposed;

        int MaxMillsTillCleared = 8000;
        int AnalysingGapMills = 2000;
        int MaxListed = 7;
        int ScaledActionPixels = 50;
        int CleardownSpeedMills = 1000;
        
        FrameProcessor processor;   // Processes the frame
        FrameAnalyser analyser;     // Analyses the frame
        
        public Main()                       // <-- Used by iSpy
        {
            //
            // Change flag below if you want to check for errors
            //

            Logger.LoggingEnabled = false;

            Logger.LogMessage("Plugin Started");

            // Audible confirm that plugin started, comment out when it gets annoying!
            SoundPlayer player = new SoundPlayer(Resources.Ignition);
            player.Play();

            processor = new FrameProcessor(Sensitivity, MaxPixelsDetail, ShowPixels);
            analyser = new FrameAnalyser(subscriptionKey, uriBase);
        }
        
        /// <summary>
        /// Called by iSpy, either for every frame (continuous), or on motion detection
        /// </summary>
        /// <param name="frame">The image to process and analyse</param>
        /// <returns></returns>
        public Bitmap ProcessFrame(Bitmap frame)  // <-- Used by iSpy
        {
            try
            {
                List<AnalysisReport> results;
                lock (analysisResultLockObject)
                {
                    results = analysisResult;
                }

                // Clone image, so I can show a copy in the config window
                var imageToAnalyse = new ImageToAnalyse { OriginalImage = (Bitmap)frame.Clone() };

                int percentChange = processor.GetChangePercentageFromLast(imageToAnalyse, results);
                double millsSinceLast = 0;
                lock (lastAnalysisLockObject)
                {
                    millsSinceLast = (DateTime.Now - lastAnalysis).TotalMilliseconds;
                }

                if (percentChange > MinAlartPercent && percentChange < MaxAlartPercent)
                {
                    // Alert triggered!!
                    Alert = "Motion!";

                    // Analyse just the active window within the frame that changed (ignoring the rest)
                    TryAnalyseImage(imageToAnalyse, millsSinceLast);
                }
                else
                {
                    // Alert cleared
                    Alert = "";

                    // After quiet period, start removing old reports
                    if (analysisResult.Count > 0 && millsSinceLast > MaxMillsTillCleared)
                    {
                        lock (analysisResultLockObject)
                        {
                            analysisResult.RemoveAt(0);
                        }
                        // Set time for next removal
                        lock (lastAnalysisLockObject)
                        {
                            lastAnalysis = DateTime.Now.AddMilliseconds((MaxMillsTillCleared * -1) + CleardownSpeedMills);
                        }
                    }
                }

                // If config window is open, show all the stages of image processing.
                if (Plugins.Configure.ShowingConfig)
                {
                    try
                    {
                        Plugins.Configure.RawImage.Image = frame;
                        Plugins.Configure.PixelsThumb.Image = imageToAnalyse.PixelatedThumbnail;
                        Plugins.Configure.ChangesThumb.Image = imageToAnalyse.ChangeThumbnail;
                        lock (analysisResultLockObject)
                        {
                            var cnt = analysisResult.Count;
                            if (cnt > 0)
                            {
                                // To stop them flashing by, just show the last result, until a new one comes
                                Plugins.Configure.ActionImage.Image = analysisResult[cnt - 1].AnalysedImageThumb;
                            }
                        }

                        var pcBar = new Bitmap(100, 1);
                        using (Graphics g = Graphics.FromImage(pcBar))
                        {
                            var brush = Alert == "" ? Brushes.Green : Brushes.Red;
                            g.DrawLine(new Pen(brush, 1), new Point(0, 0), new Point(percentChange));
                        }
                        Plugins.Configure.AlarmPercentage.Image = pcBar;
                        Plugins.Configure.PercentLabel.Text = $"{percentChange}%";
                    }
                    catch (Exception exc)
                    {
                        Logger.LogError(exc);
                        throw;
                    }
                }

                return imageToAnalyse.OriginalImage;
            }
            catch (Exception exc)
            {
                Logger.LogError(exc);
                throw;
            }
        }

        /// <summary>
        /// Try and analyse the image
        /// </summary>
        /// <param name="imageToAnalyse">The image</param>
        /// <param name="millsSinceLast">How long since last analysis</param>
        private void TryAnalyseImage(ImageToAnalyse imageToAnalyse, double millsSinceLast)
        {
            if (millsSinceLast > AnalysingGapMills) // Throttle analysis requests
            {
                lock (lastAnalysisLockObject)
                {
                    lastAnalysis = DateTime.Now;
                }

                double upScale = 1 / ((double)imageToAnalyse.PixelatedThumbnail.Width / (double)imageToAnalyse.OriginalImage.Width);

                // Convert pixel boundaries to full size image boundaries
                // Then make cropped image of the actual "action" within the frame
                // Only the moving area of the frame (the actual action) will be analysed by Azure Cognitive Services
                int scaledUpWidth = (int)((imageToAnalyse.BoundaryEndPoint.X - imageToAnalyse.BoundaryStartPoint.X) * upScale);
                int scaledUpHeight = (int)((imageToAnalyse.BoundaryEndPoint.Y - imageToAnalyse.BoundaryStartPoint.Y) * upScale);
                imageToAnalyse.ActionWindow = new Bitmap(scaledUpWidth, scaledUpHeight);
                FrameProcessor.CopyRegionIntoImage(imageToAnalyse.OriginalImage, new Rectangle((int)(imageToAnalyse.BoundaryStartPoint.X * upScale), (int)(imageToAnalyse.BoundaryStartPoint.Y * upScale), scaledUpWidth, scaledUpHeight), ref imageToAnalyse.ActionWindow, new Rectangle(0, 0, scaledUpWidth, scaledUpHeight));

                // To see the cropped action window, from within the frame, uncomment below and check the AppData log folder 
                // imageToAnalyse.ActionWindow.Save($"{Logger.LogFilePath}\\ActionSnap_{DateTime.Now.ToString("yyyyMMddHHmmss")}.bmp");

                Task.Factory.StartNew((img) =>
                {
                    try
                    {
                        var Img = img as ImageToAnalyse;

                        var newAnalysis = analyser.MakeAnalysisRequest(Img.ActionWindow).Result;

                        // Uncomment to check the calculated boundaries
                        // resultsText[0] += $" {imageToAnalyse.BoundaryStartPoint.X},{imageToAnalyse.BoundaryStartPoint.Y},{scaledWidth},{scaledHeight}";

                        Bitmap scaledAction = Img.MakeActionThumb(ScaledActionPixels);
                        newAnalysis.AnalysedImageThumb = scaledAction;

                        lock (analysisResultLockObject)
                        {
                            if (analysisResult.Count > MaxListed)
                            {
                                analysisResult.RemoveAt(0);
                            }

                            analysisResult.Add(newAnalysis);
                        }

                        if (newAnalysis.HasPerson) 
                        {
                            // Special alert for people!
                            SoundPlayer player = new SoundPlayer(Resources.Success);
                            player.Play();
                        }

                    }
                    catch (Exception exc)
                    {
                        Logger.LogError(exc);
                    }
                }, imageToAnalyse);
            }
        }

        internal void UpdateConfig(int sensitivity, int minAlartPercent, int maxAlartPercent, int maxPixelsDetail, bool showPixels)
        {
            try
            {
                MinAlartPercent = minAlartPercent;
                MaxAlartPercent = maxAlartPercent;
                Sensitivity = sensitivity;
                MaxPixelsDetail = maxPixelsDetail;
                ShowPixels = showPixels;
                processor = new FrameProcessor(Sensitivity, MaxPixelsDetail, showPixels);
            }
            catch (Exception exc)
            {
                Logger.LogError(exc);
            }
        }

        private void InitConfig()
        {
            // if config has 4 items
            if (_config != "")
            {
                string[] cfg = _config.Split('|');
                if (cfg.Length == 5)
                {
                    MinAlartPercent = Convert.ToInt32(cfg[0]);
                    MaxAlartPercent = Convert.ToInt32(cfg[1]);
                    Sensitivity = Convert.ToInt32(cfg[2]);
                    MaxPixelsDetail = Convert.ToInt32(cfg[3]);
                    ShowPixels = Convert.ToBoolean(cfg[4]);
                }
            }
        }

        public string Configure()
        {
            var cfg = new Configure(this);
            if (cfg.ShowDialog() == DialogResult.OK)
            {
                _config = $"{MinAlartPercent}|{MaxAlartPercent}|{Sensitivity}|{MaxPixelsDetail}|{ShowPixels}";

                InitConfig();
            }
            return Configuration;
        }       // <-- Used by iSpy

        //Implement IDisposable.
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Free other state (managed objects).
                }
                // Free your own state (unmanaged objects).
                // Set large fields to null.
                _disposed = true;
            }
        }

        // Use C# destructor syntax for finalization code.
        ~Main()
        {
            // Simply call Dispose(false).
            Dispose(false);
        }

    }

}
