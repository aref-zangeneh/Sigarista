/**
 * Single source of truth for admin-panel AND customer digital-menu tooltips.
 *
 * Every tooltip bubble on the admin panel or customer menu is keyed here
 * (`key` is stable and never changes once shipped — it is the DB join with
 * AdminTooltips overrides).
 * The admin manages overrides (custom text + on/off) from the
 * «راهنماهای صفحه‌ها» page; no row in the DB means the default text below
 * is shown.
 *
 * Keys starting with `menu.` belong to the customer-facing digital menu
 * (`Sugarista-Theme`) and are delivered to customers via `/api/site-settings`
 * (`menuTooltips`).
 */

export const TOOLTIP_PAGES = {
  'customer-menu': 'منوی دیجیتال مشتری',
  pos: 'صندوق فروش',
  'tooltips-page': 'راهنماهای صفحه‌ها',
  warehouses: 'انبارها',
  'menu-items': 'آیتم‌های منو',
  'system-logs': 'لاگ‌های سیستمی',
}

export const TOOLTIP_REGISTRY = [
  // ── منوی دیجیتال مشتری (Customer Digital Menu) ────────────────
  // ۱. هدر و دسترسی سریع
  {
    key: 'menu.header.notes',
    page: 'customer-menu',
    section: 'هدر و دسترسی سریع',
    label: 'دکمه یادداشت سفارش حضوری (هدر)',
    text: 'سفارش خود و دوستانتان را بدون نیاز به پرداخت آنلاین اینجا یادداشت کنید تا هنگام مراجعه به صندوق راحت اعلام کنید.',
  },
  {
    key: 'menu.header.profile',
    page: 'customer-menu',
    section: 'هدر و دسترسی سریع',
    label: 'دکمه کارت مهر و پروفایل مشتری (هدر)',
    text: 'فقط با وارد کردن شماره موبایل، کارت مهر دیجیتال، امتیاز وفاداری، موجودی کیف پول و تخفیف‌های خود را ببینید.',
  },

  // ۲. جستجو و دسته‌بندی منو
  {
    key: 'menu.browse.categories',
    page: 'customer-menu',
    section: 'جستجو و دسته‌بندی منو',
    label: 'نوار دسته‌بندی‌های منو',
    text: 'برای پرش سریع به هر بخش از منو، روی دسته‌بندی دلخواه بزنید.',
  },
  {
    key: 'menu.browse.search',
    page: 'customer-menu',
    section: 'جستجو و دسته‌بندی منو',
    label: 'جستجوی هوشمند منو',
    text: 'نام فارسی یا انگلیسی آیتم، طعم یا ترکیبات (مثل کارامل، اسپرسو، بدون قند) را جستجو کنید.',
  },
  {
    key: 'menu.browse.popular',
    page: 'customer-menu',
    section: 'جستجو و دسته‌بندی منو',
    label: 'بخش پیشنهادهای سریع (پرفروش‌ها)',
    text: 'محبوب‌ترین آیتم‌های کافه برای انتخاب سریع؛ با لمس هر مورد پنجره افزودن باز می‌شود.',
  },
  {
    key: 'menu.browse.addons',
    page: 'customer-menu',
    section: 'جستجو و دسته‌بندی منو',
    label: 'بخش افزودنی‌های منو',
    text: 'افزودنی‌ها و سیروپ‌های جانبی که می‌توانید در کنار نوشیدنی یا سفارش خود انتخاب کنید.',
  },

  // ۳. کارت محصول
  {
    key: 'menu.product.builder_badge',
    page: 'customer-menu',
    section: 'کارت محصول',
    label: 'نشان «قابل سفارشی‌سازی» روی محصول',
    text: 'این محصول قابلیت تنظیم دقیق ترکیبات (شات قهوه، نوع شیر، سیروپ و دما) به سلیقه شما را دارد؛ قیمت به‌صورت آنی محاسبه می‌شود.',
  },
  {
    key: 'menu.product.note_btn',
    page: 'customer-menu',
    section: 'کارت محصول',
    label: 'راهنمای دکمه «یادداشت» محصول',
    text: 'برای سفارش حضوری در کافه: آیتم را با سایز و گزینه‌های دلخواه به «یادداشت سفارش» اضافه کنید تا موقع خواندن در صندوق چیزی از قلم نیفتد.',
  },
  {
    key: 'menu.product.add_btn',
    page: 'customer-menu',
    section: 'کارت محصول',
    label: 'راهنمای دکمه «افزودن به سبد» آنلاین',
    text: 'برای ثبت و پرداخت آنلاین سفارش و دریافت کد پیگیری.',
  },

  // ۴. پنجره انتخاب و سفارشی‌سازی آیتم
  {
    key: 'menu.modal.sizes',
    page: 'customer-menu',
    section: 'پنجره انتخاب آیتم',
    label: 'انتخاب اندازه (سایز)',
    text: 'اندازه دلخواه خود را انتخاب کنید؛ قیمت بر اساس سایز به‌روز می‌شود.',
  },
  {
    key: 'menu.modal.options',
    page: 'customer-menu',
    section: 'پنجره انتخاب آیتم',
    label: 'گزینه‌های اجباری و دلخواه محصول',
    text: 'بخش‌های «اجباری» برای ثبت آیتم ضروری هستند و بخش‌های «دلخواه» به انتخاب شما اضافه می‌شوند.',
  },
  {
    key: 'menu.modal.builder',
    page: 'customer-menu',
    section: 'پنجره انتخاب آیتم',
    label: 'ساخت نوشیدنی سفارشی (ترکیب دلخواه)',
    text: 'مقدار هر ترکیب را با اسلایدر یا دکمه‌ها تغییر دهید؛ جمع قیمت در لحظه محاسبه می‌شود.',
  },
  {
    key: 'menu.modal.modifiers',
    page: 'customer-menu',
    section: 'پنجره انتخاب آیتم',
    label: 'تغییرات سفارش (کم‌شیرین، یخ کم و...)',
    text: 'تغییرات دلخواه آماده‌سازی برای باریستا را با یک لمس انتخاب کنید.',
  },
  {
    key: 'menu.modal.note_person',
    page: 'customer-menu',
    section: 'پنجره انتخاب آیتم',
    label: 'انتخاب نفر و توضیح در یادداشت سفارش',
    text: 'مشخص کنید این آیتم برای کدام یک از همراهان شماست (مثلاً نفر ۲) یا چه توضیحی دارد تا در لیست یادداشت تفکیک شود.',
  },

  // ۵. یادداشت سفارش حضوری
  {
    key: 'menu.notes.drawer',
    page: 'customer-menu',
    section: 'یادداشت سفارش حضوری',
    label: 'راهنمای پنل یادداشت سفارش حضوری',
    text: 'این دفترچه دیجیتال روی گوشی شما ذخیره می‌شود. سفارش خود و دوستانتان را اینجا جمع کنید و هنگام مراجعه به صندوق از روی «نمای صندوق» بخوانید یا نشان دهید.',
  },
  {
    key: 'menu.notes.counter_mode',
    page: 'customer-menu',
    section: 'یادداشت سفارش حضوری',
    label: 'حالت «نمای صندوق (تجمیعی)»',
    text: 'آیتم‌های کاملاً مشابه را با هم جمع می‌کند و با فونت درشت نمایش می‌دهد تا صندوقدار در یک نگاه ببیند. با لمس هر ردیف می‌توانید آن را تیک بزنید.',
  },
  {
    key: 'menu.notes.person_filter',
    page: 'customer-menu',
    section: 'یادداشت سفارش حضوری',
    label: 'تفکیک و فیلتر بر اساس نفرات',
    text: 'وقتی برای چند نفر آیتم یادداشت کرده‌اید، با زدن روی نام هر نفر می‌توانید فقط سفارش و جمع سهم همان نفر را ببینید.',
  },
  {
    key: 'menu.notes.memo',
    page: 'customer-menu',
    section: 'یادداشت سفارش حضوری',
    label: 'یادداشت کلی (شماره میز یا توضیح صندوق)',
    text: 'شماره میز یا هر نکته کلی که می‌خواهید به صندوقدار بگویید را اینجا بنویسید.',
  },
  {
    key: 'menu.notes.move_to_cart',
    page: 'customer-menu',
    section: 'یادداشت سفارش حضوری',
    label: 'انتقال یادداشت به سبد خرید آنلاین',
    text: 'اگر تصمیم گرفتید به‌جای مراجعه به صندوق، سفارش را آنلاین پرداخت کنید، همه آیتم‌های یادداشت با یک لمس وارد سبد خرید می‌شوند.',
  },
  {
    key: 'menu.notes.copy',
    page: 'customer-menu',
    section: 'یادداشت سفارش حضوری',
    label: 'کپی متن سفارش برای اشتراک‌گذاری',
    text: 'متن مرتب‌شده لیست سفارش و جمع قیمت را کپی می‌کند تا در تلگرام یا واتساپ برای دوستانتان بفرستید.',
  },

  // ۶. سبد خرید و تسویه حساب
  {
    key: 'menu.checkout.tier_progress',
    page: 'customer-menu',
    section: 'سبد خرید و تسویه حساب',
    label: 'نوار پیشرفت تخفیف پلکانی سبد',
    text: 'با رسیدن جمع سبد خرید به سقف مشخص‌شده، تخفیف پلکانی به‌صورت خودکار روی سفارش شما اعمال می‌شود.',
  },
  {
    key: 'menu.checkout.split_bill',
    page: 'customer-menu',
    section: 'سبد خرید و تسویه حساب',
    label: 'تسویه جداگانه (تقسیم دنگ بین مهمان‌ها)',
    text: 'سفارش مشترک میز را بر اساس آیتم، درصد یا مبلغ دلخواه بین دوستان تقسیم کنید تا هر کس سهم خود را جداگانه پرداخت کند.',
  },
  {
    key: 'menu.checkout.pickup_time',
    page: 'customer-menu',
    section: 'سبد خرید و تسویه حساب',
    label: 'انتخاب زمان تحویل حضوری',
    text: 'می‌توانید سفارش را برای «در اسرع وقت» یا یک بازه زمانی مشخص از قبل رزرو کنید تا دقیقاً موقع رسیدنتان آماده باشد.',
  },
  {
    key: 'menu.checkout.wallet',
    page: 'customer-menu',
    section: 'سبد خرید و تسویه حساب',
    label: 'پرداخت با کیف پول',
    text: 'از موجودی کیف پول و کش‌بک‌های قبلی خود برای پرداخت سریع سفارش استفاده کنید.',
  },
  {
    key: 'menu.checkout.personal_cup',
    page: 'customer-menu',
    section: 'سبد خرید و تسویه حساب',
    label: 'تخفیف لیوان شخصی',
    text: 'اگر ماگ یا لیوان شخصی همراه دارید این گزینه را فعال کنید تا تخفیف حامی محیط‌زیست کسر شود.',
  },
  {
    key: 'menu.checkout.subscription',
    page: 'customer-menu',
    section: 'سبد خرید و تسویه حساب',
    label: 'استفاده از اشتراک ماهانه',
    text: 'مشتریان دارای بسته اشتراک فعال می‌توانند سهمیه روزانه خود را روی سفارش اعمال کنند.',
  },

  // ۷. پروفایل و باشگاه مشتریان
  {
    key: 'menu.profile.stamps',
    page: 'customer-menu',
    section: 'پروفایل و باشگاه مشتریان',
    label: 'کارت مهر دیجیتال',
    text: 'با هر سفارش پرداخت‌شده یک مهر دیجیتال دریافت می‌کنید؛ با تکمیل کارت، جایزه تخفیف سفارش بعدی فعال می‌شود.',
  },
  {
    key: 'menu.profile.tier',
    page: 'customer-menu',
    section: 'پروفایل و باشگاه مشتریان',
    label: 'سطح عضویت و تخفیف وفاداری',
    text: 'بر اساس مجموع خریدهای شما، سطح عضویت ارتقا یافته و تخفیف دائمی روی همه سفارش‌ها اعمال می‌شود.',
  },
  {
    key: 'menu.profile.wallet',
    page: 'customer-menu',
    section: 'پروفایل و باشگاه مشتریان',
    label: 'کیف پول و کش‌بک در پروفایل',
    text: 'درصدی از مبلغ هر خرید به‌عنوان کش‌بک به کیف پول شما برمی‌گردد و در خریدهای بعدی قابل استفاده است.',
  },

  // ── صندوق فروش (POS) ─────────────────────────────────────────
  { key: 'pos.search', page: 'pos', label: 'جستجوی محصولات', text: 'جستجوی سریع با نام فارسی یا انگلیسی محصول. کلید میانبر: /' },
  { key: 'pos.categories', page: 'pos', label: 'دسته‌بندی‌ها', text: 'فیلتر محصولات بر اساس دسته‌بندی منو. «همه» همهٔ محصولات قابل فروش را نشان می‌دهد.' },
  { key: 'pos.product_add', page: 'pos', label: 'افزودن محصول', text: 'افزودن سریع به سبد. اگر محصول سایز یا افزودنی داشته باشد، پنجرهٔ انتخاب باز می‌شود.' },
  { key: 'pos.service_toggle', page: 'pos', label: 'نوع سرو (سالن / بیرون‌بر)', text: 'نحوهٔ سرو سفارش را تعیین می‌کند؛ روی فیش چاپی، انتخاب میز و ثبت فروش هم اثر دارد.' },
  { key: 'pos.delivery_select', page: 'pos', label: 'روش تحویل', text: 'برای بیرون‌بر: اگر سفارش با سرویس تحویل ارسال می‌شود انتخاب کنید؛ هزینه تحویل به جمع کل اضافه می‌شود.' },
  { key: 'pos.customer_name', page: 'pos', label: 'نام مشتری', text: 'نام مشتری روی فیش چاپ می‌شود. اگر خالی بماند «مهمان» ثبت می‌شود.' },
  { key: 'pos.customer_phone', page: 'pos', label: 'شماره تماس', text: 'اختیاری. با شماره تماس مشتری در باشگاه مشتریان شناسایی و سوابق فروش به او متصل می‌شود.' },
  { key: 'pos.table_input', page: 'pos', label: 'میز سفارش', text: 'میز انتخابی سفارش داخل سالن. تغییر میز از پنل «میزها» انجام می‌شود.' },
  { key: 'pos.order_note', page: 'pos', label: 'یادداشت سفارش', text: 'یادداشت کلی سفارش — روی فیش آشپزخانه چاپ می‌شود.' },
  { key: 'pos.personal_cup', page: 'pos', label: 'لیوان شخصی', text: 'برای مشتری‌هایی که لیوان خودشان را می‌آورند؛ تخفیف لیوان روی جمع کل اعمال می‌شود.' },
  { key: 'pos.use_subscription', page: 'pos', label: 'اشتراک ماهانه', text: 'به‌جای پرداخت، واحد اشتراک ماهانهٔ مشتری مصرف می‌شود.' },
  { key: 'pos.split_bill', page: 'pos', label: 'تسویه جداگانه', text: 'برای میز چندنفره: هر کس فقط سهم خودش را پرداخت می‌کند (تقسیم بر آیتم، درصد یا مبلغ).' },
  { key: 'pos.side_panels', page: 'pos', label: 'پنل‌های کنار سبد', text: 'جابه‌جایی بین سبد خرید، انتخاب میز، سفارش‌های نگه‌داشته و سفارش‌های اخیر.' },
  { key: 'pos.cart_note', page: 'pos', label: 'توضیح باریستا', text: 'توضیح فقط برای همین آیتم — روی تیکت آشپزخانه چاپ می‌شود.' },
  { key: 'pos.cart_qty', page: 'pos', label: 'تعداد آیتم', text: 'افزایش / کاهش تعداد. رسیدن به صفر، آیتم را از سبد حذف می‌کند.' },
  { key: 'pos.tables_grid', page: 'pos', label: 'نقشهٔ میزها', text: 'میزهای اشغال با نوار طلایی مشخص شده‌اند. انتخاب میز، سفارش را به حالت داخل سالن می‌برد.' },
  { key: 'pos.held_orders', page: 'pos', label: 'سفارش‌های نگه‌داشته', text: 'سفارش‌هایی که هنوز پرداخت نشده‌اند — «ادامه» برای تکمیل، «کپی» برای سفارش جدید از روی آن.' },
  { key: 'pos.recent_orders', page: 'pos', label: 'سفارش‌های اخیر', text: 'آخرین سفارش‌های ثبت‌شده — بارگذاری برای ویرایش یا سفارش مجدد.' },
  { key: 'pos.hold_button', page: 'pos', label: 'نگه‌داشت (F2)', text: 'سفارش بدون پرداخت ذخیره می‌شود تا بعداً (مثلاً بعد از سرو قهوه) تسویه شود.' },
  { key: 'pos.clear_button', page: 'pos', label: 'پاک کردن / حذف', text: 'سبد جدید را خالی می‌کند؛ در سفارش باز، سفارش را حذف می‌کند (نیاز به مجوز حذف).' },
  { key: 'pos.cash_button', page: 'pos', label: 'پرداخت نقدی (F4)', text: 'پرداخت نقدی را ثبت و دو فیش چاپ (مشتری + آشپزخانه) به صف چاپ ارسال می‌کند.' },
  { key: 'pos.card_button', page: 'pos', label: 'پرداخت کارتی (F5)', text: 'پرداخت کارتی را ثبت و دو فیش چاپ (مشتری + آشپزخانه) به صف چاپ ارسال می‌کند.' },
  { key: 'pos.totals', page: 'pos', label: 'جمع کل', text: 'جمع نهایی سفارش شامل مالیات ارزش افزوده و هزینه تحویل.' },
  { key: 'pos.refresh', page: 'pos', label: 'تازه‌سازی', text: 'منو، میزها و سفارش‌های اخیر را از سرور دوباره می‌خواند.' },

  // ── راهنماهای صفحه‌ها ───────────────────────────────────────
  { key: 'tooltips-page.search', page: 'tooltips-page', label: 'جستجوی راهنماها', text: 'بین برچسب و متن همهٔ راهنماهای پنل و منوی مشتری جست‌وجو می‌کند.' },

  // ── آیتم‌های منو ─────────────────────────────────────────────
  { key: 'menu-items.base_price', page: 'menu-items', label: 'قیمت پایه (بدون سایز)', text: 'برای نوشیدنی‌هایی که سایز ندارند (مثل اسپرسو ۳۵ تا ۴۰ میلی‌لیتری) فقط همین قیمت را پر کنید و سایزها را خالی بگذارید.' },

  // ── انبارها ─────────────────────────────────────────────────
  { key: 'warehouses.list', page: 'warehouses', label: 'فهرست انبارها', text: 'همهٔ انبارها و انبارک‌ها. موجودی انبار اصلی سیستم از صفحهٔ «انبار موجودی» مدیریت می‌شود و دست‌نخورده می‌ماند.' },
  { key: 'warehouses.name', page: 'warehouses', label: 'نام انبار', text: 'نام فارسی انبار که در فهرست و گزارش‌ها نمایش داده می‌شود؛ نام‌ها نباید تکراری باشند.' },
  { key: 'warehouses.parent', page: 'warehouses', label: 'نوع (انبار / انبارک)', text: '«انبار اصلی» مستقل است؛ انتخاب یک انبار، این ردیف را به انبارک زیرمجموعهٔ همان انبار تبدیل می‌کند.' },
  { key: 'warehouses.active', page: 'warehouses', label: 'فعال', text: 'انبار غیرفعال از انتخاب‌کننده‌ها پنهان می‌شود ولی تراکنش‌های قبلی حفظ می‌مانند.' },
  { key: 'warehouses.tree', page: 'warehouses', label: 'درخت انبارها', text: 'انبارک‌ها با نشانگر ↳ زیر انبار والدشان نمایش داده می‌شوند. جستجو، فهرست را ساده و تخت می‌کند.' },
  { key: 'warehouses.stock', page: 'warehouses', label: 'موجودی انبار', text: 'موجودی مواد اولیهٔ متصل به همین انبار — جدا از موجودی انبار اصلی.' },
  { key: 'warehouses.link', page: 'warehouses', label: 'اتصال ماده', text: 'مادهٔ اولیه را به این انبار متصل و موجودی اولیه را ثبت می‌کند.' },
  { key: 'warehouses.restock', page: 'warehouses', label: 'شارژ', text: 'افزودن به موجودی این انبار. تراکنش با نام انبار در تاریخچهٔ موجودی ثبت می‌شود.' },
  { key: 'warehouses.adjust', page: 'warehouses', label: 'تعدیل', text: 'اصلاح دستی موجودی (عدد منفی = کاهش، مثلاً اتلاف یا حذف حسابی).' },
  { key: 'warehouses.threshold', page: 'warehouses', label: 'آستانهٔ هشدار', text: 'اگر موجودی کمتر از این عدد شود، ردیف با رنگ هشدار مشخص می‌شود.' },

  // ── لاگ‌های سیستمی ──────────────────────────────────────────
  { key: 'system-logs.files', page: 'system-logs', label: 'فایل‌های لاگ', text: 'هر فایل یک روز لاگ سرور است (جدیدترین اول). اعداد کنار هر فایل، تعداد خطاها و هشدارهای همان روز را نشان می‌دهد.' },
  { key: 'system-logs.levels', page: 'system-logs', label: 'سطح لاگ', text: 'خطا = مشکل واقعی، هشدار = قابل توجه، اطلاعات = رویداد عادی، دیباگ = جزئیات فنی. برای یافتن مشکل، روی «خطا» فیلتر کنید.' },
  { key: 'system-logs.categories', page: 'system-logs', label: 'دسته', text: 'منبع رویداد: درخواست HTTP، اپلیکیشن شوگریستا، فریم‌ورک و… برای جداسازی لاگ‌های مربوط به هر بخش.' },
  { key: 'system-logs.search', page: 'system-logs', label: 'جستجو', text: 'متن جستجو در پیام، جزئیات خطا و منبع رویداد بررسی می‌شود. مثلاً شماره سفارش یا نام یک endpoint را بنویسید.' },
  { key: 'system-logs.detail', page: 'system-logs', label: 'جزئیات ردیف', text: 'کلیک روی هر ردیف، پیام کامل، منبع، جزئیات خطا (Exception) و اطلاعات فنی ردیف را باز می‌کند.' },
  { key: 'system-logs.delete_file', page: 'system-logs', label: 'حذف فایل لاگ', text: 'فایل لاگ برای همیشه از دیسک حذف می‌شود و قابل بازیابی نیست. فایل روز جاری برای ادامهٔ ثبت لاگ حفظ می‌شود.' },
]

const registryByKey = new Map(TOOLTIP_REGISTRY.map((entry) => [entry.key, entry]))

export function tooltipDefault(key) {
  return registryByKey.get(key)?.text ?? ''
}

/** All registry entries of one page, in registration order. */
export function tooltipKeysForPage(page) {
  return TOOLTIP_REGISTRY.filter((entry) => entry.page === page)
}

export function tooltipPageLabel(page) {
  return TOOLTIP_PAGES[page] ?? page
}
