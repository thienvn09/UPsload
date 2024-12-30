using DoAn.DAL;
using DoAn.Helper;
using DoAn.Models;
using DoAn.Models.Momo;
using DoAn.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Security.Cryptography;

namespace DoAn.Controllers
{
    public class CartController : Controller
    {
        private readonly ProductDAL _productDAL = new ProductDAL();
        private readonly CustomerDAL _customerDAL = new CustomerDAL();
        private readonly CartDAL _cartDAL = new CartDAL();
        private readonly IMomoService _momoService;
        private readonly MomoPaymentDAL _momoPaymentDAL = new MomoPaymentDAL();

        public CartController(IMomoService momoService)
        {
            _momoService = momoService;
        }

        public List<CartItem> Cart => HttpContext.Session.Get<List<CartItem>>(MyConst.CART_KEY) ?? new List<CartItem>();

        public IActionResult Index()
        {
            return View(Cart);
        }

        // Lấy tổng tiền thanh toán
        public IActionResult GetTotalAmount()
        {
            int totalAmount = Cart.Sum(item => item.Total);
            return Json(totalAmount);
        }

        public IActionResult AddToCart(int id, int quantity = 1)
        {
            var gioHang = Cart;
            var item = gioHang.SingleOrDefault(p => p.IdProduct == id);
            if (item == null)
            {
                Product productById = _productDAL.GetProductById(id);
                if (productById == null)
                {
                    TempData["Message"] = "Không tìm thấy sản phẩm";
                    return Redirect("/404");
                }
                item = new CartItem
                {
                    IdProduct = productById.Id,
                    Img = productById.Img,
                    Name = productById.Title,
                    Price = productById.Price,
                    Rate = productById.Rate,
                    Quantity = quantity
                };
                gioHang.Add(item);
            }
            else
            {
                item.Quantity += quantity;
            }
            HttpContext.Session.Set(MyConst.CART_KEY, gioHang);
            return RedirectToAction("Index");
        }

        public IActionResult ChangeQuantityCart(int id, bool isIncrement = true, int quantity = 1)
        {
            var gioHang = Cart;
            var item = gioHang.SingleOrDefault(p => p.IdProduct == id);
            if (item == null)
            {
                TempData["Message"] = "Không tìm thấy sản phẩm";
                return Redirect("/404");
            }
            else
            {
                if (isIncrement)
                {
                    item.Quantity += quantity;
                }
                else
                {
                    item.Quantity -= quantity;
                    if (item.Quantity <= 0)
                    {
                        gioHang.Remove(item);
                    }
                }
            }
            HttpContext.Session.Set(MyConst.CART_KEY, gioHang);
            return RedirectToAction("Index");
        }

        public IActionResult RemoveCart(int id)
        {
            var cart = Cart;
            var item = cart.SingleOrDefault(p => p.IdProduct == id);

            if (item != null)
            {
                cart.Remove(item);
                HttpContext.Session.Set(MyConst.CART_KEY, cart);
            }

            return RedirectToAction("Index");
        }

        [Authorize]
        public async Task<IActionResult> CheckOut(string paymentMethod, string orderName, string orderDescription)
        {
            try
            {
                if (!Cart.Any())
                {
                    TempData["CheckOutErrorMessage"] = "Giỏ hàng của bạn đang trống.";
                    return RedirectToAction("Index");
                }

                string? customerIdStr = HttpContext.User.FindFirstValue("CustomerId");
                if (string.IsNullOrEmpty(customerIdStr))
                {
                    TempData["CheckOutErrorMessage"] = "Bạn cần đăng nhập để thực hiện thanh toán.";
                    return Redirect("/Customer/SignIn");
                }

                int customerId = int.Parse(customerIdStr);
                var customer = _customerDAL.GetCustomerById(customerId);
                if (customer == null)
                {
                    TempData["CheckOutErrorMessage"] = "Không tìm thấy thông tin khách hàng.";
                    return RedirectToAction("Index");
                }

                // Chuẩn bị thông tin đơn hàng
                var totalAmount = Cart.Sum(item => item.Total);
                var orderInfo = new OrderInfoModel
                {
                    FullName = orderName,
                    Amount = totalAmount.ToString(),
                    OrderInfo = orderDescription
                };

                // Xử lý thanh toán
                switch (paymentMethod.ToLower())
                {
                    case "momo":
                        var momoResponse = await _momoService.CreatePaymentAsync(orderInfo);
                        if (!string.IsNullOrEmpty(momoResponse.PayUrl))
                        {
                            TempData["CheckOutSuccessMessage"] = "Chuyển đến cổng thanh toán MoMo.";
                            return Redirect(momoResponse.PayUrl);
                        }
                        TempData["CheckOutErrorMessage"] = "Không thể tạo liên kết thanh toán qua MoMo.";
                        break;

                    case "cod":
                        bool isCheckoutSuccessful = _cartDAL.CheckOut(customer, Cart);
                        if (isCheckoutSuccessful)
                        {
                            // Xóa giỏ hàng sau khi thanh toán thành công
                            HttpContext.Session.Remove(MyConst.CART_KEY);

                            // Chuyển hướng tới trang xác nhận
                            TempData["CheckOutSuccessMessage"] = "Đặt hàng thành công.";
                            return RedirectToAction("OrderSuccess", new { orderName, totalAmount, orderDescription });
                        }
                        TempData["CheckOutErrorMessage"] = "Đặt hàng thất bại.";
                        break;

                    default:
                        TempData["CheckOutErrorMessage"] = "Phương thức thanh toán không hợp lệ.";
                        break;
                }

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["CheckOutErrorMessage"] = $"Lỗi hệ thống: {ex.Message}";
                return RedirectToAction("Index");
            }
        }
        [HttpGet]
        public IActionResult MoMoCallback()
        {
            try
            {
                var requestQuery = HttpContext.Request.Query;

                // Kiểm tra trạng thái giao dịch
                var resultCode = requestQuery["resultCode"];
                var status = resultCode == "0" ? "Success" : "Failed";

                // Tạo đối tượng Payment để lưu thông tin
                var payment = new Payment
                {
                    CustomerId = int.Parse(HttpContext.User.FindFirstValue("CustomerId") ?? "0"),
                    FirstName = "Tên khách hàng", // Lấy từ thông tin người dùng
                    LastName = "",                // Lấy từ thông tin người dùng
                    Phone = "Số điện thoại",      // Lấy từ thông tin người dùng
                    Email = "Email@example.com",  // Lấy từ thông tin người dùng
                    Total = (float)decimal.Parse(requestQuery["amount"]),
                    PaymentMethodId = null,          // Giả định 1 là mã cho MoMo
                    PaymentStatus = status
                };

                // Lưu thông tin thanh toán vào cơ sở dữ liệu
                var cartDal = new CartDAL();
                var paymentId = cartDal.SavePayment(payment);

                if (paymentId > 0)
                {
                    TempData["CheckOutSuccessMessage"] = "Thanh toán thành công!";
                    return RedirectToAction("OrderSuccess");
                }
                else
                {
                    TempData["CheckOutErrorMessage"] = "Thanh toán thất bại. Vui lòng thử lại!";
                    return RedirectToAction("Index");
                }
            }
            catch (Exception ex)
            {
                TempData["CheckOutErrorMessage"] = $"Lỗi hệ thống: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        public IActionResult OrderSuccess()
        {
            // Tạo đối tượng Momoinfo và ánh xạ dữ liệu từ query string
            var momoinfo = new Momoinfo
            {
                Name = HttpContext.Request.Query["name"],
                OrderId = HttpContext.Request.Query["orderId"],
                OrderInfo = HttpContext.Request.Query["orderInfo"],
                Amount = decimal.TryParse(HttpContext.Request.Query["amount"], out var amount) ? amount : 0,
                datetime = DateTime.TryParse(HttpContext.Request.Query["responseTime"], out var dateTime) ? dateTime : DateTime.Now
            };

            // Truyền model vào View
            return View(momoinfo);
        }



        // Refresh Cart View Component
        public IActionResult RefreshCartViewComponent()
        {
            return ViewComponent("Cart");
        }

        // Lấy tổng tiền theo từng Product
        public IActionResult GetTotalProduct(int idProduct)
        {
            var productFind = Cart.Find(item => item.IdProduct == idProduct);

            int totalAmount = 0;
            if (productFind != null)
            {
                totalAmount = productFind.Total;
            }
            return Json(totalAmount);
        }

    }
}