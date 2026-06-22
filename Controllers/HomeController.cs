using Health_Guardian_AI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity; // Sử dụng thư viện có sẵn này để mã hóa mật khẩu
using System.Diagnostics;

namespace Health_Guardian_AI.Controllers
{
    public class HomeController : Controller
    {
        private readonly AppDbContext _context;
        // Công cụ mã hóa mật khẩu bảo mật chuẩn Microsoft, không lo thiếu thư viện
        private readonly PasswordHasher<User> _passwordHasher = new PasswordHasher<User>();
        public HomeController(AppDbContext context)
        {
            _context = context;
        }
        // GET: Hiển thị giao diện lúc mới vào
        public IActionResult Index()
        {
            // Đã sửa lỗi chữ "UserID" thành "UserId" cho khớp với lúc SetString
            var userId = HttpContext.Session.GetString("UserId");

            // Nếu ĐÃ ĐĂNG NHẬP, chuyển thẳng sang Dashboard, không cho ở lại trang Login
            if (!string.IsNullOrEmpty(userId))
            {
                return RedirectToAction("Index", "Dashboard"); // Đổi thành Dashboard
            }
            // Gán giá trị mặc định để View không bị lỗi null reference
            ViewBag.IsLoggedIn = false;
            ViewBag.UserFullName = "";
            ViewBag.UserRole = "";
            return View();
        }
        // Đăng Ký ở đây
        [HttpPost]
        public IActionResult Register(RegisterRequest request)
        {
            if (!ModelState.IsValid)
            {
                // Nếu lỗi, trả về trang Index kèm View để hiện báo lỗi
                ViewBag.AuthMode = "register";
                return View("Index", request);
            }

            // GỌI SQL SERVER: Kiểm tra trùng Email
            if (_context.Users.Any(u => u.Email.ToLower() == request.Email.ToLower()))
            {
                ViewBag.AuthMode = "register";
                ViewBag.Error = "Email này đã tồn tại trong hệ thống.";
                return View("Index");
            }

            var newUser = new User
            {
                Id = Guid.NewGuid().ToString(),
                FullName = request.FullName,
                Email = request.Email,
                Role = "Patient"
            };
            newUser.Password = _passwordHasher.HashPassword(newUser, request.Password);

            // GỌI SQL SERVER: Lưu vào Database
            _context.Users.Add(newUser);
            _context.SaveChanges();

            ViewBag.Success = "Đăng ký thành công! Vui lòng đăng nhập.";
            return View("Index");
        }
        [HttpPost]
        public IActionResult Login(LoginRequest request)
        {
            if (!ModelState.IsValid) return View("Index", request);

            // GỌI SQL SERVER: Tìm user dưới DB
            var user = _context.Users.FirstOrDefault(u => u.Email.ToLower() == request.Email.ToLower());

            if (user == null)
            {
                ViewBag.Error = "Email hoặc mật khẩu không chính xác.";
                return View("Index");
            }

            var result = _passwordHasher.VerifyHashedPassword(user, user.Password, request.Password);
            if (result == PasswordVerificationResult.Failed)
            {
                ViewBag.Error = "Email hoặc mật khẩu không chính xác.";
                return View("Index");
            }

            //Ghi nhận phiên đăng nhập
            HttpContext.Session.SetString("UserId", user.Id);
            HttpContext.Session.SetString("UserFullName", user.FullName);
            HttpContext.Session.SetString("UserEmail", user.Email);
            HttpContext.Session.SetString("UserRole", user.Role);

            // Chuyển hướng sang Controller Dashboard mới
            return RedirectToAction("Index", "Dashboard");
        }
        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
