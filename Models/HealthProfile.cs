using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace Health_Guardian_AI.Models
{
    public class HealthProfile
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString(); 
        // Tạo khóa ngoại chuẩn kết nối với User
        public string UserId { get; set; }
        [ForeignKey("UserId")]
        public virtual User User { get; set; }
        public double Age { get; set; }
        public string Gender { get; set; } // male | female
        public string SES { get; set; } = "Medium";// Low| Medium | High
        public double Height { get; set; }
        public double Weight { get; set; }
        public double BMI { get; set; }
        public bool Hypertension { get; set; }
        public bool HeartDisease { get; set; }
        public bool Diabetes { get; set; }
        public double AvgGlucose { get; set; }

        public string SmokingStatus { get; set; } // 'never' | 'formerly' | 'smokes'
        public DateTime RecordedDate { get; set; } = DateTime.Now;
        public virtual Prediction Prediction { get; set; } 
        public virtual InFormPrediction InFormPrediction { get; set; }
    }
}
