# AI Study Hub - Backend Services

[![Spring Boot](https://img.shields.io/badge/Spring%20Boot-3.2.5-brightgreen.svg)](https://spring.io/projects/spring-boot)
[![Java](https://img.shields.io/badge/Java-21-orange.svg)](https://www.oracle.com/java/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-16-blue.svg)](https://www.postgresql.org/)
[![Flyway](https://img.shields.io/badge/Flyway-Database%20Migration-red.svg)](https://flywaydb.org/)
[![Swagger](https://img.shields.io/badge/Swagger-OpenAPI-85EA2D.svg)](https://swagger.io/)

Tài liệu hướng dẫn cài đặt và kiểm thử (Testing) cho Backend của dự án **AI Study Hub**.

---

## 📋 Yêu cầu hệ thống (Prerequisites)

Để chạy dự án này trên máy local, bạn cần cài đặt các công cụ sau:

1.  **Java Development Kit (JDK):** Phiên bản **21** trở lên. (Khuyên dùng OpenJDK hoặc Oracle JDK).
2.  **Maven:** Công cụ quản lý thư viện và build dự án (Thường đã được tích hợp sẵn trong IntelliJ IDEA / Eclipse).
3.  **PostgreSQL:** Hệ quản trị cơ sở dữ liệu (Khuyên dùng bản 14 hoặc 16).
4.  **IDE:** IntelliJ IDEA (khuyên dùng) hoặc Eclipse, VS Code.
5.  **Git:** Dùng để quản lý source code.

---

## 🚀 Hướng dẫn cài đặt (Setup Instructions)

### Bước 1: Clone Repository
Mở Terminal hoặc Git Bash và chạy lệnh:
```bash
git clone https://github.com/SpicyPox/AI-Study-Hub-BE.git
cd AI-Study-Hub-BE
```

### Bước 2: Cài đặt Database (PostgreSQL)
1. Mở công cụ quản lý PostgreSQL (như pgAdmin, DBeaver, hoặc psql).
2. Tạo một database mới có tên là `aistudyhub_db`.
    ```sql
    CREATE DATABASE aistudyhub_db;
    ```
3. Mở file cấu hình của dự án tại: `src/main/resources/application.yaml` (hoặc `application.properties`).
4. Cập nhật thông tin kết nối database (username/password) cho phù hợp với máy local của bạn:
    ```yaml
    spring:
      datasource:
        url: jdbc:postgresql://localhost:5432/aistudyhub_db
        username: postgres   # Thay bằng username của bạn
        password: password123 # Thay bằng mật khẩu của bạn
    ```
*(Lưu ý: Flyway đã được cấu hình tự động. Khi ứng dụng chạy lần đầu tiên, nó sẽ tự động chạy các script trong thư mục `src/main/resources/db/migration` để tạo toàn bộ bảng, schema và extension `citext` cho bạn).*

### Bước 3: Build và Chạy ứng dụng
Nếu bạn dùng **IntelliJ IDEA**:
1. Open Project -> Chọn thư mục `AI-Study-Hub-BE`.
2. Đợi Maven tải hết các dependencies.
3. Chạy file `AiStudyHubBeApplication.java` (Nút Run màu xanh).

Nếu bạn dùng **Terminal (Command Line)**:
```bash
mvn clean install
mvn spring-boot:run
```

**Thành công:** Nếu Terminal in ra dòng `Started AiStudyHubBeApplication in x.xxx seconds`, ứng dụng của bạn đã chạy thành công trên cổng `8080`.

---

## 🧪 Hướng dẫn Kiểm thử API (API Testing) với Swagger

Dự án đã tích hợp sẵn **Swagger UI**, giúp bạn có giao diện trực quan để gọi API mà không cần cài thêm Postman.

### 1. Truy cập Swagger UI
Mở trình duyệt và truy cập vào đường dẫn:
👉 **[http://localhost:8080/swagger-ui/index.html](http://localhost:8080/swagger-ui/index.html)**

*(Lưu ý: Cổng 8080 là mặc định, nếu bạn đổi cổng trong cấu hình thì đổi link tương ứng).*

### 2. Luồng Test cơ bản (Authentication Flow)

Bởi vì hầu hết các API đều được bảo mật bằng JWT (JSON Web Token), bạn cần thực hiện theo các bước sau:

#### A. Đăng ký tài khoản mới (`/api/v1/auth/register`)
1. Tìm thẻ **`auth-controller`** trên Swagger UI.
2. Bấm vào mũi tên xổ xuống ở API `POST /api/v1/auth/register`.
3. Bấm nút **"Try it out"** (Góc trên bên phải của API đó).
4. Nhập dữ liệu JSON vào ô Request body (Ví dụ):
   ```json
   {
     "username": "testuser",
     "email": "test@example.com",
     "password": "Password123!"
   }
   ```
5. Bấm nút **"Execute"**. Nếu trả về HTTP Status `200` hoặc `201`, bạn đã đăng ký thành công.

#### B. Đăng nhập để lấy Token (`/api/v1/auth/authenticate` hoặc `/login`)
1. Vẫn ở thẻ **`auth-controller`**, tìm API đăng nhập (Ví dụ: `POST /api/v1/auth/authenticate`).
2. Bấm **"Try it out"**.
3. Nhập thông tin vừa đăng ký:
   ```json
   {
     "username": "testuser",
     "password": "Password123!"
   }
   ```
4. Bấm **"Execute"**.
5. Trong phần **Response body**, bạn sẽ nhận được một JSON chứa `accessToken`. **Hãy copy đoạn mã token này** (Không bao gồm dấu ngoặc kép).

#### C. Uỷ quyền (Authorize) cho Swagger
1. Cuộn lên đầu trang Swagger UI, bạn sẽ thấy một nút có biểu tượng ổ khóa màu xanh lá cây tên là **"Authorize"**.
2. Bấm vào đó.
3. Trong ô Value, nhập chữ `Bearer ` (có một khoảng trắng phía sau chữ Bearer) rồi dán token vừa copy vào.
   * *Ví dụ: `Bearer eyJhbGciOiJIUzI1NiJ9...`*
4. Bấm nút **"Authorize"**, sau đó bấm "Close".
5. 🎉 Từ bây giờ, tất cả các API bạn gọi trên trang này đều đã có Token đính kèm.

#### D. Kiểm thử các API cần xác thực (Ví dụ: Lấy thông tin cá nhân)
1. Tìm thẻ **`user-controller`**.
2. Mở API `GET /api/v1/users/me` (Lấy thông tin người dùng hiện tại).
3. Bấm **"Try it out"** -> **"Execute"**.
4. Bạn sẽ thấy thông tin cá nhân của tài khoản đang đăng nhập hiện ra ở Response với Status `200`. (Nếu bạn chưa Authorize ở bước C, API sẽ báo lỗi `401 Unauthorized`).
5. Thử tiếp API `PUT /api/v1/users/profile` để đổi email.

---

## 🛠 Cấu trúc dự án (Architecture)

Dự án sử dụng kiến trúc N-Layer (Layered Architecture):
*   **Controller Layer (`/controller`):** Xử lý HTTP Request/Response, Validate input đầu vào.
*   **Service Layer (`/service`):** Chứa toàn bộ Business Logic (Nghiệp vụ cốt lõi).
*   **Repository Layer (`/repository`):** Giao tiếp với Database, thực hiện các thao tác CRUD thông qua Spring Data JPA.
*   **Entity (`/entity`):** Cấu trúc bảng trong Database (Java Classes).
*   **DTO (`/dto`):** Data Transfer Objects - Đối tượng dùng để truyền tải dữ liệu giữa Client và Server (Che giấu Entity).
*   **Security (`/config/security`):** Cấu hình phân quyền, JWT Filter.

---
*Bản quyền thuộc về Dự án AI Study Hub.*
