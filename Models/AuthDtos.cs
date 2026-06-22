using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
namespace Health_Guardian_AI.Models
{
    public class AuthDtos
    {
    }
    public class RegisterRequest
    {
        [Required] public string FullName { get; set; }
        [Required][EmailAddress] public string Email { get; set;  }
        [Required][StringLength(100, MinimumLength = 6)] public string Password { get; set; }
        public string Role { get; set; } = "Patient";
    }
    public class LoginRequest
    {
        [Required][EmailAddress] public string Email { get; set; }
        [Required] public string Password { get; set; }
    }
    public class SaveProfileRequest
    {
        public string UserId { get; set; }
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
    }
    public class PredictAnswer 
    {
        public double Age { get; set; }
        public int Hypertension { get; set; }
        public int Heart_Disease { get; set; }
        public double BMI { get; set; }
        public double Avg_Glucose { get; set; }
        public int Diabetes { get; set; }
        public string Gender { get; set; }
        public string SES { get; set; }
        public string Smoking_Status { get; set; }

    }
    public class TopFactors
    {
        public string Feature { get; set; }
        public double Impact { get; set; }
    }

    public class StrokeResponse
    {
        public int Prediction { get; set; }
        public double Probability { get; set; }
        //Do AI trả về top_factors Json
        [JsonPropertyName("top_factors")]
        public List<TopFactors>? TopFactors { get; set; }
    }
}
