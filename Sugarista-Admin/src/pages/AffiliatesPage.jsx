import { useCallback, useEffect, useState } from 'react'
import { useAdmin } from '../context/AdminContext'
import { API_BASE_URL, apiRequest } from '../utils/adminApi'
import { formatDateTime, toPersianNumber } from '../utils/adminFormatters'
import { hasPermission, PERMISSIONS } from '../utils/adminPermissions'
import './AffiliatesPage.css'

const STATUS_META = {
  Pending: { label: 'در انتظار تایید', cls: 'pending' },
  Active: { label: 'فعال', cls: 'active' },
  Suspended: { label: 'مسدود / معلق', cls: 'suspended' },
}

const PAYOUT_STATUS_LABELS = {
  Requested: 'درخواست جدید',
  Processing: 'در حال پردازش',
  Completed: 'واریز شده',
  Rejected: 'رد شده',
}

export function AffiliatesPage() {
  const { session, setMessage, setError } = useAdmin()
  const canManage = hasPermission(session, PERMISSIONS.affiliates.update)

  const [activeTab, setActiveTab] = useState('affiliates')
  const [summary, setSummary] = useState(null)
  const [affiliates, setAffiliates] = useState([])
  const [tiers, setTiers] = useState([])
  const [categoryRates, setCategoryRates] = useState([])
  const [payouts, setPayouts] = useState([])
  const [mediaKit, setMediaKit] = useState([])
  const [loading, setLoading] = useState(false)

  // Filters
  const [statusFilter, setStatusFilter] = useState('')
  const [search, setSearch] = useState('')
  const [payoutStatusFilter, setPayoutStatusFilter] = useState('')

  // Modals & forms
  const [createModalOpen, setCreateModalOpen] = useState(false)
  const [createForm, setCreateForm] = useState({
    customerPhone: '',
    customerName: '',
    tierId: '',
    customCommissionRate: '',
    shebaNumber: '',
    preferredSlug: '',
    initialCouponCode: '',
    initialCouponPercent: '10',
  })

  const [tierRateModal, setTierRateModal] = useState(null)
  const [tierRateForm, setTierRateForm] = useState({ tierId: '', customCommissionRate: '' })

  const [couponModal, setCouponModal] = useState(null)
  const [couponForm, setCouponForm] = useState({
    code: '',
    discountType: 'Percentage',
    discountValue: '15',
    maxDiscountAmount: '',
    isFirstOrderOnly: false,
    usageLimitPerUser: '',
  })

  const [tierForm, setTierForm] = useState({
    id: null,
    name: '',
    minMonthlySales: '0',
    commissionPercentage: '5',
    creditBonusPercentage: '20',
  })

  const [payoutModal, setPayoutModal] = useState(null)
  const [payoutTrackingCode, setPayoutTrackingCode] = useState('')
  const [payoutReceiptUrl, setPayoutReceiptUrl] = useState('')
  const [uploadingFile, setUploadingFile] = useState(false)

  const [mediaForm, setMediaForm] = useState({
    title: '',
    assetType: 'Image',
    fileUrl: '',
    captionText: '',
    displayOrder: '0',
  })

  const loadAll = useCallback(async () => {
    if (!session?.token) return
    setLoading(true)
    try {
      const qs = new URLSearchParams()
      if (statusFilter) qs.set('status', statusFilter)
      if (search.trim()) qs.set('search', search.trim())
      const pqs = payoutStatusFilter ? `?status=${encodeURIComponent(payoutStatusFilter)}` : ''

      const [sumRes, affRes, tierRes, catRes, payRes, mediaRes] = await Promise.all([
        apiRequest('/admin/affiliates/summary', { token: session.token }),
        apiRequest(`/admin/affiliates${qs.toString() ? `?${qs.toString()}` : ''}`, { token: session.token }),
        apiRequest('/admin/affiliates/tiers', { token: session.token }),
        apiRequest('/admin/affiliates/category-commissions', { token: session.token }),
        apiRequest(`/admin/affiliates/payouts${pqs}`, { token: session.token }),
        apiRequest('/admin/affiliates/media-kit', { token: session.token }),
      ])

      setSummary(sumRes)
      setAffiliates(Array.isArray(affRes) ? affRes : [])
      setTiers(Array.isArray(tierRes) ? tierRes : [])
      setCategoryRates(Array.isArray(catRes) ? catRes : [])
      setPayouts(Array.isArray(payRes) ? payRes : [])
      setMediaKit(Array.isArray(mediaRes) ? mediaRes : [])
    } catch (err) {
      setError(err.message || 'خطا در دریافت اطلاعات همکاری در فروش.')
    } finally {
      setLoading(false)
    }
  }, [session?.token, statusFilter, search, payoutStatusFilter, setError])

  useEffect(() => {
    loadAll()
  }, [loadAll])

  const uploadAdminFile = async (file) => {
    if (!file || !session?.token) return null
    setUploadingFile(true)
    try {
      const fd = new FormData()
      fd.append('file', file)
      const res = await fetch(`${API_BASE_URL}/admin/uploads`, {
        method: 'POST',
        headers: { Authorization: `Bearer ${session.token}` },
        body: fd,
      })
      if (!res.ok) throw new Error('آپلود فایل ناموفق بود.')
      const json = await res.json()
      return json.url || json.path || json.fileUrl || null
    } finally {
      setUploadingFile(false)
    }
  }

  const handleStatusChange = async (affiliateId, status) => {
    if (!canManage) return
    setError('')
    try {
      await apiRequest(`/admin/affiliates/${affiliateId}/status`, {
        method: 'PATCH',
        token: session.token,
        body: { status },
      })
      setMessage(status === 'Active' ? 'حساب بازاریاب تایید شد و پیامک اطلاع‌رسانی ارسال گردید.' : 'وضعیت بازاریاب بروزرسانی شد.')
      await loadAll()
    } catch (err) {
      setError(err.message)
    }
  }

  const handleCreateAffiliate = async (event) => {
    event.preventDefault()
    if (!canManage) return
    setError('')
    try {
      await apiRequest('/admin/affiliates', {
        method: 'POST',
        token: session.token,
        body: {
          customerPhone: createForm.customerPhone.trim(),
          customerName: createForm.customerName.trim() || null,
          tierId: createForm.tierId || null,
          customCommissionRate: createForm.customCommissionRate !== '' ? Number(createForm.customCommissionRate) : null,
          shebaNumber: createForm.shebaNumber.trim() || null,
          preferredSlug: createForm.preferredSlug.trim() || null,
          initialCouponCode: createForm.initialCouponCode.trim() || null,
          initialCouponPercent: createForm.initialCouponCode.trim() ? Number(createForm.initialCouponPercent) || 10 : null,
        },
      })
      setMessage('حساب همکار فروش ایجاد و فعال شد.')
      setCreateModalOpen(false)
      setCreateForm({
        customerPhone: '',
        customerName: '',
        tierId: '',
        customCommissionRate: '',
        shebaNumber: '',
        preferredSlug: '',
        initialCouponCode: '',
        initialCouponPercent: '10',
      })
      await loadAll()
    } catch (err) {
      setError(err.message)
    }
  }

  const handleSaveTierRate = async (event) => {
    event.preventDefault()
    if (!tierRateModal || !canManage) return
    setError('')
    try {
      await apiRequest(`/admin/affiliates/${tierRateModal.id}/tier-rate`, {
        method: 'PATCH',
        token: session.token,
        body: {
          tierId: tierRateForm.tierId || null,
          customCommissionRate: tierRateForm.customCommissionRate !== '' ? Number(tierRateForm.customCommissionRate) : null,
        },
      })
      setMessage('سطح و نرخ اختصاصی بازاریاب ذخیره شد.')
      setTierRateModal(null)
      await loadAll()
    } catch (err) {
      setError(err.message)
    }
  }

  const handleCreateCoupon = async (event) => {
    event.preventDefault()
    if (!couponModal || !canManage) return
    setError('')
    try {
      await apiRequest(`/admin/affiliates/${couponModal.id}/coupons`, {
        method: 'POST',
        token: session.token,
        body: {
          code: couponForm.code.trim(),
          discountType: couponForm.discountType,
          discountValue: Number(couponForm.discountValue) || 0,
          maxDiscountAmount: couponForm.maxDiscountAmount !== '' ? Number(couponForm.maxDiscountAmount) : null,
          isFirstOrderOnly: Boolean(couponForm.isFirstOrderOnly),
          usageLimitPerUser: couponForm.usageLimitPerUser !== '' ? Number(couponForm.usageLimitPerUser) : null,
        },
      })
      setMessage('کد تخفیف اختصاصی برای بازاریاب ثبت شد.')
      setCouponModal(null)
      setCouponForm({ code: '', discountType: 'Percentage', discountValue: '15', maxDiscountAmount: '', isFirstOrderOnly: false, usageLimitPerUser: '' })
      await loadAll()
    } catch (err) {
      setError(err.message)
    }
  }

  const handleSaveTier = async (event) => {
    event.preventDefault()
    if (!canManage) return
    setError('')
    try {
      await apiRequest('/admin/affiliates/tiers', {
        method: 'POST',
        token: session.token,
        body: {
          id: tierForm.id,
          name: tierForm.name.trim(),
          minMonthlySales: Number(tierForm.minMonthlySales) || 0,
          commissionPercentage: Number(tierForm.commissionPercentage) || 0,
          creditBonusPercentage: Number(tierForm.creditBonusPercentage) || 0,
        },
      })
      setMessage('سطح پلکانی ذخیره شد.')
      setTierForm({ id: null, name: '', minMonthlySales: '0', commissionPercentage: '5', creditBonusPercentage: '20' })
      await loadAll()
    } catch (err) {
      setError(err.message)
    }
  }

  const handleSaveCategoryRate = async (categoryId, rawPct) => {
    if (!canManage) return
    setError('')
    try {
      await apiRequest('/admin/affiliates/category-commissions', {
        method: 'PUT',
        token: session.token,
        body: {
          categoryId,
          commissionPercentage: rawPct === '' || rawPct === null ? null : Number(rawPct),
        },
      })
      setMessage('نرخ پورسانت دسته‌بندی بروزرسانی شد.')
      await loadAll()
    } catch (err) {
      setError(err.message)
    }
  }

  const handleCompletePayout = async (event) => {
    event.preventDefault()
    if (!payoutModal || !canManage) return
    setError('')
    try {
      await apiRequest(`/admin/affiliates/payouts/${payoutModal.id}/complete`, {
        method: 'POST',
        token: session.token,
        body: {
          trackingCode: payoutTrackingCode.trim() || null,
          receiptImageUrl: payoutReceiptUrl.trim() || null,
        },
      })
      setMessage('تسویه حساب ثبت شد و پیامک اطلاع‌رسانی برای بازاریاب ارسال گردید.')
      setPayoutModal(null)
      setPayoutTrackingCode('')
      setPayoutReceiptUrl('')
      await loadAll()
    } catch (err) {
      setError(err.message)
    }
  }

  const handleRejectPayout = async (payoutId) => {
    if (!canManage || !window.confirm('درخواست تسویه رد شود و مبلغ به موجودی نقدی بازاریاب برگردد؟')) return
    setError('')
    try {
      await apiRequest(`/admin/affiliates/payouts/${payoutId}/reject`, {
        method: 'POST',
        token: session.token,
      })
      setMessage('درخواست تسویه رد شد و مبلغ به موجودی بازاریاب برگشت.')
      await loadAll()
    } catch (err) {
      setError(err.message)
    }
  }

  const handleSaveMediaAsset = async (event) => {
    event.preventDefault()
    if (!canManage) return
    setError('')
    try {
      await apiRequest('/admin/affiliates/media-kit', {
        method: 'POST',
        token: session.token,
        body: {
          title: mediaForm.title.trim(),
          assetType: mediaForm.assetType,
          fileUrl: mediaForm.fileUrl.trim() || null,
          captionText: mediaForm.captionText.trim() || null,
          thumbnailUrl: mediaForm.assetType === 'Image' || mediaForm.assetType === 'Logo' ? (mediaForm.fileUrl.trim() || null) : null,
          displayOrder: Number(mediaForm.displayOrder) || 0,
          isActive: true,
        },
      })
      setMessage('محتوای تبلیغاتی در Media Kit ثبت شد.')
      setMediaForm({ title: '', assetType: 'Image', fileUrl: '', captionText: '', displayOrder: '0' })
      await loadAll()
    } catch (err) {
      setError(err.message)
    }
  }

  const handleDeleteMediaAsset = async (id) => {
    if (!canManage || !window.confirm('این آیتم از بانک محتوا حذف شود؟')) return
    setError('')
    try {
      await apiRequest(`/admin/affiliates/media-kit/${id}`, { method: 'DELETE', token: session.token })
      setMessage('آیتم حذف شد.')
      await loadAll()
    } catch (err) {
      setError(err.message)
    }
  }

  return (
    <div className="aff-admin-page">
      <header className="aff-admin-head">
        <div>
          <h1>مدیریت همکاری در فروش (Affiliate)</h1>
          <p>تایید بازاریاب‌ها، سطوح پلکانی، پورسانت دسته‌بندی‌ها، تسویه‌های بانکی و بانک محتوای تبلیغاتی</p>
        </div>
        <div style={{ display: 'flex', gap: '0.5rem' }}>
          {canManage && (
            <button type="button" onClick={() => setCreateModalOpen(true)}>
              + افزودن بازاریاب جدید
            </button>
          )}
          <button type="button" className="ghost-button" onClick={loadAll} disabled={loading}>
            بروزرسانی
          </button>
        </div>
      </header>

      {/* KPIs */}
      <section className="aff-admin-kpis">
        <article className="aff-admin-kpi">
          <span>کل بازاریاب‌ها</span>
          <strong>{toPersianNumber(summary?.totalAffiliates ?? 0)}</strong>
          <span>{toPersianNumber(summary?.activeAffiliates ?? 0)} فعال • {toPersianNumber(summary?.pendingAffiliates ?? 0)} در انتظار</span>
        </article>
        <article className="aff-admin-kpi">
          <span>مجموع کلیک‌های ثبت‌شده</span>
          <strong>{toPersianNumber(summary?.totalClicks ?? 0)}</strong>
          <span>ردیابی آنلاین و کمپین‌ها</span>
        </article>
        <article className="aff-admin-kpi">
          <span>کل پورسانت تاییدشده</span>
          <strong>{toPersianNumber(summary?.totalApprovedCommission ?? 0)} ت</strong>
          <span>تعهد نقدی فعلی: {toPersianNumber(summary?.totalCashLiability ?? 0)} ت</span>
        </article>
        <article className="aff-admin-kpi">
          <span>درخواست‌های تسویه باز</span>
          <strong>{toPersianNumber(summary?.openPayoutsCount ?? 0)}</strong>
          <span>نیازمند بررسی و واریز</span>
        </article>
      </section>

      {/* Tabs */}
      <nav className="aff-admin-tabs">
        <button type="button" className={`aff-admin-tab ${activeTab === 'affiliates' ? 'aff-admin-tab--active' : ''}`} onClick={() => setActiveTab('affiliates')}>
          بازاریاب‌ها ({toPersianNumber(affiliates.length)})
        </button>
        <button type="button" className={`aff-admin-tab ${activeTab === 'tiers' ? 'aff-admin-tab--active' : ''}`} onClick={() => setActiveTab('tiers')}>
          سطوح و پورسانت دسته‌بندی‌ها
        </button>
        <button type="button" className={`aff-admin-tab ${activeTab === 'payouts' ? 'aff-admin-tab--active' : ''}`} onClick={() => setActiveTab('payouts')}>
          درخواست‌های تسویه ({toPersianNumber(summary?.openPayoutsCount ?? 0)})
        </button>
        <button type="button" className={`aff-admin-tab ${activeTab === 'media' ? 'aff-admin-tab--active' : ''}`} onClick={() => setActiveTab('media')}>
          بانک محتوا (Media Kit)
        </button>
      </nav>

      {/* TAB 1: Affiliates Directory */}
      {activeTab === 'affiliates' && (
        <section className="aff-admin-panel">
          <div className="aff-admin-filters">
            <input
              type="text"
              style={{ flex: '1 1 220px' }}
              placeholder="جستجو نام، موبایل یا شبا..."
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
            <select value={statusFilter} onChange={(e) => setStatusFilter(e.target.value)}>
              <option value="">همه وضعیت‌ها</option>
              <option value="Pending">در انتظار تایید</option>
              <option value="Active">فعال</option>
              <option value="Suspended">مسدود / معلق</option>
            </select>
          </div>

          <div className="aff-admin-table-wrap">
            <table className="aff-admin-table">
              <thead>
                <tr>
                  <th>بازاریاب</th>
                  <th>وضعیت</th>
                  <th>سطح / نرخ</th>
                  <th>عملکرد</th>
                  <th>موجودی نقدی</th>
                  <th>کدها</th>
                  {canManage && <th style={{ textAlign: 'left' }}>عملیات</th>}
                </tr>
              </thead>
              <tbody>
                {affiliates.length === 0 ? (
                  <tr><td colSpan={7} style={{ textAlign: 'center', padding: '2rem', color: 'var(--muted)' }}>بازاریابی یافت نشد.</td></tr>
                ) : affiliates.map((aff) => {
                  const st = STATUS_META[aff.status] ?? { label: aff.status, cls: 'pending' }
                  return (
                    <tr key={aff.id}>
                      <td>
                        <strong>{aff.customerName}</strong>
                        <div style={{ fontSize: '0.76rem', color: 'var(--muted)' }} dir="ltr">{aff.phoneNumber}</div>
                        {aff.shebaNumber ? <div style={{ fontSize: '0.72rem', color: 'var(--muted)' }} dir="ltr">{aff.shebaNumber}</div> : null}
                      </td>
                      <td><span className={`aff-status-chip aff-status-chip--${st.cls}`}>{st.label}</span></td>
                      <td>
                        <div><strong>{aff.tierName}</strong> ({toPersianNumber(aff.effectiveCommissionPercentage)}٪)</div>
                        {aff.customCommissionRate != null ? (
                          <small style={{ color: '#4f46e5', fontWeight: 700 }}>نرخ VIP ثابت: {toPersianNumber(aff.customCommissionRate)}٪</small>
                        ) : null}
                      </td>
                      <td style={{ fontSize: '0.78rem' }}>
                        <div>{toPersianNumber(aff.totalClicks)} کلیک • {toPersianNumber(aff.ordersCount)} سفارش</div>
                        <div style={{ color: 'var(--muted)' }}>فروش: {toPersianNumber(aff.approvedSales)} ت</div>
                      </td>
                      <td style={{ fontWeight: 700, color: '#059669', whiteSpace: 'nowrap' }}>
                        {toPersianNumber(aff.cashBalance)} ت
                        <div style={{ fontSize: '0.74rem', color: 'var(--muted)', fontWeight: 400 }}>معلق: {toPersianNumber(aff.pendingCommission)} ت</div>
                      </td>
                      <td>
                        {(aff.coupons ?? []).map((c) => (
                          <span key={c.id} className="aff-status-chip aff-status-chip--active" style={{ marginLeft: '0.25rem' }}>{c.code}</span>
                        ))}
                      </td>
                      {canManage && (
                        <td>
                          <div style={{ display: 'flex', gap: '0.3rem', justifyContent: 'flex-end', flexWrap: 'wrap' }}>
                            {aff.status !== 'Active' && (
                              <button type="button" className="aff-btn-xs aff-btn-xs--ok" onClick={() => handleStatusChange(aff.id, 'Active')}>تایید</button>
                            )}
                            {aff.status !== 'Suspended' && (
                              <button type="button" className="aff-btn-xs aff-btn-xs--danger" onClick={() => handleStatusChange(aff.id, 'Suspended')}>مسدود</button>
                            )}
                            <button
                              type="button"
                              className="aff-btn-xs"
                              onClick={() => {
                                setTierRateModal(aff)
                                setTierRateForm({
                                  tierId: aff.tierId ?? '',
                                  customCommissionRate: aff.customCommissionRate != null ? String(aff.customCommissionRate) : '',
                                })
                              }}
                            >
                              سطح / VIP
                            </button>
                            <button type="button" className="aff-btn-xs" onClick={() => setCouponModal(aff)}>+ کد تخفیف</button>
                          </div>
                        </td>
                      )}
                    </tr>
                  )
                })}
              </tbody>
            </table>
          </div>
        </section>
      )}

      {/* TAB 2: Tiers & Category Commissions */}
      {activeTab === 'tiers' && (
        <>
          <section className="aff-admin-panel">
            <h2 style={{ margin: 0, fontSize: '1.02rem' }}>سطوح پلکانی (AffiliateTiers)</h2>
            {canManage && (
              <form className="aff-admin-form-grid" onSubmit={handleSaveTier}>
                <div className="aff-admin-field">
                  <label>نام سطح</label>
                  <input required placeholder="مثلاً طلایی" value={tierForm.name} onChange={(e) => setTierForm((p) => ({ ...p, name: e.target.value }))} />
                </div>
                <div className="aff-admin-field">
                  <label>حداقل فروش ماهانه (تومان)</label>
                  <input type="number" min="0" value={tierForm.minMonthlySales} onChange={(e) => setTierForm((p) => ({ ...p, minMonthlySales: e.target.value }))} />
                </div>
                <div className="aff-admin-field">
                  <label>درصد پورسانت (٪)</label>
                  <input type="number" step="0.1" min="0" max="100" value={tierForm.commissionPercentage} onChange={(e) => setTierForm((p) => ({ ...p, commissionPercentage: e.target.value }))} />
                </div>
                <div className="aff-admin-field">
                  <label>بونوس اعتبار کافه (٪)</label>
                  <input type="number" step="0.1" min="0" max="100" value={tierForm.creditBonusPercentage} onChange={(e) => setTierForm((p) => ({ ...p, creditBonusPercentage: e.target.value }))} />
                </div>
                <button type="submit">{tierForm.id ? 'بروزرسانی سطح' : 'افزودن سطح'}</button>
              </form>
            )}
            <div className="aff-admin-table-wrap">
              <table className="aff-admin-table">
                <thead>
                  <tr><th>نام سطح</th><th>حداقل فروش ۳۰ روزه</th><th>پورسانت</th><th>بونوس اعتبار کافه</th><th>بازاریاب‌ها</th>{canManage && <th></th>}</tr>
                </thead>
                <tbody>
                  {tiers.map((t) => (
                    <tr key={t.id}>
                      <td><strong>{t.name}</strong></td>
                      <td>{toPersianNumber(t.minMonthlySales)} تومان</td>
                      <td>{toPersianNumber(t.commissionPercentage)}٪</td>
                      <td>+{toPersianNumber(t.creditBonusPercentage)}٪</td>
                      <td>{toPersianNumber(t.affiliatesCount)}</td>
                      {canManage && (
                        <td style={{ textAlign: 'left' }}>
                          <button type="button" className="aff-btn-xs" onClick={() => setTierForm({
                            id: t.id,
                            name: t.name,
                            minMonthlySales: String(t.minMonthlySales),
                            commissionPercentage: String(t.commissionPercentage),
                            creditBonusPercentage: String(t.creditBonusPercentage),
                          })}>ویرایش</button>
                        </td>
                      )}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </section>

          <section className="aff-admin-panel">
            <h2 style={{ margin: 0, fontSize: '1.02rem' }}>پورسانت اختصاصی دسته‌بندی‌های منو (CategoryCommissions)</h2>
            <div className="aff-admin-table-wrap">
              <table className="aff-admin-table">
                <thead><tr><th>دسته‌بندی</th><th>درصد پورسانت (خالی = نرخ سطح)</th>{canManage && <th>ذخیره</th>}</tr></thead>
                <tbody>
                  {categoryRates.map((cat) => (
                    <CategoryRateRow key={cat.categoryId} cat={cat} canManage={canManage} onSave={handleSaveCategoryRate} />
                  ))}
                </tbody>
              </table>
            </div>
          </section>
        </>
      )}

      {/* TAB 3: Payouts Management */}
      {activeTab === 'payouts' && (
        <section className="aff-admin-panel">
          <div className="aff-admin-filters">
            <select value={payoutStatusFilter} onChange={(e) => setPayoutStatusFilter(e.target.value)}>
              <option value="">همه تسویه‌ها</option>
              <option value="Requested">درخواست جدید</option>
              <option value="Completed">واریز شده</option>
              <option value="Rejected">رد شده</option>
            </select>
          </div>
          <div className="aff-admin-table-wrap">
            <table className="aff-admin-table">
              <thead>
                <tr><th>بازاریاب / شبا</th><th>نوع</th><th>مبلغ</th><th>وضعیت</th><th>کد رهگیری / فیش</th><th>زمان</th>{canManage && <th>عملیات</th>}</tr>
              </thead>
              <tbody>
                {payouts.length === 0 ? (
                  <tr><td colSpan={7} style={{ textAlign: 'center', padding: '2rem', color: 'var(--muted)' }}>درخواست تسویه‌ای ثبت نشده است.</td></tr>
                ) : payouts.map((p) => (
                  <tr key={p.id}>
                    <td>
                      <strong>{p.affiliateName}</strong>
                      <div style={{ fontSize: '0.75rem', color: 'var(--muted)' }} dir="ltr">{p.shebaNumber || p.phoneNumber}</div>
                    </td>
                    <td>{p.payoutType === 'WalletCredit' ? 'اعتبار کافه' : 'واریز بانکی'}</td>
                    <td style={{ fontWeight: 700 }}>{toPersianNumber(p.amount)} ت</td>
                    <td><span className="aff-status-chip aff-status-chip--pending">{PAYOUT_STATUS_LABELS[p.status] ?? p.status}</span></td>
                    <td>
                      <div dir="ltr">{p.trackingCode || '—'}</div>
                      {p.receiptImageUrl ? <a href={p.receiptImageUrl} target="_blank" rel="noreferrer" style={{ fontSize: '0.75rem' }}>مشاهده فیش</a> : null}
                    </td>
                    <td style={{ fontSize: '0.78rem' }}>{formatDateTime(p.createdAt)}</td>
                    {canManage && (
                      <td>
                        {(p.status === 'Requested' || p.status === 'Processing') && (
                          <div style={{ display: 'flex', gap: '0.3rem' }}>
                            <button type="button" className="aff-btn-xs aff-btn-xs--ok" onClick={() => setPayoutModal(p)}>ثبت واریز</button>
                            <button type="button" className="aff-btn-xs aff-btn-xs--danger" onClick={() => handleRejectPayout(p.id)}>رد</button>
                          </div>
                        )}
                      </td>
                    )}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      )}

      {/* TAB 4: Media Kit */}
      {activeTab === 'media' && (
        <section className="aff-admin-panel">
          <h2 style={{ margin: 0, fontSize: '1.02rem' }}>مدیریت بانک محتوای تبلیغاتی (Media Kit)</h2>
          {canManage && (
            <form className="aff-admin-form-grid" onSubmit={handleSaveMediaAsset}>
              <div className="aff-admin-field">
                <label>عنوان</label>
                <input required placeholder="مثلاً بنر استوری آیس لاته" value={mediaForm.title} onChange={(e) => setMediaForm((p) => ({ ...p, title: e.target.value }))} />
              </div>
              <div className="aff-admin-field">
                <label>نوع محتوا</label>
                <select value={mediaForm.assetType} onChange={(e) => setMediaForm((p) => ({ ...p, assetType: e.target.value }))}>
                  <option value="Image">تصویر / بنر</option>
                  <option value="Video">ویدیو ریلز</option>
                  <option value="Logo">لوگو</option>
                  <option value="CopyText">متن تبلیغاتی</option>
                </select>
              </div>
              {mediaForm.assetType !== 'CopyText' && (
                <div className="aff-admin-field">
                  <label>آپلود فایل یا لینک</label>
                  <input
                    type="file"
                    disabled={uploadingFile}
                    onChange={async (e) => {
                      const url = await uploadAdminFile(e.target.files?.[0])
                      if (url) setMediaForm((p) => ({ ...p, fileUrl: url }))
                    }}
                  />
                  <input placeholder="/uploads/..." value={mediaForm.fileUrl} onChange={(e) => setMediaForm((p) => ({ ...p, fileUrl: e.target.value }))} />
                </div>
              )}
              <div className="aff-admin-field" style={{ gridColumn: '1 / -1' }}>
                <label>متن تبلیغاتی پیشنهادی (کپشن استوری/پست)</label>
                <textarea rows={2} value={mediaForm.captionText} onChange={(e) => setMediaForm((p) => ({ ...p, captionText: e.target.value }))} />
              </div>
              <button type="submit" disabled={uploadingFile}>{uploadingFile ? 'در حال آپلود...' : 'افزودن به Media Kit'}</button>
            </form>
          )}
          <div className="aff-media-admin-grid">
            {mediaKit.map((asset) => (
              <article key={asset.id} className="aff-media-admin-card">
                {(asset.thumbnailUrl || asset.fileUrl) && (asset.assetType === 'Image' || asset.assetType === 'Logo') && (
                  <img src={asset.thumbnailUrl || asset.fileUrl} alt={asset.title} />
                )}
                <strong>{asset.title}</strong>
                {asset.captionText && <small style={{ color: 'var(--muted)' }}>{asset.captionText}</small>}
                {canManage && (
                  <button type="button" className="aff-btn-xs aff-btn-xs--danger" onClick={() => handleDeleteMediaAsset(asset.id)}>حذف</button>
                )}
              </article>
            ))}
          </div>
        </section>
      )}

      {/* Modal: Create Affiliate */}
      {createModalOpen && (
        <div className="aff-admin-modal-backdrop" onClick={(e) => { if (e.target === e.currentTarget) setCreateModalOpen(false) }}>
          <form className="aff-admin-modal" onSubmit={handleCreateAffiliate}>
            <h2 style={{ margin: 0 }}>افزودن مستقیم همکار فروش</h2>
            <div className="aff-admin-field">
              <label>شماره موبایل مشتری *</label>
              <input required dir="ltr" placeholder="09123456789" value={createForm.customerPhone} onChange={(e) => setCreateForm((p) => ({ ...p, customerPhone: e.target.value }))} />
            </div>
            <div className="aff-admin-field">
              <label>نام (اگر مشتری جدید است)</label>
              <input value={createForm.customerName} onChange={(e) => setCreateForm((p) => ({ ...p, customerName: e.target.value }))} />
            </div>
            <div className="aff-admin-field">
              <label>سطح اولیه</label>
              <select value={createForm.tierId} onChange={(e) => setCreateForm((p) => ({ ...p, tierId: e.target.value }))}>
                <option value="">سطح پایه خودکار</option>
                {tiers.map((t) => <option key={t.id} value={t.id}>{t.name} ({toPersianNumber(t.commissionPercentage)}٪)</option>)}
              </select>
            </div>
            <div className="aff-admin-field">
              <label>نرخ پورسانت VIP ثابت (٪ — اختیاری)</label>
              <input type="number" step="0.1" min="0" max="100" value={createForm.customCommissionRate} onChange={(e) => setCreateForm((p) => ({ ...p, customCommissionRate: e.target.value }))} />
            </div>
            <div className="aff-admin-field">
              <label>کد تخفیف اولیه (مثلاً ALI15 — اختیاری)</label>
              <input dir="ltr" value={createForm.initialCouponCode} onChange={(e) => setCreateForm((p) => ({ ...p, initialCouponCode: e.target.value }))} />
            </div>
            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '0.5rem' }}>
              <button type="button" className="ghost-button" onClick={() => setCreateModalOpen(false)}>انصراف</button>
              <button type="submit">ایجاد و فعال‌سازی</button>
            </div>
          </form>
        </div>
      )}

      {/* Modal: Tier & VIP Rate */}
      {tierRateModal && (
        <div className="aff-admin-modal-backdrop" onClick={(e) => { if (e.target === e.currentTarget) setTierRateModal(null) }}>
          <form className="aff-admin-modal" onSubmit={handleSaveTierRate}>
            <h2 style={{ margin: 0 }}>تنظیم سطح و نرخ VIP — {tierRateModal.customerName}</h2>
            <div className="aff-admin-field">
              <label>سطح</label>
              <select value={tierRateForm.tierId} onChange={(e) => setTierRateForm((p) => ({ ...p, tierId: e.target.value }))}>
                <option value="">بدون سطح</option>
                {tiers.map((t) => <option key={t.id} value={t.id}>{t.name} ({toPersianNumber(t.commissionPercentage)}٪)</option>)}
              </select>
            </div>
            <div className="aff-admin-field">
              <label>نرخ پورسانت ثابت VIP (٪ — خالی برای استفاده از قوانین پلکانی)</label>
              <input type="number" step="0.1" min="0" max="100" value={tierRateForm.customCommissionRate} onChange={(e) => setTierRateForm((p) => ({ ...p, customCommissionRate: e.target.value }))} />
            </div>
            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '0.5rem' }}>
              <button type="button" className="ghost-button" onClick={() => setTierRateModal(null)}>انصراف</button>
              <button type="submit">ذخیره</button>
            </div>
          </form>
        </div>
      )}

      {/* Modal: Create Coupon */}
      {couponModal && (
        <div className="aff-admin-modal-backdrop" onClick={(e) => { if (e.target === e.currentTarget) setCouponModal(null) }}>
          <form className="aff-admin-modal" onSubmit={handleCreateCoupon}>
            <h2 style={{ margin: 0 }}>ساخت کد تخفیف اختصاصی — {couponModal.customerName}</h2>
            <div className="aff-admin-field">
              <label>کد تخفیف (مثلاً ALI15)</label>
              <input required dir="ltr" value={couponForm.code} onChange={(e) => setCouponForm((p) => ({ ...p, code: e.target.value }))} />
            </div>
            <div className="aff-admin-field">
              <label>نوع تخفیف</label>
              <select value={couponForm.discountType} onChange={(e) => setCouponForm((p) => ({ ...p, discountType: e.target.value }))}>
                <option value="Percentage">درصدی</option>
                <option value="FixedAmount">مبلغ ثابت (تومان)</option>
              </select>
            </div>
            <div className="aff-admin-field">
              <label>مقدار تخفیف</label>
              <input required type="number" min="1" value={couponForm.discountValue} onChange={(e) => setCouponForm((p) => ({ ...p, discountValue: e.target.value }))} />
            </div>
            <label style={{ display: 'flex', alignItems: 'center', gap: '0.45rem', fontSize: '0.84rem' }}>
              <input type="checkbox" checked={couponForm.isFirstOrderOnly} onChange={(e) => setCouponForm((p) => ({ ...p, isFirstOrderOnly: e.target.checked }))} />
              فقط ویژه سفارش اول مشتریان جدید
            </label>
            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '0.5rem' }}>
              <button type="button" className="ghost-button" onClick={() => setCouponModal(null)}>انصراف</button>
              <button type="submit">ثبت کد تخفیف</button>
            </div>
          </form>
        </div>
      )}

      {/* Modal: Complete Payout with Tracking Code & Receipt Upload */}
      {payoutModal && (
        <div className="aff-admin-modal-backdrop" onClick={(e) => { if (e.target === e.currentTarget) setPayoutModal(null) }}>
          <form className="aff-admin-modal" onSubmit={handleCompletePayout}>
            <h2 style={{ margin: 0 }}>تکمیل تسویه بانکی — {payoutModal.affiliateName}</h2>
            <p style={{ margin: 0, fontSize: '0.85rem' }}>
              مبلغ: <strong>{toPersianNumber(payoutModal.amount)} تومان</strong> • شبا: <span dir="ltr">{payoutModal.shebaNumber}</span>
            </p>
            <div className="aff-admin-field">
              <label>کد رهگیری بانکی</label>
              <input required dir="ltr" placeholder="مثلاً 1234567890" value={payoutTrackingCode} onChange={(e) => setPayoutTrackingCode(e.target.value)} />
            </div>
            <div className="aff-admin-field">
              <label>آپلود تصویر فیش واریزی (اختیاری)</label>
              <input
                type="file"
                accept="image/*"
                disabled={uploadingFile}
                onChange={async (e) => {
                  const url = await uploadAdminFile(e.target.files?.[0])
                  if (url) setPayoutReceiptUrl(url)
                }}
              />
              {payoutReceiptUrl && <small style={{ color: '#059669' }}>فیش آپلود شد: {payoutReceiptUrl}</small>}
            </div>
            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '0.5rem' }}>
              <button type="button" className="ghost-button" onClick={() => setPayoutModal(null)}>انصراف</button>
              <button type="submit" disabled={uploadingFile}>تایید واریز و ارسال پیامک</button>
            </div>
          </form>
        </div>
      )}
    </div>
  )
}

function CategoryRateRow({ cat, canManage, onSave }) {
  const [val, setVal] = useState(cat.commissionPercentage != null ? String(cat.commissionPercentage) : '')
  useEffect(() => {
    setVal(cat.commissionPercentage != null ? String(cat.commissionPercentage) : '')
  }, [cat.commissionPercentage])

  return (
    <tr>
      <td><strong>{cat.categoryName}</strong></td>
      <td>
        <input
          type="number"
          step="0.1"
          min="0"
          max="100"
          disabled={!canManage}
          placeholder="نرخ سطح پیش‌فرض"
          style={{ width: '150px', minHeight: '34px', padding: '0.3rem 0.6rem', borderRadius: '8px', border: '1px solid var(--border)' }}
          value={val}
          onChange={(e) => setVal(e.target.value)}
        />
      </td>
      {canManage && (
        <td>
          <button type="button" className="aff-btn-xs aff-btn-xs--ok" onClick={() => onSave(cat.categoryId, val)}>ذخیره</button>
        </td>
      )}
    </tr>
  )
}
