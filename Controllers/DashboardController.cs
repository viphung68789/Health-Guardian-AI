using Health_Guardian_AI.Models;
using Health_Guardian_AI.Service;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Text.Json;

namespace Health_Guardian_AI.Controllers
{
    public class DashboardController : Controller
    {
        private readonly AppDbContext _context;
        private readonly HttpClient _httpClient;
        private readonly GeminiService _geminiService;
        public DashboardController(AppDbContext context,HttpClient httpClient, GeminiService geminiService)
        {
            _context = context;
            _httpClient = httpClient;
            _geminiService = geminiService;
        }
        public IActionResult Index()
        {
            // BẢO MẬT: Kiểm tra xem có Session chưa, nếu chưa bắt quay lại trang Login
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("UserId")))
            {
                return RedirectToAction("Index", "Home");
            }

            //Có thể truyền thông tin user ra View nếu cần

            ViewBag.UserFullName = HttpContext.Session.GetString("UserFullName");
            ViewBag.UserEmail = HttpContext.Session.GetString("UserEmail");
            var userId = HttpContext.Session.GetString("UserId");
            var viewProfile = _context.HealthProfiles.Where(u => u.UserId == userId).OrderByDescending(o => o.RecordedDate).FirstOrDefault();
            if (viewProfile == null)
            {
                return View(new SaveProfileRequest ());
            }
            var modelProfile = new SaveProfileRequest{
                Age = viewProfile.Age,
                Gender = viewProfile.Gender,
                Height = viewProfile.Height,
                Weight = viewProfile.Weight,
                SmokingStatus =viewProfile.SmokingStatus,
                SES = viewProfile.SES
            };
            return View(modelProfile);
        }
        [HttpPost]
        public IActionResult SaveProfile([FromBody] SaveProfileRequest model)
        {
            var userId = HttpContext.Session.GetString("UserId");
            if (userId == null)
            {
                return Unauthorized();
            }
            model.UserId = userId;
            if(model.AvgGlucose <=0)
            {
                model.AvgGlucose = model.Diabetes ? 140 : 90;
            }
            var profile = new HealthProfile
            {
                UserId = userId,
                Age = model.Age,
                Gender = model.Gender,
                SES = model.SES,
                Height = model.Height,
                Weight = model.Weight,
                Hypertension = model.Hypertension,
                HeartDisease = model.HeartDisease,
                Diabetes = model.Diabetes,
                AvgGlucose = model.AvgGlucose,
                SmokingStatus = model.SmokingStatus
            };
            profile.BMI = Math.Round(profile.Weight / Math.Pow(profile.Height / 100, 2), 1);
            _context.HealthProfiles.Add(profile);
            _context.SaveChanges();
            var profileId = _context.HealthProfiles.Where(p => p.UserId == userId).OrderByDescending(p => p.RecordedDate).Select(p => p.Id).FirstOrDefault();
            HttpContext.Session.SetString("ProfileID",profileId);
            HttpContext.Session.SetString("UserBMI", profile.BMI.ToString());
            return Json(new { success = true, bmi = profile.BMI });
        }
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Index","Home");
        }
        [HttpPost]
        public async Task<IActionResult> Predict([FromBody]PredictAnswer model)
        {

            var client = new HttpClient();

            var json = JsonSerializer.Serialize(model);

            var content = new StringContent(json,Encoding.UTF8,"application/json");

            var response = await client.PostAsync("https://stroke-api-ym2h.onrender.com/predict",content);

            if (!response.IsSuccessStatusCode)
            {
                return View("Error");
            }

            var resultJson = await response.Content.ReadAsStringAsync();

            var result = JsonSerializer.Deserialize<StrokeResponse>(resultJson,new JsonSerializerOptions{PropertyNameCaseInsensitive = true});

            var save = new Prediction
            {
                HPId = HttpContext.Session.GetString("ProfileID"),
                Probability = result.Probability,
                Factors_1 = result.TopFactors?.ElementAtOrDefault(0)?.Feature ??"",
                Factors_2 = result.TopFactors?.ElementAtOrDefault(1)?.Feature ??"",
                Result = result.Prediction
            };
            _context.Predictions.Add(save);
            await _context.SaveChangesAsync();
            return Json(result);
            
        }
        public async Task<IActionResult> ProfileUpdatePartial()
        {
            var userId = HttpContext.Session.GetString("UserId");
            if (_context.HealthProfiles.Where(o => o.UserId == userId).IsNullOrEmpty())
            {
                return PartialView("_ProfileUpdatePartial");
            }
            var BMI = _context.HealthProfiles.Where(x => x.UserId == userId).OrderByDescending(p => p.RecordedDate).Select(s => s.BMI).FirstOrDefault();
            var HealId = _context.HealthProfiles.Where(x => x.UserId == userId).OrderByDescending(o => o.RecordedDate).Select(s => s.Id).FirstOrDefault().ToString();
            var Probability = _context.Predictions.Where(x => x.HPId == HealId).Select(s => s.Probability).FirstOrDefault();
            var fact_1 = _context.Predictions.Where(x => x.HPId == HealId).Select(s => s.Factors_1).FirstOrDefault();
            var fact_2 = _context.Predictions.Where(x => x.HPId == HealId).Select(s => s.Factors_2).FirstOrDefault();
            ViewBag.UserBMI = BMI;
            ViewBag.Problem = Math.Round(Probability * 100, 2);
            ViewBag.fact_1 = fact_1;
            ViewBag.fact_2 = fact_2;
            return PartialView("_ProfileUpdatePartial");
        }
        public IActionResult ChartStroke()
        {
            var userId = HttpContext.Session.GetString("UserId");
            var PreChart = _context.HealthProfiles
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.RecordedDate)
            .Select(x => new
            {
                // Nếu hồ sơ chưa có kết quả dự đoán (Prediction null), lấy mặc định là 0
                // Nhân 100 để chuyển đổi giá trị xác suất từ (0 -> 1) thành (0% -> 100%)
                pro = x.Prediction != null ? Math.Round(x.Prediction.Probability * 100, 2) : 0,

                // Định dạng ngày tháng hiển thị dưới trục X dựa trên ngày tạo hồ sơ
                mydate = x.RecordedDate.ToString("dd/MM/yyyy HH:mm")
            })
            .Take(5).Reverse().ToList();
            foreach (var item in PreChart)
            {
                Console.WriteLine(item);
            }
            return Json(PreChart);
        }
    }
}
