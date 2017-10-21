using DigitalEyes.iSpy.DetectAnalyse.Helpers;
using Newtonsoft.Json;
using Plugins;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Media;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace DigitalEyes.iSpy.DetectAnalyse.Model
{
    class FrameAnalyser
    {

        static string subscriptionKey;
        static string uriBase;

        public FrameAnalyser(string SubscriptionKey, string UriBase)
        {
            subscriptionKey = SubscriptionKey;
            uriBase = UriBase;
        }

        // Set to width you want text to flow over image
        const int MaxTextCharWidth = 50;

        // set trigger tags
        static List<string> personTags = new List<string> { "man", "woman", "person", "people", "boy", "girl" };

        public async Task<AnalysisReport> MakeAnalysisRequest(string imageFilePath)
        {
            byte[] byteData = GetImageFileAsByteArray(imageFilePath);
            return await RequestAnalyseByteArrayImage(byteData);
        }

        public async Task<AnalysisReport> MakeAnalysisRequest(Bitmap image)
        {
            byte[] byteData = ImageToByte(image);
            return await RequestAnalyseByteArrayImage(byteData);
        }

        static async Task<AnalysisReport> RequestAnalyseByteArrayImage(byte[] byteData)
        {
            var report = new AnalysisReport
            {
                Reports = new List<string>() { $"{DateTime.Now}" }
            };

            string responseContentString = "";

            try
            {
                HttpClient client = new HttpClient();
                
                client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", subscriptionKey);
                string requestParameters = "visualFeatures=Categories,Description,Color&language=en";
                string uri = uriBase + "?" + requestParameters;

                HttpResponseMessage response;

                using (ByteArrayContent content = new ByteArrayContent(byteData))
                {
                    content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                    response = await client.PostAsync(uri, content);
                }

                // Get the JSON response.
                responseContentString = await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
                report.Reports.Add(ex.Message);
                return report;
            }
            
            try
            {
                Logger.LogMessage(responseContentString);

                dynamic r =JsonConvert.DeserializeObject(responseContentString);

                // Categories
                if (r.categories != null)
                {
                    Newtonsoft.Json.Linq.JArray cats = r.categories;
                    foreach (dynamic cat in cats)
                    {
                        var name = (string)cat.name;
                        report.Reports.Add($"Category: {name} ({cat.score}))");

                        // Check for person
                        if (name.ToLower().Contains("people"))
                        {
                            report.HasPerson = true;
                        }
                    }
                }
                else
                {
                    report.Reports.Add("Uncategorised");
                }

                // Tags
                if (r.description.tags != null)
                {
                    var tags = "Tags: ";
                    foreach (string tag in r.description.tags)
                    {
                        tags += "[" + tag + "] ";
                        if (tags.Length > MaxTextCharWidth)
                        {
                            report.Reports.Add(tags);
                            tags = "";
                        }

                        // Check for person
                        if (personTags.Contains(tag))
                        {
                            report.HasPerson = true;
                        }
                    }
                    if (tags != "")
                        report.Reports.Add(tags);
                }
                else
                {
                    report.Reports.Add("No tags");
                }

                // Captions
                if (r.description.captions != null)
                {
                    report.Reports.Add("Captions: ");
                    foreach (dynamic caption in r.description.captions)
                    {
                        report.Reports.Add($"\"{caption.text}\" ({caption.confidence})");
                    }
                }
                else
                {
                    report.Reports.Add("No captions");
                }
            }
            catch (Exception exc)
            {
                Logger.LogError(exc);
                report.Reports.Add("Error: " + exc.Message);
            }

            return report;
        }

        /// <summary>
        /// Returns the contents of the specified file as a byte array.
        /// </summary>
        /// <param name="imageFilePath">The image file to read.</param>
        /// <returns>The byte array of the image data.</returns>
        static byte[] GetImageFileAsByteArray(string imageFilePath)
        {
            FileStream fileStream = new FileStream(imageFilePath, FileMode.Open, FileAccess.Read);
            BinaryReader binaryReader = new BinaryReader(fileStream);
            return binaryReader.ReadBytes((int)fileStream.Length);
        }

        public static byte[] ImageToByte(Image img)
        {
            ImageConverter converter = new ImageConverter();
            return (byte[])converter.ConvertTo(img, typeof(byte[]));
        }
    }
}
