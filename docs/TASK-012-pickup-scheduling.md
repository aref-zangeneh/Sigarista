# TASK-012 — سفارش پیش از مراجعه و زمان‌بندی تحویل حضوری

وضعیت: پیاده‌سازی و تست انجام شد. زمان‌بندی به‌صورت پیش‌فرض **خاموش** است و از پنل مدیریت فعال می‌شود.

---

## ۱. خلاصهٔ کار

مشتری می‌تواند در مرحلهٔ پرداخت، بین «در اسرع وقت» و «انتخاب زمان» یکی را انتخاب کند. در حالت انتخاب زمان:

- روز و بازهٔ تحویل حضوری از **تنظیمات کافه** ساخته می‌شود (ساعت کاری هر روز، طول بازه، ظرفیت، حداقل فاصلهٔ آماده‌سازی، حداکثر روزهای آینده).
- فقط بازه‌های معتبر نمایش داده می‌شوند؛ بازه‌های پر با برچسب **«تکمیل»** و به‌صورت غیرفعال (نه حذف‌شده) نشان داده می‌شوند.
- همهٔ محاسبات زمانی در **منطقهٔ زمانی کافه** انجام می‌شود، نه ساعت دستگاه مشتری.
- سرور در لحظهٔ ثبت نهایی، بازه و ظرفیت را دوباره اعتبارسنجی می‌کند و با قفل تراکنشی از ثبت هم‌زمان دو سفارش در آخرین ظرفیت جلوگیری می‌کند.
- پیش از ثبت: موجودی، قیمت و اعتبار بازه بررسی می‌شود. در صورت تغییر قیمت، تا وقتی مشتری صریحاً تأیید نکند سفارشی ثبت نمی‌شود.
- پس از ثبت: زمان تحویل، زمان تقریبی آماده‌شدن، وضعیت و امکان لغو/تغییر زمان تا مهلت مجاز نمایش داده می‌شود.

---

## ۲. تنظیمات کافه (بدون هیچ مقدار Hard-coded)

همهٔ مقادیر روی `SiteSettings` ذخیره می‌شوند و در پنل مدیریت → «تنظیمات سایت» → بخش **«سفارش پیش از مراجعه»** قابل ویرایش‌اند.

| تنظیم | فیلد | پیش‌فرض | بازهٔ مجاز |
|---|---|---|---|
| فعال بودن زمان‌بندی | `PickupSchedulingEnabled` | `false` | — |
| طول هر بازه | `PickupSlotMinutes` | `15` | ۵ تا ۱۲۰ دقیقه |
| ظرفیت هر بازه | `PickupSlotCapacity` | `5` | ۱ تا ۱۰۰۰ |
| واحد ظرفیت | `PickupCapacityMode` | `Orders` | `Orders` یا `Items` |
| حداقل فاصلهٔ سفارش تا تحویل | `PickupMinLeadMinutes` | `30` | ۰ تا ۱۴۴۰ دقیقه |
| حداکثر روزهای آینده | `PickupMaxDaysAhead` | `7` | ۰ تا ۶۰ روز |
| مهلت لغو/تغییر | `PickupCancelCutoffMinutes` | `60` | ۰ تا ۱۰۰۸۰ دقیقه |
| برگشت خودکار به کیف پول | `PickupAutoRefundOnCancel` | `true` | — |
| متن سیاست بازپرداخت | `PickupRefundPolicyNote` | متن فارسی پیش‌فرض | حداکثر ۱۰۰۰ کاراکتر |

- ساعت کاری و روزهای تعطیل از جدول موجود `CafeOpenHours` خوانده می‌شود (شنبه تا جمعه، هر روز `IsOpen` + `OpenTime` + `CloseTime`).
- منطقهٔ زمانی از `SiteSettings.TimeZoneId` خوانده می‌شود (پیش‌فرض `Asia/Tehran`).

پیاده‌سازی: `Sugarista.Application/Pickup/PickupSettingsSnapshot.cs`

---

## ۳. الگوریتم ساخت بازه‌ها

`PickupScheduleCalculator.BuildDaySlots` برای هر روز:

1. اگر کافه تعطیل باشد یا زمان‌بندی خاموش باشد، لیست خالی برمی‌گرداند.
2. از `OpenTime` تا `CloseTime` بازه‌های پشت‌سرهم به اندازهٔ `SlotMinutes` می‌سازد؛ بازه‌ای که انتهایش از `CloseTime` بگذرد حذف می‌شود.
3. بازه‌هایی که شروعشان کمتر از `now + MinLeadMinutes` باشد حذف می‌شوند.
4. مصرف هر بازه از سفارش‌های غیرلغوشده/غیرناموفق همان بازه محاسبه می‌شود (`Orders` = یک واحد به ازای هر سفارش، `Items` = مجموع تعداد اقلام).
5. بازه‌های پر **حذف نمی‌شوند**؛ با `IsFull = true`، `RemainingCapacity = 0` و برچسب «تکمیل» برگردانده می‌شوند.
6. همهٔ زمان‌ها ابتدا در منطقهٔ زمانی کافه ساخته و سپس به UTC نرمال می‌شوند (برای سازگاری با `timestamptz` در PostgreSQL).

اعتبارسنجی یک بازهٔ درخواستی (`Validate`) کدهای خطای ماشین‌خوان زیر را برمی‌گرداند:

`past_date`، `beyond_max_days`، `closed_day`، `invalid_slot` (خارج از شبکهٔ بازه‌ها)، `outside_hours`، `too_soon`، `slot_full`، `scheduling_disabled`.

در صورت خطا، `NearestAvailableSlot` نیز محاسبه می‌شود (نزدیک‌ترین بازهٔ خالی تا `min(MaxDaysAhead, 14)` روز آینده) تا کلاینت دکمهٔ «انتخاب نزدیک‌ترین بازهٔ خالی» را نشان دهد.

---

## ۴. اعتبارسنجی سمت سرور و جلوگیری از Race Condition

در `CreateOrderCheckoutCommandHandler` هنگام ثبت سفارش زمان‌بندی‌شده:

1. **Idempotency**: اگر `IdempotencyKey` تکراری باشد، `409 duplicate_order` با کد پیگیری سفارش قبلی برمی‌گردد (ایندکس یکتای فیلترشده روی `Orders.IdempotencyKey`).
2. **فعال بودن زمان‌بندی**: اگر `DeliveryAt` فرستاده شده باشد ولی `PickupSchedulingEnabled = false`، `409 scheduling_disabled`.
3. **موجودی و منو**: محصول حذف/غیرفعال/غیرقابل‌سفارش یا کمبود موجودی → `409 item_unavailable` همراه با نام محصول، دلیل، موجودی و مقدار درخواستی.
4. **قیمت**: مقایسهٔ قیمت واحد و جمع سبد با آنچه کلاینت نمایش داده → `409 price_changed` با قیمت قبلی/جدید هر خط و جمع جدید. ثبت فقط با `AcceptPriceChanges = true` (تأیید صریح مشتری) ادامه پیدا می‌کند.
5. **اعتبار بازه**: `ValidateAsync` قبل از درج.
6. **قفل ظرفیت**: `PickupSlotReservationService` یک تراکنش باز می‌کند و `pg_advisory_xact_lock(hashtext(slotKey))` را برای همان بازه می‌گیرد؛ سپس `ValidateCapacityOnlyAsync` دوباره ظرفیت را می‌خواند. فقط پس از درج سفارش، تراکنش Commit می‌شود. قفل با Commit/Rollback آزاد می‌شود، پس خطای Request باعث قفل دائمی نمی‌شود.

نتیجه: در تست هم‌زمانی روی آخرین ظرفیت، دقیقاً یک درخواست `200` و دیگری `409 slot_full` گرفت و بازه پس از آن `isFull = true` شد.

پیاده‌سازی:
- `Sugarista.Application/Orders/Commands/CreateOrderCheckout/CreateOrderCheckoutCommand.cs`
- `Sugarista.Infrastructure/Services/PickupSlotReservationService.cs`
- `Sugarista.Application/Orders/CheckoutConflictException.cs`
- `Sugarista.Api/Middleware/ExceptionHandlingMiddleware.cs` (تبدیل به HTTP 409 با `code` و جزئیات ساختاریافته)

---

## ۵. وضعیت سفارش، ETA و اطلاع‌رسانی

وضعیت‌ها همان وضعیت‌های موجود سیستم هستند: `Registered → Preparing → Ready → Completed` و `Cancelled`.

`GET /api/orders/status/{trackingCode}` علاوه بر اطلاعات قبلی، این فیلدها را برمی‌گرداند:

- `id` (برای لغو/تغییر زمان)
- `deliveryAt` (زمان تحویل حضوری، UTC)
- `estimatedReadyAt` = `ReadyAt ?? DeliveryAt ?? CreatedAt + MinLeadMinutes`
- `pickupScheduled`
- `canCancel` / `canReschedule`
- `changeBlockedReason` (مثلاً «مهلت لغو گذشته است.»)
- `refundPolicyNote`
- `cancelCutoffMinutes`

اطلاع‌رسانی:

- آماده‌شدن سفارش: `SendOrderReadyAsync` (از قبل موجود است و در `UpdateOrderFulfillment` صدا زده می‌شود).
- تغییر زمان تحویل: `SendPickupScheduleChangedAsync` در سرویس‌های Logging و Kavenegar پیاده‌سازی شد.
- خطای ارسال پیامک، ثبت سفارش/تغییر زمان را شکست نمی‌دهد (فقط Log می‌شود).

---

## ۶. لغو و تغییر زمان

### مهلت مجاز
`PickupScheduleCalculator.IsWithinChangeWindow`:

- سفارش `Cancelled`/`Failed` یا `Completed` → مجاز نیست.
- سفارش زمان‌بندی‌شده: تا **دقیقاً** `DeliveryAt - CancelCutoffMinutes` مجاز است؛ یک ثانیه بعد از آن ممنوع.
- سفارش «در اسرع وقت»: فقط تا وقتی وضعیت `Registered` است.

`BuildChangeBlockedReason` دلیل فارسی می‌دهد: «این سفارش قبلاً لغو شده است.»، «این سفارش تحویل داده شده است.»، «آماده‌سازی سفارش شروع شده و امکان لغو یا تغییر نیست.»، «مهلت لغو گذشته است.»

### لغو
`POST /api/orders/{id}/cancel` با بدنهٔ `{ "trackingCode": "..." }`

- موجودی/انبار و پرداخت‌های در انتظار آزاد/لغو می‌شوند.
- ظرفیت بازه خودبه‌خود آزاد می‌شود چون ظرفیت از `DeliveryAt` مشتق می‌شود.
- اگر `PickupAutoRefundOnCancel = true` و مبلغی پرداخت شده باشد، مبلغ کیف پول به‌صورت خودکار برگشت می‌شود. بازپرداخت درگاه آنلاین همچنان توسط کافه انجام می‌شود (`refundHandledByCafe = true`).
- پیام موفقیت: «سفارش لغو شد و ظرفیت بازه آزاد شد.»

### تغییر زمان
`POST /api/orders/{id}/reschedule` با بدنهٔ `{ "trackingCode": "...", "deliveryAt": "..." }`

- بازهٔ جدید همان قواعد ثبت سفارش را دارد (ساعت کاری، lead time، ظرفیت).
- با همان قفل تراکنشی ظرفیت بررسی و سپس `DeliveryAt` به‌روزرسانی می‌شود؛ بازهٔ قبلی خودبه‌خود آزاد می‌شود.
- پس از موفقیت، پیامک تغییر زمان ارسال می‌شود.
- پیام موفقیت شامل تاریخ/ساعت شمسی جدید است.

---

## ۷. رابط کاربری

### پنل مدیریت
- بخش جدید «سفارش پیش از مراجعه» در `Sugarista-Admin/src/pages/SiteSettingsPage.jsx` با همهٔ تنظیمات، سوییچ فعال/غیرفعال، راهنمای هر فیلد و پیش‌نمایش سیاست بازپرداخت.
- نمایش `deliveryAt` سفارش در `OrderEditorModal.jsx`.
- مقادیر پیش‌فرض در `Sugarista-Admin/src/constants/adminForms.js`.

### منوی مشتری (Theme)
- انتخاب بین «در اسرع وقت» و «انتخاب زمان».
- چیپ‌های روز (با برچسب «تعطیل» برای روزهای بسته) و چیپ‌های بازه؛ بازه‌های پر غیرفعال با برچسب «تکمیل».
- پنل تعارض برای قیمت/موجودی/بازه با دکمه‌های «تأیید قیمت جدید و ادامه»، «حذف محصولات ناموجود»، «انتخاب نزدیک‌ترین بازهٔ خالی» و «انتخاب زمان دیگر».
- صفحهٔ موفقیت: کد پیگیری، وضعیت، زمان تحویل، ETA، دکمه‌های «تغییر زمان تحویل» و «لغو سفارش»، دلیل غیرفعال بودن در صورت عبور از مهلت، و متن سیاست بازپرداخت.
- به‌روزرسانی خودکار وضعیت هر ۳۰ ثانیه (`?order=success&code=...`).
- RTL، اعداد فارسی، حداقل ارتفاع ۴۴ پیکسل برای دکمه‌ها/چیپ‌ها، پیام‌های خطای فارسی و قابل‌اقدام.

---

## ۸. فایل‌های اصلی

### Backend
- `Sugarista/src/Sugarista.Domain/Entities/SiteSettings.cs`
- `Sugarista/src/Sugarista.Domain/Entities/Order.cs` (`DeliveryAt` به UTC، `IdempotencyKey`)
- `Sugarista/src/Sugarista.Application/Pickup/` (Snapshot، Calculator، SlotService، Reservation interface، GetPickupSlots، Cancel/Reschedule)
- `Sugarista/src/Sugarista.Application/Orders/Commands/CreateOrderCheckout/CreateOrderCheckoutCommand.cs`
- `Sugarista/src/Sugarista.Application/Orders/Queries/GetOrderByTrackingCode/GetOrderByTrackingCodeQuery.cs`
- `Sugarista/src/Sugarista.Api/Controllers/OrdersController.cs`
- `Sugarista/src/Sugarista.Api/Middleware/ExceptionHandlingMiddleware.cs`
- `Sugarista/src/Sugarista.Infrastructure/Services/PickupSlotReservationService.cs`
- `Sugarista/src/Sugarista.Infrastructure/Data/Migrations/20261107001727_AddPickupScheduling.cs`
- `Sugarista/tests/Sugarista.Application.UnitTests/PickupScheduleCalculatorTests.cs`

### Admin
- `Sugarista-Admin/src/pages/SiteSettingsPage.jsx`
- `Sugarista-Admin/src/constants/adminForms.js`
- `Sugarista-Admin/src/components/OrderEditorModal.jsx`

### Theme
- `Sugarista-Theme/index.html`
- `Sugarista-Theme/assets/js/app.js`
- `Sugarista-Theme/assets/css/style.css`

---

## ۹. نتایج تست سناریوهای لبه‌ای

### تست‌های واحد (۱۳ تست جدید)
`PickupScheduleCalculatorTests.cs` — کل مجموعه: **۸۹ تست، ۰ خطا**.

| # | سناریو | نتیجه |
|---|---|---|
| ۱ | ساخت بازه‌های پشت‌سرهم داخل ساعت کاری | ✅ ۶ بازهٔ ۳۰ دقیقه‌ای از ۰۹:۰۰ تا ۱۲:۰۰ |
| ۲ | روز تعطیل / زمان‌بندی خاموش | ✅ لیست خالی |
| ۳ | حذف بازه‌های داخل حداقل فاصلهٔ آماده‌سازی | ✅ اولین بازهٔ مجاز بعد از lead time |
| ۴ | نگه‌داشتن بازهٔ پر و علامت `IsFull` | ✅ بازهٔ پر حذف نشد، ظرفیت باقی‌مانده صفر |
| ۵ | مرز ساعت شروع (۰۹:۰۰) | ✅ معتبر |
| ۶ | تاریخ گذشته / فراتر از حداکثر روز / روز تعطیل | ✅ کدهای `past_date`، `beyond_max_days`، `closed_day` |
| ۷ | زمان خارج از شبکهٔ بازه‌ها / خارج از ساعت کاری | ✅ `invalid_slot`، `outside_hours` |
| ۸ | خیلی نزدیک / بازهٔ پر | ✅ `too_soon`، `slot_full` |
| ۹ | زمان‌بندی خاموش | ✅ `scheduling_disabled` |
| ۱۰ | تبدیل منطقهٔ زمانی کافه | ✅ ۰۹:۳۰ تهران = ۰۶:۰۰ UTC |
| ۱۱ | مرز دقیق مهلت لغو | ✅ تا `DeliveryAt - 60m` مجاز، یک ثانیه بعد «مهلت لغو گذشته است.» |
| ۱۲ | دلیل لغو سفارش لغوشده | ✅ «این سفارش قبلاً لغو شده است.» |
| ۱۳ | تطبیق فقط مرزهای دقیق بازه | ✅ ۰۹:۰۰ و ۱۰:۳۰ بله؛ ۱۰:۱۵ و ۰۸:۴۵ خیر |

### تست‌های E2E روی API محلی
| # | سناریو | نتیجه |
|---|---|---|
| ۱ | دریافت بازه‌ها | ✅ روزها، برچسب شمسی، `isOpen`، بازه‌ها و `isFull` درست |
| ۲ | تغییر قیمت هنگام ثبت | ✅ `409 price_changed` با قیمت قبلی/جدید و جمع جدید؛ پس از `AcceptPriceChanges=true` → `200` |
| ۳ | ارسال دوباره با همان IdempotencyKey | ✅ `409 duplicate_order` با کد پیگیری سفارش اول |
| ۴ | دو درخواست هم‌زمان روی آخرین ظرفیت | ✅ دقیقاً یک `200` و یک `409 slot_full`؛ بازه سپس `isFull=true` |
| ۵ | موجودی صفر | ✅ `409 item_unavailable` با نام «اسپرسو» و `available=0` |
| ۶ | زمان‌بندی خاموش + ارسال `deliveryAt` | ✅ `409 scheduling_disabled` |
| ۷ | لغو پس از عبور از مهلت | ✅ `400` با «مهلت لغو گذشته است.» |
| ۸ | تغییر زمان | ✅ `200`، زمان جدید ثبت شد و ظرفیت بازهٔ قبلی آزاد شد |
| ۹ | لغو سفارش | ✅ `200`، وضعیت `Cancelled`، `canCancel=false`، `canReschedule=false`، دلیل نمایش داده شد |
| ۱۰ | وضعیت سفارش | ✅ `deliveryAt`، `estimatedReadyAt`، `pickupScheduled`، فلگ‌های لغو/تغییر و متن بازپرداخت |
| ۱۱ | UI تم | ✅ انتخاب «انتخاب زمان»، چیپ روز/بازه، عدم نمایش «تعطیل»/«تکمیل» در جای نامربوط، عرض دراور در RTL درست |
| ۱۲ | UI پنل مدیریت | ✅ بخش تنظیمات با مقادیر ذخیره‌شدهٔ دیتابیس رندر شد (۱۵ دقیقه، ظرفیت ۵، Orders، ۳۰، ۷، ۶۰، بازپرداخت روشن) |

> برای تست E2E محلی، `Payment__AllowMockInProduction` و `IsOnlineSalesEnabled` موقتاً روشن شدند و پس از تست به حالت قبل برگردانده شدند.

---

## ۱۰. تنظیمات / تصمیم‌های محصولی که هنوز لازم است

1. **بازپرداخت پرداخت آنلاین هنگام لغو/تغییر زمان**: الان فقط مبلغ کیف پول به‌صورت خودکار برمی‌گردد و بازپرداخت درگاه آنلاین دستی توسط کافه انجام می‌شود. باید مشخص شود:
   - آیا درگاه باید خودکار Refund شود؟
   - آیا کارمزد/جریمهٔ لغو وجود دارد؟
   - سیاست دقیق برای لغو پس از پرداخت ولی قبل از مهلت.
2. **تأیید مقادیر پیش‌فرض**: طول بازه ۱۵ دقیقه، ظرفیت ۵، واحد `Orders`، lead time ۳۰ دقیقه، حداکثر ۷ روز، مهلت لغو ۶۰ دقیقه. این‌ها باید توسط مالک محصول تأیید شوند.
3. **تعطیلات استثنایی**: در حال حاضر فقط ساعت کاری هفتگی (`CafeOpenHours`) پشتیبانی می‌شود. تقویم تعطیلات/روزهای خاص وجود ندارد؛ در صورت نیاز باید مدل و UI جدا اضافه شود.
4. **ظرفیت بر اساس ایستگاه آماده‌سازی**: فعلاً ظرفیت تعداد سفارش/آیتم است، نه بار هر ایستگاه (بارista/آشپزخانه).
5. **محدودیت تعداد تغییر زمان**: هیچ سقفی برای تعداد Reschedule وجود ندارد.
6. **حداکثر تعداد سفارش هر مشتری در یک بازه**: وجود ندارد.
7. **منبع ETA**: فعلاً `ReadyAt ?? DeliveryAt ?? CreatedAt + MinLeadMinutes`؛ اگر кухня برآورد دقیق‌تری دارد باید منبع جدا تعریف شود.
8. **پیامک**: متن پیامک تغییر زمان و آماده‌شدن باید با ارائه‌دهنده و لحن برند نهایی شود؛ همچنین تصمیم بگیرید آیا برای هر تغییر وضعیت پیامک برود یا فقط `Ready`.
9. **زمان‌بندی وقتی فروش آنلاین خاموش است**: الان زمان‌بندی مستقل از `IsOnlineSalesEnabled` است ولی چون ثبت سفارش آنلاین بسته است عملاً استفاده نمی‌شود؛ در صورت نیاز به سفارش تلفنی زمان‌بندی‌شده باید مسیر جدا تعریف شود.
10. **پرداخت در محل vs آنلاین**: جریان فعلی از همان پرداخت موجود استفاده می‌کند؛ اگر «پرداخت هنگام مراجعه» برای سفارش زمان‌بندی‌شده لازم است، باید تصمیم و پیاده‌سازی جدا شود.

---

## ۱۱. فعال‌سازی و مهاجرت

- مهاجرت `20261107001727_AddPickupScheduling` به‌صورت خودکار در استارت API اعمال می‌شود (`Database.MigrateAsync`).
- برای فعال‌سازی: پنل مدیریت → تنظیمات سایت → «سفارش پیش از مراجعه» → سوییچ «فعال کردن زمان‌بندی تحویل حضوری».
- تا وقتی این سوییچ خاموش است، `/api/orders/pickup-slots` با `enabled=false` و لیست خالی برمی‌گردد و ارسال `deliveryAt` در ثبت سفارش `409 scheduling_disabled` می‌دهد.
- ستون‌های جدید `SiteSettings` با پیش‌فرض‌های عملیاتی (۱۵/۵/Orders/۳۰/۷/۶۰/true) و ایندکس‌های `IX_Orders_DeliveryAt` و ایندکس یکتای فیلترشدهٔ `IX_Orders_IdempotencyKey` اضافه شده‌اند.
