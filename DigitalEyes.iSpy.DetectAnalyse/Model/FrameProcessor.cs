using DigitalEyes.iSpy.DetectAnalyse.Helpers;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Media;
using System.Text;
using System.Threading.Tasks;

namespace DigitalEyes.iSpy.DetectAnalyse.Model
{
    class FrameProcessor
    {
        private Bitmap lastBmp;
        private int maxPixels;
        int sensitivity;
        int setWidth;
        int setHeight;
        double totalPixels;
        bool showPixels;
        int fontSize = 7;
        Font font;
        Brush backgroundBrush;

        public FrameProcessor(int Sensitivity, int MaxPixelsWidthHeight, bool ShowPixels)
        {
            sensitivity = Sensitivity;
            maxPixels = MaxPixelsWidthHeight;
            showPixels = ShowPixels;
            font = new Font("Verdana", fontSize);
            backgroundBrush = new SolidBrush(Color.FromArgb(80, 0, 0, 0));
        }

        public int GetChangePercentageFromLast(ImageToAnalyse imageToAnalyse, List<AnalysisReport> analysis)
        {
            if (setWidth == 0)
            {
                // Scale to fit
                var ratioX = (double)maxPixels / imageToAnalyse.OriginalImage.Width;
                var ratioY = (double)maxPixels / imageToAnalyse.OriginalImage.Height;
                var ratio = Math.Min(ratioX, ratioY);
                setWidth = (int)(imageToAnalyse.OriginalImage.Width * ratio);
                setHeight = (int)(imageToAnalyse.OriginalImage.Height * ratio);

                totalPixels = setHeight * setWidth;
            }

            imageToAnalyse.PixelatedThumbnail = ScaleImage(imageToAnalyse.OriginalImage, setWidth, setHeight);
            imageToAnalyse.ChangeThumbnail = (Bitmap)imageToAnalyse.PixelatedThumbnail.Clone(); // will have change highlighted in red later

            if (lastBmp != null)
            {
                ConvertToGreyscaleAndComparePixelsWithLast(imageToAnalyse);

                lastBmp = imageToAnalyse.PixelatedThumbnail; // Save now, to compare with next

                // Picture in picture
                if (showPixels)
                {
                    // Make enlarged pixel 'picture in picture'
                    try
                    {
                        imageToAnalyse.ChangeThumbnail = ScaleUpThePixels(imageToAnalyse.ChangeThumbnail, 10);
                        CopyRegionIntoImage(imageToAnalyse.ChangeThumbnail, new Rectangle(0, 0, imageToAnalyse.ChangeThumbnail.Width, imageToAnalyse.ChangeThumbnail.Height), ref imageToAnalyse.OriginalImage, new Rectangle(0, 0, imageToAnalyse.ChangeThumbnail.Width, imageToAnalyse.ChangeThumbnail.Height));
                    }
                    catch (Exception exc)
                    {
                        Logger.LogError(exc);
                    }
                }

                // Add analysis results
                if (analysis != null && analysis.Count > 0)
                {
                    var y = 30; // Starting Y pos

                    using (Graphics g = Graphics.FromImage(imageToAnalyse.OriginalImage))
                    {
                        string separator = $"{Environment.NewLine}";

                        try
                        {
                            foreach (AnalysisReport result in analysis)
                            {
                                g.DrawImage(result.AnalysedImageThumb, new Rectangle(0, y, result.AnalysedImageThumb.Width, result.AnalysedImageThumb.Height), new Rectangle(0, 0, result.AnalysedImageThumb.Width, result.AnalysedImageThumb.Height), GraphicsUnit.Pixel);

                                foreach (var row in result.Reports)
                                {
                                    var x = result.AnalysedImageThumb.Width + 10;
                                    var size = g.MeasureString(row, font);
                                    var rect = new RectangleF(x, y, size.Width, size.Height);
                                    g.FillRectangle(backgroundBrush, rect);
                                    g.DrawString(row, new Font("Verdana", fontSize), new SolidBrush(Color.White), x, y);

                                    y += 10;
                                }

                                y += 4;
                            }
                        }
                        catch (Exception exc)
                        {
                            Logger.LogError(exc);
                            // ignore for now, probably too many rows?
                        }
                    }
                }
            }
            else
            {
                lastBmp = imageToAnalyse.PixelatedThumbnail;
            }

            double pc = (imageToAnalyse.ChangedPixels / totalPixels) * 100;

            return Convert.ToInt32(Math.Round(pc, 0));
        }

        private void ConvertToGreyscaleAndComparePixelsWithLast(ImageToAnalyse imageToAnalyse)
        {
            Color p;
            int changedPixels = 0;

            //Set opposites for later adjust
            imageToAnalyse.BoundaryStartPoint.X = imageToAnalyse.PixelatedThumbnail.Width - 1;
            imageToAnalyse.BoundaryStartPoint.Y = imageToAnalyse.PixelatedThumbnail.Height - 1;
            imageToAnalyse.BoundaryEndPoint.X = 0;
            imageToAnalyse.BoundaryEndPoint.Y = 0;

            //grayscale and compare percent
            try
            {
                for (int y = 0; y < imageToAnalyse.PixelatedThumbnail.Height; y++)
                {
                    for (int x = 0; x < imageToAnalyse.PixelatedThumbnail.Width; x++)
                    {
                        // Get next pixel
                        p = imageToAnalyse.PixelatedThumbnail.GetPixel(x, y);

                        // Extract ARGB from pixel
                        int a = p.A;
                        int r = p.R;
                        int g = p.G;
                        int b = p.B;

                        // Get average
                        int avg = (r + g + b) / 3;

                        // Black and white
                        //avg = (avg < 128) ? 1 : 0;

                        // Set new pixel value
                        var col = Color.FromArgb(a, avg, avg, avg);
                        imageToAnalyse.PixelatedThumbnail.SetPixel(x, y, col);

                        // Compare to last frame same pixel
                        try
                        {
                            var lastPixel = lastBmp.GetPixel(x, y);
                            if (avg < lastPixel.R - sensitivity || avg > lastPixel.R + sensitivity)
                            {
                                changedPixels++;

                                // Determine action window boundaries
                                if (x < imageToAnalyse.BoundaryStartPoint.X)
                                    imageToAnalyse.BoundaryStartPoint.X = x;
                                if (x > imageToAnalyse.BoundaryEndPoint.X)
                                    imageToAnalyse.BoundaryEndPoint.X = x;

                                if (y < imageToAnalyse.BoundaryStartPoint.Y)
                                    imageToAnalyse.BoundaryStartPoint.Y = y;
                                if (y > imageToAnalyse.BoundaryEndPoint.Y)
                                    imageToAnalyse.BoundaryEndPoint.Y = y;

                                imageToAnalyse.ChangeThumbnail.SetPixel(x, y, Color.Red);
                            }
                        }
                        catch (Exception exc)
                        {
                            Logger.LogError(exc);
                        }
                    }
                }
            }
            catch (Exception exc)
            {
                Logger.LogError(exc);
            }

            imageToAnalyse.ChangedPixels = changedPixels;
        }

        public Bitmap ScaleUpThePixels(Bitmap snapshot, int scale)
        {
            var newWidth = snapshot.Width * scale;
            var newHeight = snapshot.Height * scale;
            var newImage = new Bitmap(newWidth, newHeight);

            for (int y = 0; y < snapshot.Height; y++)
            {
                for (int x = 0; x < snapshot.Width; x++)
                {
                    var px = snapshot.GetPixel(x, y);
                    var xStart = x * scale;
                    var yStart = y * scale;

                    for (var y2 = 0; y2 < scale; y2++)
                    {
                        for (var x2 = 0; x2 < scale; x2++)
                        {
                            newImage.SetPixel(xStart + x2, yStart + y2, px);
                        }
                    }
                }
            }

            return newImage;
        }

        public static void CopyRegionIntoImage(Bitmap srcBitmap, Rectangle srcRegion, ref Bitmap destBitmap, Rectangle destRegion)
        {
            using (Graphics grD = Graphics.FromImage(destBitmap))
            {
                grD.DrawImage(srcBitmap, destRegion, srcRegion, GraphicsUnit.Pixel);
            }
        }

        public static Bitmap ScaleImage(Image image, int width, int height)
        {
            var newImage = new Bitmap(width, height);

            using (var graphics = Graphics.FromImage(newImage))
                graphics.DrawImage(image, 0, 0, width, height);

            return newImage;
        }
    }

}
