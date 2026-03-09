# ระบบแปลงรหัสสินค้า Z → Barcode จริง

## ภาพรวม
โค้ด Visual FoxPro สำหรับแปลง Z article (รหัสชั่วคราว) ให้เป็น barcode จริงจาก Center

---

## โครงสร้างตาราง

### CProfile (ข้อมูลโปรไฟล์สินค้า)
| ฟิลด์ | คำอธิบาย |
|-------|----------|
| Article | รหัสสินค้า |
| LinkArticle | เชื่อม Z ↔ Article จริง (สองทาง) |
| Wg | น้ำหนัก |

### CPriceSale (ข้อมูลราคาขาย)
| ฟิลด์ | คำอธิบาย |
|-------|----------|
| Article | รหัสสินค้า |
| Barcode | barcode สินค้า |
| FnCode | รหัสรูปแบบ |
| EpoxyColor | สีอีพ็อกซี่ |
| ListGem | รายการอัญมณี |
| RingSize | ขนาดแหวน |
| LinkBar | เชื่อม barcode |

### ตารางที่ถูก UPDATE
| ตาราง | ฟิลด์ที่เปลี่ยน |
|-------|----------------|
| OrdDOrder | barcode, wg, ttwg, edesfn |
| OrdLotNo | barcode, wg, ttwg, edesfn |
| OrdHistory | barcode, wg, ttwg |

> **หมายเหตุ:** Article ไม่ถูกเปลี่ยน ยังคงเป็น Z article เดิม

---

## Logic การทำงาน

```
1. OrdDOrder.article = 'Z240463'
                │
                ▼
2. SELECT FROM CProfile WHERE LinkArticle = 'Z240463'
   → ได้ Article = '154670024.16'
                │
                ▼
3. JOIN CPriceSale WHERE Article = '154670024.16'
   + fncode, epoxycolor, listgem, ringsize ตรงกัน
   → ได้ Barcode = '2415400000123', Wg = 1.82
                │
                ▼
4. UPDATE OrdDOrder SET barcode = '2415400000123'
   (+ wg, ttwg, edesfn ถ้า wg เดิม = 0)
```

---

## ตัวอย่างข้อมูล

### CProfile
| Article | LinkArticle |
|---------|-------------|
| Z240463 | 154670024.16 |
| 154670024.16 | Z240463 |

### CPriceSale
| Article | Barcode | LinkBar |
|---------|---------|---------|
| Z240463 | 24Z2400000794 | |
| 154670024.16 | 2415400000123 | 24Z2400000794 |

---

## สรุป
**Z article → CProfile.LinkArticle → Article จริง → CPriceSale → Barcode ใหม่ → UPDATE Order**
