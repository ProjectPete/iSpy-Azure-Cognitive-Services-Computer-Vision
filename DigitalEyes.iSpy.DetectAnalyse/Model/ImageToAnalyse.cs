using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DigitalEyes.iSpy.DetectAnalyse.Model
{
    class ImageToAnalyse
    {
        public Bitmap OriginalImage;
        public Bitmap PixelatedThumbnail;
        public Bitmap ChangeThumbnail;
        public Bitmap ActionWindow;
        public Point BoundaryStartPoint;
        public Point BoundaryEndPoint;
        public int ChangedPixels;

        internal Bitmap MakeActionThumb(double scaledActionPixels)
        {
            // Make the thumbnail version, which is posted to the reports and shown in configure window.
            // Scale to stretch and fit (keep dimensions)
            var ratioX = scaledActionPixels / ActionWindow.Width;
            var ratioY = scaledActionPixels / ActionWindow.Height;
            var ratio = Math.Min(ratioX, ratioY);
            var thumbWidth = (int)(ActionWindow.Width * ratio);
            var thumbHeight = (int)(ActionWindow.Height * ratio);

            var scaledAction = FrameProcessor.ScaleImage(ActionWindow, thumbWidth, thumbHeight);
            // To check/save/see the thumb, uncomment below and check the AppData log folder 
            //scaledAction.Save($"{Logger.LogFilePath}\\ActionSnapThumb_{DateTime.Now.ToString("yyyyMMddHHmmss")}.bmp");

            using (Graphics g = Graphics.FromImage(scaledAction))
            {
                var borderHeight = scaledAction.Height - 1;
                var borderWidth = scaledAction.Width - 1;
                g.DrawLine(new Pen(Brushes.White, 1), new Point(0, 0), new Point(0, borderHeight));
                g.DrawLine(new Pen(Brushes.White, 1), new Point(0, 0), new Point(borderWidth, 0));
                g.DrawLine(new Pen(Brushes.White, 1), new Point(0, borderHeight), new Point(borderWidth, borderHeight));
                g.DrawLine(new Pen(Brushes.White, 1), new Point(borderWidth, 0), new Point(borderWidth, borderHeight));
            }

            return scaledAction;
        }
    }
}
