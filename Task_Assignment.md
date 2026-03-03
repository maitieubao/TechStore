# Danh Sách Các Task Bổ Sung/Hoàn Thiện Cho Dự Án TechStore
Theo yêu cầu Assignment (sau khi đã nới lỏng đề tài), dưới đây là danh sách phân loại các tính năng còn thiếu mà bạn cần bổ sung để hoàn thành 100% mục tiêu môn học.

## 🔥 Nhóm 1: Yêu Cầu Về Thông Tin Người Dùng (Guest & Customer)
Đây là các yêu cầu liên quan trực tiếp đến bảng `AppUser` và Account Controller.

- [x] **Task 1.1: Bổ sung đủ 6 trường thông tin khi đăng ký.**
  - **Mô tả:** Hiện tại DTO `RegisterDto` chỉ có (FullName, UserName, Email, Password - 4 trường).
  - **Hành động:** 
    - Bổ sung thêm `PhoneNumber` (Số điện thoại) và `Address` (Địa chỉ) hoặc `DateOfBirth` vào `RegisterDto.cs`.
    - Cập nhật View `Register.cshtml` bên giao diện `TechStore.Web` để hiển thị input.
    - Sửa API/Service đăng ký để map các trường này vào `AppUser`.

- [x] **Task 1.2: Chức năng Cập nhật thông tin cá nhân.**
  - **Mô tả:** Khách hàng cần một trang "Hồ sơ cá nhân" để sửa thông tin.
  - **Hành động:** 
    - Tạo `ProfileController` bên Web MVC.
    - Bổ sung lệnh update thông tin user trong API và Identity Service.
    - Thiết kế form `EditProfile.cshtml` (Tên, SDT, Địa chỉ...).
    - Thêm chức năng cho phép Admin cũng tự cập nhật hồ sơ cá nhân giống Customer.

- [ ] **Task 1.3: Đăng nhập hệ thống qua Google.**
  - **Mô tả:** Yêu cầu đăng nhập có thêm tuỳ chọn qua Google.
  - **Hành động:** 
    - Đăng ký Project ở Google Console để lấy `ClientId` và `ClientSecret`.
    - Tích hợp `Microsoft.AspNetCore.Authentication.Google` vào Frontend Web MVC hoăc làm phương thức lấy Token từ Frontend đập vô API.

---

## 💻 Nhóm 2: Quản Lý Của Admin (Amin Panel)
Dự án của bạn đã có dashboard, quản lý sản phẩm, đơn hàng tốt. Nhưng thiếu đối tượng quản lý.

- [x] **Task 2.1: Quản lý các tài khoản người dùng (CRUD).**
  - **Mô tả:** Admin phải xem list danh sách user, block/delete tài khoản.
  - **Hành động:**
    - Tạo `UsersController` trong `TechStore.API` (GET/POST/PUT/DELETE Users).
    - Phải có điều kiện chặn: "Không cho phép tự xoá tài khoản Admin đang login".
    - Code `UserManagerController` ở `Areas/Admin` bên `TechStore.Web`.
    - Làm giao diện hiển thị List Users trên bảng.

- [ ] **Task 2.2: Quản lý Combo Sản phẩm.**
  - **Mô tả:** Yêu cầu (guest có thể xem thực đơn theo combo/cửa hàng thức ăn, Admin có CRUD combo). Mặc dù là đồ Tech, bạn vẫn phải có "Deal Combo".
  - **Hành động:**
    - Bạn có thể làm 2 hướng: 
      + **Hướng 1 (Nhanh):** Thêm thuộc tính `IsCombo` (bool) vào bảng `Product`, ở Admin quản lý có checkbox "Là Combo". Giao diện lọc chia ra Product thường và Combo riêng.
      + **Hướng 2 (Khó):** Tạo bảng `Combo` riêng, liên kết n-n với `Product` (1 Combo chứa Nhiều Product).
    - Tạo API để CRUD Combo.
    - Code MVC ở Admin để làm các chức năng này.

---

## 🌐 Nhóm 3: Các Tính Năng Web Dành Cho Khách
Bạn đã làm rất tốt phần lọc phân liệt sản phẩm của Guest. Cần một số tu chỉnh sau (nếu chưa có).

- [ ] **Task 3.1: Theo dõi tình trạng các hóa đơn mới mua.**
  - **Mô tả:** Khách cần có 1 giao diện kiểm tra xem đơn hàng mình vừa lưu thành công, và Admin đã duyệt thành "Đang Giao" chưa.
  - **Hành động:** 
    - Kiểm tra và đảm bảo có `OrderHistoryController` hoặc View hiển thị các `Orders` của User đang login và trạng thái cụ thể của chúng.

---

## ⚙️ Nhóm 4: Hosting & Triển khai
- [ ] **Task 4.1: Deploy web app lên Hosting**
  - **Mô tả:** Ứng dụng phải được chạy public (có thể là bài lab cuối).
  - **Hành động:** 
    - Build Release `TechStore.API` publish lên máy chủ (Docker/VPS/SmarterASP/Vercel).
    - Build Release `TechStore.Web` trỏ Swagger kết nối API.
    - Migrate Data DB lên SQL Server online.
