import { useEffect, useState } from 'react'
import { apiRequest, getReadableError } from '../utils/adminApi'
import { useAdmin } from '../context/AdminContext'
import './SpecialRoutesPage.css'

const STATUS_LABELS = {
  open: 'باز',
  closed: 'بسته',
  fixed: 'ثابت',
}

export function SpecialRoutesPage() {
  const { session } = useAdmin()
  const [routes, setRoutes] = useState([])
  const [pending, setPending] = useState('')
  const [flash, setFlash] = useState('')
  const [error, setError] = useState('')
  const [confirmKey, setConfirmKey] = useState(null)

  const loadRoutes = async () => {
    try {
      const data = await apiRequest('/admin/route-flags', { token: session.token })
      setRoutes(Array.isArray(data) ? data : data?.routes ?? [])
    } catch (requestError) {
      setError(getReadableError(requestError))
    }
  }

  useEffect(() => {
    loadRoutes()
  }, [])

  const toggleRoute = async (route, enabled) => {
    setPending(route.key)
    setError('')
    setFlash('')
    try {
      const result = await apiRequest('/admin/route-flags', {
        method: 'PUT',
        token: session.token,
        body: { key: route.key, enabled },
      })
      setFlash(result?.message || (enabled ? 'باز شد.' : 'بسته شد.'))
      await loadRoutes()
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setPending('')
      setConfirmKey(null)
      setTimeout(() => setFlash(''), 6000)
    }
  }

  return (
    <div className="special-routes-page" dir="rtl">
      <header className="special-routes-header">
        <div>
          <h1>مسیرهای متفرقهٔ سایت</h1>
          <p className="special-routes-subtitle">
            باز/بسته کردن مسیرهای جانبی در زمان نیاز — بدون دیپلوی و بدون رستارت.
            تغییرات بلافاصله اعمال میشوند و در فایل پیکربندی ذخیره میمانند (بین rebuildها).
          </p>
        </div>
      </header>

      {error ? <p className="special-routes-banner error">{error}</p> : null}
      {flash ? <p className="special-routes-banner ok">{flash}</p> : null}

      <table className="special-routes-table">
        <thead>
          <tr>
            <th>مسیر</th>
            <th>آدرس</th>
            <th>وضعیت</th>
            <th>کنترل</th>
            <th>توضیح</th>
          </tr>
        </thead>
        <tbody>
          {routes.map((route) => (
            <tr key={route.key} className={route.editable ? '' : 'informational'}>
              <td>
                <span className="route-label">{route.label}</span>
              </td>
              <td>
                <code dir="ltr">{route.path}</code>
              </td>
              <td>
                <span className={`status-badge ${route.enabled ? 'open' : 'closed'}`}>
                  {route.enabled ? STATUS_LABELS.open : STATUS_LABELS.closed}
                </span>
              </td>
              <td>
                {route.editable ? (
                  confirmKey === route.key ? (
                    <span className="confirm-actions">
                      <button
                        className="btn btn-danger"
                        disabled={pending === route.key}
                        onClick={() => toggleRoute(route, !route.enabled)}
                      >
                        {pending === route.key ? 'در حال اعمال…' : route.enabled ? 'بله، ببند' : 'بله، باز کن'}
                      </button>
                      <button className="btn btn-ghost" onClick={() => setConfirmKey(null)}>
                        انصراف
                      </button>
                    </span>
                  ) : (
                    <button
                      className={`btn ${route.enabled ? 'btn-danger' : 'btn-open'}`}
                      onClick={() => setConfirmKey(route.key)}
                    >
                      {route.enabled ? 'بستن مسیر' : 'باز کردن مسیر'}
                    </button>
                  )
                ) : (
                  <span className="fixed-note">{STATUS_LABELS.fixed}</span>
                )}
              </td>
              <td>
                <p className="route-description">{route.description}</p>
              </td>
            </tr>
          ))}
        </tbody>
      </table>

      <div className="special-routes-notes">
        <h2>نکته‌ها</h2>
        <ul>
          <li>
            صفحهٔ راه‌اندازی (<code dir="ltr">/api/setup</code>) بعد از پایان اولین راه‌اندازی به‌صورت
            پیش‌فرض بسته است؛ مشاهده و تغییر در این صفحه امن (harmless) است. برای تغییر رمز دیتابیس
            از این صفحه، مسیر را موقتاً باز کنید، کار را انجام دهید و بلافاصله دوباره ببندید.
          </li>
          <li>
            بستن <code dir="ltr">/health</code> فقط دسترسی بیرونی را قطع میکند — بررسی سلامت خود
            کانتینر همیشه کار میکند و سایت را نمی‌اندازد.
          </li>
          <li>
            هر مسیری که از پنل بسته شود، <strong>به صفحهٔ اصلی سایت ریدایرکت می‌شود</strong> (نه 404) —
            بازدیدکننده‌ای که لینک قدیمی دارد به سایت برمی‌گردد.
          </li>
          <li>
            Adminer (<code dir="ltr">/db-admin</code>) پیش‌فرض بسته است؛ فقط برای جلسات نگهداری بازش
            کنید و بعد از کار بلافاصله ببندید.
          </li>
          <li>
            مسیرهای «ثابت» بخشی از معماری کنترل‌شدهٔ پروکسی‌اند و همیشه با حالت امن پیش‌فرض کار میکنند.
          </li>
        </ul>
      </div>
    </div>
  )
}
