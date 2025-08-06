using System.ComponentModel.DataAnnotations;

namespace MVS_Project.Models
{
    public class DirectionsRequest
    {
        [Required]
        public List<double[]> Coordinates { get; set; } = new List<double[]>();

        public string Profile { get; set; } = "driving-car";
        public bool Instructions { get; set; } = false;
        public bool Geometry { get; set; } = true;
    }
}
