export const UNIT_OPTIONS = ['ml', 'l', 'gr', 'kg', 'oz', 'tsp', 'tbsp', 'cup', 'pieces']

export const LOCATION_OPTIONS = [
  { value: 'main', label: 'انبار اصلی' },
  { value: 'bar', label: 'بار' },
  { value: 'kitchen', label: 'آشپزخانه' },
  { value: 'dry', label: 'انبار خشک' },
  { value: 'refrigerator', label: 'یخچال' },
]

export const emptyRestockForm = { quantity: '', note: '' }
export const emptyAdjustForm = { delta: '', note: '' }
export const emptyAlertForm = { minAlertQuantity: '' }
export const emptySaleForm = { menuItemId: '', quantitySold: 1 }

export const emptyCategoryForm = {
  id: '',
  slug: '',
  name: '',
  displayOrder: 0,
  isActive: true,
}

export const emptyMenuItemForm = {
  id: '',
  categoryId: '',
  persianName: '',
  englishName: '',
  description: '',
  imageUrl: '',
  galleryImageUrls: '',
  specialOfferPrice: '',
  taxPercent: 0,
  isOrderable: true,
  preparingDurationMinutes: '',
  calories: '',
  allergens: '',
  volume: '',
  smallPrice: '',
  smallSpecialOfferPrice: '',
  smallVolume: '',
  mediumPrice: '',
  mediumSpecialOfferPrice: '',
  mediumVolume: '',
  largePrice: '',
  largeSpecialOfferPrice: '',
  largeVolume: '',
  tags: '',
  badge: '',
  featured: false,
  accent: 'linear-gradient(135deg, #5b3425, #8a5339)',
  shortLabel: '',
  displayOrder: 0,
  isActive: true,
}

export const emptyAddonForm = {
  id: '',
  persianName: '',
  englishName: '',
  description: '',
  price: 0,
  tags: '',
  displayOrder: 0,
  isActive: true,
}

export const emptyModifierForm = {
  id: '',
  persianName: '',
  englishName: '',
  displayOrder: 0,
  isActive: true,
}

export const emptyIngredientForm = {
  id: '',
  persianName: '',
  englishName: '',
  unit: 'ml',
  description: '',
  displayOrder: 0,
  isActive: true,
  inventoryLocation: 'main',
  unitCost: '',
}

export const emptyRecipeLineForm = {
  ingredientId: '',
  quantity: '',
}

export const emptyRecipeState = {
  selectedMenuItemId: '',
  description: '',
  lines: [],
  lineForm: emptyRecipeLineForm,
}

export const emptySalonTableForm = {
  id: '',
  name: '',
  capacity: 2,
  displayOrder: 0,
  isActive: true,
}

export const emptyOccupyForm = {
  tableId: '',
  peopleCount: 1,
  note: '',
}

export const emptySalonStatus = {
  timestamp: '',
  totalTables: 0,
  occupiedTables: 0,
  freeTables: 0,
  currentCustomers: 0,
  tables: [],
}

export const emptySettings = {
  id: '',
  cafeName: '',
  browserTitle: '',
  metaDescription: '',
  eyebrow: '',
  brandTitle: '',
  brandSubtitle: '',
  heroBadgeText: '',
  heroTitle: '',
  heroDescription: '',
  heroPrimaryButtonLabel: '',
  heroSecondaryButtonLabel: '',
  heroHours: '',
  howToOrderLabel: '',
  howToOrderTitle: '',
  howToOrderStatus: '',
  stepOneTitle: '',
  stepOneDescription: '',
  stepTwoTitle: '',
  stepTwoDescription: '',
  stepThreeTitle: '',
  stepThreeDescription: '',
  quickAccessLabel: '',
  quickAccessTitle: '',
  quickAccessDescription: '',
  popularLabel: '',
  popularTitle: '',
  popularDescription: '',
  menuLabel: '',
  menuTitle: '',
  menuToolbarHint: '',
  searchPlaceholder: '',
  visitLabel: '',
  visitTitle: '',
  visitDescription: '',
  contactPhone: '',
  address: '',
  breakfastHours: '',
  takeawayStatus: '',
  instagramUrl: '',
  footerText: '',
  footerPopularLabel: '',
  footerMenuLabel: '',
  footerCallLabel: '',
}

export const settingsSections = [
  {
    title: 'برند و هویت',
    fields: [
      ['cafeName', 'نام کافه'],
      ['browserTitle', 'عنوان مرورگر'],
      ['metaDescription', 'متن توضیحات متا', 'textarea'],
      ['eyebrow', 'متن بالای عنوان'],
      ['brandTitle', 'عنوان اصلی برند'],
      ['brandSubtitle', 'زیرعنوان برند'],
    ],
  },
  {
    title: 'بخش هیرو',
    fields: [
      ['heroBadgeText', 'متن نشان هیرو'],
      ['heroTitle', 'عنوان هیرو'],
      ['heroDescription', 'توضیح هیرو', 'textarea'],
      ['heroPrimaryButtonLabel', 'متن دکمه اصلی'],
      ['heroSecondaryButtonLabel', 'متن دکمه دوم'],
      ['heroHours', 'ساعت کاری'],
    ],
  },
  {
    title: 'مراحل سفارش',
    fields: [
      ['howToOrderLabel', 'برچسب مراحل سفارش'],
      ['howToOrderTitle', 'عنوان مراحل سفارش'],
      ['howToOrderStatus', 'متن وضعیت'],
      ['stepOneTitle', 'عنوان مرحله ۱'],
      ['stepOneDescription', 'توضیح مرحله ۱', 'textarea'],
      ['stepTwoTitle', 'عنوان مرحله ۲'],
      ['stepTwoDescription', 'توضیح مرحله ۲', 'textarea'],
      ['stepThreeTitle', 'عنوان مرحله ۳'],
      ['stepThreeDescription', 'توضیح مرحله ۳', 'textarea'],
    ],
  },
  {
    title: 'بخش‌های محتوایی',
    fields: [
      ['quickAccessLabel', 'برچسب دسترسی سریع'],
      ['quickAccessTitle', 'عنوان دسترسی سریع'],
      ['quickAccessDescription', 'توضیح دسترسی سریع', 'textarea'],
      ['popularLabel', 'برچسب پرفروش‌ها'],
      ['popularTitle', 'عنوان پرفروش‌ها'],
      ['popularDescription', 'توضیح پرفروش‌ها', 'textarea'],
      ['menuLabel', 'برچسب منو'],
      ['menuTitle', 'عنوان منو'],
      ['menuToolbarHint', 'راهنمای نوار منو'],
      ['searchPlaceholder', 'متن جستجو'],
      ['visitLabel', 'برچسب اطلاعات کافه'],
      ['visitTitle', 'عنوان اطلاعات کافه'],
      ['visitDescription', 'توضیح اطلاعات کافه', 'textarea'],
    ],
  },
  {
    title: 'تماس و فوتر',
    fields: [
      ['contactPhone', 'شماره تماس', 'tel'],
      ['address', 'آدرس', 'textarea'],
      ['breakfastHours', 'ساعت صبحانه'],
      ['takeawayStatus', 'وضعیت بیرون‌بر'],
      ['instagramUrl', 'لینک اینستاگرام', 'url'],
      ['footerText', 'متن فوتر'],
      ['footerPopularLabel', 'متن لینک پرفروش‌ها'],
      ['footerMenuLabel', 'متن لینک منو'],
      ['footerCallLabel', 'متن لینک تماس'],
    ],
  },
]
