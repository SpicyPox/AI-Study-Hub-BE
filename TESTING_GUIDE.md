# Hướng Dẫn Kiểm Thử (Testing Guide) - Phân hệ Tài khoản & Tài liệu

Tài liệu này cung cấp các bước và luồng nghiệp vụ chi tiết để kiểm thử các chức năng Backend của dự án AI Study Hub, tập trung vào 2 phân hệ chính: **Tài khoản & Phân quyền (Auth)** và **Quản lý & Lưu trữ Tài liệu (Documents & Cloud)**.

---

## 1. Yêu Cầu Chuẩn Bị
*   **Môi trường**: Backend server chạy cục bộ ở port mặc định (thường là `http://localhost:5241` hoặc `https://localhost:7196`).
*   **Công cụ**: Cài đặt **Postman** hoặc **Insomnia** để giả lập các request.
*   **Biến môi trường**: Đảm bảo trong Header của mọi request (trừ Register và Login) đều có đính kèm Bearer Token:
    *   Key: `Authorization`
    *   Value: `Bearer <chuỗi_token_nhận_được_khi_login>`

---

## 2. Luồng Kiểm Thử Phân Hệ Tài Khoản (Auth)

### Bước 2.1: Đăng ký tài khoản (Register)
*   **Endpoint:** `POST /api/auth/register`
*   **Body (JSON):**
    ```json
    {
      "email": "testuser@gmail.com",
      "password": "Password123!",
      "fullName": "Test User"
    }
    ```
*   **Kết quả mong đợi:** Trả về HTTP Status `200 OK`, kèm thông tin user và access token.

### Bước 2.2: Đăng nhập (Login)
*   **Endpoint:** `POST /api/auth/login`
*   **Body (JSON):**
    ```json
    {
      "email": "testuser@gmail.com",
      "password": "Password123!"
    }
    ```
*   **Kết quả mong đợi:** Trả về `accessToken` và `refreshToken`. Copy `accessToken` để dùng cho các bước tiếp theo.

### Bước 2.3: Lấy thông tin tài khoản hiện tại (Auth Me)
*   **Endpoint:** `GET /api/auth/me`
*   **Kết quả mong đợi:** Trả về thông tin chi tiết của user hiện tại tương ứng với token. 

---

## 3. Luồng Kiểm Thử Phân Hệ Tài Liệu (Documents & Cloud)

*Lưu ý: Bắt buộc gắn Bearer Token vào tất cả các API dưới đây.*

### Bước 3.1: Upload tài liệu [R06, R14]
*   **Endpoint:** `POST /api/documents/upload`
*   **Body (form-data):**
    *   Key `file` (type: File): Chọn 1 file PDF, DOCX hoặc TXT để upload.
*   **Kết quả mong đợi:** Hệ thống sẽ tự động upload file lên **Cloudinary**, lưu thông tin vào Database, và trả về một JSON object chứa thông tin file, trong đó quan trọng nhất là `id` (Document ID). *Hãy lưu lại ID này để test các API sau.*

### Bước 3.2: Kiểm tra trạng thái upload [R15]
*   **Endpoint:** `GET /api/documents/{id}/status` *(thay `{id}` bằng Document ID ở bước 3.1)*
*   **Kết quả mong đợi:** Trả về JSON thông báo trạng thái `status: "uploaded"` kèm theo URL thô `cloudUrl` trên Cloudinary.

### Bước 3.3: Lấy Preview File [R16]
*   **Endpoint:** `GET /api/documents/{id}/preview`
*   **Kết quả mong đợi:** Trả về URL dùng để preview (xem trước) trên trình duyệt, cùng với thông tin tên file, loại file và dung lượng.

### Bước 3.4: Xem chi tiết một tài liệu [R08]
*   **Endpoint:** `GET /api/documents/{id}`
*   **Kết quả mong đợi:** Trả về toàn bộ siêu dữ liệu (metadata) của tài liệu như ngày tạo, dung lượng, định dạng, quyền riêng tư, và ID môn học (nếu có).

### Bước 3.5: Chỉnh sửa thông tin tài liệu [R09]
*   **Endpoint:** `PATCH /api/documents/{id}`
*   **Body (JSON):**
    ```json
    {
      "title": "Tên tài liệu mới",
      "description": "Cập nhật mô tả tài liệu",
      "isPublic": true
    }
    ```
*   **Kết quả mong đợi:** Trả về HTTP 200, tài liệu được cập nhật thành công (ví dụ đổi trạng thái từ Private sang Public).

### Bước 3.6: Xem danh sách tài liệu, Tìm kiếm & Lọc [R07, R12, R13]
*   **Endpoint:** `GET /api/documents`
*   **Query Parameters (Tuỳ chọn để test chức năng lọc):**
    *   `q=từ khóa`: Để tìm kiếm theo tên [R12].
    *   `type=pdf`: Để lọc theo loại file.
    *   `subjectId={uuid}`: Để lọc theo môn học [R13].
    *   `scope=public`: Để xem các tài liệu công khai từ cộng đồng. Để trống nếu chỉ muốn xem tài liệu của mình.
*   **Kết quả mong đợi:** Trả về danh sách tài liệu thoả mãn các điều kiện lọc.

### Bước 3.7: Tải xuống tài liệu [R11]
*   **Endpoint:** `GET /api/documents/{id}/download`
*   **Kết quả mong đợi:** Trả về một link Download bảo mật hoặc stream trực tiếp nội dung file để client tải về máy.

### Bước 3.8: Xóa tài liệu [R10]
*   **Endpoint:** `DELETE /api/documents/{id}`
*   **Kết quả mong đợi:** Trả về HTTP `204 No Content`. Trạng thái `isDeleted` của tài liệu trong Database chuyển thành `true` (Soft delete), file sẽ không còn xuất hiện trong danh sách lấy ra ở Bước 3.6.

---
**Kết Thúc Luồng Kiểm Thử Cơ Bản.** Mọi lỗi phát sinh trong quá trình upload (ví dụ: file quá lớn, sai định dạng) sẽ bị chặn ở Bước 3.1 và trả về HTTP `400 Bad Request` kèm theo thông báo lỗi cụ thể.
