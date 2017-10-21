using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DigitalEyes.iSpy.DetectAnalyse.Model
{
    class AnalysisReport
    {
        public Bitmap AnalysedImageThumb { get; set; }
        public List<string> Reports { get; set; }
        public bool HasPerson { get; set; }
    }
}
