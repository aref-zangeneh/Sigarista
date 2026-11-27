import { useCallback, useEffect, useMemo, useState } from 'react'
import { useAdmin } from '../context/AdminContext'
import { apiRequest } from '../utils/adminApi'
import { formatDateTime, toPersianNumber, toPersianNumber as formatPrice } from '../utils/adminFormatters'
import { hasPermission, PERMISSIONS } from '../utils/adminPermissions'
import { ShamsiDatePicker } from '../components/ShamsiDatePicker.jsx'
import {
  buildWalletsQueryString,
  buildWalletTransactionsQueryString,
  computeBalancePreview,
  getWalletTxMeta,
  parseWalletAmount,
  QUICK_TOPUP_PRESETS,
  WALLET_TX_TYPE_OPTIONS,
} from '../utils/walletHelpers.js'
import './WalletsPage.css'

const INITIAL_WALLET_FILTERS = {
  page: 1,
  pageSize: 25,
  search: '',
  balanceFilter: 'all',
  minBalance: '',
  maxBalance: '',
  sortBy: 'balance_desc',
}

const INITIAL_TX_FILTERS = {
  page: 1,
  pageSize: 30,
  customerId: '',
  type: '',
  direction: 'all',
  search: '',
  fromDate: '',
  toDate: '',
  minAmount: '',
  maxAmount: '',
}

const EMPTY_PACKAGE_FORM = {
  id: null,
  amount: '',
  bonusAmount: '0',
  displayOrder: '0',
  isEnabled: true,
}

export function WalletsPage() {
  const { session, setMessage, setError } = useAdmin()
  const canManage = hasPermission(session, PERMISSIONS.wallets.update)

  const [activeTab, setActiveTab] = useState('wallets')

  // Summary & KPIs
  const [summary, setSummary] = useState(null)
  const [summaryLoading, setSummaryLoading] = useState(false)
  const [expiringCashback, setExpiringCashback] = useState(false)

  // Directory tab
  const [walletFilters, setWalletFilters] = useState(INITIAL_WALLET_FILTERS)
  const [walletsData, setWalletsData] = useState({ items: [], totalCount: 0, pageNumber: 1, totalPages: 1 })
  const [walletsLoading, setWalletsLoading] = useState(false)

  // Global Ledger tab
  const [txFilters, setTxFilters] = useState(INITIAL_TX_FILTERS)
  const [txData, setTxData] = useState({ items: [], totalCount: 0, pageNumber: 1, totalPages: 1 })
  const [txLoading, setTxLoading] = useState(false)

  // Packages tab
  const [packages, setPackages] = useState([])
  const [packagesLoading, setPackagesLoading] = useState(false)
  const [packageForm, setPackageForm] = useState(EMPTY_PACKAGE_FORM)
  const [packageSaving, setPackageSaving] = useState(false)

  // Settings tab
  const [settingsForm, setSettingsForm] = useState(null)
  const [settingsSaving, setSettingsSaving] = useState(false)

  // Adjust / Package Charge Modal
  const [adjustModal, setAdjustModal] = useState(null) // { customer, mode: 'charge' | 'debit' | 'package' }
  const [adjustAmount, setAdjustAmount] = useState('')
  const [adjustNote, setAdjustNote] = useState('')
  const [selectedPackageId, setSelectedPackageId] = useState('')
  const [adjustSaving, setAdjustSaving] = useState(false)

  // Customer Wallet History Drawer/Modal
  const [historyModal, setHistoryModal] = useState(null) // AdminCustomerWalletDto
  const [historyLoading, setHistoryLoading] = useState(false)

  const enabledPackages = useMemo(
    () => packages.filter((pack) => pack.isEnabled !== false),
    [packages],
  )

  const loadSummary = useCallback(async () => {
    if (!session?.token) return
    setSummaryLoading(true)
    try {
      const data = await apiRequest('/admin/wallets/summary', { token: session.token })
      setSummary(data)
      if (data?.settings) {
        setSettingsForm({
          walletEnabled: Boolean(data.settings.walletEnabled),
          walletCashbackPercent: String(data.settings.walletCashbackPercent ?? 0),
          walletCashbackMinOrderAmount: String(data.settings.walletCashbackMinOrderAmount ?? 0),
          walletCashbackMaxPerOrderAmount: String(data.settings.walletCashbackMaxPerOrderAmount ?? 0),
          walletCashbackExpiryDays: String(data.settings.walletCashbackExpiryDays ?? 0),
          walletPaymentMaxPercent: String(data.settings.walletPaymentMaxPercent ?? 100),
          pickupRefundMode: data.settings.pickupRefundMode || 'Wallet',
        })
      }
    } catch (err) {
      setError(err.message || 'خطا در دریافت خلاصه کیف پول‌ها.')
    } finally {
      setSummaryLoading(false)
    }
  }, [session?.token, setError])

  const loadWallets = useCallback(async (filters = walletFilters) => {
    if (!session?.token) return
    setWalletsLoading(true)
    try {
      const qs = buildWalletsQueryString(filters)
      const data = await apiRequest(`/admin/wallets?${qs}`, { token: session.token })
      setWalletsData(data ?? { items: [], totalCount: 0, pageNumber: 1, totalPages: 1 })
    } catch (err) {
      setError(err.message || 'خطا در دریافت فهرست کیف پول‌ها.')
    } finally {
      setWalletsLoading(false)
    }
  }, [session?.token, walletFilters, setError])

  const loadTransactions = useCallback(async (filters = txFilters) => {
    if (!session?.token) return
    setTxLoading(true)
    try {
      const qs = buildWalletTransactionsQueryString(filters)
      const data = await apiRequest(`/admin/wallets/transactions?${qs}`, { token: session.token })
      setTxData(data ?? { items: [], totalCount: 0, pageNumber: 1, totalPages: 1 })
    } catch (err) {
      setError(err.message || 'خطا در دریافت دفتر کل تراکنش‌ها.')
    } finally {
      setTxLoading(false)
    }
  }, [session?.token, txFilters, setError])

  const loadPackages = useCallback(async () => {
    if (!session?.token) return
    setPackagesLoading(true)
    try {
      const data = await apiRequest('/admin/wallets/packages', { token: session.token })
      setPackages(Array.isArray(data) ? data : [])
    } catch (err) {
      setError(err.message || 'خطا در دریافت بسته‌های شارژ.')
    } finally {
      setPackagesLoading(false)
    }
  }, [session?.token, setError])

  useEffect(() => {
    loadSummary()
    loadPackages()
  }, [loadSummary, loadPackages])

  useEffect(() => {
    if (activeTab === 'wallets') {
      loadWallets(walletFilters)
    }
  }, [activeTab, walletFilters, loadWallets])

  useEffect(() => {
    if (activeTab === 'transactions') {
      loadTransactions(txFilters)
    }
  }, [activeTab, txFilters, loadTransactions])

  const refreshAll = async () => {
    await Promise.all([
      loadSummary(),
      loadPackages(),
      activeTab === 'wallets' ? loadWallets(walletFilters) : Promise.resolve(),
      activeTab === 'transactions' ? loadTransactions(txFilters) : Promise.resolve(),
    ])
  }

  const openAdjustModal = (customer, mode = 'charge') => {
    setAdjustModal({ customer, mode })
    setAdjustAmount('')
    setAdjustNote('')
    setSelectedPackageId(enabledPackages[0]?.id ?? '')
  }

  const openHistoryModal = async (customerId) => {
    if (!session?.token) return
    setHistoryLoading(true)
    setHistoryModal({ customerId, customerName: '...', phoneNumber: '', balance: 0, transactions: [] })
    try {
      const data = await apiRequest(`/admin/wallets/${customerId}`, { token: session.token })
      setHistoryModal(data)
    } catch (err) {
      setError(err.message || 'خطا در دریافت تاریخچه کیف پول مشتری.')
      setHistoryModal(null)
    } finally {
      setHistoryLoading(false)
    }
  }

  const balancePreview = useMemo(() => {
    if (!adjustModal) return null
    if (adjustModal.mode === 'package') {
      const pack = enabledPackages.find((p) => p.id === selectedPackageId)
      const base = Number(adjustModal.customer.balance) || 0
      const add = pack ? (Number(pack.amount) || 0) + (Number(pack.bonusAmount) || 0) : 0
      return {
        amount: add,
        signedAmount: add,
        nextBalance: base + add,
        isOverdraft: false,
        isValid: Boolean(pack),
        packageObj: pack ?? null,
      }
    }
    return computeBalancePreview(adjustModal.customer.balance, adjustAmount, adjustModal.mode)
  }, [adjustModal, adjustAmount, enabledPackages, selectedPackageId])

  const handleAdjustSubmit = async (event) => {
    event.preventDefault()
    if (!adjustModal || !canManage || !balancePreview?.isValid) return

    setAdjustSaving(true)
    setError('')
    try {
      let updatedWallet
      if (adjustModal.mode === 'package') {
        updatedWallet = await apiRequest(`/admin/wallets/${adjustModal.customer.customerId}/topup-package`, {
          method: 'POST',
          token: session.token,
          body: {
            customerId: adjustModal.customer.customerId,
            packageId: selectedPackageId,
            note: adjustNote.trim() || null,
          },
        })
        setMessage(`بسته شارژ برای ${adjustModal.customer.customerName} با موفقیت اعمال شد.`)
      } else {
        updatedWallet = await apiRequest(`/admin/wallets/${adjustModal.customer.customerId}/adjust`, {
          method: 'POST',
          token: session.token,
          body: {
            customerId: adjustModal.customer.customerId,
            amount: balancePreview.signedAmount,
            description: adjustNote.trim() || null,
          },
        })
        setMessage(
          adjustModal.mode === 'charge'
            ? `کیف پول ${adjustModal.customer.customerName} به مبلغ ${formatPrice(balancePreview.amount)} تومان شارژ شد.`
            : `مبلغ ${formatPrice(balancePreview.amount)} تومان از کیف پول ${adjustModal.customer.customerName} کسر شد.`,
        )
      }

      setAdjustModal(null)
      if (historyModal?.customerId === adjustModal.customer.customerId && updatedWallet) {
        setHistoryModal(updatedWallet)
      }
      await refreshAll()
    } catch (err) {
      setError(err.message || 'خطا در ثبت تراکنش کیف پول.')
    } finally {
      setAdjustSaving(false)
    }
  }

  const handleExpireCashback = async () => {
    if (!canManage) return
    setExpiringCashback(true)
    setError('')
    try {
      const result = await apiRequest('/admin/wallets/expire-cashback', {
        method: 'POST',
        token: session.token,
      })
      if (result.expiredLotsCount > 0) {
        setMessage(
          `${toPersianNumber(result.expiredLotsCount)} مورد کش‌بک منقضی‌شده در ${toPersianNumber(result.affectedWalletsCount)} کیف پول تسویه شد.`,
        )
      } else {
        setMessage('هیچ کش‌بک منقضی‌شده‌ای برای تسویه وجود نداشت.')
      }
      await refreshAll()
    } catch (err) {
      setError(err.message || 'خطا در تسویه کش‌بک‌های منقضی‌شده.')
    } finally {
      setExpiringCashback(false)
    }
  }

  const handleSavePackage = async (event) => {
    event.preventDefault()
    if (!canManage) return
    const amount = parseWalletAmount(packageForm.amount)
    const bonusRaw = packageForm.bonusAmount === '' || packageForm.bonusAmount === '0' ? 0 : parseWalletAmount(packageForm.bonusAmount)
    if (!amount) {
      setError('مبلغ شارژ بسته باید عددی مثبت باشد.')
      return
    }
    if (bonusRaw === null) {
      setError('مبلغ هدیه بسته نامعتبر است.')
      return
    }

    setPackageSaving(true)
    setError('')
    try {
      await apiRequest('/admin/wallets/packages', {
        method: 'POST',
        token: session.token,
        body: {
          id: packageForm.id,
          amount,
          bonusAmount: bonusRaw,
          displayOrder: Number(packageForm.displayOrder) || 0,
          isEnabled: Boolean(packageForm.isEnabled),
        },
      })
      setMessage(packageForm.id ? 'بسته شارژ بروزرسانی شد.' : 'بسته شارژ جدید ثبت شد.')
      setPackageForm(EMPTY_PACKAGE_FORM)
      await loadPackages()
    } catch (err) {
      setError(err.message || 'خطا در ذخیره بسته شارژ.')
    } finally {
      setPackageSaving(false)
    }
  }

  const handleDeletePackage = async (packId) => {
    if (!canManage || !window.confirm('آیا از حذف این بسته شارژ مطمئن هستید؟')) return
    setError('')
    try {
      await apiRequest(`/admin/wallets/packages/${packId}`, {
        method: 'DELETE',
        token: session.token,
      })
      setMessage('بسته شارژ حذف شد.')
      await loadPackages()
    } catch (err) {
      setError(err.message || 'خطا در حذف بسته شارژ.')
    }
  }

  const handleSaveSettings = async (event) => {
    event.preventDefault()
    if (!canManage || !settingsForm) return
    setSettingsSaving(true)
    setError('')
    try {
      await apiRequest('/admin/wallets/settings', {
        method: 'PUT',
        token: session.token,
        body: {
          walletEnabled: Boolean(settingsForm.walletEnabled),
          walletCashbackPercent: Number(settingsForm.walletCashbackPercent) || 0,
          walletCashbackMinOrderAmount: Number(settingsForm.walletCashbackMinOrderAmount) || 0,
          walletCashbackMaxPerOrderAmount: Number(settingsForm.walletCashbackMaxPerOrderAmount) || 0,
          walletCashbackExpiryDays: Number(settingsForm.walletCashbackExpiryDays) || 0,
          walletPaymentMaxPercent: Number(settingsForm.walletPaymentMaxPercent) || 100,
          pickupRefundMode: settingsForm.pickupRefundMode || 'Wallet',
        },
      })
      setMessage('تنظیمات کیف پول با موفقیت ذخیره شد.')
      await loadSummary()
    } catch (err) {
      setError(err.message || 'خطا در ذخیره تنظیمات کیف پول.')
    } finally {
      setSettingsSaving(false)
    }
  }

  return (
    <div className="wallets-page">
      {/* Header */}
      <header className="wallets-header">
        <div className="wallets-header-title">
          <h1>مدیریت کیف پول مشتریان</h1>
          <p>پایش موجودی‌ها، شارژ و تعدیل سریع، دفتر کل تراکنش‌ها، بسته‌های هدیه‌دار و قوانین کش‌بک</p>
        </div>
        <div className="wallets-header-actions">
          {summary ? (
            <span
              className={`wallets-status-pill ${
                summary.settings?.walletEnabled ? 'wallets-status-pill--on' : 'wallets-status-pill--off'
              }`}
            >
              {summary.settings?.walletEnabled ? '● کیف پول فعال' : '● کیف پول غیرفعال'}
            </span>
          ) : null}

          {canManage && summary?.dueExpiredCashbackCount > 0 ? (
            <button
              type="button"
              className="ghost-button"
              disabled={expiringCashback}
              onClick={handleExpireCashback}
            >
              {expiringCashback
                ? 'در حال تسویه...'
                : `تسویه ${toPersianNumber(summary.dueExpiredCashbackCount)} کش‌بک منقضی`}
            </button>
          ) : null}

          <button type="button" className="ghost-button" onClick={refreshAll} disabled={summaryLoading}>
            بروزرسانی
          </button>
        </div>
      </header>

      {/* KPI Strip */}
      <section className="wallets-kpi-grid" aria-label="خلاصه وضعیت کیف پول‌ها">
        <article className="wallets-kpi-card wallets-kpi-card--primary">
          <span className="wallets-kpi-label">کل موجودی در گردش (تعهد کافه)</span>
          <strong className="wallets-kpi-value">{formatPrice(summary?.totalBalance ?? 0)} تومان</strong>
          <span className="wallets-kpi-sub">
            {toPersianNumber(summary?.activeWalletsCount ?? 0)} کیف پول دارای موجودی از{' '}
            {toPersianNumber(summary?.totalCustomersCount ?? 0)} مشتری
          </span>
        </article>

        <article className="wallets-kpi-card">
          <span className="wallets-kpi-label">مجموع شارژهای انجام‌شده</span>
          <strong className="wallets-kpi-value">{formatPrice(summary?.totalTopUpAmount ?? 0)} تومان</strong>
          <span className="wallets-kpi-sub">
            مجموع {toPersianNumber(summary?.totalTransactionsCount ?? 0)} تراکنش ثبت‌شده
          </span>
        </article>

        <article className="wallets-kpi-card">
          <span className="wallets-kpi-label">کش‌بک و هدایای اعطاشده</span>
          <strong className="wallets-kpi-value">{formatPrice(summary?.totalCashbackAmount ?? 0)} تومان</strong>
          <span className="wallets-kpi-sub">
            نرخ فعلی: {toPersianNumber(summary?.settings?.walletCashbackPercent ?? 0)}٪
            {(summary?.expiringSoonCashbackAmount ?? 0) > 0
              ? ` • ${formatPrice(summary.expiringSoonCashbackAmount)} ت در ۷ روز آینده منقضی`
              : ''}
          </span>
        </article>

        <article className="wallets-kpi-card">
          <span className="wallets-kpi-label">پرداخت‌شده در سفارش‌ها</span>
          <strong className="wallets-kpi-value">{formatPrice(summary?.totalPurchaseAmount ?? 0)} تومان</strong>
          <span className="wallets-kpi-sub">
            برگشت وجه لغو: {formatPrice(summary?.totalRefundAmount ?? 0)} تومان
          </span>
        </article>
      </section>

      {/* Tabs */}
      <nav className="wallets-tabs" aria-label="بخش‌های مدیریت کیف پول">
        <button
          type="button"
          className={`wallets-tab ${activeTab === 'wallets' ? 'wallets-tab--active' : ''}`}
          onClick={() => setActiveTab('wallets')}
        >
          کیف پول مشتریان
        </button>
        <button
          type="button"
          className={`wallets-tab ${activeTab === 'transactions' ? 'wallets-tab--active' : ''}`}
          onClick={() => setActiveTab('transactions')}
        >
          دفتر کل تراکنش‌ها
        </button>
        <button
          type="button"
          className={`wallets-tab ${activeTab === 'packages' ? 'wallets-tab--active' : ''}`}
          onClick={() => setActiveTab('packages')}
        >
          بسته‌های شارژ ({toPersianNumber(packages.length)})
        </button>
        <button
          type="button"
          className={`wallets-tab ${activeTab === 'settings' ? 'wallets-tab--active' : ''}`}
          onClick={() => setActiveTab('settings')}
        >
          تنظیمات و قوانین
        </button>
      </nav>

      {/* Tab 1: Customer Wallets Directory */}
      {activeTab === 'wallets' && (
        <section className="wallets-panel">
          <div className="wallets-filters">
            <input
              type="text"
              className="wallets-filter-search"
              placeholder="جستجوی نام مشتری یا شماره موبایل..."
              value={walletFilters.search}
              onChange={(e) =>
                setWalletFilters((prev) => ({ ...prev, search: e.target.value, page: 1 }))
              }
            />
            <select
              value={walletFilters.balanceFilter}
              onChange={(e) =>
                setWalletFilters((prev) => ({ ...prev, balanceFilter: e.target.value, page: 1 }))
              }
            >
              <option value="all">همه مشتریان</option>
              <option value="positive">فقط دارای موجودی</option>
              <option value="has_activity">دارای سابقه تراکنش</option>
              <option value="zero">موجودی صفر</option>
            </select>
            <input
              type="text"
              className="wallets-filter-num"
              placeholder="حداقل موجودی"
              value={walletFilters.minBalance}
              onChange={(e) =>
                setWalletFilters((prev) => ({ ...prev, minBalance: e.target.value, page: 1 }))
              }
            />
            <select
              value={walletFilters.sortBy}
              onChange={(e) =>
                setWalletFilters((prev) => ({ ...prev, sortBy: e.target.value, page: 1 }))
              }
            >
              <option value="balance_desc">بیشترین موجودی</option>
              <option value="balance_asc">کمترین موجودی</option>
              <option value="recent">جدیدترین تراکنش</option>
              <option value="spent_desc">بیشترین مصرف</option>
              <option value="name">نام مشتری (الفبا)</option>
            </select>
            {(walletFilters.search || walletFilters.balanceFilter !== 'all' || walletFilters.minBalance) && (
              <button
                type="button"
                className="ghost-button"
                onClick={() => setWalletFilters(INITIAL_WALLET_FILTERS)}
              >
                پاک‌کردن فیلترها
              </button>
            )}
          </div>

          <div className="wallets-table-wrap">
            <table className="wallets-table">
              <thead>
                <tr>
                  <th>مشتری</th>
                  <th>موجودی فعلی</th>
                  <th>مجموع شارژ</th>
                  <th>کش‌بک</th>
                  <th>مصرف‌شده</th>
                  <th>آخرین تراکنش</th>
                  <th style={{ textAlign: 'left' }}>عملیات</th>
                </tr>
              </thead>
              <tbody>
                {walletsLoading ? (
                  <tr>
                    <td colSpan={7} className="wallets-empty">در حال بارگذاری...</td>
                  </tr>
                ) : walletsData.items.length === 0 ? (
                  <tr>
                    <td colSpan={7} className="wallets-empty">مشتری با این مشخصات یافت نشد.</td>
                  </tr>
                ) : (
                  walletsData.items.map((row) => (
                    <tr key={row.customerId}>
                      <td>
                        <div className="wallets-customer-cell">
                          <span className="wallets-customer-name">{row.customerName}</span>
                          <span className="wallets-customer-meta">
                            <span dir="ltr">{row.phoneNumber}</span>
                            {row.membershipTierName ? (
                              <span className="wallets-tier-chip">{row.membershipTierName}</span>
                            ) : null}
                          </span>
                        </div>
                      </td>
                      <td>
                        <span
                          className={`wallets-balance-cell ${
                            row.balance > 0 ? 'wallets-balance-cell--positive' : 'wallets-balance-cell--zero'
                          }`}
                        >
                          {formatPrice(row.balance)} ت
                        </span>
                      </td>
                      <td>{formatPrice(row.totalTopUp)} ت</td>
                      <td>{formatPrice(row.totalCashback)} ت</td>
                      <td>{formatPrice(row.totalSpent)} ت</td>
                      <td>
                        {row.lastTransactionAt ? formatDateTime(row.lastTransactionAt) : '—'}
                      </td>
                      <td>
                        <div className="wallets-row-actions">
                          {canManage && (
                            <>
                              <button
                                type="button"
                                className="wallets-btn-xs wallets-btn-xs--charge"
                                onClick={() => openAdjustModal(row, 'charge')}
                              >
                                + شارژ
                              </button>
                              <button
                                type="button"
                                className="wallets-btn-xs wallets-btn-xs--debit"
                                disabled={row.balance <= 0}
                                onClick={() => openAdjustModal(row, 'debit')}
                              >
                                − کسر
                              </button>
                            </>
                          )}
                          <button
                            type="button"
                            className="wallets-btn-xs"
                            onClick={() => openHistoryModal(row.customerId)}
                          >
                            تاریخچه ({toPersianNumber(row.transactionCount)})
                          </button>
                        </div>
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>

          <div className="wallets-pagination">
            <span>
              نمایش {toPersianNumber(walletsData.items.length)} از {toPersianNumber(walletsData.totalCount)} مشتری
              (صفحه {toPersianNumber(walletsData.pageNumber)} از {toPersianNumber(Math.max(1, walletsData.totalPages))})
            </span>
            <div className="wallets-pagination-btns">
              <button
                type="button"
                className="ghost-button"
                disabled={walletsData.pageNumber <= 1 || walletsLoading}
                onClick={() => setWalletFilters((prev) => ({ ...prev, page: Math.max(1, prev.page - 1) }))}
              >
                قبلی
              </button>
              <button
                type="button"
                className="ghost-button"
                disabled={walletsData.pageNumber >= walletsData.totalPages || walletsLoading}
                onClick={() => setWalletFilters((prev) => ({ ...prev, page: prev.page + 1 }))}
              >
                بعدی
              </button>
            </div>
          </div>
        </section>
      )}

      {/* Tab 2: Global Transactions Ledger */}
      {activeTab === 'transactions' && (
        <section className="wallets-panel">
          <div className="wallets-filters">
            <input
              type="text"
              className="wallets-filter-search"
              placeholder="جستجو نام مشتری، موبایل، شرح یا کد پیگیری سفارش..."
              value={txFilters.search}
              onChange={(e) => setTxFilters((prev) => ({ ...prev, search: e.target.value, page: 1 }))}
            />
            <select
              value={txFilters.type}
              onChange={(e) => setTxFilters((prev) => ({ ...prev, type: e.target.value, page: 1 }))}
            >
              {WALLET_TX_TYPE_OPTIONS.map((opt) => (
                <option key={opt.value} value={opt.value}>{opt.label}</option>
              ))}
            </select>
            <select
              value={txFilters.direction}
              onChange={(e) => setTxFilters((prev) => ({ ...prev, direction: e.target.value, page: 1 }))}
            >
              <option value="all">همه جهت‌ها</option>
              <option value="in">واریزی (+)</option>
              <option value="out">برداشتی (−)</option>
            </select>
            <ShamsiDatePicker
              value={txFilters.fromDate}
              onChange={(val) => setTxFilters((prev) => ({ ...prev, fromDate: val, page: 1 }))}
              placeholder="از تاریخ"
            />
            <ShamsiDatePicker
              value={txFilters.toDate}
              onChange={(val) => setTxFilters((prev) => ({ ...prev, toDate: val, page: 1 }))}
              placeholder="تا تاریخ"
            />
            <input
              type="text"
              className="wallets-filter-num"
              placeholder="حداقل مبلغ"
              value={txFilters.minAmount}
              onChange={(e) => setTxFilters((prev) => ({ ...prev, minAmount: e.target.value, page: 1 }))}
            />
            {(txFilters.search || txFilters.type || txFilters.direction !== 'all' || txFilters.fromDate || txFilters.toDate || txFilters.minAmount) && (
              <button
                type="button"
                className="ghost-button"
                onClick={() => setTxFilters(INITIAL_TX_FILTERS)}
              >
                پاک‌کردن
              </button>
            )}
          </div>

          <div className="wallets-table-wrap">
            <table className="wallets-table">
              <thead>
                <tr>
                  <th>زمان</th>
                  <th>مشتری</th>
                  <th>نوع</th>
                  <th>مبلغ</th>
                  <th>مانده پس از تراکنش</th>
                  <th>شرح / سفارش</th>
                </tr>
              </thead>
              <tbody>
                {txLoading ? (
                  <tr>
                    <td colSpan={6} className="wallets-empty">در حال بارگذاری...</td>
                  </tr>
                ) : txData.items.length === 0 ? (
                  <tr>
                    <td colSpan={6} className="wallets-empty">تراکنشی با این فیلترها یافت نشد.</td>
                  </tr>
                ) : (
                  txData.items.map((tx) => {
                    const meta = getWalletTxMeta(tx.type)
                    const isPositive = tx.amount > 0
                    return (
                      <tr key={tx.id}>
                        <td style={{ whiteSpace: 'nowrap' }}>{formatDateTime(tx.createdAt)}</td>
                        <td>
                          <div className="wallets-customer-cell">
                            <span className="wallets-customer-name">{tx.customerName}</span>
                            <span className="wallets-customer-meta" dir="ltr">{tx.phoneNumber}</span>
                          </div>
                        </td>
                        <td>
                          <span className={`wallets-tx-badge wallets-tx-badge--${meta.tone}`}>
                            {meta.label}
                          </span>
                        </td>
                        <td className={isPositive ? 'wallets-amount-in' : 'wallets-amount-out'}>
                          {isPositive ? '+' : '−'} {formatPrice(Math.abs(tx.amount))} ت
                        </td>
                        <td style={{ whiteSpace: 'nowrap', fontWeight: 600 }}>
                          {formatPrice(tx.balanceAfter)} ت
                        </td>
                        <td>
                          <div>{tx.description}</div>
                          {tx.orderTrackingCode ? (
                            <small style={{ color: 'var(--muted, #64748b)' }}>
                              سفارش: {tx.orderTrackingCode}
                            </small>
                          ) : null}
                        </td>
                      </tr>
                    )
                  })
                )}
              </tbody>
            </table>
          </div>

          <div className="wallets-pagination">
            <span>
              نمایش {toPersianNumber(txData.items.length)} از {toPersianNumber(txData.totalCount)} تراکنش
              (صفحه {toPersianNumber(txData.pageNumber)} از {toPersianNumber(Math.max(1, txData.totalPages))})
            </span>
            <div className="wallets-pagination-btns">
              <button
                type="button"
                className="ghost-button"
                disabled={txData.pageNumber <= 1 || txLoading}
                onClick={() => setTxFilters((prev) => ({ ...prev, page: Math.max(1, prev.page - 1) }))}
              >
                قبلی
              </button>
              <button
                type="button"
                className="ghost-button"
                disabled={txData.pageNumber >= txData.totalPages || txLoading}
                onClick={() => setTxFilters((prev) => ({ ...prev, page: prev.page + 1 }))}
              >
                بعدی
              </button>
            </div>
          </div>
        </section>
      )}

      {/* Tab 3: Top-Up Packages */}
      {activeTab === 'packages' && (
        <section className="wallets-panel">
          <div>
            <h2 style={{ margin: '0 0 0.25rem', fontSize: '1.05rem' }}>بسته‌های شارژ هدیه‌دار</h2>
            <p style={{ margin: 0, fontSize: '0.84rem', color: 'var(--muted, #64748b)' }}>
              بسته‌های آماده (مثلاً شارژ ۵۰۰٬۰۰۰ تومان + ۵۰٬۰۰۰ تومان هدیه) برای شارژ سریع در پنل و صندوق.
            </p>
          </div>

          {canManage && (
            <form className="wallets-package-form" onSubmit={handleSavePackage}>
              <div className="wallets-field">
                <label>مبلغ پرداختی مشتری (تومان)</label>
                <input
                  type="text"
                  required
                  placeholder="مثلاً 500000"
                  value={packageForm.amount}
                  onChange={(e) => setPackageForm((prev) => ({ ...prev, amount: e.target.value }))}
                />
              </div>
              <div className="wallets-field">
                <label>هدیه / بونوس (تومان)</label>
                <input
                  type="text"
                  placeholder="مثلاً 50000"
                  value={packageForm.bonusAmount}
                  onChange={(e) => setPackageForm((prev) => ({ ...prev, bonusAmount: e.target.value }))}
                />
              </div>
              <div className="wallets-field">
                <label>ترتیب نمایش</label>
                <input
                  type="number"
                  value={packageForm.displayOrder}
                  onChange={(e) => setPackageForm((prev) => ({ ...prev, displayOrder: e.target.value }))}
                />
              </div>
              <div className="wallets-field">
                <label style={{ display: 'flex', alignItems: 'center', gap: '0.45rem', cursor: 'pointer' }}>
                  <input
                    type="checkbox"
                    checked={packageForm.isEnabled}
                    onChange={(e) => setPackageForm((prev) => ({ ...prev, isEnabled: e.target.checked }))}
                    style={{ minHeight: 'auto', width: 'auto' }}
                  />
                  فعال باشد
                </label>
              </div>
              <div style={{ display: 'flex', gap: '0.4rem' }}>
                <button type="submit" disabled={packageSaving}>
                  {packageSaving ? 'در حال ذخیره...' : packageForm.id ? 'بروزرسانی بسته' : 'افزودن بسته'}
                </button>
                {packageForm.id && (
                  <button
                    type="button"
                    className="ghost-button"
                    onClick={() => setPackageForm(EMPTY_PACKAGE_FORM)}
                  >
                    انصراف
                  </button>
                )}
              </div>
            </form>
          )}

          {packagesLoading ? (
            <div className="wallets-empty">در حال بارگذاری بسته‌ها...</div>
          ) : packages.length === 0 ? (
            <div className="wallets-empty">هنوز بسته شارژی تعریف نشده است.</div>
          ) : (
            <div className="wallets-packages-grid">
              {packages.map((pack) => (
                <article
                  key={pack.id}
                  className={`wallets-package-card ${pack.isEnabled ? '' : 'wallets-package-card--disabled'}`}
                >
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                    <strong>شارژ {formatPrice(pack.amount)} ت</strong>
                    <span className={`wallets-tx-badge ${pack.isEnabled ? 'wallets-tx-badge--success' : 'wallets-tx-badge--neutral'}`}>
                      {pack.isEnabled ? 'فعال' : 'غیرفعال'}
                    </span>
                  </div>
                  <div style={{ fontSize: '0.84rem', color: '#059669', fontWeight: 600 }}>
                    + {formatPrice(pack.bonusAmount)} تومان هدیه
                  </div>
                  <div style={{ fontSize: '0.78rem', color: 'var(--muted, #64748b)' }}>
                    اعتبار نهایی مشتری: {formatPrice((pack.amount || 0) + (pack.bonusAmount || 0))} تومان
                  </div>
                  {canManage && (
                    <div style={{ display: 'flex', gap: '0.4rem', marginTop: '0.4rem' }}>
                      <button
                        type="button"
                        className="wallets-btn-xs"
                        onClick={() =>
                          setPackageForm({
                            id: pack.id,
                            amount: String(pack.amount),
                            bonusAmount: String(pack.bonusAmount),
                            displayOrder: String(pack.displayOrder ?? 0),
                            isEnabled: Boolean(pack.isEnabled),
                          })
                        }
                      >
                        ویرایش
                      </button>
                      <button
                        type="button"
                        className="wallets-btn-xs wallets-btn-xs--debit"
                        onClick={() => handleDeletePackage(pack.id)}
                      >
                        حذف
                      </button>
                    </div>
                  )}
                </article>
              ))}
            </div>
          )}
        </section>
      )}

      {/* Tab 4: Wallet Rules & Settings */}
      {activeTab === 'settings' && settingsForm && (
        <section className="wallets-panel">
          <div>
            <h2 style={{ margin: '0 0 0.25rem', fontSize: '1.05rem' }}>قوانین و تنظیمات کیف پول</h2>
            <p style={{ margin: 0, fontSize: '0.84rem', color: 'var(--muted, #64748b)' }}>
              تغییر این تنظیمات بلافاصله روی سفارش‌های آنلاین، صندوق (POS) و اعطای کش‌بک اعمال می‌شود.
            </p>
          </div>

          <form onSubmit={handleSaveSettings} style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
            <label style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', fontWeight: 600 }}>
              <input
                type="checkbox"
                disabled={!canManage}
                checked={settingsForm.walletEnabled}
                onChange={(e) => setSettingsForm((prev) => ({ ...prev, walletEnabled: e.target.checked }))}
              />
              سیستم کیف پول و کش‌بک فعال باشد
            </label>

            <div className="wallets-settings-grid">
              <div className="wallets-field">
                <label>درصد کش‌بک پیش‌فرض (٪)</label>
                <input
                  type="number"
                  step="0.1"
                  min="0"
                  max="100"
                  disabled={!canManage}
                  value={settingsForm.walletCashbackPercent}
                  onChange={(e) => setSettingsForm((prev) => ({ ...prev, walletCashbackPercent: e.target.value }))}
                />
                <small>۰ = کش‌بک خودکار غیرفعال است (سطح عضویت می‌تواند این مقدار را بازنویسی کند)</small>
              </div>

              <div className="wallets-field">
                <label>حداقل مبلغ سفارش برای کش‌بک (تومان)</label>
                <input
                  type="number"
                  min="0"
                  disabled={!canManage}
                  value={settingsForm.walletCashbackMinOrderAmount}
                  onChange={(e) => setSettingsForm((prev) => ({ ...prev, walletCashbackMinOrderAmount: e.target.value }))}
                />
                <small>۰ = بدون حداقل مبلغ</small>
              </div>

              <div className="wallets-field">
                <label>سقف کش‌بک در هر سفارش (تومان)</label>
                <input
                  type="number"
                  min="0"
                  disabled={!canManage}
                  value={settingsForm.walletCashbackMaxPerOrderAmount}
                  onChange={(e) => setSettingsForm((prev) => ({ ...prev, walletCashbackMaxPerOrderAmount: e.target.value }))}
                />
                <small>۰ = بدون سقف</small>
              </div>

              <div className="wallets-field">
                <label>مدت انقضای کش‌بک (روز)</label>
                <input
                  type="number"
                  min="0"
                  max="3650"
                  disabled={!canManage}
                  value={settingsForm.walletCashbackExpiryDays}
                  onChange={(e) => setSettingsForm((prev) => ({ ...prev, walletCashbackExpiryDays: e.target.value }))}
                />
                <small>۰ = کش‌بک هیچ‌وقت منقضی نمی‌شود</small>
              </div>

              <div className="wallets-field">
                <label>سقف پرداخت با کیف پول در هر سفارش (٪)</label>
                <input
                  type="number"
                  min="1"
                  max="100"
                  disabled={!canManage}
                  value={settingsForm.walletPaymentMaxPercent}
                  onChange={(e) => setSettingsForm((prev) => ({ ...prev, walletPaymentMaxPercent: e.target.value }))}
                />
                <small>۱۰۰ = مشتری می‌تواند کل مبلغ سفارش را از کیف پول بپردازد</small>
              </div>

              <div className="wallets-field">
                <label>نحوه بازگشت وجه لغو سفارش پیکاپ</label>
                <select
                  disabled={!canManage}
                  value={settingsForm.pickupRefundMode}
                  onChange={(e) => setSettingsForm((prev) => ({ ...prev, pickupRefundMode: e.target.value }))}
                >
                  <option value="Wallet">بازگشت خودکار به کیف پول مشتری</option>
                  <option value="Manual">بازگشت دستی (کارت‌به‌کارت / نقدی)</option>
                </select>
              </div>
            </div>

            {canManage && (
              <div>
                <button type="submit" disabled={settingsSaving}>
                  {settingsSaving ? 'در حال ذخیره...' : 'ذخیره تنظیمات کیف پول'}
                </button>
              </div>
            )}
          </form>
        </section>
      )}

      {/* Adjust / Charge / Package Modal */}
      {adjustModal && (
        <div
          className="wallets-modal-backdrop"
          role="dialog"
          aria-modal="true"
          onClick={(e) => {
            if (e.target === e.currentTarget && !adjustSaving) setAdjustModal(null)
          }}
        >
          <div className="wallets-modal">
            <div className="wallets-modal-head">
              <div>
                <h2>عملیات کیف پول — {adjustModal.customer.customerName}</h2>
                <small style={{ color: 'var(--muted, #64748b)' }} dir="ltr">
                  {adjustModal.customer.phoneNumber}
                </small>
              </div>
              <button
                type="button"
                className="wallets-modal-close"
                aria-label="بستن"
                onClick={() => setAdjustModal(null)}
              >
                ×
              </button>
            </div>

            <div className="wallets-mode-switch">
              <button
                type="button"
                className={`wallets-mode-btn ${adjustModal.mode === 'charge' ? 'wallets-mode-btn--active-charge' : ''}`}
                onClick={() => setAdjustModal((prev) => ({ ...prev, mode: 'charge' }))}
              >
                + شارژ دستی
              </button>
              <button
                type="button"
                className={`wallets-mode-btn ${adjustModal.mode === 'debit' ? 'wallets-mode-btn--active-debit' : ''}`}
                onClick={() => setAdjustModal((prev) => ({ ...prev, mode: 'debit' }))}
              >
                − کسر / تعدیل
              </button>
              <button
                type="button"
                className={`wallets-mode-btn ${adjustModal.mode === 'package' ? 'wallets-mode-btn--active-package' : ''}`}
                disabled={enabledPackages.length === 0}
                onClick={() => setAdjustModal((prev) => ({ ...prev, mode: 'package' }))}
              >
                🎁 بسته شارژ
              </button>
            </div>

            <form onSubmit={handleAdjustSubmit} style={{ display: 'flex', flexDirection: 'column', gap: '0.85rem' }}>
              {adjustModal.mode === 'package' ? (
                <div className="wallets-field">
                  <label>انتخاب بسته شارژ</label>
                  <select
                    value={selectedPackageId}
                    onChange={(e) => setSelectedPackageId(e.target.value)}
                  >
                    {enabledPackages.map((pack) => (
                      <option key={pack.id} value={pack.id}>
                        شارژ {formatPrice(pack.amount)} ت + هدیه {formatPrice(pack.bonusAmount)} ت (جمع:{' '}
                        {formatPrice((pack.amount || 0) + (pack.bonusAmount || 0))} ت)
                      </option>
                    ))}
                  </select>
                </div>
              ) : (
                <>
                  <div className="wallets-field">
                    <label>مبلغ (تومان)</label>
                    <input
                      type="text"
                      inputMode="numeric"
                      autoFocus
                      placeholder="مثلاً 100000"
                      value={adjustAmount}
                      onChange={(e) => setAdjustAmount(e.target.value)}
                    />
                  </div>
                  <div className="wallets-presets">
                    {QUICK_TOPUP_PRESETS.map((preset) => (
                      <button
                        key={preset}
                        type="button"
                        className="wallets-preset-chip"
                        onClick={() => setAdjustAmount(String(preset))}
                      >
                        {formatPrice(preset)} ت
                      </button>
                    ))}
                  </div>
                </>
              )}

              <div className="wallets-field">
                <label>توضیحات / علت (اختیاری)</label>
                <input
                  type="text"
                  maxLength={250}
                  placeholder={
                    adjustModal.mode === 'debit'
                      ? 'مثلاً اصلاح خطای ثبت قبلی'
                      : 'مثلاً شارژ نقدی در صندوق'
                  }
                  value={adjustNote}
                  onChange={(e) => setAdjustNote(e.target.value)}
                />
              </div>

              <div
                className={`wallets-preview-box ${balancePreview?.isOverdraft ? 'wallets-preview-box--error' : ''}`}
              >
                <div>
                  <span>موجودی فعلی: </span>
                  <strong>{formatPrice(adjustModal.customer.balance)} ت</strong>
                </div>
                <div>
                  {balancePreview?.isOverdraft ? (
                    <strong>مبلغ کسر بیشتر از موجودی کیف پول است!</strong>
                  ) : (
                    <>
                      <span>موجودی جدید: </span>
                      <strong>{formatPrice(balancePreview?.nextBalance ?? adjustModal.customer.balance)} ت</strong>
                    </>
                  )}
                </div>
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '0.5rem' }}>
                <button type="button" className="ghost-button" onClick={() => setAdjustModal(null)}>
                  انصراف
                </button>
                <button type="submit" disabled={adjustSaving || !balancePreview?.isValid}>
                  {adjustSaving
                    ? 'در حال ثبت...'
                    : adjustModal.mode === 'debit'
                      ? 'ثبت کسر از کیف پول'
                      : adjustModal.mode === 'package'
                        ? 'اعمال بسته شارژ'
                        : 'ثبت شارژ کیف پول'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Customer Wallet History Modal */}
      {historyModal && (
        <div
          className="wallets-modal-backdrop"
          role="dialog"
          aria-modal="true"
          onClick={(e) => {
            if (e.target === e.currentTarget) setHistoryModal(null)
          }}
        >
          <div className="wallets-modal wallets-modal--wide">
            <div className="wallets-modal-head">
              <div>
                <h2>تاریخچه کیف پول — {historyModal.customerName}</h2>
                <div style={{ fontSize: '0.84rem', color: 'var(--muted, #64748b)', marginTop: '0.2rem' }}>
                  <span dir="ltr">{historyModal.phoneNumber}</span> • موجودی فعلی:{' '}
                  <strong style={{ color: '#059669' }}>{formatPrice(historyModal.balance)} تومان</strong>
                </div>
              </div>
              <div style={{ display: 'flex', gap: '0.4rem', alignItems: 'center' }}>
                {canManage && !historyLoading && (
                  <>
                    <button
                      type="button"
                      className="wallets-btn-xs wallets-btn-xs--charge"
                      onClick={() => openAdjustModal(historyModal, 'charge')}
                    >
                      + شارژ
                    </button>
                    <button
                      type="button"
                      className="wallets-btn-xs wallets-btn-xs--debit"
                      disabled={(historyModal.balance ?? 0) <= 0}
                      onClick={() => openAdjustModal(historyModal, 'debit')}
                    >
                      − کسر
                    </button>
                  </>
                )}
                <button
                  type="button"
                  className="wallets-modal-close"
                  aria-label="بستن"
                  onClick={() => setHistoryModal(null)}
                >
                  ×
                </button>
              </div>
            </div>

            {historyLoading ? (
              <div className="wallets-empty">در حال بارگذاری تراکنش‌ها...</div>
            ) : (historyModal.transactions ?? []).length === 0 ? (
              <div className="wallets-empty">هنوز هیچ تراکنشی برای این مشتری ثبت نشده است.</div>
            ) : (
              <div className="wallets-table-wrap">
                <table className="wallets-table">
                  <thead>
                    <tr>
                      <th>زمان</th>
                      <th>نوع</th>
                      <th>مبلغ</th>
                      <th>مانده بعد</th>
                      <th>شرح</th>
                    </tr>
                  </thead>
                  <tbody>
                    {historyModal.transactions.map((tx) => {
                      const meta = getWalletTxMeta(tx.type)
                      const isPositive = tx.amount > 0
                      return (
                        <tr key={tx.id}>
                          <td style={{ whiteSpace: 'nowrap' }}>{formatDateTime(tx.createdAt)}</td>
                          <td>
                            <span className={`wallets-tx-badge wallets-tx-badge--${meta.tone}`}>
                              {meta.label}
                            </span>
                          </td>
                          <td className={isPositive ? 'wallets-amount-in' : 'wallets-amount-out'}>
                            {isPositive ? '+' : '−'} {formatPrice(Math.abs(tx.amount))} ت
                          </td>
                          <td style={{ whiteSpace: 'nowrap' }}>{formatPrice(tx.balanceAfter)} ت</td>
                          <td>{tx.description}</td>
                        </tr>
                      )
                    })}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        </div>
      )}
    </div>
  )
}
