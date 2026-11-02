# Sugarista Print Agent (macOS)

چاپ فیش حرارتی سفارش‌ها روی پرینتر متصل به لپ‌تاپ فروشگاه (مثل **GA-E200I**، عرض کاغذ ۸ سانتی‌متر / ۳۸۴ نقطه).

## چطور کار می‌کند؟

1. هر وقت سفارشی **پرداخت شود** — چه از پنل POS (نقدی/کارت) و چه آنلاین (درگاه) و چه پرداخت تقسیمی (سهم آخر) — بک‌اند **دو job چاپ** در جدول `PrintJobs` می‌سازد:
   - `Customer` — فیش کامل مشتری (اقلام، مبالغ، روش پرداخت، شماره پیگیری)
   - `Barista` — تیکت باریستا (کد سفارش درشت، اقلام و تعداد، سایز/افزودنی‌ها، یادداشت‌ها، بدون قیمت)
2. این ایجنت هر چند ثانیه از API سرور jobهای `Pending` را می‌گیرد (`GET /api/print-agent/pending`).
3. هر job را به یک تصویر **۵۷۶ نقطه‌ای** PNG تبدیل می‌کند (۷۲ میلی‌متر قابل چاپ از کاغذ ۸۰ میلی‌متری، در ۲۰۳ dpi) — با **شکل‌دهی درست فارسی (RTL + حروف چسبیده)**، ارقام فارسی و فونت **وزیرمتن** — و از طریق CUPS (`lp`) به پرینتر می‌فرستد.
4. بعد از موفقیت، job را `Printed` علامت می‌زند؛ در صورت خطا تا ۳ بار تلاش می‌کند و بعد `Failed` ثبت می‌شود.

> چون پرینتر با USB به مک‌بوک وصل است و سرور روی اینترنت است، چاپ از سمت مک انجام می‌شود؛ سرور فقط صف jobها را نگه می‌دارد.

## نصب (یک‌بار)

```bash
cd Sugarista-PrintAgent
# 1) فایل تنظیمات را بسازید و آدرس سرور و کلید ایجنت را در آن بگذارید
cp config.example.json config.json
nano config.json
# 2) نصب + اجرای خودکار هنگام لاگین
chmod +x install.sh run.sh
./install.sh
```

`install.sh` این کارها را انجام می‌دهد:

- ساخت محیط پایتون (`venv`) و نصب `pillow`, `arabic-reshaper`, `python-bidi`
- بررسی وجود صف پرینتر در CUPS (`lpstat -p`)
- ساخت و لود `LaunchAgent` به نام `com.sugarista.printagent` با `KeepAlive` (اگر کرش کند خودش دوباره بالا می‌آید)

## تنظیمات `config.json`

| کلید | توضیح |
|---|---|
| `serverUrl` | آدرس سرور، بدون `/api` — مثلاً `https://sugarista.example.com` |
| `apiKey` | همان مقدار `PRINT_AGENT_KEY` در `.env` سرور |
| `printerName` | نام صف CUPS پرینتر — با `lpstat -p` ببینید (مثلاً `GA-E200I`) |
| `pollSeconds` | فاصلهٔ بررسی صف (پیش‌فرض ۴ ثانیه) |
| `maxJobsPerPoll` | حداکثر job در هر بار (پیش‌فرض ۱۰) |
| `printTimeoutSeconds` | مهلت ارسال هر فیش به پرینتر |
| `printOptions` | گزینه‌های `lp` — پیش‌فرض `PageSize=X72MMY2000MM`، `PageType=Variable`، `CutType=DocCutMode` (بدون این‌ها CUPS ممکن است تصویر را روی A4 مقیاس کند و خروجی کشیده/ناخوانا شود) |

## دستورهای روزمره

```bash
./run.sh                        # اجرای دستی در ترمینال (برای دیباگ)
node agent.js --once            # یک‌بار صف را خالی کن و خارج شو (تست سریع)
./install.sh --uninstall        # توقف و حذف اجرای خودکار
tail -f ~/Library/Logs/SugaristaPrintAgent.log   # لاگ ایجنت
lpstat -p GA-E200I              # وضعیت پرینتر
lpstat -o                       # صف چاپ
```

## عیب‌یابی

| مشکل | بررسی |
|---|---|
| `401` در لاگ | `apiKey` با `PRINT_AGENT_KEY` سرور یکی نیست |
| `printer queue not found` | نام صف در `lpstat -p` را در `config.json` بگذارید |
| فیش چاپ می‌شود ولی خالی/کوچک است | `printOptions` باید `PageSize=X72MMY2000MM` داشته باشد؛ تصویر دقیقاً ۵۷۶ نقطه عرض دارد |
| بعد از چند سطر، کاراکترهای ناخوانا چاپ می‌شود | اندازهٔ صفحهٔ صف روی A4 مانده — `lpadmin -p GA-E200I -o PageSize=X72MMY2000MM -o PageType=Variable` |
| فارسی مربع‌مربع می‌شود | فونت `fonts/Vazirmatn-Regular.ttf` در پوشهٔ ایجنت باشد |
| job در صف می‌ماند | سرور در دسترس نیست یا کلید اشتباه است — لاگ را ببینید |

## پیش‌نیازها

- macOS با Node.js 18+ و Python 3.10+
- درایور پرینتر نصب‌شده و صف CUPS موجود
