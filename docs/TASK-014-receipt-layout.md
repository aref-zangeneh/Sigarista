# TASK-014 — طرح داینامیک فیش چاپی (فیش مشتری و فیش آشپزخانه)

وضعیت: پیاده‌سازی کامل (بک‌اند + پنل ادمین با پیش‌نمایش زنده + رندرر پرینت‌ایجنت) و تست‌های واحد سبز.

---

## ۱. خلاصهٔ کار

ادمین می‌تواند **چیدمان نهایی فیش چاپی** را از پنل مدیریت، به‌صورت بصری و زنده تغییر دهد: ستون‌بندی جدول آیتم‌ها، سطرهای اطلاعاتی، ردیف‌های جمع، متن و اندازهٔ فونت‌ها. طرح روی دو نسخهٔ چاپ اعمال می‌شود:

- **فیش مشتری (فاکتور)** — کپی `Customer`
- **فیش آشپزخانه (باریستا)** — کپی `Barista` (بدون هیچ مبلغی)

تغییرات تسک‌های قبلی در فیش‌ها منعکس شده است:

| تسک | اثر در فیش |
|---|---|
| TASK-013 (گروه‌های آپشن) | هر خط، گزینه‌های انتخابی خودش را دارد (ساختارمند، از اسنپ‌شات)؛ سطر جزئیات `options` روی هر دو نسخهٔ پیش‌فرض فعال است و با کلید `showOptionPrices` مبلغ هر گزینه به‌صورت `(+۱۲۰٬۰۰۰)` چاپ می‌شود. جمع افزودِ آپشن‌ها (`optionTotalLabel`) هم در payload هست. |
| نوشیدنی‌ساز | سطر جزئیات `customDrink` (خلاصهٔ سفارشی‌سازی) هر دو نسخه. |
| تقسیم صورت‌حساب | سطر `payer` («سهم …») در فیش مشتری، درخواست باگ‌قبلی حفظ شد. |
| TASK-012 (زمان‌بندی تحویل) | سطر اطلاعاتی جدید `pickupTime` («تحویل: …» با تقویم کافه) در فیش مشتری؛ منتظر نمی‌ماند تا فقط در متن اعلام شود. |

---

## ۲. مدل طرح (`ResolvedReceiptLayout`)

یک سند JSON یکپارچه روی `SiteSettings.ReceiptLayoutJson` (تا ۱۲٬۰۰۰ نویسه، `null` = پیش‌فرض کلاسیک). کد: `Sugarista.Application/Printing/ReceiptLayout/`.

```
customer:
  header:    { showAddress, showPhone }
  headerRows: [code, createdAt, channel, service, table, pickupTime,
               customerName, customerPhone, note]     ← ترتیب و انتخاب دلخواه
  items:     { columns[n], subLines[n], showHeaderRow,
               showOptionPrices, showUnitPriceNote, fontSize }
  totals:    { rows[n] : subtotal | deliveryFee | tax | total + label/visible }
  paymentRows: [paymentMethod, reference, amountPaid]
  footer:    { text(جانشین FooterText تنظیمات), showThanks, showCafeName }
  fonts:     { header, info, item, subtotal, total }
barista:
  title, infoRows[n], items{ columns[n], subLines[n], showOptionPrices, fontSize },
  showOrderNote, footer{text, showSign}, fonts{title, code, info, item}
```

**هندسهٔ جدول آیتم‌ها** (در رندرر):

- ستون `name` ناحیهٔ انعطاف‌پذیر سمت راست را می‌گیرد.
- ستون `qty` به‌صورت اینلاین «۲ × اسپرسو» در همان افقی دیده می‌شود.
- ستون‌های مبلغی `unitPrice` و `total` از حاشیهٔ چپ ردیف‌های واقعی «باند» می‌سازند؛ ترتیب و عرض (٪ بین ۱۵ تا ۹۰ یا خودکار از داده) قابل تنظیم‌اند؛ `showHeaderRow` ردیف عنوان («قیمت واحد»، «جمع») اضافه می‌کند.
- جزئیات زیر نام (`subLines`) با ترتیب دلخواه چاپ می‌شوند: `size`, `options`, `modifiers`, `customDrink`, `payer`, `baristaNote`.

قواعد حذف‌ناپذیر (اعتبارسنجی فارسی):

- ستون «نام آیتم» و «جمع» در فیش مشتری و ردیف «مبلغ نهایی» قابل حذف نیستند.
- فیش آشپزخانه ستون مبلغی ندارد (پیام اختصاصی).
- شناسه‌های ناشناس، ستون تکراری، عرض خارج از بازه و فونت خارج از ۱۲–۹۰ رد می‌شوند؛ ارسال ناقص (PATCH) بخش‌های غایب را از پیش‌فرض پر می‌کند.

---

## ۳. API و جریان چاپ

```
GET /api/admin/receipt-layout   → سند حل‌شده (حتی اگر ذخیره نشده باشد، پیش‌فرض)
PUT /api/admin/receipt-layout   → merge روی پیش‌فرض + اعتبارسنجی + ذخیره (204)
دسترسی: SiteSettings.View / SiteSettings.Update
```

`ReceiptPrintJobService` هنگام صف‌کردن فیش، سند حل‌شده را داخل خود payload (`layout`) می‌فرستد — پرینت‌ایجنت آفلاین بدون هیچ درخواست اضافه، مستقیم همان را اعمال می‌کند و payload همچنان self-contained است. payload بدنهٔ آیتم‌ها حالا شامل `options[]`, `optionTotalLabel`, `customDrinkSummary[]` و `order.pickupAtLabel` است.

`render.py` (پرینت‌ایجنت) با هر payload بدون `layout` دقیقاً مثل قبل رفتار می‌کند (سازگاری عقب‌گرد)؛ با `layout`، همهٔ انتخاب‌های ادمین را اعمال می‌کند.

---

## ۴. پنل مدیریت

بخش **«طرح فیش‌های چاپی»** در صفحهٔ تنظیمات سایت (`ReceiptLayoutPanel.jsx`) با ذخیرهٔ مستقل (PUT) — جدا از فرم بزرگ تنظیمات:

- تب «فیش مشتری (فاکتور)» و «فیش آشپزخانه (باریستا)».
- سطرها/ستون‌ها با دکمه‌های ↑↓ و ✕ و افزودن از مخزن کلیدهای مجاز.
- برچسب سفارشی ردیف‌های جمع و ستون‌ها؛ عرض باندهای مبلغی به درصد.
- فونت‌ها، پانویس، رفتارهای آپشن (مبلغ برای هر گزینه).
- **پیش‌نمایش زندهٔ کاغذ حرارتی** کنار فرم که با هر تغییر فوراً به‌روز می‌شود.

---

## ۵. مایگریشن و سازگاری

- `AddReceiptLayout`: ستون `ReceiptLayoutJson` روی `SiteSettings` (+ ALTER اطمینان‌محور در `ApplicationDbContextSeeder`).
- پرداخت/صف موجود بدون تغییر: idempotency صف، گرفتن کپی پنل ادمین و کنترل `PrintJobs` دست‌نخورده.
