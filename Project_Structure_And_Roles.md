# Cấu trúc Dự án TechStore và Vai trò của từng Folder

Dự án TechStore của bạn được tổ chức theo kiến trúc **Clean Architecture** (hoặc N-Tier tiên tiến) kết hợp với **ASP.NET Core**. Kiến trúc này giúp tách biệt rõ ràng các mối quan tâm (Separation of Concerns), làm cho code dễ bảo trì, dễ test và dễ mở rộng.

Dưới đây là sơ đồ cấu trúc cấp cao trong thư mục `src` và vai trò cụ thể của từng project/folder:

## 1. Mức Project (Các tầng Kiến trúc)

### 🏢 `TechStore.Domain` (Tầng Cốt lõi / Nguyên thuỷ)
Đây là trái tim của hệ thống. Tầng này không phụ thuộc vào bất kỳ tầng nào khác (không Entity Framework, không thư viện Web).
*   **Vai trò:** Định nghĩa các quy tắc kinh doanh cốt lõi và các thực thể dữ liệu.
*   **Các folder chính bên trong:**
    *   `Entities/`: Các lớp đại diện cho bảng trong Database (Product, Order, Category, AppUser, v.v.).
    *   `Enums/`: Các hằng số liệt kê (Trạng thái đơn hàng, Phương thức thanh toán...).
    *   `Exceptions/`: Các lỗi (Error) đặc thù của quy trình nghiệp vụ.
    *   `Common/`: Các lớp dùng chung cho domain, ví dụ `BaseEntity` (chứa Id, CreateDate...).

### ⚙️ `TechStore.Application` (Tầng Ứng dụng / Nghiệp vụ)
Tầng này phụ thuộc vào `TechStore.Domain`. Nó định nghĩa "Ứng dụng này có thể làm những gì" (Use-cases).
*   **Vai trò:** Chứa logic nghiệp vụ xử lý dữ liệu (Business Logic), nhưng chưa can thiệp tới Database thực sự.
*   **Các folder chính bên trong:**
    *   `Interfaces/`: Khai báo các giao diện (Interfaces) cho Repositories (IProductRepository...) hoặc các Services (IEmailService). Việc triển khai (Implementation) sẽ nằm ở tầng Infrastructure.
    *   `DTOs/` hoặc `ViewModels/`: Đối tượng truyền dữ liệu (Data Transfer Objects), giúp bảo mật/không phơi bày Entity thực tế ra bên ngoài.
    *   `Services/` hoặc `CQRS/` (Commands/Queries/Handlers): Các logic tạo đơn hàng, tính toán giá, v.v.

### 🗄️ `TechStore.Infrastructure` (Tầng Cơ sở hạ tầng)
Tầng này phụ thuộc vào `Application` và `Domain`.
*   **Vai trò:** Xử lý việc giao tiếp với thế giới bên ngoài như Cơ sở dữ liệu, File System, Email, SMS, Payment Gateway.
*   **Các folder chính bên trong:**
    *   `Data/` hoặc `Persistence/`: Chứa `ApplicationDbContext` (cấu hình Entity Framework Core gọi tới SQL Server).
    *   `Repositories/`: Triển khai phần đọc/ghi Database cho các Interface đã định nghĩa ở tầng Application (ví dụ viết các câu LINQ).
    *   `Migrations/`: Lịch sử các file tự động sinh ra khi thay đổi cấu trúc bảng SQL.
    *   `Services/`: Triển khai các dịch vụ ngoại vi thực tế. Ví dụ: `SmtpEmailService` (Gửi mail qua SMTP), Dịch vụ upload ảnh, Token JWT.

### 🌐 `TechStore.API` (Tầng Giao diện Lập trình / Presentation)
Tầng này phụ thuộc vào tất cả lớp trên để hoạt động, thường là backend thuần cho Mobile App hoặc các Frontend (như React/Vue hoặc cho Web MVC gọi tới).
*   **Vai trò:** Cung cấp các RESTful API Enpoints cho Client bên ngoài. 
*   **Các folder chính bên trong:**
    *   `Controllers/`: Nhận HTTP Request (GET, POST, PUT, DELETE), gọi logic từ tầng `Application` và trả về JSON (`ProductsController`, `OrdersController`...).
    *   `Extensions/`: Cấu hình cho Swagger (Tài liệu API), JWT Authentication, CORS.

### 🖥️ `TechStore.Web` (Tầng Giao diện Người dùng / Frontend MVC)
Đây là một Project MVC (Model-View-Controller) hoạt động như Client.
*   **Vai trò:** Hiển thị giao diện người dùng (UI) bằng HTML/CSS/JS (Razor Pages), thường gọi API từ `TechStore.API` hoặc có thể tương tác trực tiếp qua Service layer.
*   **Các folder chính bên trong:**
    *   `Areas/`: Chia web ra thành các phần lớn độc lập.
        *   `Admin/`: Khu vực Quản trị viên (Dashboard, Quản lý Sản phẩm, Danh mục...).
        *   `User/`: Khu vực Của khách hàng (Hồ sơ, Lịch sử đặt hàng...).
    *   `Controllers/`: Nhận request của trình duyệt và render ra các View tương ứng (`HomeController`, `ProductController`).
    *   `Views/`: Cấu trúc file `.cshtml` chứa HTML/CSS.
    *   `Services/`: Có thể chứa API Clients dùng `HttpClient` để gọi sang `TechStore.API`.
    *   `wwwroot/`: Nơi lưu trữ các file tĩnh (CSS, JS, Images, Fonts) public cho trình duyệt tải.

### 🔗 `TechStore.Shared` (Tầng Dùng chung/Tiện ích)
*   **Vai trò:** Thư viện chứa các Class nhỏ gọn, Enum, hằng số (Constants), hoặc các DTO dùng chung cho cả `TechStore.API`, `TechStore.Web` và `TechStore.Application`. Tránh việc phải lặp lại code ở nhiều project.

---

## Tóm tắt quy trình hoạt động:
1.  Người dùng click trên Web (`TechStore.Web` -> `Views` -> `Controllers`).
2.  `TechStore.Web` gửi yêu cầu (HTTP call) tới `TechStore.API` (`Controllers`).
3.  `TechStore.API` tiếp nhận, xác thực và gọi một Service ở tầng `TechStore.Application`.
4.  Dịch vụ trong `TechStore.Application` áp dụng các quy tắc kinh doanh dựa trên `TechStore.Domain` Entities.
5.  Dịch vụ yêu cầu `TechStore.Infrastructure` (`Repositories`) lấy/lưu dữ liệu từ/xuống Database.
6.  Kết quả đi ngược trở lại qua các tầng và hiển thị lên màn hình người dùng.
