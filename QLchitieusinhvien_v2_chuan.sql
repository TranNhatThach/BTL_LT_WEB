-- ===================================================================================
-- HỆ THỐNG QUẢN LÝ CHI TIÊU SINH VIÊN (QLChiTieuSV)
-- BẢN SCRIPT CƠ SỞ DỮ LIỆU CHUẨN HÓA THEO ĐẶC TẢ context.md
-- Tương thích hoàn hảo với ASP.NET Core 8.0 / 10.0 MVC & Entity Framework Core
-- ===================================================================================

-- 1. TẠO CƠ SỞ DỮ LIỆU
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'QUANLYCHITIEUSINHVIEN')
BEGIN
    CREATE DATABASE QUANLYCHITIEUSINHVIEN;
END
GO

USE QUANLYCHITIEUSINHVIEN;
GO

-- 2. XÓA CÁC BẢNG CŨ THEO THỨ TỰ PHỤ THUỘC (NẾU CÓ ĐỂ CHẠY LẠI TÁI LẬP)
IF OBJECT_ID('Notifications', 'U') IS NOT NULL DROP TABLE Notifications;
IF OBJECT_ID('SavingsGoals', 'U') IS NOT NULL DROP TABLE SavingsGoals;
IF OBJECT_ID('Budgets', 'U') IS NOT NULL DROP TABLE Budgets;
IF OBJECT_ID('Transactions', 'U') IS NOT NULL DROP TABLE Transactions;
IF OBJECT_ID('Categories', 'U') IS NOT NULL DROP TABLE Categories;
IF OBJECT_ID('Users', 'U') IS NOT NULL DROP TABLE Users;
GO

-- ===================================================================================
-- 3. TẠO CÁC BẢNG DỮ LIỆU
-- ===================================================================================

-- -----------------------------------------------------------------------------------
-- Bảng 1: Users (Tài khoản người dùng: Quản trị viên & Sinh viên)
-- -----------------------------------------------------------------------------------
CREATE TABLE Users (
    UserId          INT IDENTITY(1,1) NOT NULL,
    FullName        NVARCHAR(150)     NOT NULL,
    Email           NVARCHAR(256)     NOT NULL,
    PasswordHash    NVARCHAR(500)     NOT NULL,
    StudentCode     NVARCHAR(50)      NULL,          -- NULL nếu là Admin
    SchoolName      NVARCHAR(200)     NULL,          -- Trường học
    ClassName       NVARCHAR(100)     NULL,          -- Lớp
    AvatarPath      NVARCHAR(500)     NULL,          -- Đường dẫn ảnh đại diện
    Role            NVARCHAR(20)      NOT NULL CONSTRAINT DF_Users_Role DEFAULT 'Student', -- 'Student' hoặc 'Admin'
    IsActive        BIT               NOT NULL CONSTRAINT DF_Users_IsActive DEFAULT 1,     -- 1: Hoạt động, 0: Khóa
    CreatedAt       DATETIME2         NOT NULL CONSTRAINT DF_Users_CreatedAt DEFAULT SYSDATETIME(),
    UpdatedAt       DATETIME2         NULL,

    CONSTRAINT PK_Users PRIMARY KEY (UserId),
    CONSTRAINT CK_Users_Role CHECK (Role IN ('Student', 'Admin'))
);

-- Chỉ mục duy nhất cho Email và Mã sinh viên (chống trùng lặp tài khoản)
CREATE UNIQUE INDEX UQ_Users_Email ON Users(Email);
CREATE UNIQUE INDEX UQ_Users_StudentCode ON Users(StudentCode) WHERE StudentCode IS NOT NULL;
GO

-- -----------------------------------------------------------------------------------
-- Bảng 2: Categories (Danh mục thu nhập và chi tiêu)
-- -----------------------------------------------------------------------------------
CREATE TABLE Categories (
    CategoryId      INT IDENTITY(1,1) NOT NULL,
    Name            NVARCHAR(100)     NOT NULL,
    Type            NVARCHAR(20)      NOT NULL,      -- 'Income' hoặc 'Expense'
    Icon            NVARCHAR(100)     NULL,          -- Bootstrap Icon class (vd: 'bi-cup-hot', 'bi-wallet2')
    Color           NVARCHAR(20)      NULL,          -- Mã màu Hex để hiển thị biểu đồ và badge (vd: '#fd7e14')
    UserId          INT               NULL,          -- NULL: Danh mục hệ thống mặc định; Có giá trị: Danh mục cá nhân của SV
    IsDefault       BIT               NOT NULL CONSTRAINT DF_Categories_IsDefault DEFAULT 0,
    IsActive        BIT               NOT NULL CONSTRAINT DF_Categories_IsActive DEFAULT 1,  -- 1: Đang dùng, 0: Ẩn/Ngừng dùng
    CreatedAt       DATETIME2         NOT NULL CONSTRAINT DF_Categories_CreatedAt DEFAULT SYSDATETIME(),

    CONSTRAINT PK_Categories PRIMARY KEY (CategoryId),
    CONSTRAINT FK_Categories_Users FOREIGN KEY (UserId) REFERENCES Users(UserId) ON DELETE SET NULL,
    CONSTRAINT CK_Categories_Type CHECK (Type IN ('Income', 'Expense'))
);
GO

-- -----------------------------------------------------------------------------------
-- Bảng 3: Transactions (Giao dịch thu chi)
-- -----------------------------------------------------------------------------------
CREATE TABLE Transactions (
    TransactionId   INT IDENTITY(1,1) NOT NULL,
    UserId          INT               NOT NULL,      -- Sinh viên thực hiện giao dịch
    CategoryId      INT               NOT NULL,      -- Danh mục giao dịch
    Type            NVARCHAR(20)      NOT NULL,      -- 'Income' hoặc 'Expense'
    Amount          DECIMAL(18,2)     NOT NULL,      -- Số tiền giao dịch
    TransactionDate DATE              NOT NULL,      -- Ngày giao dịch (không được vượt quá ngày hiện tại)
    Note            NVARCHAR(MAX)     NULL,          -- Ghi chú (hỗ trợ HTML từ TinyMCE)
    InvoiceImagePath NVARCHAR(500)    NULL,          -- Đường dẫn ảnh hóa đơn (lưu trong wwwroot/uploads/invoices/)
    CreatedAt       DATETIME2         NOT NULL CONSTRAINT DF_Transactions_CreatedAt DEFAULT SYSDATETIME(),
    UpdatedAt       DATETIME2         NULL,

    CONSTRAINT PK_Transactions PRIMARY KEY (TransactionId),
    CONSTRAINT FK_Transactions_Users FOREIGN KEY (UserId) REFERENCES Users(UserId),
    CONSTRAINT FK_Transactions_Categories FOREIGN KEY (CategoryId) REFERENCES Categories(CategoryId),
    CONSTRAINT CK_Transactions_Type CHECK (Type IN ('Income', 'Expense')),
    CONSTRAINT CK_Transactions_Amount CHECK (Amount > 0)
);

-- Chỉ mục tối ưu tốc độ lọc lịch sử, dashboard và phân trang
CREATE INDEX IX_Transactions_UserId_Date ON Transactions(UserId, TransactionDate DESC);
CREATE INDEX IX_Transactions_CategoryId ON Transactions(CategoryId);
GO

-- -----------------------------------------------------------------------------------
-- Bảng 4: Budgets (Ngân sách chi tiêu theo danh mục từng tháng)
-- -----------------------------------------------------------------------------------
CREATE TABLE Budgets (
    BudgetId        INT IDENTITY(1,1) NOT NULL,
    UserId          INT               NOT NULL,      -- Sinh viên sở hữu ngân sách
    CategoryId      INT               NOT NULL,      -- Danh mục chi tiêu (chỉ áp dụng cho Expense)
    Month           INT               NOT NULL,      -- Tháng (1 - 12)
    Year            INT               NOT NULL,      -- Năm (vd: 2026)
    LimitAmount     DECIMAL(18,2)     NOT NULL,      -- Hạn mức ngân sách
    CreatedAt       DATETIME2         NOT NULL CONSTRAINT DF_Budgets_CreatedAt DEFAULT SYSDATETIME(),
    UpdatedAt       DATETIME2         NULL,

    CONSTRAINT PK_Budgets PRIMARY KEY (BudgetId),
    CONSTRAINT FK_Budgets_Users FOREIGN KEY (UserId) REFERENCES Users(UserId),
    CONSTRAINT FK_Budgets_Categories FOREIGN KEY (CategoryId) REFERENCES Categories(CategoryId),
    CONSTRAINT CK_Budgets_Month CHECK (Month BETWEEN 1 AND 12),
    CONSTRAINT CK_Budgets_LimitAmount CHECK (LimitAmount > 0),
    -- Đảm bảo mỗi sinh viên chỉ có duy nhất 1 ngân sách cho cùng 1 danh mục trong cùng 1 tháng
    CONSTRAINT UQ_Budgets_User_Category_Month_Year UNIQUE (UserId, CategoryId, Month, Year)
);
GO

-- -----------------------------------------------------------------------------------
-- Bảng 5: SavingsGoals (Mục tiêu tiết kiệm tài chính)
-- -----------------------------------------------------------------------------------
CREATE TABLE SavingsGoals (
    GoalId          INT IDENTITY(1,1) NOT NULL,
    UserId          INT               NOT NULL,      -- Sinh viên sở hữu mục tiêu
    Name            NVARCHAR(200)     NOT NULL,      -- Tên mục tiêu (vd: 'Mua laptop mới', 'Học phí kỳ tới')
    TargetAmount    DECIMAL(18,2)     NOT NULL,      -- Số tiền mục tiêu
    CurrentAmount   DECIMAL(18,2)     NOT NULL CONSTRAINT DF_SavingsGoals_CurrentAmount DEFAULT 0, -- Số tiền hiện có
    Deadline        DATE              NOT NULL,      -- Ngày đến hạn hoàn thành
    CreatedAt       DATETIME2         NOT NULL CONSTRAINT DF_SavingsGoals_CreatedAt DEFAULT SYSDATETIME(),
    UpdatedAt       DATETIME2         NULL,

    CONSTRAINT PK_SavingsGoals PRIMARY KEY (GoalId),
    CONSTRAINT FK_SavingsGoals_Users FOREIGN KEY (UserId) REFERENCES Users(UserId),
    CONSTRAINT CK_SavingsGoals_TargetAmount CHECK (TargetAmount > 0),
    CONSTRAINT CK_SavingsGoals_CurrentAmount CHECK (CurrentAmount >= 0)
);
GO

-- -----------------------------------------------------------------------------------
-- Bảng 6: Notifications (Thông báo & Cảnh báo ngân sách tự động)
-- -----------------------------------------------------------------------------------
CREATE TABLE Notifications (
    NotificationId  INT IDENTITY(1,1) NOT NULL,
    UserId          INT               NOT NULL,      -- Sinh viên nhận thông báo
    BudgetId        INT               NOT NULL,      -- Ngân sách kích hoạt cảnh báo
    AlertLevel      NVARCHAR(20)      NOT NULL,      -- 'Warning' (80%-99%) hoặc 'Exceeded' (>= 100%)
    Title           NVARCHAR(200)     NOT NULL,      -- Tiêu đề
    Message         NVARCHAR(1000)    NOT NULL,      -- Nội dung cảnh báo chi tiết
    IsRead          BIT               NOT NULL CONSTRAINT DF_Notifications_IsRead DEFAULT 0, -- 0: Chưa đọc, 1: Đã đọc
    CreatedAt       DATETIME2         NOT NULL CONSTRAINT DF_Notifications_CreatedAt DEFAULT SYSDATETIME(),

    CONSTRAINT PK_Notifications PRIMARY KEY (NotificationId),
    CONSTRAINT FK_Notifications_Users FOREIGN KEY (UserId) REFERENCES Users(UserId),
    CONSTRAINT FK_Notifications_Budgets FOREIGN KEY (BudgetId) REFERENCES Budgets(BudgetId) ON DELETE CASCADE,
    CONSTRAINT CK_Notifications_AlertLevel CHECK (AlertLevel IN ('Warning', 'Exceeded')),
    -- Tránh sinh nhiều thông báo trùng lặp cho cùng 1 mức cảnh báo của 1 ngân sách
    CONSTRAINT UQ_Notifications_Budget_AlertLevel UNIQUE (BudgetId, AlertLevel)
);
GO


-- ===================================================================================
-- 4. DỮ LIỆU KHỞI TẠO (SEED DATA DEMO)
-- ===================================================================================

-- 4.1. Khởi tạo Tài khoản:
-- Mật khẩu mặc định:
--   Admin:   'Admin@123'   -> Hash BCrypt bên dưới
--   Student: 'Student@123' -> Hash BCrypt bên dưới
INSERT INTO Users (FullName, Email, PasswordHash, StudentCode, SchoolName, ClassName, Role, IsActive, CreatedAt)
VALUES 
(N'Quản trị viên Hệ thống', 'admin@utc.edu.vn', '$2a$11$mR/Vwa8oN7KYKTigTon5wOMNYkGnG5qERsn8LstND9ypF14.rpVo6', NULL, N'Đại học Giao thông Vận tải', NULL, 'Admin', 1, SYSDATETIME()),
(N'Trần Nhật Thạch', 'student@utc.edu.vn', '$2a$11$.MuQ6KnBOmWV.6CT5jj8wORlALZKClrQcM3D97zHVdYCqi7d8bbMG', '211200001', N'Đại học Giao thông Vận tải', N'CNTT K62', 'Student', 1, SYSDATETIME());
GO

-- 4.2. Khởi tạo Danh mục mặc định hệ thống (IsDefault = 1, UserId = NULL)
-- Đầy đủ 9 danh mục theo đặc tả bảng mục 4.1 của context.md
INSERT INTO Categories (Name, Type, Icon, Color, UserId, IsDefault, IsActive, CreatedAt)
VALUES 
-- Danh mục Thu nhập (Income)
(N'Tiền lương, làm thêm', 'Income', 'bi-cash-stack', '#198754', NULL, 1, 1, SYSDATETIME()),
(N'Chu cấp từ gia đình', 'Income', 'bi-house-heart', '#0d6efd', NULL, 1, 1, SYSDATETIME()),
(N'Học bổng', 'Income', 'bi-mortarboard-fill', '#ffc107', NULL, 1, 1, SYSDATETIME()),
(N'Thu nhập khác', 'Income', 'bi-wallet2', '#20c997', NULL, 1, 1, SYSDATETIME()),

-- Danh mục Chi tiêu (Expense)
(N'Ăn uống', 'Expense', 'bi-cup-hot-fill', '#fd7e14', NULL, 1, 1, SYSDATETIME()),
(N'Nhà trọ', 'Expense', 'bi-building', '#6610f2', NULL, 1, 1, SYSDATETIME()),
(N'Đi lại', 'Expense', 'bi-bus-front-fill', '#0dcaf0', NULL, 1, 1, SYSDATETIME()),
(N'Học tập', 'Expense', 'bi-book-half', '#0d6efd', NULL, 1, 1, SYSDATETIME()),
(N'Mua sắm', 'Expense', 'bi-bag-heart-fill', '#d63384', NULL, 1, 1, SYSDATETIME()),
(N'Giải trí', 'Expense', 'bi-controller', '#6f42c1', NULL, 1, 1, SYSDATETIME()),
(N'Chi phí khác', 'Expense', 'bi-tags-fill', '#6c757d', NULL, 1, 1, SYSDATETIME());
GO

-- 4.3. Khởi tạo Ngân sách mẫu cho sinh viên (UserId = 2) trong tháng hiện tại (Tháng 10/2026)
DECLARE @StudentId INT = (SELECT UserId FROM Users WHERE Email = 'student@utc.edu.vn');
DECLARE @CatAnUong INT = (SELECT CategoryId FROM Categories WHERE Name = N'Ăn uống');
DECLARE @CatNhaTro INT = (SELECT CategoryId FROM Categories WHERE Name = N'Nhà trọ');
DECLARE @CatDiLai  INT = (SELECT CategoryId FROM Categories WHERE Name = N'Đi lại');

INSERT INTO Budgets (UserId, CategoryId, Month, Year, LimitAmount, CreatedAt)
VALUES 
(@StudentId, @CatAnUong, 10, 2026, 2000000.00, SYSDATETIME()), -- Ngân sách ăn uống: 2.000.000 VNĐ
(@StudentId, @CatNhaTro, 10, 2026, 1500000.00, SYSDATETIME()), -- Ngân sách nhà trọ: 1.500.000 VNĐ
(@StudentId, @CatDiLai,  10, 2026,  300000.00, SYSDATETIME()); -- Ngân sách đi lại:   300.000 VNĐ
GO

-- 4.4. Khởi tạo Giao dịch mẫu cho sinh viên (UserId = 2)
DECLARE @StudentId INT = (SELECT UserId FROM Users WHERE Email = 'student@utc.edu.vn');
DECLARE @CatLuong  INT = (SELECT CategoryId FROM Categories WHERE Name = N'Tiền lương, làm thêm');
DECLARE @CatChuCap INT = (SELECT CategoryId FROM Categories WHERE Name = N'Chu cấp từ gia đình');
DECLARE @CatAnUong INT = (SELECT CategoryId FROM Categories WHERE Name = N'Ăn uống');
DECLARE @CatNhaTro INT = (SELECT CategoryId FROM Categories WHERE Name = N'Nhà trọ');
DECLARE @CatDiLai  INT = (SELECT CategoryId FROM Categories WHERE Name = N'Đi lại');
DECLARE @CatHocTap INT = (SELECT CategoryId FROM Categories WHERE Name = N'Học tập');

INSERT INTO Transactions (UserId, CategoryId, Type, Amount, TransactionDate, Note, InvoiceImagePath, CreatedAt)
VALUES 
-- Thu nhập tháng 10
(@StudentId, @CatChuCap, 'Income',  4000000.00, '2026-10-01', N'Gia đình gửi sinh hoạt phí đầu tháng', NULL, SYSDATETIME()),
(@StudentId, @CatLuong,  'Income',  1500000.00, '2026-10-05', N'Lương trợ giảng / làm thêm bán thời gian', NULL, SYSDATETIME()),

-- Chi tiêu tháng 10
(@StudentId, @CatNhaTro, 'Expense', 1500000.00, '2026-10-02', N'Đóng tiền thuê phòng trọ tháng 10', NULL, SYSDATETIME()),
(@StudentId, @CatAnUong, 'Expense',  850000.00, '2026-10-04', N'Đi siêu thị mua thức ăn cả tuần', NULL, SYSDATETIME()),
(@StudentId, @CatAnUong, 'Expense',  850000.00, '2026-10-08', N'Ăn uống và liên hoan nhóm (Đạt 85% hạn mức ăn uống)', NULL, SYSDATETIME()),
(@StudentId, @CatDiLai,  'Expense',  120000.00, '2026-10-06', N'Đổ xăng xe máy và vé xe bus tháng', NULL, SYSDATETIME()),
(@StudentId, @CatHocTap, 'Expense',  250000.00, '2026-10-07', N'Mua giáo trình môn Lập trình Web', NULL, SYSDATETIME());
GO

-- 4.5. Khởi tạo Thông báo mẫu tương ứng (Cảnh báo 85% ngân sách Ăn uống)
DECLARE @StudentId INT = (SELECT UserId FROM Users WHERE Email = 'student@utc.edu.vn');
DECLARE @CatAnUong INT = (SELECT CategoryId FROM Categories WHERE Name = N'Ăn uống');
DECLARE @BudgetId  INT = (SELECT BudgetId FROM Budgets WHERE UserId = @StudentId AND CategoryId = @CatAnUong AND Month = 10 AND Year = 2026);

INSERT INTO Notifications (UserId, BudgetId, AlertLevel, Title, Message, IsRead, CreatedAt)
VALUES 
(@StudentId, @BudgetId, 'Warning', 
 N'Cảnh báo ngân sách Ăn uống', 
 N'Cảnh báo: Bạn đã sử dụng 85.0% ngân sách Ăn uống tháng này (1.700.000 / 2.000.000 VNĐ). Số tiền còn lại là 300.000 VNĐ.', 
 0, SYSDATETIME());
GO

-- 4.6. Khởi tạo Mục tiêu tiết kiệm mẫu
DECLARE @StudentId INT = (SELECT UserId FROM Users WHERE Email = 'student@utc.edu.vn');

INSERT INTO SavingsGoals (UserId, Name, TargetAmount, CurrentAmount, Deadline, CreatedAt)
VALUES 
(@StudentId, N'Mua Laptop đồ họa mới', 15000000.00, 4500000.00, '2026-12-31', SYSDATETIME()),
(@StudentId, N'Quỹ dự phòng khẩn cấp',  3000000.00, 1500000.00, '2026-11-30', SYSDATETIME());
GO

-- ===================================================================================
-- 5. TRUY VẤN KIỂM TRA DỮ LIỆU SAU KHI TẠO
-- ===================================================================================
PRINT N'=== KIỂM TRA THÔNG TIN HỆ THỐNG ===';
SELECT 'Users' AS TableName, COUNT(*) AS TotalRows FROM Users
UNION ALL
SELECT 'Categories', COUNT(*) FROM Categories
UNION ALL
SELECT 'Budgets', COUNT(*) FROM Budgets
UNION ALL
SELECT 'Transactions', COUNT(*) FROM Transactions
UNION ALL
SELECT 'Notifications', COUNT(*) FROM Notifications
UNION ALL
SELECT 'SavingsGoals', COUNT(*) FROM SavingsGoals;
GO
