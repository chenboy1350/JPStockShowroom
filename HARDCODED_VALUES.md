# Hardcoded Values Report — JPStockShowRoom

> สรุปจุดที่มีค่า hardcode ทั้งหมดในโปรเจค จัดกลุ่มตามความเสี่ยง

---

## 🔴 CRITICAL — ข้อมูลสำคัญ / ความปลอดภัย

### Connection Strings & Passwords

| ไฟล์ | Key | ค่า |
|------|-----|-----|
| `appsettings.Development.json:3` | JPDBEntries | `Data Source=SERVER; User ID=admin; Password=jp` |
| `appsettings.Development.json:5` | SPDBEntries | `Data Source=localhost; User ID=sa; Password=Bestszaza369` |
| `appsettings.Development.json:6` | SWDBEntries | `Data Source=localhost; User ID=sa; Password=Bestszaza369` |
| `appsettings.Production.json:3` | JPDBEntries | `Data Source=SERVER; User ID=admin; Password=jp` |
| `appsettings.Production.json:4` | SPDBEntries | `Data Source=192.168.2.237; User ID=sa; Password=P@55W0rd` |
| `appsettings.Production.json:5` | SWDBEntries | `Data Source=192.168.2.23; User ID=sa; Password=P@55W0rd` |

### API Keys

| ไฟล์ | Key | ค่า |
|------|-----|-----|
| `appsettings.Development.json:14` | APIKey | `SmV3ZWx5UHJpbmNlc3NBUElLZXk` (Base64) |
| `appsettings.Production.json:12` | APIKey | `SmV3ZWx5UHJpbmNlc3NBUklLZXk` (Base64) |

---

## 🟠 HIGH — IP / Endpoint

| ไฟล์ | บรรทัด | ค่า | หมายเหตุ |
|------|--------|-----|---------|
| `appsettings.Development.json:16-30` | 16–30 | `https://192.168.2.13/SUT_JPWEBAPI/api/...` | PIS API (Dev) |
| `appsettings.Production.json:14-28` | 14–28 | `https://192.168.2.237/...` / `https://192.168.2.23/...` | PIS API (Prod) |

---

## 🟡 MEDIUM — User ID / Permission / Business Logic

### Hardcoded User IDs

| ไฟล์ | บรรทัด | ค่า | ใช้ทำอะไร |
|------|--------|-----|----------|
| `Views/Partial/_StockManagement.cshtml:124` | 124 | `== 1 \|\| == 28` | แสดงปุ่ม "เพิ่มสินค้า" |
| `Views/Partial/_StockManagement.cshtml:127` | 127 | `== 1 \|\| == 28` | แสดงปุ่ม "Import Excel" |
| `Views/Home/Index.cshtml:324` | 324 | `== 1 \|\| == 28` | `_canDeleteAdminStock` |

### Hardcoded Permission ID

| ไฟล์ | บรรทัด | ค่า | หมายเหตุ |
|------|--------|-----|---------|
| `Services/Implement/PISService.cs:73` | 73 | `PermissionId == 3` | เช็ค approver role |

### Hardcoded String ใน Business Logic

| ไฟล์ | บรรทัด | ค่า | หมายเหตุ |
|------|--------|-----|---------|
| `Services/Helper/StockGroupKeyHelper.cs:29-31` | 29–31 | `"ADMIN"` | ReceiveNo ของ stock ที่ admin เพิ่ม |
| `Services/Implement/AdminStockService.cs:65` | 65 | `"ADMIN"` | ReceiveNo ตอน insert |
| `Services/Implement/AdminStockService.cs:66` | 66 | `""` | CustCode ว่างเปล่า |
| `Services/Implement/ReceiveManagementService.cs:272` | 272 | `"ZJP"` | เปรียบเทียบ CustCode |

---

## 🔵 LOW-MEDIUM — Magic Numbers

### Limits / Pagination

| ไฟล์ | บรรทัด | ค่า | หมายเหตุ |
|------|--------|-----|---------|
| `Services/Implement/ReceiveManagementService.cs:47` | 47 | `.Take(300)` | JP receive max records |
| `Services/Implement/ReceiveManagementService.cs:99` | 99 | `.Take(100)` | SP receive max records |
| `Services/Implement/BreakService.cs:36` | 36 | `.Take(100)` | Break records limit |
| `Services/Implement/AdminStockService.cs:95` | 95 | `.Skip(1)` | ข้าม header row ใน Excel |
| `wwwroot/js/StockManagement.js:331` | 331 | `STOCK_PAGE_SIZE = 20` | จำนวนรายการต่อหน้า |

### Cache / Session

| ไฟล์ | บรรทัด | ค่า | หมายเหตุ |
|------|--------|-----|---------|
| `Services/Implement/CacheService.cs:36` | 36 | `TimeSpan.FromHours(4)` | อายุ cache default |
| `Program.cs:54` | 54 | `TimeSpan.FromDays(7)` | อายุ cookie |

### Image / Report

| ไฟล์ | บรรทัด | ค่า | หมายเหตุ |
|------|--------|-----|---------|
| `Services/Implement/ReportService.cs:110` | 110 | `width=140, height=120` | ขนาด resize รูปใน report |
| `Services/Implement/ReportService.cs:120` | 120 | `70` | JPEG compression quality |
| `wwwroot/js/StockManagement.js:488,691,771` | หลายจุด | `width="80" height="80"` | ขนาดรูปในตาราง |
| `wwwroot/js/PrintReport.js` | หลายจุด | `width="50" height="50"` | ขนาดรูปใน report |

### UI / JavaScript

| ไฟล์ | บรรทัด | ค่า | หมายเหตุ |
|------|--------|-----|---------|
| `wwwroot/js/site.js:13` | 13 | `timer: 3000` | Toast notification timeout |
| `wwwroot/js/site.js:16` | 16 | `width: '380px'` | Toast width |
| `wwwroot/js/site.js:189` | 189 | `scrollTop > 200` | Threshold ปุ่ม back-to-top |
| `wwwroot/js/site.js:283` | 283 | `2000` | Clipboard tooltip delay |
| `wwwroot/js/site.js:164` | 164 | `maximumFractionDigits: 2` | ทศนิยม number format |

---

## 🔵 LOW — Cookie / Cache Key Names

| ไฟล์ | ค่า | หมายเหตุ |
|------|-----|---------|
| `Program.cs:52` | `"JPApp.Auth"` | ชื่อ cookie |
| `Program.cs:53` | `"/Auth/Login"` | Login path |
| `Services/Implement/AuthService.cs:51` | `"AccessToken"` | Cookie key |
| `Services/Implement/AuthService.cs:60` | `"RefreshToken"` | Cookie key |
| `Services/Implement/AuthService.cs:72` | `"RememberedUsername"` | Cookie key |
| `Services/Implement/AuthService.cs:79` | `"RememberMeChecked"` | Cookie key |
| `Services/Implement/PISService.cs:18` | `"EmployeeList"` | Cache key |
| `Services/Implement/PISService.cs:92` | `"UserList"` | Cache key |
| `Controllers/AuthController.cs:50-52` | `"EmployeeList"`, `"DepartmentList"`, `"UserList"` | Cache keys |

---

## สรุป

| ระดับ | จำนวนจุด | ตัวอย่าง |
|-------|---------|---------|
| 🔴 CRITICAL | 8 | Password, API Key ใน appsettings |
| 🟠 HIGH | 2 | IP / Endpoint hardcode |
| 🟡 MEDIUM | 10 | User ID, Permission ID, Business string |
| 🔵 LOW-MEDIUM | 15+ | Magic numbers, limits, image sizes |
| 🔵 LOW | 10+ | Cookie/Cache key names |

### จุดที่ควรแก้ก่อน

1. **User IDs 1 และ 28** — ควรใช้ Role หรือ Permission แทน ตอนนี้กระจายอยู่ใน 3 ไฟล์
2. **Permission ID 3** — ควรทำเป็น enum หรือ constant
3. **`"ADMIN"` string** — ควรทำเป็น constant ใน StockGroupKeyHelper แล้ว reference จากที่เดียว
4. **`Take(300)`, `Take(100)`** — ควรย้ายไปเป็น config หรือ constant เพื่อ tune ได้ง่าย
5. **Connection strings / passwords** — ควรใช้ environment variables หรือ secrets manager
