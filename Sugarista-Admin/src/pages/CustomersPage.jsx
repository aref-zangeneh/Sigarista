import { useCallback, useEffect, useState } from 'react'
import { useAdmin } from '../context/AdminContext'
import { formatPrice } from '../utils/posCart'
import { formatDateTime, toPersianNumber } from '../utils/adminFormatters'

const CHANNEL_LABELS = {
  pos: 'صندوق فروش',
  online: 'سفارش آنلاین',
  admin: 'ویرایش ادمین',
}

const STATUS_LABELS = {
  PendingPayment: 'در انتظار پرداخت',
  PartiallyPaid: 'پرداخت جزئی',
  Paid: 'پرداخت‌شده',
  Failed: 'ناموفق',
  Cancelled: 'لغوشده',
  Held: 'نگه‌داشته',
}

function channelLabel(channel) {
  return CHANNEL_LABELS[channel] ?? channel
}

function statusLabel(status) {
  return STATUS_LABELS[status] ?? status
}

export function CustomersPage() {
  const { session, apiRequest, getReadableError } = useAdmin()

  const [customers, setCustomers] = useState([])
  const [totalCount, setTotalCount] = useState(0)
  const [page, setPage] = useState(1)
  const [pageSize] = useState(25)
  const [search, setSearch] = useState('')
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [message, setMessage] = useState('')
  const [detail, setDetail] = useState(null)
  const [detailLoading, setDetailLoading] = useState(false)
  const [savingId, setSavingId] = useState(null)
  const [groupOptions, setGroupOptions] = useState([])
  const [memberships, setMemberships] = useState([])
  const [selectedGroupId, setSelectedGroupId] = useState('')
  const [subscriptionPackages, setSubscriptionPackages] = useState([])
  const [subscriptions, setSubscriptions] = useState([])
  const [selectedPackageId, setSelectedPackageId] = useState('')
  const [benefitsSaving, setBenefitsSaving] = useState(false)

  const loadCustomers = useCallback(
    async (nextPage = page, nextSearch = search) => {
      if (!session?.token) return
      setLoading(true)
      setError('')
      try {
        const params = new URLSearchParams({
          page: String(nextPage),
          pageSize: String(pageSize),
        })
        if (nextSearch.trim()) {
          params.set('search', nextSearch.trim())
        }
        const data = await apiRequest(`/admin/customers?${params.toString()}`, { token: session.token })
        setCustomers(Array.isArray(data.items) ? data.items : [])
        setTotalCount(data.totalCount ?? (Array.isArray(data.items) ? data.items.length : 0))
      } catch (requestError) {
        setError(getReadableError(requestError))
      } finally {
        setLoading(false)
      }
    },
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [session?.token, page, search, pageSize],
  )

  useEffect(() => {
    loadCustomers(page, search)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [page])

  const handleSearch = (event) => {
    event.preventDefault()
    setPage(1)
    loadCustomers(1, search)
  }

  const openDetail = async (customer) => {
    setDetailLoading(true)
    setError('')
    try {
      const data = await apiRequest(`/admin/customers/${customer.id}`, { token: session.token })
      setDetail(data)
      setBirthdayValue(data.dateOfBirth ?? '')
      loadWallet(customer.id)
      loadWalletPackages()
      loadBenefits(customer.id)
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setDetailLoading(false)
    }
  }

  // ── کیف پول و کش‌بک ─────────────────────────────────────────────
  const [wallet, setWallet] = useState(null)
  const [walletLoading, setWalletLoading] = useState(false)
  const [walletSaving, setWalletSaving] = useState(false)
  const [walletAmount, setWalletAmount] = useState('')
  const [walletNote, setWalletNote] = useState('')
  const [walletPackages, setWalletPackages] = useState([])

  const loadWallet = async (customerId) => {
    if (!session?.token || !customerId) return
    setWalletLoading(true)
    try {
      const data = await apiRequest(`/admin/customers/${customerId}/wallet`, { token: session.token })
      setWallet(data)
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setWalletLoading(false)
    }
  }

  const loadWalletPackages = async () => {
    if (!session?.token) return
    try {
      const data = await apiRequest('/admin/wallet-packages', { token: session.token })
      const rows = Array.isArray(data) ? data : []
      setWalletPackages(rows.filter((row) => row.isEnabled !== false).slice(0, 6))
    } catch {
      setWalletPackages([])
    }
  }

  const loadBenefits = async (customerId) => {
    if (!session?.token || !customerId) return
    try {
      const [groupsData, membershipsData, packagesData, subscriptionsData] = await Promise.all([
        apiRequest('/admin/customer-groups', { token: session.token }),
        apiRequest(`/admin/customers/${customerId}/groups`, { token: session.token }),
        apiRequest('/admin/subscription-packages', { token: session.token }),
        apiRequest(`/admin/customers/${customerId}/subscriptions`, { token: session.token }),
      ])
      setGroupOptions(Array.isArray(groupsData) ? groupsData.filter((group) => group.isEnabled !== false) : [])
      setMemberships(Array.isArray(membershipsData) ? membershipsData : [])
      setSubscriptionPackages(
        Array.isArray(packagesData) ? packagesData.filter((pack) => pack.isEnabled !== false) : [],
      )
      setSubscriptions(Array.isArray(subscriptionsData) ? subscriptionsData : [])
    } catch (requestError) {
      setError(getReadableError(requestError))
    }
  }

  const assignGroup = async () => {
    if (!detail?.id || !selectedGroupId) return
    setBenefitsSaving(true)
    setError('')
    setMessage('')
    try {
      await apiRequest(`/admin/customers/${detail.id}/groups`, {
        method: 'POST',
        token: session.token,
        body: { customerId: detail.id, groupId: selectedGroupId },
      })
      setSelectedGroupId('')
      await loadBenefits(detail.id)
      setMessage('مشتری به گروه اضافه شد.')
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setBenefitsSaving(false)
    }
  }

  const removeGroup = async (groupId) => {
    if (!detail?.id) return
    setBenefitsSaving(true)
    setError('')
    setMessage('')
    try {
      await apiRequest(`/admin/customers/${detail.id}/groups/${groupId}`, {
        method: 'DELETE',
        token: session.token,
      })
      await loadBenefits(detail.id)
      setMessage('عضویت مشتری در گروه حذف شد.')
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setBenefitsSaving(false)
    }
  }

  const activateSubscription = async () => {
    if (!detail?.id || !selectedPackageId) return
    setBenefitsSaving(true)
    setError('')
    setMessage('')
    try {
      await apiRequest(`/admin/customers/${detail.id}/subscriptions`, {
        method: 'POST',
        token: session.token,
        body: { customerId: detail.id, packageId: selectedPackageId },
      })
      setSelectedPackageId('')
      await loadBenefits(detail.id)
      setMessage('اشتراک برای مشتری فعال شد.')
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setBenefitsSaving(false)
    }
  }

  const cancelSubscription = async (subscription) => {
    if (!detail?.id) return
    if (
      !window.confirm(
        `لغو اشتراک «${subscription.packageName}»؟\nمعادل ${formatPrice(subscription.remainingValue)} تومان به کیف پول مشتری برگشت می‌خورد.`,
      )
    ) {
      return
    }
    setBenefitsSaving(true)
    setError('')
    setMessage('')
    try {
      await apiRequest(`/admin/customers/${detail.id}/subscriptions/${subscription.id}/cancel`, {
        method: 'POST',
        token: session.token,
        body: { subscriptionId: subscription.id, transferToWallet: true },
      })
      await loadBenefits(detail.id)
      await loadWallet(detail.id)
      setMessage('اشتراک لغو و مبلغ باقی‌مانده به کیف پول برگشت داده شد.')
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setBenefitsSaving(false)
    }
  }

  const submitWalletAdjustment = async (direction) => {
    const value = Number(walletAmount)
    if (!value || value <= 0) {
      setError('مبلغ شارژ/اصلاح را وارد کنید.')
      return
    }
    setWalletSaving(true)
    setError('')
    setMessage('')
    try {
      const data = await apiRequest(`/admin/customers/${detail.id}/wallet`, {
        method: 'POST',
        token: session.token,
        body: {
          customerId: detail.id,
          amount: direction === 'charge' ? value : -value,
          description: walletNote.trim() || null,
        },
      })
      setWallet(data)
      setWalletAmount('')
      setWalletNote('')
      setMessage(direction === 'charge'
        ? `کیف پول ${formatPrice(value)} تومان شارژ شد.`
        : `${formatPrice(value)} تومان از کیف پول اصلاح شد.`)
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setWalletSaving(false)
    }
  }

  const [birthdayValue, setBirthdayValue] = useState('')
  const [birthdaySaving, setBirthdaySaving] = useState(false)

  const submitBirthday = async () => {
    if (!detail?.id || !birthdayValue) return
    setBirthdaySaving(true)
    setError('')
    setMessage('')
    try {
      await apiRequest(`/admin/customers/${detail.id}`, {
        method: 'PUT',
        token: session.token,
        body: {
          id: detail.id,
          persianName: detail.persianName,
          email: detail.email,
          notes: detail.notes,
          isActive: detail.isActive,
          dateOfBirth: birthdayValue || null,
        },
      })
      setDetail((current) => ({ ...(current ?? {}), dateOfBirth: birthdayValue }))
      setMessage('تاریخ تولد ذخیره شد.')
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setBirthdaySaving(false)
    }
  }

  const chargePackage = async (pack) => {
    if (!detail?.id) return
    setWalletSaving(true)
    setError('')
    setMessage('')
    try {
      const total = Number(pack.amount) + Number(pack.bonusAmount || 0)
      const data = await apiRequest(`/admin/customers/${detail.id}/wallet`, {
        method: 'POST',
        token: session.token,
        body: {
          customerId: detail.id,
          amount: total,
          description:
            pack.bonusAmount > 0
              ? `شارژ بسته: ${formatPrice(pack.amount)} + ${formatPrice(pack.bonusAmount)} هدیه`
              : `شارژ بسته: ${formatPrice(pack.amount)}`,
        },
      })
      setWallet(data)
      setWalletNote('')
      setMessage('بستهٔ شارژ با موفقیت اعمال شد.')
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setWalletSaving(false)
    }
  }

  const WALLET_TYPE_LABELS = {
    TopUp: 'شارژ',
    Cashback: 'کش‌بک',
    Purchase: 'پرداخت سفارش',
    Refund: 'برگشت وجه',
    Adjustment: 'اصلاح ادمین',
  }

  const toggleActive = async (customer) => {
    setSavingId(customer.id)
    setError('')
    setMessage('')
    try {
      await apiRequest(`/admin/customers/${customer.id}`, {
        method: 'PUT',
        token: session.token,
        body: {
          id: customer.id,
          persianName: customer.persianName,
          email: customer.email,
          notes: customer.notes,
          isActive: !customer.isActive,
        },
      })
      setMessage(customer.isActive ? 'مشتری غیرفعال شد.' : 'مشتری فعال شد.')
      loadCustomers()
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setSavingId(null)
    }
  }

  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize))

  return (
    <section className="panel-stack">
      {detail ? (
        <section className="panel customer-detail-panel">
          <div className="panel-head">
            <div>
              <h3>پرونده مشتری: {detail.persianName}</h3>
              <p>
                {toPersianNumber(detail.phoneNumber)} — {channelLabel(detail.firstChannel)} تا {channelLabel(detail.lastChannel)}
              </p>
            </div>
            <button type="button" className="ghost-button" onClick={() => setDetail(null)}>
              بستن پرونده
            </button>
          </div>
          <div className="stat-grid">
            <div className="stat-card">
              <span>مجموع خرید (پرداخت‌شده)</span>
              <strong>{formatPrice(detail.totalPaidAmount)} تومان</strong>
            </div>
            <div className="stat-card">
              <span>سفارش‌های پرداخت‌شده</span>
              <strong>{toPersianNumber(detail.paidOrders)}</strong>
            </div>
            <div className="stat-card">
              <span>میانگین هر سفارش</span>
              <strong>{formatPrice(detail.averageOrderValue)} تومان</strong>
            </div>
            <div className="stat-card">
              <span>امتیاز باشگاه مشتریان</span>
              <strong>{toPersianNumber(detail.loyaltyPoints)}</strong>
            </div>
          </div>
          <h4>سفارش‌های اخیر</h4>
          <table className="admin-table">
            <thead>
              <tr>
                <th>کد پیگیری</th>
                <th>مبلغ کل</th>
                <th>مالیات</th>
                <th>هزینه تحویل</th>
                <th>وضعیت</th>
                <th>کانال</th>
                <th>تاریخ</th>
              </tr>
            </thead>
            <tbody>
              {(detail.recentOrders ?? []).map((order) => (
                <tr key={order.orderId}>
                  <td>{order.trackingCode}</td>
                  <td>{formatPrice(order.totalAmount)}</td>
                  <td>{formatPrice(order.taxTotal)}</td>
                  <td>{formatPrice(order.deliveryFee)}</td>
                  <td>{statusLabel(order.status)}</td>
                  <td>
                    {order.channel === 'Pos' ? 'صندوق' : order.channel === 'Online' ? 'آنلاین' : order.channel}
                    {order.serviceType ? ` (${order.serviceType === 'InPerson' ? 'حضوری' : order.serviceType === 'Online' ? 'آنلاین' : order.serviceType})` : ''}
                  </td>
                  <td>{formatDateTime(order.createdAt)}</td>
                </tr>
              ))}
              {(detail.recentOrders ?? []).length === 0 && (
                <tr>
                  <td colSpan={7}>هنوز سفارشی برای این مشتری ثبت نشده است.</td>
                </tr>
              )}
            </tbody>
          </table>

          {/* ── تاریخ تولد ── */}
          <div className="panel-head wallet-panel-head">
            <div>
              <h4>تاریخ تولد (میلادی)</h4>
              <p>پایهٔ تخفیف تولد؛ بعد از ذخیره، هدیهٔ تولد در بازهٔ تعیین‌شدهٔ تنظیمات فعال می‌شود.</p>
            </div>
          </div>
          <div className="birthday-edit-row">
            <input
              className="ltr-input"
              dir="ltr"
              type="date"
              value={birthdayValue}
              onChange={(event) => setBirthdayValue(event.target.value)}
            />
            <button type="button" disabled={birthdaySaving || !birthdayValue} onClick={submitBirthday}>
              {birthdaySaving ? 'در حال ذخیره...' : 'ذخیرهٔ تاریخ تولد'}
            </button>
          </div>

          {/* ── کیف پول مشتری ── */}
          <div className="panel-head wallet-panel-head">
            <div>
              <h4>کیف پول</h4>
              <p>شارژ حضوری، کش‌بک خودکار و اصلاح دستی — همهٔ حرکات لاگ می‌شوند و موجودی هرگز منفی نمی‌شود.</p>
            </div>
          </div>
          {walletLoading ? (
            <p>در حال دریافت کیف پول...</p>
          ) : (
            <>
              {(walletPackages.length > 0) && (
                <div className="wallet-package-row">
                  {walletPackages.map((pack) => (
                    <button
                      key={pack.id}
                      type="button"
                      className="wallet-package-chip"
                      disabled={walletSaving}
                      onClick={() => chargePackage(pack)}
                      title={pack.bonusAmount > 0 ? `هدیه: ${formatPrice(pack.bonusAmount)}` : undefined}
                    >
                      <strong>{formatPrice(pack.amount)}</strong>
                      {pack.bonusAmount > 0 && <span>+{formatPrice(pack.bonusAmount)} هدیه</span>}
                    </button>
                  ))}
                </div>
              )}
              <div className="stat-grid">
                <div className="stat-card">
                  <span>موجودی کیف پول</span>
                  <strong>{formatPrice(wallet?.balance ?? 0)} تومان</strong>
                </div>
              </div>
              <div className="wallet-adjust-row">
                <input
                  className="ltr-input"
                  dir="ltr"
                  type="number"
                  min="0"
                  step="10000"
                  placeholder="مبلغ (تومان)"
                  value={walletAmount}
                  onChange={(event) => setWalletAmount(event.target.value)}
                />
                <input
                  placeholder="توضیح (اختیاری)"
                  maxLength={200}
                  value={walletNote}
                  onChange={(event) => setWalletNote(event.target.value)}
                />
                <button type="button" disabled={walletSaving} onClick={() => submitWalletAdjustment('charge')}>
                  شارژ (پول نقد مشتری)
                </button>
                <button type="button" className="ghost-button" disabled={walletSaving} onClick={() => submitWalletAdjustment('correction')}>
                  اصلاح (کسر)
                </button>
              </div>
              <h4>تراکنش‌های اخیر</h4>
              <table className="admin-table">
                <thead>
                  <tr>
                    <th>نوع</th>
                    <th>مبلغ</th>
                    <th>مانده بعد از حرکت</th>
                    <th>توضیح</th>
                    <th>تاریخ</th>
                  </tr>
                </thead>
                <tbody>
                  {(wallet?.transactions ?? []).map((movement) => (
                    <tr key={movement.id}>
                      <td>{WALLET_TYPE_LABELS[movement.type] ?? movement.type}</td>
                      <td className={movement.amount > 0 ? 'wallet-in' : 'wallet-out'}>
                        {movement.amount > 0 ? '+' : '−'} {formatPrice(Math.abs(movement.amount))}
                      </td>
                      <td>{formatPrice(movement.balanceAfter)}</td>
                      <td>{movement.description}</td>
                      <td>{formatDateTime(movement.createdAt)}</td>
                    </tr>
                  ))}
                  {(wallet?.transactions ?? []).length === 0 && (
                    <tr>
                      <td colSpan={5}>هنوز تراکنشی ثبت نشده است.</td>
                    </tr>
                  )}
                </tbody>
              </table>
            </>
          )}

          {/* ── گروه‌های مشتری ── */}
          <div className="panel-head wallet-panel-head">
            <div>
              <h4>گروه‌های مشتری</h4>
              <p>عضویت فعال گروه، تخفیف دائمی آن را هنگام سفارش اعمال می‌کند.</p>
            </div>
          </div>
          <div className="birthday-edit-row">
            <select
              value={selectedGroupId}
              onChange={(event) => setSelectedGroupId(event.target.value)}
            >
              <option value="">انتخاب گروه…</option>
              {groupOptions.map((group) => (
                <option key={group.id} value={group.id}>
                  {group.name} ({group.discountPercent}٪)
                </option>
              ))}
            </select>
            <button type="button" disabled={benefitsSaving || !selectedGroupId} onClick={assignGroup}>
              افزودن به گروه
            </button>
          </div>
          <table className="admin-table">
            <thead>
              <tr>
                <th>گروه</th>
                <th>تخفیف</th>
                <th>منبع</th>
                <th>اعتبار تا</th>
                <th>وضعیت</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {memberships.map((membership) => (
                <tr key={membership.groupId}>
                  <td>{membership.groupName}</td>
                  <td>{membership.discountPercent}٪</td>
                  <td>{membership.source === 'EmailDomain' ? 'دامنهٔ ایمیل' : 'دستی'}</td>
                  <td>{membership.expiresAt ? formatDateTime(membership.expiresAt) : 'بدون انقضا'}</td>
                  <td>{membership.isActive ? 'فعال' : 'منقضی'}</td>
                  <td>
                    <button
                      type="button"
                      className="link-button link-danger"
                      disabled={benefitsSaving}
                      onClick={() => removeGroup(membership.groupId)}
                    >
                      حذف
                    </button>
                  </td>
                </tr>
              ))}
              {memberships.length === 0 && (
                <tr>
                  <td colSpan={6}>این مشتری عضو هیچ گروهی نیست.</td>
                </tr>
              )}
            </tbody>
          </table>

          {/* ── اشتراک ماهانه ── */}
          <div className="panel-head wallet-panel-head">
            <div>
              <h4>اشتراک ماهانه</h4>
              <p>فروش بستهٔ اشتراک به مشتری؛ استفاده‌های باقی‌مانده هنگام لغو به کیف پول برمی‌گردد.</p>
            </div>
          </div>
          <div className="birthday-edit-row">
            <select
              value={selectedPackageId}
              onChange={(event) => setSelectedPackageId(event.target.value)}
            >
              <option value="">انتخاب بسته…</option>
              {subscriptionPackages.map((pack) => (
                <option key={pack.id} value={pack.id}>
                  {pack.name} — {formatPrice(pack.price)} تومان
                </option>
              ))}
            </select>
            <button type="button" disabled={benefitsSaving || !selectedPackageId} onClick={activateSubscription}>
              فعال‌سازی اشتراک
            </button>
          </div>
          <table className="admin-table">
            <thead>
              <tr>
                <th>بسته</th>
                <th>استفاده</th>
                <th>باقی‌مانده</th>
                <th>ارزش باقی‌مانده</th>
                <th>اعتبار تا</th>
                <th>وضعیت</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {subscriptions.map((subscription) => (
                <tr key={subscription.id}>
                  <td>{subscription.packageName}</td>
                  <td>
                    {subscription.usedCount} / {subscription.totalUseCount}
                  </td>
                  <td>{subscription.remainingUses}</td>
                  <td>{formatPrice(subscription.remainingValue)} تومان</td>
                  <td>{formatDateTime(subscription.expiresAt)}</td>
                  <td>
                    {subscription.status === 'Active'
                      ? 'فعال'
                      : subscription.status === 'Cancelled'
                        ? 'لغو‌شده'
                        : 'منقضی'}
                  </td>
                  <td>
                    {subscription.status === 'Active' ? (
                      <button
                        type="button"
                        className="link-button link-danger"
                        disabled={benefitsSaving}
                        onClick={() => cancelSubscription(subscription)}
                      >
                        لغو و بازپرداخت
                      </button>
                    ) : null}
                  </td>
                </tr>
              ))}
              {subscriptions.length === 0 && (
                <tr>
                  <td colSpan={7}>این مشتری اشتراکی ندارد.</td>
                </tr>
              )}
            </tbody>
          </table>
        </section>
      ) : null}

      <section className="panel">
        <div className="panel-head">
          <div>
            <h3>باشگاه مشتریان</h3>
            <p>مشتریان یکپارچه از همه کانال‌ها (صندوق فروش، سفارش آنلاین، ویرایش ادمین) — همراه با آمادۀ خرید و امتیاز وفاداری.</p>
          </div>
          <form className="inline-form" onSubmit={handleSearch}>
            <input
              type="search"
              value={search}
              placeholder="جستجو: نام، شماره تماس یا ایمیل..."
              onChange={(event) => setSearch(event.target.value)}
            />
            <button type="submit" className="ghost-button">
              جستجو
            </button>
            <button type="button" className="ghost-button" onClick={() => loadCustomers()}>
              بروزرسانی
            </button>
          </form>
        </div>

        {error ? <p className="feedback feedback-error">{error}</p> : null}
        {message ? <p className="feedback feedback-success">{message}</p> : null}
        {loading ? <p>در حال بارگذاری مشتریان...</p> : null}

        {!loading && customers.length === 0 ? (
          <p>هنوز مشتری‌ای ثبت نشده است؛ با اولین سفارش صندوق فروش یا سفارش آنلاین، پروندهٔ مشتری خودکار ساخته می‌شود.</p>
        ) : null}

        {customers.length > 0 ? (
          <table className="admin-table">
            <thead>
              <tr>
                <th>مشتری</th>
                <th>شماره تماس</th>
                <th>کانال‌ها</th>
                <th>سفارش‌ها</th>
                <th>مجموع خرید</th>
                <th>میانگین سفارش</th>
                <th>مالیات پرداختی</th>
                <th>امتیاز</th>
                <th>آخرین خرید</th>
                <th>وضعیت</th>
                <th>عملیات</th>
              </tr>
            </thead>
            <tbody>
              {customers.map((customer) => (
                <tr key={customer.id} className={customer.isActive ? '' : 'row-muted'}>
                  <td>{customer.persianName}</td>
                  <td>{toPersianNumber(customer.phoneNumber)}</td>
                  <td>
                    {channelLabel(customer.firstChannel)}
                    {customer.lastChannel !== customer.firstChannel ? ` → ${channelLabel(customer.lastChannel)}` : ''}
                  </td>
                  <td>
                    {toPersianNumber(customer.paidOrders)} از {toPersianNumber(customer.totalOrders)}
                  </td>
                  <td>{formatPrice(customer.totalPaidAmount)}</td>
                  <td>{formatPrice(customer.averageOrderValue)}</td>
                  <td>{formatPrice(customer.totalTax)}</td>
                  <td className="loyalty-points">{toPersianNumber(customer.loyaltyPoints)}</td>
                  <td>{formatDateTime(customer.lastOrderAt)}</td>
                  <td>{customer.isActive ? 'فعال' : 'غیرفعال'}</td>
                  <td className="table-actions">
                    <button type="button" className="ghost-button" onClick={() => openDetail(customer)}>
                      پرونده
                    </button>
                    <button
                      type="button"
                      className="ghost-button"
                      disabled={savingId === customer.id}
                      onClick={() => toggleActive(customer)}
                    >
                      {customer.isActive ? 'غیرفعال' : 'فعال'}
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        ) : null}

        {totalPages > 1 ? (
          <div className="pagination-row">
            <button type="button" className="ghost-button" disabled={page <= 1} onClick={() => setPage(page - 1)}>
              قبلی
            </button>
            <span>
              صفحهٔ {toPersianNumber(page)} از {toPersianNumber(totalPages)} — مجموع {toPersianNumber(totalCount)} مشتری
            </span>
            <button
              type="button"
              className="ghost-button"
              disabled={page >= totalPages}
              onClick={() => setPage(page + 1)}
            >
              بعدی
            </button>
          </div>
        ) : null}

        {detailLoading ? <p>در حال دریافت پرونده مشتری...</p> : null}
      </section>
    </section>
  )
}
