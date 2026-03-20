# JPStockShowRoom

ระบบจัดการ Stock และ Showroom สำหรับสินค้าเครื่องประดับ
รองรับการรับสินค้า, จัดถาด, ยืม-คืน, เบิก, ส่งซ่อม และออกรายงาน PDF

---

## Tech Stack

| หมวด | เทคโนโลยี |
|------|-----------|
| Framework | ASP.NET Core MVC (.NET 9.0) |
| ORM | Entity Framework Core 9 + Dapper |
| PDF | QuestPDF |
| Excel | ClosedXML |
| Image | SkiaSharp |
| Logging | Serilog (Console + File) |
| Auth | Cookie Authentication |
| Frontend | AdminLTE 3, Bootstrap 4, jQuery, Select2, SweetAlert2 |

---

## สถาปัตยกรรม

```
Client (Browser)
    └── ASP.NET Core MVC
            ├── Controllers/
            │     ├── HomeController   — ฟีเจอร์หลักทั้งหมด
            │     └── AuthController   — Login / Logout
            ├── Services/
            │     ├── Interface/       — contracts
            │     └── Implement/       — business logic
            ├── Data/
            │     ├── JPDbContext      — ฐานข้อมูล PrincessData (Job/Order)
            │     ├── SPDbContext      — ฐานข้อมูล JPStockPacking (รับของ)
            │     └── SWDbContext      — ฐานข้อมูล JPStockShowroom (Stock หลัก)
            └── Views/
                  ├── Home/Index       — Dashboard หลัก
                  └── Partial/         — ส่วนย่อยแต่ละโมดูล
```

---

## ฐานข้อมูล

โปรเจคเชื่อมต่อ SQL Server 3 ฐาน:

| Connection Key | ฐานข้อมูล | หน้าที่ |
|---------------|-----------|--------|
| `JPDBEntries` | PrincessData | ข้อมูล Job/Order จากระบบหลัก (อ่านอย่างเดียว) |
| `SPDBEntries` | JPStockPacking | รับสินค้า, Lot, Export, Assignment |
| `SWDBEntries` | JPStockShowroom | Stock, Tray, Borrow, Withdrawal, Break |

---

## ฟีเจอร์หลัก

### รับสินค้า (Receive Management)
- ดึงรายการรับสินค้าจาก JP และ SP
- นำเข้า Receive No เพื่อสร้าง Stock
- รองรับ SP Receive และ Sample Lot

### จัดการ Stock
- ค้นหา/กรองสินค้าตาม Article, ประเภท, สถานะ
- แยก tab สินค้าทั่วไป / รอลงทะเบียน (Z-Article)
- Admin เพิ่มสินค้าเองได้ หรือ Import จาก Excel
- ลบสินค้าที่ admin เพิ่ม (เฉพาะ user ที่กำหนด)

### ถาด (Tray Management)
- สร้าง/ลบถาด
- นำสินค้าลงถาด / ย้ายออกจากถาด
- ดูรายละเอียดสินค้าในถาด

### ยืม-คืน (Borrow)
- บันทึกการยืมสินค้า
- ติดตามสินค้าที่ยืมอยู่
- คืนสินค้า + สร้างเอกสารยืม PDF

### เบิกออก (Withdrawal)
- เบิกสินค้าออกจาก Stock
- ประวัติการเบิก
- สร้างเอกสารเบิก PDF

### ส่งซ่อม (Break)
- บันทึกสินค้าชำรุด พร้อมระบุอาการ
- รายการส่งซ่อมทั้งหมด
- สร้างเอกสารส่งซ่อม PDF

### รายงาน
- Stock Report (มีรูป / ไม่มีรูป)
- Withdrawal / Borrow / Break Report

### การจัดการผู้ใช้ (Permission)
- เชื่อมต่อกับระบบ PIS (ระบบบุคคล)
- กำหนด Permission ต่อ User แต่ละคน

---

## การตั้งค่า

### 1. Connection Strings

ใน `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "JPDBEntries": "Data Source=...;Initial Catalog=PrincessData;...",
    "SPDBEntries": "Data Source=...;Initial Catalog=JPStockPacking;...",
    "SWDBEntries": "Data Source=...;Initial Catalog=JPStockShowroom;..."
  }
}
```

### 2. API Settings (PIS)

```json
{
  "APIKey": "<base64-api-key>",
  "Audience": "SW",
  "AccessToken": "https://<server>/api/auth/AccessToken",
  "RefreshToken": "https://<server>/api/auth/RefreshToken",
  "RevokeToken": "https://<server>/api/auth/RevokeToken",
  "GetEmployee": "https://<server>/api/PIS/GetEmployee",
  "GetDepartment": "https://<server>/api/PIS/GetDepartment",
  "GetUser": "https://<server>/api/PIS/GetUser"
}
```

### 3. App Settings

```json
{
  "AppVersion": "1.0.0",
  "UseByPass": false
}
```

`UseByPass: true` — ข้าม authentication (ใช้ตอน dev ถ้าไม่มี PIS API)

---

## การรัน

### Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- SQL Server (เข้าถึงได้ทั้ง 3 ฐาน)
- ตั้งค่า connection strings และ API endpoints ใน `appsettings.Development.json`

### วิธีรัน

**Option 1 — dotnet CLI**

```bash
# clone repo
git clone <repo-url>
cd JPStockShowRoom

# restore dependencies
dotnet restore

# รันแบบ HTTP (port 5128)
dotnet run --project JPStockShowRoom --launch-profile http

# รันแบบ HTTPS (port 7147)
dotnet run --project JPStockShowRoom --launch-profile https
```

เปิดเบราว์เซอร์ที่:
- HTTP → `http://localhost:5128`
- HTTPS → `https://localhost:7147`

**Option 2 — Visual Studio / Rider**

เปิด `JPStockShowRoom.sln` แล้วกด Run (เลือก profile `http` หรือ `https`)

**Option 3 — IIS Express** (Visual Studio)

เลือก profile `IIS Express` → `http://localhost:49454`

### Publish (Production)

```bash
dotnet publish JPStockShowRoom -c Release -o ./publish
```

ก่อน deploy ให้แก้ไข `appsettings.Production.json` ให้ถูกต้อง

---

## โครงสร้างไฟล์

```
JPStockShowRoom/
├── Controllers/
│   ├── HomeController.cs
│   └── AuthController.cs
├── Data/
│   ├── JPDbContext/
│   ├── SPDbContext/
│   └── SWDbContext/
├── Models/
├── Services/
│   ├── Helper/
│   ├── Interface/
│   ├── Implement/
│   └── Middleware/
├── Views/
│   ├── Auth/
│   ├── Home/
│   ├── Partial/
│   └── Shared/
├── wwwroot/
│   ├── js/
│   └── css/
├── logs/
├── appsettings.json
├── appsettings.Development.json
├── appsettings.Production.json
└── Program.cs
```

---

## Logging

ไฟล์ log อยู่ที่ `logs/` แบ่งตามระดับ:

```
logs/
├── Information/log-YYYYMMDD.log
└── Error/log-YYYYMMDD.log
```

เก็บย้อนหลัง 30 วัน (rolling daily)

---

## หมายเหตุ

- ดู [HARDCODED_VALUES.md](HARDCODED_VALUES.md) สำหรับรายการค่าที่ยัง hardcode อยู่ในโค้ด
- ฐานข้อมูลใช้ Collation `Thai_100_CI_AI`
- รูปภาพสินค้าดึงจาก SP ผ่าน endpoint `GetImage`
