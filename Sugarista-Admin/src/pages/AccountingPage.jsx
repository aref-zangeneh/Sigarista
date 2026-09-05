import { useCallback, useEffect, useMemo, useState } from 'react'
import { useAdmin } from '../context/AdminContext'
import { PERMISSIONS, hasPermission } from '../utils/adminPermissions'
import './accounting.css'

const TABS = [
  { id: 'overview', label: 'نمای کلی' },
  { id: 'partners', label: 'شرکا' },
  { id: 'journal', label: 'دفتر روزنامه' },
  { id: 'reports', label: 'گزارش‌ها' },
  { id: 'profit', label: 'تقسیم سود' },
]

function formatMoney(value) {
  const amount = Number(value ?? 0)
  return `${amount.toLocaleString('fa-IR')} تومان`
}

function todayIso() {
  return new Date().toISOString().slice(0, 10)
}

function monthStartIso() {
  const date = new Date()
  date.setDate(1)
  return date.toISOString().slice(0, 10)
}

export function AccountingPage() {
  const { session, apiRequest, getReadableError, saving, setSaving, setMessage, setError } = useAdmin()
  const [activeTab, setActiveTab] = useState('overview')
  const [loading, setLoading] = useState(false)
  const [localError, setLocalError] = useState('')

  const [fromDate, setFromDate] = useState(monthStartIso())
  const [toDate, setToDate] = useState(todayIso())
  const [asOfDate, setAsOfDate] = useState(todayIso())

  const [overview, setOverview] = useState(null)
  const [partners, setPartners] = useState([])
  const [journal, setJournal] = useState([])
  const [trialBalance, setTrialBalance] = useState(null)
  const [incomeStatement, setIncomeStatement] = useState(null)
  const [balanceSheet, setBalanceSheet] = useState(null)
  const [profitDraft, setProfitDraft] = useState(null)

  const [partnerForm, setPartnerForm] = useState({
    id: null,
    name: '',
    ownershipPercentage: '',
    email: '',
    phone: '',
    notes: '',
    isActive: true,
  })

  const [capitalForm, setCapitalForm] = useState({
    partnerId: '',
    type: 'Contribution',
    amount: '',
    transactionDate: todayIso(),
    description: '',
  })

  const [expenseForm, setExpenseForm] = useState({
    entryDate: todayIso(),
    amount: '',
    description: '',
    paidFromCash: true,
  })

  const canCreate = hasPermission(session, PERMISSIONS.accounting?.create)
  const canUpdate = hasPermission(session, PERMISSIONS.accounting?.update)

  const authRequest = useCallback(
    (path, options = {}) => apiRequest(path, { ...options, token: session?.token }),
    [apiRequest, session?.token],
  )

  const loadOverview = useCallback(async () => {
    const data = await authRequest(`/admin/accounting/overview?fromDate=${fromDate}&toDate=${toDate}`)
    setOverview(data)
  }, [authRequest, fromDate, toDate])

  const loadPartners = useCallback(async () => {
    const data = await authRequest('/admin/accounting/partners')
    setPartners(data ?? [])
  }, [authRequest])

  const loadJournal = useCallback(async () => {
    const data = await authRequest(`/admin/accounting/journal?fromDate=${fromDate}&toDate=${toDate}&limit=100`)
    setJournal(data ?? [])
  }, [authRequest, fromDate, toDate])

  const loadReports = useCallback(async () => {
    const [trial, income, balance] = await Promise.all([
      authRequest(`/admin/accounting/reports/trial-balance?asOfDate=${asOfDate}`),
      authRequest(`/admin/accounting/reports/income-statement?fromDate=${fromDate}&toDate=${toDate}`),
      authRequest(`/admin/accounting/reports/balance-sheet?asOfDate=${asOfDate}`),
    ])
    setTrialBalance(trial)
    setIncomeStatement(income)
    setBalanceSheet(balance)
  }, [authRequest, asOfDate, fromDate, toDate])

  const refreshActiveTab = useCallback(async () => {
    setLoading(true)
    setLocalError('')
    try {
      if (activeTab === 'overview') {
        await loadOverview()
      } else if (activeTab === 'partners') {
        await loadPartners()
      } else if (activeTab === 'journal') {
        await loadJournal()
      } else if (activeTab === 'reports') {
        await loadReports()
      } else if (activeTab === 'profit') {
        await loadPartners()
        const draft = await authRequest('/admin/accounting/profit-distribution/draft', {
          method: 'POST',
          body: { periodStart: fromDate, periodEnd: toDate },
        })
        setProfitDraft(draft)
      }
    } catch (error) {
      setLocalError(getReadableError(error))
    } finally {
      setLoading(false)
    }
  }, [activeTab, fromDate, getReadableError, loadJournal, loadOverview, loadPartners, loadReports, toDate, authRequest])

  useEffect(() => {
    refreshActiveTab()
  }, [refreshActiveTab])

  const partnerOptions = useMemo(
    () => partners.filter((partner) => partner.isActive),
    [partners],
  )

  async function handlePartnerSubmit(event) {
    event.preventDefault()
    if (!canCreate && !canUpdate) {
      return
    }

    setSaving(true)
    setError('')
    try {
      const payload = {
        name: partnerForm.name.trim(),
        ownershipPercentage: Number(partnerForm.ownershipPercentage),
        email: partnerForm.email.trim() || null,
        phone: partnerForm.phone.trim() || null,
        notes: partnerForm.notes.trim() || null,
        isActive: partnerForm.isActive,
      }

      if (partnerForm.id) {
        await authRequest(`/admin/accounting/partners/${partnerForm.id}`, {
          method: 'PUT',
          body: payload,
        })
        setMessage('اطلاعات شریک به‌روزرسانی شد.')
      } else {
        await authRequest('/admin/accounting/partners', {
          method: 'POST',
          body: payload,
        })
        setMessage('شریک جدید ثبت شد.')
      }

      setPartnerForm({
        id: null,
        name: '',
        ownershipPercentage: '',
        email: '',
        phone: '',
        notes: '',
        isActive: true,
      })
      await loadPartners()
      if (activeTab === 'overview') {
        await loadOverview()
      }
    } catch (error) {
      setError(getReadableError(error))
    } finally {
      setSaving(false)
    }
  }

  function editPartner(partner) {
    setPartnerForm({
      id: partner.id,
      name: partner.name,
      ownershipPercentage: String(partner.ownershipPercentage),
      email: partner.email ?? '',
      phone: partner.phone ?? '',
      notes: partner.notes ?? '',
      isActive: partner.isActive,
    })
  }

  async function handleCapitalSubmit(event) {
    event.preventDefault()
    if (!canCreate) {
      return
    }

    setSaving(true)
    setError('')
    try {
      await authRequest('/admin/accounting/partner-capital', {
        method: 'POST',
        body: {
          partnerId: capitalForm.partnerId,
          type: capitalForm.type,
          amount: Number(capitalForm.amount),
          transactionDate: capitalForm.transactionDate,
          description: capitalForm.description.trim() || null,
        },
      })
      setMessage('تراکنش سرمایه ثبت و سند حسابداری صادر شد.')
      setCapitalForm((current) => ({ ...current, amount: '', description: '' }))
      await Promise.all([loadPartners(), loadOverview(), loadJournal()])
    } catch (error) {
      setError(getReadableError(error))
    } finally {
      setSaving(false)
    }
  }

  async function handleExpenseSubmit(event) {
    event.preventDefault()
    if (!canCreate) {
      return
    }

    setSaving(true)
    setError('')
    try {
      await authRequest('/admin/accounting/expenses', {
        method: 'POST',
        body: {
          entryDate: expenseForm.entryDate,
          amount: Number(expenseForm.amount),
          description: expenseForm.description.trim(),
          paidFromCash: expenseForm.paidFromCash,
        },
      })
      setMessage('هزینه جاری ثبت شد.')
      setExpenseForm((current) => ({ ...current, amount: '', description: '' }))
      await Promise.all([loadOverview(), loadJournal(), loadReports()])
    } catch (error) {
      setError(getReadableError(error))
    } finally {
      setSaving(false)
    }
  }

  async function handleFinalizeProfit() {
    if (!canUpdate || !profitDraft) {
      return
    }

    setSaving(true)
    setError('')
    try {
      await authRequest('/admin/accounting/profit-distribution/finalize', {
        method: 'POST',
        body: {
          periodStart: profitDraft.periodStart,
          periodEnd: profitDraft.periodEnd,
          lines: profitDraft.lines.map((line) => ({
            partnerId: line.partnerId,
            allocatedAmount: line.allocatedAmount,
          })),
        },
      })
      setMessage('تقسیم سود نهایی شد و سند حسابداری ثبت گردید.')
      await Promise.all([loadOverview(), loadPartners(), loadJournal()])
    } catch (error) {
      setError(getReadableError(error))
    } finally {
      setSaving(false)
    }
  }

  return (
    <section className="accounting-shell">
      <header className="panel-head">
        <div>
          <p className="eyebrow">Accounting</p>
          <h2>حسابداری حرفه‌ای</h2>
          <p>ثبت دوطرفه، سرمایه شرکا، گزارش‌های مالی و تقسیم سود — یکپارچه با POS و انبار</p>
        </div>
        <div className="form-panel" style={{ minWidth: 280 }}>
          <label>
            از تاریخ
            <input type="date" value={fromDate} onChange={(event) => setFromDate(event.target.value)} />
          </label>
          <label>
            تا تاریخ
            <input type="date" value={toDate} onChange={(event) => setToDate(event.target.value)} />
          </label>
          <label>
            گزارش تا تاریخ
            <input type="date" value={asOfDate} onChange={(event) => setAsOfDate(event.target.value)} />
          </label>
          <button type="button" onClick={refreshActiveTab} disabled={loading || saving}>
            {loading ? 'در حال بارگذاری…' : 'بروزرسانی'}
          </button>
        </div>
      </header>

      {localError ? <p className="feedback error">{localError}</p> : null}

      <div className="accounting-tabs">
        {TABS.map((tab) => (
          <button
            key={tab.id}
            type="button"
            className={activeTab === tab.id ? 'active' : ''}
            onClick={() => setActiveTab(tab.id)}
          >
            {tab.label}
          </button>
        ))}
      </div>

      {activeTab === 'overview' && overview ? (
        <>
          <div className="accounting-metrics">
            <div className="metric-card">
              <span>موجودی صندوق</span>
              <strong>{formatMoney(overview.cashBalance)}</strong>
            </div>
            <div className="metric-card">
              <span>دریافتنی کارت</span>
              <strong>{formatMoney(overview.cardReceivable)}</strong>
            </div>
            <div className="metric-card">
              <span>بدهی به تأمین‌کنندگان</span>
              <strong>{formatMoney(overview.accountsPayable)}</strong>
            </div>
            <div className="metric-card">
              <span>ارزش موجودی کالا</span>
              <strong>{formatMoney(overview.inventoryValue)}</strong>
            </div>
            <div className="metric-card">
              <span>درآمد دوره</span>
              <strong className="amount-positive">{formatMoney(overview.periodRevenue)}</strong>
            </div>
            <div className="metric-card">
              <span>هزینه دوره</span>
              <strong className="amount-negative">{formatMoney(overview.periodExpenses)}</strong>
            </div>
            <div className="metric-card">
              <span>سود خالص دوره</span>
              <strong>{formatMoney(overview.periodNetProfit)}</strong>
            </div>
            <div className="metric-card">
              <span>سرمایه شرکا</span>
              <strong>{formatMoney(overview.totalPartnerCapital)}</strong>
            </div>
          </div>

          <article className="panel">
            <div className="panel-head">
              <h3>وضعیت شرکا</h3>
              <p>{overview.activePartnerCount} شریک فعال — {overview.journalEntryCount} سند در دوره</p>
            </div>
            <div className="accounting-table-wrap">
              <table className="accounting-table">
                <thead>
                  <tr>
                    <th>نام</th>
                    <th>سهم (%)</th>
                    <th>سرمایه‌گذاری</th>
                    <th>برداشت</th>
                    <th>حقوق فعلی</th>
                    <th>سهم سود دوره</th>
                  </tr>
                </thead>
                <tbody>
                  {overview.partners.map((partner) => (
                    <tr key={partner.id}>
                      <td>{partner.name}</td>
                      <td>{partner.ownershipPercentage.toLocaleString('fa-IR')}</td>
                      <td>{formatMoney(partner.contributedCapital)}</td>
                      <td>{formatMoney(partner.withdrawals)}</td>
                      <td>{formatMoney(partner.equityBalance)}</td>
                      <td>{formatMoney(partner.expectedProfitShare)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </article>
        </>
      ) : null}

      {activeTab === 'partners' ? (
        <div className="accounting-grid">
          <article className="panel form-panel">
            <h3>{partnerForm.id ? 'ویرایش شریک' : 'ثبت شریک'}</h3>
            <form onSubmit={handlePartnerSubmit}>
              <label>
                نام
                <input
                  required
                  value={partnerForm.name}
                  onChange={(event) => setPartnerForm({ ...partnerForm, name: event.target.value })}
                />
              </label>
              <label>
                درصد مالکیت
                <input
                  required
                  type="number"
                  min="0"
                  max="100"
                  step="0.01"
                  value={partnerForm.ownershipPercentage}
                  onChange={(event) => setPartnerForm({ ...partnerForm, ownershipPercentage: event.target.value })}
                />
              </label>
              <label>
                ایمیل
                <input
                  type="email"
                  value={partnerForm.email}
                  onChange={(event) => setPartnerForm({ ...partnerForm, email: event.target.value })}
                />
              </label>
              <label>
                تلفن
                <input
                  value={partnerForm.phone}
                  onChange={(event) => setPartnerForm({ ...partnerForm, phone: event.target.value })}
                />
              </label>
              <label>
                یادداشت
                <textarea
                  rows={3}
                  value={partnerForm.notes}
                  onChange={(event) => setPartnerForm({ ...partnerForm, notes: event.target.value })}
                />
              </label>
              <label className="checkbox-row">
                <input
                  type="checkbox"
                  checked={partnerForm.isActive}
                  onChange={(event) => setPartnerForm({ ...partnerForm, isActive: event.target.checked })}
                />
                فعال
              </label>
              {(canCreate || canUpdate) && (
                <button type="submit" disabled={saving}>
                  {partnerForm.id ? 'ذخیره تغییرات' : 'ثبت شریک'}
                </button>
              )}
            </form>

            <hr />

            <h3>سرمایه / برداشت</h3>
            <form onSubmit={handleCapitalSubmit}>
              <label>
                شریک
                <select
                  required
                  value={capitalForm.partnerId}
                  onChange={(event) => setCapitalForm({ ...capitalForm, partnerId: event.target.value })}
                >
                  <option value="">انتخاب کنید</option>
                  {partnerOptions.map((partner) => (
                    <option key={partner.id} value={partner.id}>
                      {partner.name}
                    </option>
                  ))}
                </select>
              </label>
              <label>
                نوع
                <select
                  value={capitalForm.type}
                  onChange={(event) => setCapitalForm({ ...capitalForm, type: event.target.value })}
                >
                  <option value="Contribution">آورده سرمایه</option>
                  <option value="Withdrawal">برداشت</option>
                </select>
              </label>
              <label>
                مبلغ
                <input
                  required
                  type="number"
                  min="1"
                  value={capitalForm.amount}
                  onChange={(event) => setCapitalForm({ ...capitalForm, amount: event.target.value })}
                />
              </label>
              <label>
                تاریخ
                <input
                  type="date"
                  required
                  value={capitalForm.transactionDate}
                  onChange={(event) => setCapitalForm({ ...capitalForm, transactionDate: event.target.value })}
                />
              </label>
              <label>
                توضیح
                <input
                  value={capitalForm.description}
                  onChange={(event) => setCapitalForm({ ...capitalForm, description: event.target.value })}
                />
              </label>
              {canCreate && (
                <button type="submit" disabled={saving}>
                  ثبت تراکنش
                </button>
              )}
            </form>

            <hr />

            <h3>هزینه جاری</h3>
            <form onSubmit={handleExpenseSubmit}>
              <label>
                تاریخ
                <input
                  type="date"
                  required
                  value={expenseForm.entryDate}
                  onChange={(event) => setExpenseForm({ ...expenseForm, entryDate: event.target.value })}
                />
              </label>
              <label>
                مبلغ
                <input
                  required
                  type="number"
                  min="1"
                  value={expenseForm.amount}
                  onChange={(event) => setExpenseForm({ ...expenseForm, amount: event.target.value })}
                />
              </label>
              <label>
                شرح
                <input
                  required
                  value={expenseForm.description}
                  onChange={(event) => setExpenseForm({ ...expenseForm, description: event.target.value })}
                />
              </label>
              <label className="checkbox-row">
                <input
                  type="checkbox"
                  checked={expenseForm.paidFromCash}
                  onChange={(event) => setExpenseForm({ ...expenseForm, paidFromCash: event.target.checked })}
                />
                پرداخت از صندوق (در غیر این صورت حساب پرداختنی)
              </label>
              {canCreate && (
                <button type="submit" disabled={saving}>
                  ثبت هزینه
                </button>
              )}
            </form>
          </article>

          <article className="panel">
            <div className="panel-head">
              <h3>لیست شرکا</h3>
            </div>
            <div className="accounting-table-wrap">
              <table className="accounting-table">
                <thead>
                  <tr>
                    <th>نام</th>
                    <th>سهم</th>
                    <th>آورده</th>
                    <th>برداشت</th>
                    <th>حقوق</th>
                    <th>وضعیت</th>
                    <th />
                  </tr>
                </thead>
                <tbody>
                  {partners.map((partner) => (
                    <tr key={partner.id}>
                      <td>{partner.name}</td>
                      <td>{partner.ownershipPercentage.toLocaleString('fa-IR')}%</td>
                      <td>{formatMoney(partner.totalContributions)}</td>
                      <td>{formatMoney(partner.totalWithdrawals)}</td>
                      <td>{formatMoney(partner.equityBalance)}</td>
                      <td>{partner.isActive ? 'فعال' : 'غیرفعال'}</td>
                      <td>
                        {canUpdate ? (
                          <button type="button" onClick={() => editPartner(partner)}>
                            ویرایش
                          </button>
                        ) : null}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </article>
        </div>
      ) : null}

      {activeTab === 'journal' ? (
        <article className="panel">
          <div className="panel-head">
            <h3>دفتر روزنامه</h3>
            <p>هر سند به‌صورت خودکار از POS، خرید انبار، سرمایه شرکا و هزینه‌ها صادر می‌شود.</p>
          </div>
          {journal.map((entry) => (
            <div key={entry.id} className="journal-entry-card">
              <div className="journal-entry-head">
                <div>
                  <strong>{entry.referenceNumber}</strong>
                  <span className="partner-chip">{entry.sourceType}</span>
                </div>
                <div>
                  <span>{entry.entryDate}</span>
                  <span> — {entry.description}</span>
                </div>
              </div>
              <table className="accounting-table">
                <thead>
                  <tr>
                    <th>حساب</th>
                    <th>بدهکار</th>
                    <th>بستانکار</th>
                    <th>شریک</th>
                  </tr>
                </thead>
                <tbody>
                  {entry.lines.map((line) => (
                    <tr key={line.id}>
                      <td>{line.accountNamePersian}</td>
                      <td>{line.debit ? formatMoney(line.debit) : '—'}</td>
                      <td>{line.credit ? formatMoney(line.credit) : '—'}</td>
                      <td>{line.partnerName ?? '—'}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ))}
          {journal.length === 0 && !loading ? <p className="feedback">سندی در این بازه ثبت نشده است.</p> : null}
        </article>
      ) : null}

      {activeTab === 'reports' ? (
        <div className="panel-stack">
          {trialBalance ? (
            <article className="panel">
              <div className="panel-head">
                <h3>تراز آزمایشی — {trialBalance.asOfDate}</h3>
                <p>
                  جمع بدهکار: {formatMoney(trialBalance.totalDebit)} — جمع بستانکار:{' '}
                  {formatMoney(trialBalance.totalCredit)}
                </p>
              </div>
              <div className="accounting-table-wrap">
                <table className="accounting-table">
                  <thead>
                    <tr>
                      <th>کد</th>
                      <th>حساب</th>
                      <th>بدهکار</th>
                      <th>بستانکار</th>
                    </tr>
                  </thead>
                  <tbody>
                    {trialBalance.lines.map((line) => (
                      <tr key={line.accountCode}>
                        <td>{line.accountCode}</td>
                        <td>{line.accountNamePersian}</td>
                        <td>{line.debit ? formatMoney(line.debit) : '—'}</td>
                        <td>{line.credit ? formatMoney(line.credit) : '—'}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </article>
          ) : null}

          {incomeStatement ? (
            <article className="panel">
              <div className="panel-head">
                <h3>صورت سود و زیان</h3>
                <p>
                  درآمد: {formatMoney(incomeStatement.totalRevenue)} — هزینه:{' '}
                  {formatMoney(incomeStatement.totalExpenses)} — سود خالص:{' '}
                  {formatMoney(incomeStatement.netProfit)}
                </p>
              </div>
              <div className="accounting-grid">
                <div>
                  <h4>درآمدها</h4>
                  <ul>
                    {incomeStatement.revenueLines.map((line) => (
                      <li key={line.accountCode}>
                        {line.accountNamePersian}: {formatMoney(line.amount)}
                      </li>
                    ))}
                  </ul>
                </div>
                <div>
                  <h4>هزینه‌ها</h4>
                  <ul>
                    {incomeStatement.expenseLines.map((line) => (
                      <li key={line.accountCode}>
                        {line.accountNamePersian}: {formatMoney(line.amount)}
                      </li>
                    ))}
                  </ul>
                </div>
              </div>
            </article>
          ) : null}

          {balanceSheet ? (
            <article className="panel">
              <div className="panel-head">
                <h3>ترازنامه — {balanceSheet.asOfDate}</h3>
                <p>
                  دارایی: {formatMoney(balanceSheet.totalAssets)} — بدهی:{' '}
                  {formatMoney(balanceSheet.totalLiabilities)} — حقوق: {formatMoney(balanceSheet.totalEquity)}
                </p>
              </div>
              <div className="accounting-grid">
                <div>
                  <h4>دارایی‌ها</h4>
                  <ul>
                    {balanceSheet.assets.map((line) => (
                      <li key={line.accountCode}>
                        {line.accountNamePersian}: {formatMoney(line.amount)}
                      </li>
                    ))}
                  </ul>
                </div>
                <div>
                  <h4>بدهی‌ها</h4>
                  <ul>
                    {balanceSheet.liabilities.map((line) => (
                      <li key={line.accountCode}>
                        {line.accountNamePersian}: {formatMoney(line.amount)}
                      </li>
                    ))}
                  </ul>
                  <h4>حقوق صاحبان سهام</h4>
                  <ul>
                    {balanceSheet.equity.map((line) => (
                      <li key={line.accountCode}>
                        {line.accountNamePersian}: {formatMoney(line.amount)}
                      </li>
                    ))}
                  </ul>
                </div>
              </div>
            </article>
          ) : null}
        </div>
      ) : null}

      {activeTab === 'profit' && profitDraft ? (
        <article className="panel">
          <div className="panel-head">
            <h3>پیش‌نویس تقسیم سود</h3>
            <p>
              دوره {profitDraft.periodStart} تا {profitDraft.periodEnd} — سود خالص:{' '}
              {formatMoney(profitDraft.totalNetProfit)}
            </p>
          </div>
          <div className="accounting-table-wrap">
            <table className="accounting-table">
              <thead>
                <tr>
                  <th>شریک</th>
                  <th>درصد</th>
                  <th>مبلغ تخصیص</th>
                </tr>
              </thead>
              <tbody>
                {profitDraft.lines.map((line) => (
                  <tr key={line.partnerId}>
                    <td>{line.partnerName}</td>
                    <td>{line.ownershipPercentage.toLocaleString('fa-IR')}%</td>
                    <td>{formatMoney(line.allocatedAmount)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {canUpdate ? (
            <button type="button" onClick={handleFinalizeProfit} disabled={saving || profitDraft.lines.length === 0}>
              نهایی‌سازی و ثبت سند
            </button>
          ) : null}
        </article>
      ) : null}
    </section>
  )
}
