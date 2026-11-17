/**
 * Single source of truth for admin-panel tooltips.
 *
 * Every tooltip bubble on the panel is keyed here (`key` is stable and never
 * changes once shipped — it is the DB join with AdminTooltips overrides).
 * The admin manages overrides (custom text + on/off) from the
 * «راهنماهای صفحه‌ها» page; no row in the DB means the default text below
 * is shown.
 *
 * When adding a new Tip to a page, register its key here in the matching
 * page group.
 */

export const TOOLTIP_PAGES = {
  pos: 'صندوق فروش',
  'tooltips-page': 'راهنماهای صفحه‌ها',
  warehouses: 'انبارها',
}

export const TOOLTIP_REGISTRY = [
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
  { key: 'tooltips-page.search', page: 'tooltips-page', label: 'جستجوی راهنماها', text: 'بین برچسب و متن همهٔ راهنماهای پنل جست‌وجو می‌کند.' },

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
