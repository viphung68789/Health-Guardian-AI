using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Health_Guardian_AI.Models
{
    public class InFormPrediction
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string HPId { get; set; }
        [ForeignKey("HPId")]
        public virtual HealthProfile HealthProfile { get; set; }
        public string Messenge { get; set; }

    }
}
