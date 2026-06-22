using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Health_Guardian_AI.Models
{
    public class Prediction
    {
        [Key]
        public string Id { get; set;  } = Guid.NewGuid().ToString();
        public string HPId { get; set; }
        [ForeignKey("HPId")]
        public virtual HealthProfile HealthProfile { get; set; } 
        public double Probability { get; set; }
        public string Factors_1 { get; set; } = "";//Nguyên nhân chính gây ra nguy cơ đột quỵ cao
        public string Factors_2 { get; set; } = "";//Nguyên nhân chính gây ra nguy cơ đột quỵ cao
        public int Result {  get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.Now;

    }
}
