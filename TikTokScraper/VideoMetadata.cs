using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TikTokScraper
{
    public class VideoMetadata
    {
        public string Url { get; set; }
        public string Views { get; set; }
        public string Likes { get; set; }
        public string Comments { get; set; }
        public string Shares { get; set; }
        public string Caption { get; set; }
        public string UploadTime { get; set; }
    }
}
