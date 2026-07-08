import { useEffect, useMemo, useState } from 'react'
import './App.css'

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? '/api'
const SESSION_STORAGE_KEY = 'sugarista.admin.session'

const tabOptions = [
  { id: 'overview', label: 'نمای کلی' },
  { id: 'categories', label: 'دسته‌بندی‌ها' },
  { id: 'menu-items', label: 'آیتم‌های منو' },
  { id: 'site-settings', label: 'تنظیمات سایت' },
]

const emptyCategoryForm = {
  id: '',
  slug: '',
  name: '',
  displayOrder: 0,
  isActive: true,
}

const emptyMenuItemForm = {
  id: '',
  categoryId: '',
  name: '',
  description: '',
  price: 0,
  tags: '',
  badge: '',
  featured: false,
  accent: 'linear-gradient(135deg, #5b3425, #8a5339)',
  shortLabel: '',
  displayOrder: 0,
  isActive: true,
}

const emptySettings = {
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

const settingsSections = [
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

function loadStoredSession() {
  try {
    const rawValue = window.localStorage.getItem(SESSION_STORAGE_KEY)
    return rawValue ? JSON.parse(rawValue) : null
  } catch {
    return null
  }
}

function splitTags(tagValue) {
  return tagValue
    .split(',')
    .map((tag) => tag.trim())
    .filter(Boolean)
}

function joinTags(tags) {
  return tags.join('، ')
}

function toPersianNumber(value) {
  return new Intl.NumberFormat('fa-IR').format(value)
}

function getReadableError(error) {
  const message = error instanceof Error ? error.message : 'خطای ناشناخته'

  if (message === 'Unauthorized.' || message.includes('401')) {
    return 'نشست شما منقضی شده یا دسترسی ندارید. دوباره وارد شوید.'
  }

  if (message === 'Invalid username or password.') {
    return 'نام کاربری یا رمز عبور نادرست است.'
  }

  if (message === 'Resource not found.') {
    return 'آیتم موردنظر پیدا نشد.'
  }

  return message
}

async function apiRequest(path, { method = 'GET', body, token } = {}) {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    method,
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
    body: body ? JSON.stringify(body) : undefined,
  })

  if (!response.ok) {
    let message = `درخواست با خطای ${response.status} مواجه شد.`

    try {
      const payload = await response.json()
      message =
        payload.detail ||
        payload.title ||
        (payload.errors ? Object.values(payload.errors).flat().join('، ') : message)
    } catch {
      // Ignore invalid JSON payloads and keep the fallback message.
    }

    throw new Error(message)
  }

  if (response.status === 204) {
    return null
  }

  return response.json()
}

function App() {
  const [activeTab, setActiveTab] = useState('overview')
  const [session, setSession] = useState(loadStoredSession)
  const [loginForm, setLoginForm] = useState({ username: 'admin', password: 'ChangeMe123!' })
  const [categories, setCategories] = useState([])
  const [menuItems, setMenuItems] = useState([])
  const [siteSettings, setSiteSettings] = useState(emptySettings)
  const [categoryForm, setCategoryForm] = useState(emptyCategoryForm)
  const [menuItemForm, setMenuItemForm] = useState(emptyMenuItemForm)
  const [loading, setLoading] = useState(false)
  const [saving, setSaving] = useState(false)
  const [message, setMessage] = useState('')
  const [error, setError] = useState('')

  const featuredCount = useMemo(
    () => menuItems.filter((item) => item.featured && item.isActive).length,
    [menuItems],
  )

  useEffect(() => {
    document.documentElement.lang = 'fa'
    document.documentElement.dir = 'rtl'
    document.title = 'پنل مدیریت شوگریستا'
  }, [])

  useEffect(() => {
    if (session?.token) {
      window.localStorage.setItem(SESSION_STORAGE_KEY, JSON.stringify(session))
      loadAdminData(session.token)
      return
    }

    window.localStorage.removeItem(SESSION_STORAGE_KEY)
  }, [session])

  async function loadAdminData(token = session?.token) {
    if (!token) {
      return
    }

    setLoading(true)
    setError('')

    try {
      const [categoryData, itemData, settingsData] = await Promise.all([
        apiRequest('/admin/categories', { token }),
        apiRequest('/admin/menu-items', { token }),
        apiRequest('/admin/site-settings', { token }),
      ])

      setCategories(categoryData)
      setMenuItems(itemData)
      setSiteSettings(settingsData)
      setCategoryForm((currentForm) =>
        currentForm.id ? currentForm : { ...emptyCategoryForm, displayOrder: categoryData.length },
      )
      setMenuItemForm((currentForm) =>
        currentForm.id
          ? currentForm
          : {
              ...emptyMenuItemForm,
              categoryId: categoryData[0]?.id ?? '',
              displayOrder: itemData.length,
            },
      )
    } catch (requestError) {
      if (requestError.message.includes('401')) {
        setSession(null)
      }

      setError(getReadableError(requestError))
    } finally {
      setLoading(false)
    }
  }

  async function handleLogin(event) {
    event.preventDefault()
    setSaving(true)
    setError('')
    setMessage('')

    try {
      const nextSession = await apiRequest('/auth/login', {
        method: 'POST',
        body: loginForm,
      })

      setSession(nextSession)
      setMessage('ورود با موفقیت انجام شد.')
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setSaving(false)
    }
  }

  function handleLogout() {
    setSession(null)
    setCategories([])
    setMenuItems([])
    setSiteSettings(emptySettings)
    setCategoryForm(emptyCategoryForm)
    setMenuItemForm(emptyMenuItemForm)
    setMessage('از حساب کاربری خارج شدید.')
  }

  async function handleCategorySubmit(event) {
    event.preventDefault()
    setSaving(true)
    setError('')
    setMessage('')

    try {
      const path = categoryForm.id
        ? `/admin/categories/${categoryForm.id}`
        : '/admin/categories'

      await apiRequest(path, {
        method: categoryForm.id ? 'PUT' : 'POST',
        token: session.token,
        body: {
          slug: categoryForm.slug,
          name: categoryForm.name,
          displayOrder: Number(categoryForm.displayOrder),
          isActive: categoryForm.isActive,
        },
      })

      setCategoryForm({ ...emptyCategoryForm, displayOrder: categories.length })
      setMessage('دسته‌بندی با موفقیت ذخیره شد.')
      await loadAdminData()
      setActiveTab('categories')
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setSaving(false)
    }
  }

  async function handleDeleteCategory(categoryId) {
    if (!window.confirm('از حذف این دسته‌بندی مطمئن هستید؟')) {
      return
    }

    setSaving(true)
    setError('')
    setMessage('')

    try {
      await apiRequest(`/admin/categories/${categoryId}`, {
        method: 'DELETE',
        token: session.token,
      })

      if (categoryForm.id === categoryId) {
        setCategoryForm(emptyCategoryForm)
      }

      setMessage('دسته‌بندی حذف شد.')
      await loadAdminData()
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setSaving(false)
    }
  }

  async function handleMenuItemSubmit(event) {
    event.preventDefault()
    setSaving(true)
    setError('')
    setMessage('')

    try {
      const path = menuItemForm.id
        ? `/admin/menu-items/${menuItemForm.id}`
        : '/admin/menu-items'

      await apiRequest(path, {
        method: menuItemForm.id ? 'PUT' : 'POST',
        token: session.token,
        body: {
          categoryId: menuItemForm.categoryId,
          name: menuItemForm.name,
          description: menuItemForm.description,
          price: Number(menuItemForm.price),
          tags: splitTags(menuItemForm.tags),
          badge: menuItemForm.badge.trim() || null,
          featured: menuItemForm.featured,
          accent: menuItemForm.accent,
          shortLabel: menuItemForm.shortLabel,
          displayOrder: Number(menuItemForm.displayOrder),
          isActive: menuItemForm.isActive,
        },
      })

      setMenuItemForm({
        ...emptyMenuItemForm,
        categoryId: categories[0]?.id ?? '',
        displayOrder: menuItems.length,
      })
      setMessage('آیتم منو با موفقیت ذخیره شد.')
      await loadAdminData()
      setActiveTab('menu-items')
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setSaving(false)
    }
  }

  async function handleDeleteMenuItem(itemId) {
    if (!window.confirm('از حذف این آیتم منو مطمئن هستید؟')) {
      return
    }

    setSaving(true)
    setError('')
    setMessage('')

    try {
      await apiRequest(`/admin/menu-items/${itemId}`, {
        method: 'DELETE',
        token: session.token,
      })

      if (menuItemForm.id === itemId) {
        setMenuItemForm({
          ...emptyMenuItemForm,
          categoryId: categories[0]?.id ?? '',
          displayOrder: Math.max(menuItems.length - 1, 0),
        })
      }

      setMessage('آیتم منو حذف شد.')
      await loadAdminData()
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setSaving(false)
    }
  }

  async function handleSettingsSubmit(event) {
    event.preventDefault()
    setSaving(true)
    setError('')
    setMessage('')

    try {
      await apiRequest('/admin/site-settings', {
        method: 'PUT',
        token: session.token,
        body: { ...siteSettings },
      })

      setMessage('تنظیمات سایت با موفقیت ذخیره شد.')
      await loadAdminData()
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setSaving(false)
    }
  }

  function editCategory(category) {
    setCategoryForm({
      id: category.id,
      slug: category.slug,
      name: category.name,
      displayOrder: category.displayOrder,
      isActive: category.isActive,
    })
    setActiveTab('categories')
  }

  function editMenuItem(item) {
    setMenuItemForm({
      id: item.id,
      categoryId: item.categoryId,
      name: item.name,
      description: item.description,
      price: item.price,
      tags: joinTags(item.tags),
      badge: item.badge ?? '',
      featured: item.featured,
      accent: item.accent,
      shortLabel: item.shortLabel,
      displayOrder: item.displayOrder,
      isActive: item.isActive,
    })
    setActiveTab('menu-items')
  }

  function resetCategoryForm() {
    setCategoryForm({
      ...emptyCategoryForm,
      displayOrder: categories.length,
    })
  }

  function resetMenuItemForm() {
    setMenuItemForm({
      ...emptyMenuItemForm,
      categoryId: categories[0]?.id ?? '',
      displayOrder: menuItems.length,
    })
  }

  if (!session?.token) {
    return (
      <main className="login-shell">
        <section className="login-card">
          <p className="eyebrow">پنل مدیریت شوگریستا</p>
          <h1>مدیریت کامل رابط کاربری کافه</h1>
          <p className="login-copy">
            برای مدیریت دسته‌بندی‌ها، آیتم‌های منو و محتوای عمومی منوی QR وارد شوید.
          </p>
          <form className="login-form" onSubmit={handleLogin}>
            <label>
              <span>نام کاربری</span>
              <input
                className="ltr-input"
                value={loginForm.username}
                onChange={(event) =>
                  setLoginForm((currentForm) => ({ ...currentForm, username: event.target.value }))
                }
                autoComplete="username"
              />
            </label>
            <label>
              <span>رمز عبور</span>
              <input
                className="ltr-input"
                type="password"
                value={loginForm.password}
                onChange={(event) =>
                  setLoginForm((currentForm) => ({ ...currentForm, password: event.target.value }))
                }
                autoComplete="current-password"
              />
            </label>
            <button type="submit" disabled={saving}>
              {saving ? 'در حال ورود...' : 'ورود'}
            </button>
          </form>
          {error ? <p className="feedback feedback-error">{error}</p> : null}
          <p className="login-hint">
            حساب پیش‌فرض: <code>admin</code> / <code>ChangeMe123!</code>
          </p>
        </section>
      </main>
    )
  }

  return (
    <div className="admin-shell">
      <aside className="sidebar">
        <div>
          <p className="eyebrow">پنل مدیریت شوگریستا</p>
          <h1>کنترل محتوا</h1>
          <p className="sidebar-copy">
            از اینجا می‌توانید منوی QR، آیتم‌های ویژه و تمام متن‌های عمومی سایت را مدیریت کنید.
          </p>
        </div>

        <nav className="tab-list" aria-label="ناوبری مدیریت">
          {tabOptions.map((tabOption) => (
            <button
              key={tabOption.id}
              type="button"
              className={tabOption.id === activeTab ? 'tab-button tab-button-active' : 'tab-button'}
              onClick={() => setActiveTab(tabOption.id)}
            >
              {tabOption.label}
            </button>
          ))}
        </nav>

        <div className="session-card">
          <p className="session-title">{session.displayName}</p>
          <p className="session-copy ltr-text">@{session.username}</p>
          <button type="button" className="ghost-button" onClick={handleLogout}>
            خروج
          </button>
        </div>
      </aside>

      <main className="content">
        <header className="topbar">
          <div>
            <p className="eyebrow">آدرس API متصل</p>
            <h2 className="ltr-text">{API_BASE_URL}</h2>
          </div>
          <button type="button" className="ghost-button" onClick={() => loadAdminData()}>
            تازه‌سازی اطلاعات
          </button>
        </header>

        {message ? <p className="feedback feedback-success">{message}</p> : null}
        {error ? <p className="feedback feedback-error">{error}</p> : null}
        {loading ? <p className="feedback">در حال بارگذاری جدیدترین اطلاعات...</p> : null}

        {activeTab === 'overview' ? (
          <section className="panel-stack">
            <section className="stat-grid">
              <article className="stat-card">
                <span>تعداد دسته‌بندی‌ها</span>
                <strong>{toPersianNumber(categories.length)}</strong>
              </article>
              <article className="stat-card">
                <span>آیتم‌های فعال منو</span>
                <strong>{toPersianNumber(menuItems.filter((item) => item.isActive).length)}</strong>
              </article>
              <article className="stat-card">
                <span>آیتم‌های ویژه</span>
                <strong>{toPersianNumber(featuredCount)}</strong>
              </article>
            </section>

            <section className="panel">
              <h3>اقدام‌های سریع</h3>
              <div className="quick-actions">
                <button type="button" onClick={() => setActiveTab('categories')}>
                  افزودن دسته‌بندی
                </button>
                <button type="button" onClick={() => setActiveTab('menu-items')}>
                  افزودن آیتم منو
                </button>
                <button type="button" onClick={() => setActiveTab('site-settings')}>
                  ویرایش متن‌های عمومی
                </button>
              </div>
            </section>

            <section className="panel">
              <h3>خلاصه محتوای زنده</h3>
              <div className="snapshot-grid">
                <div>
                  <span className="field-label">نام کافه</span>
                  <strong>{siteSettings.cafeName}</strong>
                </div>
                <div>
                  <span className="field-label">عنوان هیرو</span>
                  <strong>{siteSettings.heroTitle}</strong>
                </div>
                <div>
                  <span className="field-label">شماره تماس</span>
                  <strong className="ltr-text">{siteSettings.contactPhone}</strong>
                </div>
              </div>
            </section>
          </section>
        ) : null}

        {activeTab === 'categories' ? (
          <section className="two-column-layout">
            <form className="panel form-panel" onSubmit={handleCategorySubmit}>
              <div className="panel-head">
                <div>
                  <h3>{categoryForm.id ? 'ویرایش دسته‌بندی' : 'دسته‌بندی جدید'}</h3>
                  <p>دسته‌بندی‌ها فیلترها و گروه‌بندی منوی عمومی را کنترل می‌کنند.</p>
                </div>
                <button type="button" className="ghost-button" onClick={resetCategoryForm}>
                  پاک کردن فرم
                </button>
              </div>
              <label>
                <span>اسلاگ انگلیسی</span>
                <input
                  className="ltr-input"
                  value={categoryForm.slug}
                  onChange={(event) =>
                    setCategoryForm((currentForm) => ({ ...currentForm, slug: event.target.value }))
                  }
                />
              </label>
              <label>
                <span>نام دسته‌بندی</span>
                <input
                  value={categoryForm.name}
                  onChange={(event) =>
                    setCategoryForm((currentForm) => ({ ...currentForm, name: event.target.value }))
                  }
                />
              </label>
              <label>
                <span>ترتیب نمایش</span>
                <input
                  className="ltr-input"
                  type="number"
                  value={categoryForm.displayOrder}
                  onChange={(event) =>
                    setCategoryForm((currentForm) => ({
                      ...currentForm,
                      displayOrder: Number(event.target.value),
                    }))
                  }
                />
              </label>
              <label className="checkbox-row">
                <input
                  type="checkbox"
                  checked={categoryForm.isActive}
                  onChange={(event) =>
                    setCategoryForm((currentForm) => ({
                      ...currentForm,
                      isActive: event.target.checked,
                    }))
                  }
                />
                <span>دسته‌بندی فعال باشد</span>
              </label>
              <button type="submit" disabled={saving}>
                {saving ? 'در حال ذخیره...' : 'ذخیره دسته‌بندی'}
              </button>
            </form>

            <section className="panel list-panel">
              <div className="panel-head">
                <div>
                  <h3>دسته‌بندی‌های موجود</h3>
                  <p>{toPersianNumber(categories.length)} رکورد</p>
                </div>
              </div>
              <div className="table-shell">
                <table>
                  <thead>
                    <tr>
                      <th>نام</th>
                      <th>اسلاگ</th>
                      <th>تعداد آیتم</th>
                      <th>وضعیت</th>
                      <th></th>
                    </tr>
                  </thead>
                  <tbody>
                    {categories.map((category) => (
                      <tr key={category.id}>
                        <td>{category.name}</td>
                        <td className="ltr-text">{category.slug}</td>
                        <td>{toPersianNumber(category.itemCount)}</td>
                        <td>{category.isActive ? 'فعال' : 'مخفی'}</td>
                        <td className="table-actions">
                          <button type="button" className="link-button" onClick={() => editCategory(category)}>
                            ویرایش
                          </button>
                          <button
                            type="button"
                            className="link-button link-danger"
                            onClick={() => handleDeleteCategory(category.id)}
                          >
                            حذف
                          </button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </section>
          </section>
        ) : null}

        {activeTab === 'menu-items' ? (
          <section className="two-column-layout">
            <form className="panel form-panel" onSubmit={handleMenuItemSubmit}>
              <div className="panel-head">
                <div>
                  <h3>{menuItemForm.id ? 'ویرایش آیتم منو' : 'آیتم جدید منو'}</h3>
                  <p>آیتم‌های منو هم در بخش پرفروش‌ها و هم در منوی عمومی نمایش داده می‌شوند.</p>
                </div>
                <button type="button" className="ghost-button" onClick={resetMenuItemForm}>
                  پاک کردن فرم
                </button>
              </div>
              <label>
                <span>دسته‌بندی</span>
                <select
                  value={menuItemForm.categoryId}
                  onChange={(event) =>
                    setMenuItemForm((currentForm) => ({
                      ...currentForm,
                      categoryId: event.target.value,
                    }))
                  }
                >
                  <option value="">یک دسته‌بندی انتخاب کنید</option>
                  {categories.map((category) => (
                    <option key={category.id} value={category.id}>
                      {category.name}
                    </option>
                  ))}
                </select>
              </label>
              <label>
                <span>نام آیتم</span>
                <input
                  value={menuItemForm.name}
                  onChange={(event) =>
                    setMenuItemForm((currentForm) => ({ ...currentForm, name: event.target.value }))
                  }
                />
              </label>
              <label>
                <span>توضیحات</span>
                <textarea
                  rows="4"
                  value={menuItemForm.description}
                  onChange={(event) =>
                    setMenuItemForm((currentForm) => ({
                      ...currentForm,
                      description: event.target.value,
                    }))
                  }
                />
              </label>
              <div className="field-grid">
                <label>
                  <span>قیمت</span>
                  <input
                    className="ltr-input"
                    type="number"
                    value={menuItemForm.price}
                    onChange={(event) =>
                      setMenuItemForm((currentForm) => ({
                        ...currentForm,
                        price: Number(event.target.value),
                      }))
                    }
                  />
                </label>
                <label>
                  <span>حروف کوتاه</span>
                  <input
                    className="ltr-input"
                    value={menuItemForm.shortLabel}
                    onChange={(event) =>
                      setMenuItemForm((currentForm) => ({
                        ...currentForm,
                        shortLabel: event.target.value,
                      }))
                    }
                  />
                </label>
              </div>
              <label>
                <span>برچسب‌ها (با کاما جدا کنید)</span>
                <input
                  value={menuItemForm.tags}
                  onChange={(event) =>
                    setMenuItemForm((currentForm) => ({ ...currentForm, tags: event.target.value }))
                  }
                />
              </label>
              <div className="field-grid">
                <label>
                  <span>نشان ویژه</span>
                  <input
                    value={menuItemForm.badge}
                    onChange={(event) =>
                      setMenuItemForm((currentForm) => ({ ...currentForm, badge: event.target.value }))
                    }
                  />
                </label>
                <label>
                  <span>ترتیب نمایش</span>
                  <input
                    className="ltr-input"
                    type="number"
                    value={menuItemForm.displayOrder}
                    onChange={(event) =>
                      setMenuItemForm((currentForm) => ({
                        ...currentForm,
                        displayOrder: Number(event.target.value),
                      }))
                    }
                  />
                </label>
              </div>
              <label>
                <span>گرادینت پس‌زمینه</span>
                <input
                  className="ltr-input"
                  value={menuItemForm.accent}
                  onChange={(event) =>
                    setMenuItemForm((currentForm) => ({ ...currentForm, accent: event.target.value }))
                  }
                />
              </label>
              <div className="field-grid compact-grid">
                <label className="checkbox-row">
                  <input
                    type="checkbox"
                    checked={menuItemForm.featured}
                    onChange={(event) =>
                      setMenuItemForm((currentForm) => ({
                        ...currentForm,
                        featured: event.target.checked,
                      }))
                    }
                  />
                  <span>در بخش ویژه نمایش داده شود</span>
                </label>
                <label className="checkbox-row">
                  <input
                    type="checkbox"
                    checked={menuItemForm.isActive}
                    onChange={(event) =>
                      setMenuItemForm((currentForm) => ({
                        ...currentForm,
                        isActive: event.target.checked,
                      }))
                    }
                  />
                  <span>قابل نمایش باشد</span>
                </label>
              </div>
              <button type="submit" disabled={saving || !categories.length}>
                {saving ? 'در حال ذخیره...' : 'ذخیره آیتم منو'}
              </button>
            </form>

            <section className="panel list-panel">
              <div className="panel-head">
                <div>
                  <h3>آیتم‌های موجود منو</h3>
                  <p>{toPersianNumber(menuItems.length)} رکورد</p>
                </div>
              </div>
              <div className="table-shell">
                <table>
                  <thead>
                    <tr>
                      <th>نام</th>
                      <th>دسته‌بندی</th>
                      <th>قیمت</th>
                      <th>وضعیت</th>
                      <th></th>
                    </tr>
                  </thead>
                  <tbody>
                    {menuItems.map((item) => (
                      <tr key={item.id}>
                        <td>
                          <strong>{item.name}</strong>
                          <div className="table-subtext">{joinTags(item.tags)}</div>
                        </td>
                        <td>{item.categoryName}</td>
                        <td>{toPersianNumber(item.price)}</td>
                        <td>{item.isActive ? 'نمایش داده می‌شود' : 'مخفی'}</td>
                        <td className="table-actions">
                          <button type="button" className="link-button" onClick={() => editMenuItem(item)}>
                            ویرایش
                          </button>
                          <button
                            type="button"
                            className="link-button link-danger"
                            onClick={() => handleDeleteMenuItem(item.id)}
                          >
                            حذف
                          </button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </section>
          </section>
        ) : null}

        {activeTab === 'site-settings' ? (
          <form className="panel-stack" onSubmit={handleSettingsSubmit}>
            {settingsSections.map((section) => (
              <section key={section.title} className="panel">
                <div className="panel-head">
                  <div>
                    <h3>{section.title}</h3>
                    <p>این مقدارها مستقیما ظاهر منوی QR عمومی را کنترل می‌کنند.</p>
                  </div>
                </div>
                <div className="settings-grid">
                  {section.fields.map(([fieldName, fieldLabel, fieldType]) => (
                    <label
                      key={fieldName}
                      className={fieldType === 'textarea' ? 'full-width' : undefined}
                    >
                      <span>{fieldLabel}</span>
                      {fieldType === 'textarea' ? (
                        <textarea
                          className={fieldName === 'instagramUrl' ? 'ltr-input' : ''}
                          rows="4"
                          value={siteSettings[fieldName]}
                          onChange={(event) =>
                            setSiteSettings((currentSettings) => ({
                              ...currentSettings,
                              [fieldName]: event.target.value,
                            }))
                          }
                        />
                      ) : (
                        <input
                          className={fieldType === 'url' || fieldName === 'contactPhone' ? 'ltr-input' : ''}
                          type={fieldType ?? 'text'}
                          value={siteSettings[fieldName]}
                          onChange={(event) =>
                            setSiteSettings((currentSettings) => ({
                              ...currentSettings,
                              [fieldName]: event.target.value,
                            }))
                          }
                        />
                      )}
                    </label>
                  ))}
                </div>
              </section>
            ))}
            <button type="submit" className="save-settings-button" disabled={saving}>
              {saving ? 'در حال ذخیره...' : 'ذخیره تنظیمات سایت'}
            </button>
          </form>
        ) : null}
      </main>
    </div>
  )
}

export default App
