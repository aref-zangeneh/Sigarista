import { useCallback, useEffect, useMemo, useState } from 'react'
import { useAdmin } from '../context/AdminContext'
import { hasPermission } from '../utils/adminPermissions'
import { gregorianIsoToShamsiLabel } from '../utils/shamsiDate'
import { formatDateTime, toPersianNumber } from '../utils/adminFormatters'
import { ShamsiDatePicker } from '../components/ShamsiDatePicker'
import './HrPage.css'

const PAY_KINDS = [
  { value: 'Monthly', label: 'حقوق ثابت ماهانه', glossaryKey: 'hr.plan.kind.Monthly' },
  { value: 'Hourly', label: 'ساعتی', glossaryKey: 'hr.plan.kind.Hourly' },
  { value: 'ShiftBased', label: 'شیفتی', glossaryKey: 'hr.plan.kind.ShiftBased' },
  { value: 'CommissionOnly', label: 'فقط پورسانت', glossaryKey: 'hr.plan.kind.CommissionOnly' },
  { value: 'MonthlyPlusCommission', label: 'ثابت + پورسانت', glossaryKey: 'hr.plan.kind.MonthlyPlusCommission' },
  { value: 'HourlyPlusCommission', label: 'ساعتی + پورسانت', glossaryKey: 'hr.plan.kind.HourlyPlusCommission' },
]

const MONTH_NAMES = [
  'فروردین', 'اردیبهشت', 'خرداد', 'تیر', 'مرداد', 'شهریور',
  'مهر', 'آبان', 'آذر', 'دی', 'بهمن', 'اسفند',
]

function payKindLabel(kind) {
  return PAY_KINDS.find((entry) => entry.value === kind)?.label ?? kind
}

function payPlanSummary(plan) {
  if (!plan) return '— ساختار پرداخت ثبت نشده —'
  const fa = (value) => Number(value).toLocaleString('fa-IR')
  switch (plan.kind) {
    case 'Monthly':
      return `${fa(plan.salaryAmount)} تومان ماهانه`
    case 'Hourly':
      return `${fa(plan.hourlyRate)} تومان هر ساعت`
    case 'ShiftBased':
      return `${fa(plan.shiftRate)} تومان هر شیفت`
    case 'CommissionOnly':
      return `${fa(plan.commissionPercent)}٪ پورسانت`
    case 'MonthlyPlusCommission':
      return `${fa(plan.salaryAmount)} تومان + ${fa(plan.commissionPercent)}٪ پورسانت`
    case 'HourlyPlusCommission':
      return `${fa(plan.hourlyRate)} تومان هر ساعت + ${fa(plan.commissionPercent)}٪ پورسانت`
    default:
      return plan.kind
  }
}

const PAYROLL_STATUS_LABELS = {
  Draft: 'پیش‌نویس',
  Finalized: 'قطعی‌شده',
  FullyPaid: 'کامل پرداخت‌شده',
}

const ENTRY_STATUS_LABELS = {
  Draft: 'پیش‌نویس',
  Paid: 'پرداخت‌شده',
}

/* Editable-tooltip: «؟» shows the merchant-owned definition */
function InfoTip({ termKey, glossary }) {
  const text = glossary?.[termKey]
  if (!text) return null
  return (
    <button type="button" className="info-tip" title={text} aria-label="توضیح">
      ؟
    </button>
  )
}

function weekdayLabel(weekday) {
  return HrShamsiWeekday[weekday] ?? ''
}

const HrShamsiWeekday = {
  6: 'شنبه',
  0: 'یکشنبه',
  1: 'دوشنبه',
  2: 'سه‌شنبه',
  3: 'چهارشنبه',
  4: 'پنجشنبه',
  5: 'جمعه',
}

function PunchButtons({ punchStatus, doesPunch }) {
  return (
    <span className="hr-punch-buttons">
      {punchStatus.map((status) => (
        <button
          key={status.employeeId}
          type="button"
          className={`ghost-button${status.state === 'In' ? ' hr-punch-out' : ''}`}
          onClick={() => doesPunch(status.employeeId, status.state === 'In' ? 'out' : 'in')}
          title={status.state === 'In' ? 'ثبت خروج' : 'ثبت ورود'}
        >
          {status.employeeName} {status.state === 'In' ? '— ثبت خروج' : '— ثبت ورود'}
        </button>
      ))}
    </span>
  )
}

function emptyEmployeeForm() {
  return {
    id: null,
    persianName: '',
    englishName: '',
    phoneNumber: '',
    nationalId: '',
    position: '',
    hireDate: '',
    notes: '',
  }
}

function defaultPayForm() {
  return {
    kind: 'Monthly',
    salaryAmount: '',
    hourlyRate: '',
    shiftRate: '',
    commissionPercent: '0',
    overtimeMultiplier: '1.4',
    notes: '',
  }
}

HrPage.weekdayLabel = weekdayLabel

export function HrPage() {
  const { session, apiRequest, getReadableError } = useAdmin()
  const token = session?.token

  const canView = hasPermission(session, 'admin.hr.view')
  const canCreate = hasPermission(session, 'admin.hr.create')
  const canUpdate = hasPermission(session, 'admin.hr.update')
  const canDelete = hasPermission(session, 'admin.hr.delete')

  const [tab, setTab] = useState('employees')
  const [error, setError] = useState('')
  const [message, setMessage] = useState('')
  const [loading, setLoading] = useState(false)

  const [glossary, setGlossary] = useState(null)

  // employees
  const [employees, setEmployees] = useState([])
  const [search, setSearch] = useState('')
  const [activeOnly, setActiveOnly] = useState(false)
  const [employeeForm, setEmployeeForm] = useState(null)
  const [planForm, setPlanForm] = useState(null)
  const [savingEmployee, setSavingEmployee] = useState(false)
  const [detail, setDetail] = useState(null)

  // work logs
  const [workLogs, setWorkLogs] = useState([])
  const [workLogEmployeeId, setWorkLogEmployeeId] = useState('')
  const [workLogForm, setWorkLogForm] = useState({
    id: null,
    gregorianDate: '',
    workedHours: '',
    overtimeHours: '0',
    shiftCount: '0',
    note: '',
  })
  const [editingLogId, setEditingLogId] = useState(null)

  // payroll
  const [periods, setPeriods] = useState([])
  const [selectedPeriodId, setSelectedPeriodId] = useState('')
  const [periodDetail, setPeriodDetail] = useState(null)
  const [payDefaultYear, setPayDefaultYear] = useState(0)
  const [payDefaultMonth, setPayDefaultMonth] = useState(1)
  const [entryForm, setEntryForm] = useState(null)

  // planning (task 17): café hours + approved shifts
  const [openHoursDraft, setOpenHoursDraft] = useState(null)
  const [shiftPlans, setShiftPlans] = useState([])
  const [shiftForm, setShiftForm] = useState({ employeeId: '', weekday: 6, startTime: '16:00', endTime: '23:00' })

  // attendance (task 18): punch live status + log
  const [punchStatus, setPunchStatus] = useState([])
  const [punches, setPunches] = useState([])
  const [punchEmployeeId, setPunchEmployeeId] = useState('')

  const loadGlossary = useCallback(async () => {
    if (!token) return
    try {
      const data = await apiRequest('/admin/hr/glossary', { token })
      const map = {}
      for (const term of data) {
        map[term.key] = term.definition
      }
      setGlossary(map)
    } catch (requestError) {
      setError(getReadableError(requestError))
    }
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [getReadableError, token])

  const loadEmployees = useCallback(async (nextSearch = '', nextActiveOnly = activeOnly) => {
    if (!token) return
    setLoading(true)
    setError('')
    try {
      const params = new URLSearchParams({ page: '1', pageSize: '100', search: nextSearch })
      if (nextActiveOnly) params.set('activeOnly', 'true')
      const data = await apiRequest(`/admin/hr/employees?${params.toString()}`, { token })
      setEmployees(Array.isArray(data.items) ? data.items : [])
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setLoading(false)
    }
  // eslint-disable-next-line react-hooks/exhaustive-deps
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [token])

  const loadPeriods = useCallback(async () => {
    if (!token) return
    try {
      const data = await apiRequest('/admin/hr/periods', { token })
      setPeriods(Array.isArray(data) ? data : [])
    } catch (requestError) {
      setError(getReadableError(requestError))
    }
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [token])

  const loadWorkLogs = useCallback(async (nextEmployeeId = workLogEmployeeId) => {
    if (!token) return
    setLoading(true)
    try {
      const params = new URLSearchParams({ page: '1', pageSize: '100' })
      if (nextEmployeeId) params.set('employeeId', nextEmployeeId)
      const data = await apiRequest(`/admin/hr/work-logs?${params.toString()}`, { token })
      setWorkLogs(Array.isArray(data.items) ? data.items : [])
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setLoading(false)
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [workLogEmployeeId, token])

  const loadPeriodDetail = useCallback(async (periodId) => {
    if (!token || !periodId) return
    setLoading(true)
    try {
      const data = await apiRequest(`/admin/hr/periods/${periodId}`, { token })
      setPeriodDetail(data)
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setLoading(false)
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [token])

  useEffect(() => {
    if (!canView || !token) return
    loadGlossary()
    loadEmployees()
    loadPeriods()
    // default shamsi month/year for the payroll tab (fa-IR calendar)
    try {
      const parts = new Intl.DateTimeFormat('fa-IR-u-nu-latn', { year: 'numeric', month: 'numeric' }).format(new Date())
      const match = /(\d+)\s*\/\s*(\d+)/.exec(parts)
      if (match) {
        setPayDefaultYear(Number(match[1]))
        setPayDefaultMonth(Number(match[2]))
      }
    } catch {
      setPayDefaultYear(1405)
      setPayDefaultMonth(7)
    }
  }, []) // eslint-disable-line react-hooks/exhaustive-deps

  const resetFeedback = () => {
    setError('')
    setMessage('')
  }

  /* ── Employees ─────────────────────────────────────────── */

  const openCreate = () => {
    resetFeedback()
    setEmployeeForm(emptyEmployeeForm())
    setPlanForm(defaultPayForm())
  }

  const openEdit = (employee) => {
    resetFeedback()
    setEmployeeForm({
      id: employee.id,
      persianName: employee.persianName,
      englishName: employee.englishName ?? '',
      phoneNumber: employee.phoneNumber ?? '',
      nationalId: employee.nationalId ?? '',
      position: employee.position,
      hireDate: employee.hireDate,
      notes: employee.notes ?? '',
    })
    const plan = employee.activePayPlan
    setPlanForm({
      id: plan?.id ?? null,
      kind: plan?.kind ?? 'Monthly',
      salaryAmount: plan?.salaryAmount ?? '',
      hourlyRate: plan?.hourlyRate ?? '',
      shiftRate: plan?.shiftRate ?? '',
      commissionPercent: plan?.commissionPercent ?? '0',
      overtimeMultiplier: plan?.overtimeMultiplier ?? '1.4',
      notes: plan?.notes ?? '',
    })
  }

  const saveEmployee = async () => {
    if (!employeeForm || !planForm) return
    setSavingEmployee(true)
    resetFeedback()
    try {
      let employeeId = employeeForm.id
      if (employeeId) {
        await apiRequest(`/admin/hr/employees/${employeeId}`, {
          method: 'PUT',
          token,
          body: {
            persianName: employeeForm.persianName,
            englishName: employeeForm.englishName,
            phoneNumber: employeeForm.phoneNumber,
            nationalId: employeeForm.nationalId,
            position: employeeForm.position,
            hireDate: employeeForm.hireDate,
            notes: employeeForm.notes,
          },
        })
      } else {
        const created = await apiRequest('/admin/hr/employees', {
          method: 'POST',
          token,
          body: {
            persianName: employeeForm.persianName,
            englishName: employeeForm.englishName,
            phoneNumber: employeeForm.phoneNumber,
            nationalId: employeeForm.nationalId,
            position: employeeForm.position,
            hireDate: employeeForm.hireDate || new Date().toISOString().slice(0, 10),
            notes: employeeForm.notes,
          },
        })
        employeeId = created.id
      }

      // pay plan: create a new version whenever the shape changes
      const planChanged = JSON.stringify({
        kind: planForm.kind,
        salaryAmount: planForm.salaryAmount,
        hourlyRate: planForm.hourlyRate,
        shiftRate: planForm.shiftRate,
        commissionPercent: planForm.commissionPercent,
        overtimeMultiplier: planForm.overtimeMultiplier,
        notes: planForm.notes,
      }) !== JSON.stringify({
        kind: employeeForm.id ?
          (employees.find((e) => e.id === employeeId)?.activePayPlan?.kind ?? planForm.kind) : planForm.kind,
        salaryAmount: employeeForm.id ?
          (employees.find((e) => e.id === employeeId)?.activePayPlan?.salaryAmount ?? '') : planForm.salaryAmount,
        hourlyRate: employeeForm.id ?
          (employees.find((e) => e.id === employeeId)?.activePayPlan?.hourlyRate ?? '') : planForm.hourlyRate,
        shiftRate: employeeForm.id ?
          (employees.find((e) => e.id === employeeId)?.activePayPlan?.shiftRate ?? '') : planForm.shiftRate,
        commissionPercent: employeeForm.id ?
          (employees.find((e) => e.id === employeeId)?.activePayPlan?.commissionPercent ?? '') : planForm.commissionPercent,
        overtimeMultiplier: employeeForm.id ?
          (employees.find((e) => e.id === employeeId)?.activePayPlan?.overtimeMultiplier ?? '') : planForm.overtimeMultiplier,
        notes: employeeForm.id ?
          (employees.find((e) => e.id === employeeId)?.activePayPlan?.notes ?? '') : planForm.notes,
      })

      if (!employeeForm.id || planChanged) {
        await apiRequest(`/admin/hr/employees/${employeeId}/pay-plan`, {
          method: 'POST',
          token,
          body: {
            kind: planForm.kind,
            salaryAmount: Number(planForm.salaryAmount || 0),
            hourlyRate: Number(planForm.hourlyRate || 0),
            shiftRate: Number(planForm.shiftRate || 0),
            commissionPercent: Number(planForm.commissionPercent || 0),
            overtimeMultiplier: Number(planForm.overtimeMultiplier || 1.4),
            notes: planForm.notes,
          },
        })
      }

      setEmployeeForm(null)
      setPlanForm(null)
      setMessage('پرونده پرسنلی ذخیره شد.')
      loadEmployees(search, activeOnly)
      loadPeriods()
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setSavingEmployee(false)
    }
  }

  const toggleActive = async (employee) => {
    resetFeedback()
    try {
      await apiRequest(`/admin/hr/employees/${employee.id}/active`, {
        method: 'PATCH',
        token,
        body: { isActive: !employee.isActive, terminationDate: null },
      })
      setMessage(employee.isActive ? 'پرسنل غیرفعال شد (تاریخچه حفظ می‌ماند).' : 'پرسنل فعال شد.')
      loadEmployees(search, activeOnly)
    } catch (requestError) {
      setError(getReadableError(requestError))
    }
  }

  const openDetail = async (employee) => {
    resetFeedback()
    try {
      const data = await apiRequest(`/admin/hr/employees/${employee.id}`, { token })
      setDetail(data)
    } catch (requestError) {
      setError(getReadableError(requestError))
    }
  }

  /* ── Work logs ─────────────────────────────────────────── */

  const saveWorkLog = async () => {
    if (!workLogForm.gregorianDate) {
      setError('تاریخ کارکرد را انتخاب کنید.')
      return
    }
    const effectiveEmployeeId = workLogForm.employeeId || workLogEmployeeId
    if (!effectiveEmployeeId) {
      setError('ابتدا یک پرسنل را انتخاب کنید.')
      return
    }
    resetFeedback()
    try {
      await apiRequest('/admin/hr/work-logs', {
        method: 'POST',
        token,
        body: {
          employeeId: effectiveEmployeeId,
          workDate: workLogForm.gregorianDate,
          workedHours: Number(workLogForm.workedHours || 0),
          overtimeHours: Number(workLogForm.overtimeHours || 0),
          shiftCount: Number(workLogForm.shiftCount || 0),
          note: workLogForm.note,
        },
      })
      setMessage('بروزرسانی کارکرد ثبت شد.')
      setWorkLogForm({ id: null, gregorianDate: '', workedHours: '', overtimeHours: '0', shiftCount: '0', note: '' })
      setEditingLogId(null)
      loadWorkLogs()
    } catch (requestError) {
      setError(getReadableError(requestError))
    }
  }

  const deleteWorkLog = async (log) => {
    resetFeedback()
    try {
      await apiRequest(`/admin/hr/work-logs/${log.id}`, { method: 'DELETE', token })
      setMessage('ردیف کارکرد حذف شد.')
      loadWorkLogs()
    } catch (requestError) {
      setError(getReadableError(requestError))
    }
  }

  const startEditLog = (log) => {
    setEditingLogId(log.id)
    setWorkLogForm({
      id: log.id,
      gregorianDate: log.workDate,
      workedHours: String(log.workedHours),
      overtimeHours: String(log.overtimeHours),
      shiftCount: String(log.shiftCount),
      note: log.note ?? '',
    })
  }

  /* ── Payroll ───────────────────────────────────────────── */

  const upsertPeriod = async () => {
    resetFeedback()
    setLoading(true)
    try {
      const data = await apiRequest('/admin/hr/periods', {
        method: 'POST',
        token,
        body: { shamsiYear: payDefaultYear, shamsiMonth: payDefaultMonth },
      })
      setPeriodDetail(data)
      setSelectedPeriodId(data.id)
      await loadPeriods()
      setMessage(
        data.status === 'Draft'
          ? 'دوره ساخته/بروزرسانی شد — اعداد بر اساس کارکردها و ساختار پرداخت محاسبه گردید.'
          : 'این قبلاً بسته شده است؛ برای دیدن اعداد نمایش داده شد.',
      )
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setLoading(false)
    }
  }

  const adjustEntry = async () => {
    if (!entryForm || !periodDetail) return
    resetFeedback()
    try {
      const data = await apiRequest(
        `/admin/hr/periods/${periodDetail.id}/entries/${entryForm.entryId}`,
        {
          method: 'PUT',
          token,
          body: {
            commissionSalesAmount: Number(entryForm.commissionSalesAmount || 0),
            bonus: Number(entryForm.bonus || 0),
            deductions: Number(entryForm.deductions || 0),
            advance: Number(entryForm.advance || 0),
            note: entryForm.note,
          },
        })
      setPeriodDetail(data)
      setEntryForm(null)
      setMessage('فیش حقوقی بروزرسانی شد.')
      loadPeriods()
    } catch (requestError) {
      setError(getReadableError(requestError))
    }
  }

  const openEntryForm = (entry) => {
    resetFeedback()
    setEntryForm({
      entryId: entry.id,
      employeeName: entry.employeeName,
      commissionSalesAmount: entry.commissionSalesAmount,
      commissionPercent: entry.commissionPercent,
      bonus: entry.bonus,
      deductions: entry.deductions,
      advance: entry.advance,
      note: entry.note ?? '',
    })
  }

  const finalizePeriod = async () => {
    resetFeedback()
    try {
      const data = await apiRequest(`/admin/hr/periods/${periodDetail.id}/finalize`, { method: 'POST', token })
      setPeriodDetail(data)
      loadPeriods()
      setMessage('دوره قطعی شد — اعداد قفل شدند و پرداخت ممکن است.')
    } catch (requestError) {
      setError(getReadableError(requestError))
    }
  }

  const reopenPeriod = async () => {
    resetFeedback()
    try {
      await apiRequest(`/admin/hr/periods/${periodDetail.id}/reopen`, { method: 'POST', token })
      await loadPeriodDetail(periodDetail.id)
      loadPeriods()
      setMessage('دوره به پیش‌نویس بازگشت.')
    } catch (requestError) {
      setError(getReadableError(requestError))
    }
  }

  const payEntry = async (entry) => {
    resetFeedback()
    try {
      const data = await apiRequest(
        `/admin/hr/periods/${periodDetail.id}/entries/${entry.id}/pay`,
        { method: 'POST', token })
      setPeriodDetail(data)
      loadPeriods()
      setMessage(`حقوق ${entry.employeeName} پرداخت شد و سند حسابداری ثبت گردید.`)
    } catch (requestError) {
      setError(getReadableError(requestError))
    }
  }

  const payAll = async () => {
    resetFeedback()
    try {
      const data = await apiRequest(`/admin/hr/periods/${periodDetail.id}/pay-all`, { method: 'POST', token })
      setPeriodDetail(data)
      loadPeriods()
      setMessage('همهٔ فیش‌های پرداخت‌نشده پرداخت شدند.')
    } catch (requestError) {
      setError(getReadableError(requestError))
    }
  }

  /* ── Glossary ──────────────────────────────────────────── */

  const saveGlossaryTerm = async (termKey, definition) => {
    resetFeedback()
    try {
      await apiRequest(`/admin/hr/glossary/${encodeURIComponent(termKey)}`, {
        method: 'PUT',
        token,
        body: { definition },
      })
      setGlossary((current) => ({ ...current, [termKey]: definition }))
      setMessage('تعریف ذخیره شد — tooltip همهٔ بخش‌ها از همین متن استفاده می‌کند.')
    } catch (requestError) {
      setError(getReadableError(requestError))
    }
  }

  /* ── Planning: café hours + shifts ─────────────────────── */

  const loadOpenHours = useCallback(async () => {
    if (!token) return
    try {
      const data = await apiRequest('/admin/hr/open-hours', { token })
      setOpenHoursDraft((data ?? []).map((row) => ({ ...row })))
    } catch (requestError) {
      setError(getReadableError(requestError))
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [token])

  const saveOpenHours = async () => {
    resetFeedback()
    try {
      const data = await apiRequest('/admin/hr/open-hours', {
        method: 'PUT',
        token,
        body: {
          days: (openHoursDraft ?? []).map((row) => ({
            weekday: row.weekday,
            isOpen: row.isOpen,
            openTime: row.openTime,
            closeTime: row.closeTime,
          })),
        },
      })
      setOpenHoursDraft((data ?? []).map((row) => ({ ...row })))
      setMessage('ساعات بازگشایی ذخیره شد.')
    } catch (requestError) {
      setError(getReadableError(requestError))
    }
  }

  const loadShiftPlans = useCallback(async () => {
    if (!token) return
    try {
      const data = await apiRequest('/admin/hr/shifts', { token })
      setShiftPlans(Array.isArray(data) ? data : [])
    } catch (requestError) {
      setError(getReadableError(requestError))
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [token])

  const saveShift = async () => {
    resetFeedback()
    try {
      await apiRequest('/admin/hr/shifts', {
        method: 'POST',
        token,
        body: {
          employeeId: shiftForm.employeeId,
          weekday: Number(shiftForm.weekday),
          startTime: shiftForm.startTime,
          endTime: shiftForm.endTime,
        },
      })
      setMessage('شیفت مصوب ذخیره شد.')
      loadShiftPlans()
    } catch (requestError) {
      setError(getReadableError(requestError))
    }
  }

  const deleteShift = async (plan) => {
    resetFeedback()
    try {
      await apiRequest(`/admin/hr/shifts/${plan.id}`, { method: 'DELETE', token })
      setMessage('شیفت حذف شد.')
      loadShiftPlans()
    } catch (requestError) {
      setError(getReadableError(requestError))
    }
  }

  /* ── Attendance: punch in / out ─────────────────────────── */

  const loadPunchStatus = useCallback(async () => {
    if (!token) return
    try {
      const data = await apiRequest('/admin/hr/punches/status', { token })
      setPunchStatus(Array.isArray(data) ? data : [])
    } catch (requestError) {
      setError(getReadableError(requestError))
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [token])

  const loadPunches = useCallback(async () => {
    if (!token) return
    try {
      const params = new URLSearchParams({ page: 1, pageSize: 50 })
      if (punchEmployeeId) params.set('employeeId', punchEmployeeId)
      const data = await apiRequest(`/admin/hr/punches?${params.toString()}`, { token })
      setPunches(Array.isArray(data.items) ? data.items : [])
    } catch (requestError) {
      setError(getReadableError(requestError))
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [punchEmployeeId, token])

  const doPunch = async (employeeId, direction) => {
    resetFeedback()
    try {
      await apiRequest(`/admin/hr/punches/${direction}`, {
        method: 'POST',
        token,
        body: direction === 'in' ? { employeeId, note: null } : { employeeId },
      })
      setMessage(direction === 'in' ? 'ورود ثبت شد.' : 'خروج ثبت شد — ساعت کارکرد محاسبه شد.')
      await loadPunchStatus()
      loadPunches()
    } catch (requestError) {
      setError(getReadableError(requestError))
    }
  }

  const loadPlannedData = async () => {
    await loadOpenHours()
    loadShiftPlans()
  }

  const glossaryEntries = useMemo(() => {
    if (!glossary) return []
    return Object.entries(glossary).sort(([a], [b]) => a.localeCompare(b))
  }, [glossary])

  if (!canView) {
    return (
      <section className="panel-stack">
        <section className="panel">
          <div className="panel-head">
            <div>
              <h3>منابع انسانی</h3>
            </div>
          </div>
          <p className="table-subtext">شما دسترسی لازم برای این بخش را ندارید.</p>
        </section>
      </section>
    )
  }

  return (
    <section className="panel-stack">
      <div className="hr-tabs" role="tablist">
        {[
          { id: 'employees', label: 'پرسنل' },
          { id: 'planning', label: 'برنامه‌ریزی' },
          { id: 'attendance', label: 'حضور و غیاب' },
          { id: 'worklogs', label: 'کارکرد دستی' },
          { id: 'payroll', label: 'حقوق و دستمزد' },
          { id: 'glossary', label: 'تعریف اصطلاحات' },
        ].map((entry) => (
          <button
            key={entry.id}
            type="button"
            className={`hr-tab-button${tab === entry.id ? ' is-active' : ''}`}
            onClick={() => {
              setTab(entry.id)
              resetFeedback()
              if (entry.id === 'planning') loadPlannedData()
              if (entry.id === 'attendance') {
                loadPunchStatus()
                loadPunches()
              }
              if (entry.id === 'worklogs') {
                if (employees.length === 0) loadEmployees()
              }
            }}
          >
            {entry.label}
          </button>
        ))}
      </div>

      {error ? <p className="feedback feedback-error">{error}</p> : null}
      {message ? <p className="feedback feedback-success">{message}</p> : null}

      {/* ─────────── Employees tab ─────────── */}
      {tab === 'employees' ? (
        <section className="panel">
          <div className="panel-head">
            <div>
              <h3>پروندهٔ پرسنل {<InfoTip termKey="hr.employee" glossary={glossary} />}</h3>
              <p>
                پرونده هر عضو تیم کافه همراه ساختار پرداخت حقوق — از حقوق ثابت و ساعتی تا شیفتی و پورسانتی.
              </p>
            </div>
            <form className="inline-form" onSubmit={(event) => { event.preventDefault(); loadEmployees(search, activeOnly) }}>
              <input
                type="search"
                value={search}
                placeholder="جستجو: نام، سمت یا تماس..."
                onChange={(event) => setSearch(event.target.value)}
              />
              <label className="hr-checkbox">
                <input
                  type="checkbox"
                  checked={activeOnly}
                  onChange={(event) => {
                    setActiveOnly(event.target.checked)
                    loadEmployees(search, event.target.checked)
                  }}
                />
                فقط فعال‌ها
              </label>
              {canCreate ? (
                <button type="button" className="ghost-button" onClick={openCreate}>
                  + پرسنل جدید
                </button>
              ) : null}
            </form>
          </div>

          {loading ? <p>در حال بارگذاری...</p> : null}

          {!loading && employees.length === 0 ? (
            <p>هنوز پرسنلی ثبت نشده است؛ دکمهٔ «+ پرسنل جدید» را بزنید.</p>
          ) : null}

          {employees.length > 0 ? (
            <table className="admin-table">
              <thead>
                <tr>
                  <th>نام</th>
                  <th>سمت {<InfoTip termKey="hr.position" glossary={glossary} />}</th>
                  <th>تماس</th>
                  <th>شروع به کار</th>
                  <th>ساختار پرداخت</th>
                  <th>وضعیت</th>
                  <th>عملیات</th>
                </tr>
              </thead>
              <tbody>
                {employees.map((employee) => (
                  <tr key={employee.id} className={employee.isActive ? '' : 'row-muted'}>
                    <td>{employee.persianName}</td>
                    <td>{employee.position}</td>
                    <td>{employee.phoneNumber ? toPersianNumber(employee.phoneNumber) : '—'}</td>
                    <td>{gregorianIsoToShamsiLabel(employee.hireDate) || '—'}</td>
                    <td className="hr-plan-cell">
                      <span className="hr-pay-badge">{payKindLabel(employee.activePayPlan?.kind)}</span>
                      <span className="table-subtext">{payPlanSummary(employee.activePayPlan)}</span>
                    </td>
                    <td>{employee.isActive ? 'فعال' : 'غیرفعال'}</td>
                    <td className="table-actions">
                      <button type="button" className="ghost-button" onClick={() => openDetail(employee)}>
                        پرونده
                      </button>
                      {canCreate || canUpdate ? (
                        <button type="button" className="ghost-button" onClick={() => openEdit(employee)}>
                          ویرایش
                        </button>
                      ) : null}
                      {canUpdate ? (
                        <button type="button" className="ghost-button" disabled={savingEmployee} onClick={() => toggleActive(employee)}>
                          {employee.isActive ? 'غیرفعال' : 'فعال'}
                        </button>
                      ) : null}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          ) : null}

          {detail ? (
            <div className="modal-backdrop" onClick={() => setDetail(null)}>
              <div className="modal-card hr-detail-modal" onClick={(event) => event.stopPropagation()}>
                <h3>پرونده {detail.persianName}</h3>
                <div className="stat-grid">
                  <div className="stat-card">
                    <span>سمت</span>
                    <strong>{detail.position}</strong>
                  </div>
                  <div className="stat-card">
                    <span>شروع به کار</span>
                    <strong>{gregorianIsoToShamsiLabel(detail.hireDate)}</strong>
                  </div>
                  <div className="stat-card">
                    <span>وضعیت</span>
                    <strong>{detail.isActive ? 'فعال' : 'غیرفعال'}</strong>
                  </div>
                </div>

                <h4>تاریخچه ساختار پرداخت {<InfoTip termKey="hr.plan.kind.Monthly" glossary={glossary} />}</h4>
                <table className="admin-table">
                  <thead>
                    <tr>
                      <th>نوع</th>
                      <th>جزئیات</th>
                      <th>فعال</th>
                      <th>تاریخ ثبت</th>
                    </tr>
                  </thead>
                  <tbody>
                    {detail.payPlanHistory.map((plan) => (
                      <tr key={plan.id}>
                        <td>{payKindLabel(plan.kind)}</td>
                        <td>{payPlanSummary(plan)}{plan.overtimeMultiplier ? ` | ضریب اضافه‌کار ${toPersianNumber(plan.overtimeMultiplier)}` : ''}</td>
                        <td>{plan.isActive ? '✔' : '—'}</td>
                        <td>{formatDateTime(plan.createdAt)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>

                <h4>فیش‌های حقوقی</h4>
                <table className="admin-table">
                  <thead>
                    <tr>
                      <th>دوره</th>
                      <th>وضعیت</th>
                      <th>مبلغ</th>
                    </tr>
                  </thead>
                  <tbody>
                    {detail.payrollEntries.length === 0 ? (
                      <tr><td colSpan={3}>هنوز فیشی ساخته نشده است.</td></tr>
                    ) : detail.payrollEntries.map((entry) => (
                      <tr key={entry.id}>
                        <td>{entry.periodLabel}</td>
                        <td>{ENTRY_STATUS_LABELS[entry.status] ?? entry.status}</td>
                        <td>{Number(entry.totalPay).toLocaleString('fa-IR')} تومان</td>
                      </tr>
                    ))}
                  </tbody>
                </table>

                <h4>آخرین کارکردها</h4>
                <table className="admin-table">
                  <thead>
                    <tr>
                      <th>تاریخ</th>
                      <th>ساعت</th>
                      <th>اضافه‌کار</th>
                      <th>شیفت</th>
                    </tr>
                  </thead>
                  <tbody>
                    {detail.recentWorkLogs.length === 0 ? (
                      <tr><td colSpan={4}>ردیفی ثبت نشده است.</td></tr>
                    ) : detail.recentWorkLogs.map((log) => (
                      <tr key={log.id}>
                        <td>{gregorianIsoToShamsiLabel(log.workDate)}</td>
                        <td>{toPersianNumber(log.workedHours)}</td>
                        <td>{toPersianNumber(log.overtimeHours)}</td>
                        <td>{toPersianNumber(log.shiftCount)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>

                <div className="modal-actions">
                  <button type="button" className="ghost-button" onClick={() => setDetail(null)}>
                    بستن
                  </button>
                </div>
              </div>
            </div>
          ) : null}
        </section>
      ) : null}

      {/* ─────────── Work logs tab ─────────── */}
      {tab === 'worklogs' ? (
        <section className="panel">
          <div className="panel-head">
            <div>
              <h3>کارکرد و حضور {<InfoTip termKey="hr.worklog.workedHours" glossary={glossary} />}</h3>
              <p>برای هر نفر و هر روز، ساعت کارکرد، اضافه‌کار و تعداد شیفت را ثبت کنید — مبنای فیش حقوقی دوره است.</p>
            </div>
            <form className="inline-form" onSubmit={(event) => { event.preventDefault(); loadWorkLogs(workLogEmployeeId) }}>
              <select value={workLogEmployeeId} onChange={(event) => { setWorkLogEmployeeId(event.target.value); loadWorkLogs(event.target.value) }}>
                <option value="">همهٔ پرسنل</option>
                {employees.filter((employee) => employee.isActive).map((employee) => (
                  <option key={employee.id} value={employee.id}>{employee.persianName} — {employee.position}</option>
                ))}
              </select>
              <button type="submit" className="ghost-button">بروزرسانی</button>
            </form>
          </div>

          <div className="hr-worklog-form field-grid">
            <label>
              <span>پرسنل {<InfoTip termKey="hr.employee" glossary={glossary} />}</span>
              <select
                value={workLogForm.employeeId ?? workLogEmployeeId}
                onChange={(event) => setWorkLogForm((form) => ({ ...form, employeeId: event.target.value }))}
              >
                <option value="">انتخاب کنید...</option>
                {employees.filter((employee) => employee.isActive).map((employee) => (
                  <option key={employee.id} value={employee.id}>{employee.persianName} — {employee.position}</option>
                ))}
              </select>
            </label>
            <label>
              <span>تاریخ</span>
              <ShamsiDatePicker
                value={workLogForm.gregorianDate}
                onChange={(value) => setWorkLogForm((form) => ({ ...form, gregorianDate: value }))}
                placeholder="کلیک کنید تا تقویم باز شود"
              />
            </label>
            <label>
              <span>ساعت کارکرد (کل) {<InfoTip termKey="hr.worklog.workedHours" glossary={glossary} />}</span>
              <input
                type="number"
                min="0"
                max="24"
                step="0.5"
                value={workLogForm.workedHours}
                onChange={(event) => setWorkLogForm((form) => ({ ...form, workedHours: event.target.value }))}
              />
            </label>
            <label>
              <span>اضافه‌کار {<InfoTip termKey="hr.worklog.overtimeHours" glossary={glossary} />}</span>
              <input
                type="number"
                min="0"
                max="12"
                step="0.5"
                value={workLogForm.overtimeHours}
                onChange={(event) => setWorkLogForm((form) => ({ ...form, overtimeHours: event.target.value }))}
              />
            </label>
            <label>
              <span>تعداد شیفت {<InfoTip termKey="hr.worklog.shiftCount" glossary={glossary} />}</span>
              <input
                type="number"
                min="0"
                max="3"
                value={workLogForm.shiftCount}
                onChange={(event) => setWorkLogForm((form) => ({ ...form, shiftCount: event.target.value }))}
              />
            </label>
            <label>
              <span>یادداشت</span>
              <input
                type="text"
                value={workLogForm.note}
                onChange={(event) => setWorkLogForm((form) => ({ ...form, note: event.target.value }))}
              />
            </label>
            <div className="hr-form-actions">
              {canUpdate ? (
                <button type="button" className="ghost-button" onClick={saveWorkLog}>
                  {editingLogId ? 'بروزرسانی ردیف' : 'ثبت کارکرد'}
                </button>
              ) : null}
              {editingLogId ? (
                <button
                  type="button"
                  className="ghost-button"
                  onClick={() => {
                    setEditingLogId(null)
                    setWorkLogForm({ id: null, gregorianDate: '', workedHours: '', overtimeHours: '0', shiftCount: '0', note: '' })
                  }}
                >
                  انصراف
                </button>
              ) : null}
            </div>
          </div>

          {loading ? <p>در حال بارگذاری...</p> : null}

          {workLogs.length === 0 && !loading ? (
            <p>ردیفی ثبت نشده است.</p>
          ) : null}

          {workLogs.length > 0 ? (
            <table className="admin-table">
              <thead>
                <tr>
                  <th>پرسنل</th>
                  <th>تاریخ</th>
                  <th>ساعت کارکرد</th>
                  <th>اضافه‌کار</th>
                  <th>شیفت</th>
                  <th>یادداشت</th>
                  <th>عملیات</th>
                </tr>
              </thead>
              <tbody>
                {workLogs.map((log) => (
                  <tr key={log.id}>
                    <td>{employees.find((employee) => employee.id === log.employeeId)?.persianName ?? '—'}</td>
                    <td>{gregorianIsoToShamsiLabel(log.workDate)}</td>
                    <td>{toPersianNumber(log.workedHours)}</td>
                    <td>{toPersianNumber(log.overtimeHours)}</td>
                    <td>{toPersianNumber(log.shiftCount)}</td>
                    <td>{log.note ?? '—'}</td>
                    <td className="table-actions">
                      {canUpdate ? (
                        <button type="button" className="ghost-button" onClick={() => startEditLog(log)}>
                          ویرایش
                        </button>
                      ) : null}
                      {canDelete ? (
                        <button type="button" className="ghost-button danger" onClick={() => deleteWorkLog(log)}>
                          حذف
                        </button>
                      ) : null}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          ) : null}
        </section>
      ) : null}

      {/* ─────────── Planning tab: café hours + approved shifts ─────────── */}
      {tab === 'planning' ? (
        <>
          <section className="panel">
            <div className="panel-head">
              <div>
                <h3>ساعات بازگشایی کافه {<InfoTip termKey="hr.openHours" glossary={glossary} />}</h3>
                <p>برای هر روز هفته، باز بودن و ساعت باز/بسته شدن را تنظیم کنید.</p>
              </div>
              {canUpdate ? (
                <button type="button" className="ghost-button" onClick={saveOpenHours} disabled={!openHoursDraft?.length}>
                  ذخیره ساعات
                </button>
              ) : null}
            </div>

            <table className="admin-table">
              <thead>
                <tr>
                  <th>روز</th>
                  <th>باز است؟</th>
                  <th>باز شدن</th>
                  <th>بسته شدن</th>
                  <th>ساعت خدمات</th>
                </tr>
              </thead>
              <tbody>
                {(openHoursDraft ?? []).map((row, index) => (
                  <tr key={row.weekday}>
                    <td>{HrPage.weekdayLabel(row.weekday)}</td>
                    <td>
                      <input
                        type="checkbox"
                        checked={row.isOpen}
                        onChange={(event) => setOpenHoursDraft((draft) => {
                          const next = [...draft]
                          next[index] = { ...row, isOpen: event.target.checked }
                          return next
                        })}
                      />
                    </td>
                    <td>
                      <input
                        type="time"
                        value={row.openTime}
                        disabled={!row.isOpen}
                        onChange={(event) => setOpenHoursDraft((draft) => {
                          const next = [...draft]
                          next[index] = { ...row, openTime: event.target.value }
                          return next
                        })}
                      />
                    </td>
                    <td>
                      <input
                        type="time"
                        value={row.closeTime}
                        disabled={!row.isOpen}
                        onChange={(event) => setOpenHoursDraft((draft) => {
                          const next = [...draft]
                          next[index] = { ...row, closeTime: event.target.value }
                          return next
                        })}
                      />
                    </td>
                    <td>{row.isOpen ? toPersianNumber(Math.round((row.scheduledMinutes ?? 0) / 6) / 10) : '—'} ساعت</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </section>

          <section className="panel">
            <div className="panel-head">
              <div>
                <h3>شیفت‌های مصوب پرسنل {<InfoTip termKey="hr.shiftPlan" glossary={glossary} />}</h3>
                <p>برای هر نفر و هر روز هفته، شیفت مصوب را تعیین کنید — مبنای مقایسهٔ حضور واقعی برای اضافه‌کار و کسرکار.</p>
              </div>
            </div>

            {canUpdate ? (
              <div className="hr-worklog-form field-grid">
                <label>
                  <span>پرسنل *</span>
                  <select
                    value={shiftForm.employeeId}
                    onChange={(event) => setShiftForm((form) => ({ ...form, employeeId: event.target.value }))}
                  >
                    <option value="">انتخاب کنید...</option>
                    {employees.map((employee) => (
                      <option key={employee.id} value={employee.id}>{employee.persianName} — {employee.position}</option>
                    ))}
                  </select>
                </label>
                <label>
                  <span>روز * {<InfoTip termKey="hr.shiftPlan" glossary={glossary} />}</span>
                  <select
                    value={shiftForm.weekday}
                    onChange={(event) => setShiftForm((form) => ({ ...form, weekday: Number(event.target.value) }))}
                  >
                    {[6, 0, 1, 2, 3, 4, 5].map((weekday) => (
                      <option key={weekday} value={weekday}>{HrPage.weekdayLabel(weekday)}</option>
                    ))}
                  </select>
                </label>
                <label>
                  <span>از ساعت *</span>
                  <input
                    type="time"
                    value={shiftForm.startTime}
                    onChange={(event) => setShiftForm((form) => ({ ...form, startTime: event.target.value }))}
                  />
                </label>
                <label>
                  <span>تا ساعت *</span>
                  <input
                    type="time"
                    value={shiftForm.endTime}
                    onChange={(event) => setShiftForm((form) => ({ ...form, endTime: event.target.value }))}
                  />
                </label>
                <div className="hr-form-actions">
                  <button type="button" className="ghost-button" onClick={saveShift}>
                    ثبت شیفت
                  </button>
                </div>
              </div>
            ) : null}

            {!shiftPlans.length ? (
              <p>شیفت مصوبی ثبت نشده است — بدون آن، اضافه‌کار و کسرکار قابل مقایسه نیست.</p>
            ) : (
              <table className="admin-table">
                <thead>
                  <tr>
                    <th>پرسنل</th>
                    <th>روز</th>
                    <th>از</th>
                    <th>تا</th>
                    <th>ساعت مصوب</th>
                    <th>عملیات</th>
                  </tr>
                </thead>
                <tbody>
                  {shiftPlans.map((plan) => (
                    <tr key={plan.id}>
                      <td>{plan.employeeName}</td>
                      <td>{plan.weekdayLabel}</td>
                      <td dir="ltr">{plan.startTime}</td>
                      <td dir="ltr">{plan.endTime}</td>
                      <td>{toPersianNumber(Math.round((plan.scheduledMinutes / 6)) / 10)} ساعت</td>
                      <td className="table-actions">
                        {canUpdate ? (
                          <button
                            type="button"
                            className="ghost-button"
                            onClick={() => setShiftForm(() => ({
                              employeeId: plan.employeeId,
                              weekday: plan.weekday,
                              startTime: plan.startTime,
                              endTime: plan.endTime,
                            }))}
                          >
                            ویرایش
                          </button>
                        ) : null}
                        {canDelete ? (
                          <button type="button" className="ghost-button danger" onClick={() => deleteShift(plan)}>
                            حذف
                          </button>
                        ) : null}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </section>
        </>
      ) : null}

      {/* ─────────── Attendance tab: punch in/out ─────────── */}
      {tab === 'attendance' ? (
        <section className="panel">
          <div className="panel-head">
            <div>
              <h3>حضور و غیاب {(canUpdate) ? <PunchButtons punchStatus={punchStatus} doesPunch={doPunch} glossary={glossary} /> : null}</h3>
              <p>دکمه‌های «ورود» و «خروج» دقیقا زمان حضور واقعی را ثبت می‌کنند — مبناي حقوق دوره.</p>
              <p className="table-subtext">{<InfoTip termKey="hr.punch" glossary={glossary} />} وضعیت‌ها: {<InfoTip termKey="hr.punchState" glossary={glossary} />}</p>
            </div>
            <form className="inline-form" onSubmit={(event) => { event.preventDefault(); loadPunches() }}>
              <select
                value={punchEmployeeId}
                onChange={(event) => { setPunchEmployeeId(event.target.value); loadPunches() }}
              >
                <option value="">همهٔ پرسنل</option>
                {employees.map((employee) => (
                  <option key={employee.id} value={employee.id}>{employee.persianName} — {employee.position}</option>
                ))}
              </select>
              <button type="submit" className="ghost-button">بروزرسانی</button>
            </form>
          </div>

          <div className="stat-grid">
            {punchStatus.length === 0 ? <p>پرسنلی ثبت نشده است.</p> : punchStatus.map((status) => (
              <div key={status.employeeId} className="stat-card">
                <span>{status.employeeName}</span>
                <strong>{status.state === 'In' ? 'پانچ باز ⏳' : status.state === 'Out' ? 'خروج ثبت شد ✓' : 'بدون ثبت'}</strong>
                <span className="table-subtext">
                  {status.punchInAt ? 'ورود: ' + formatDateTime(status.punchInAt) : 'امروز پانچی ندارد'}
                </span>
              </div>
            ))}
          </div>

          {!punches.length ? (
            <p>در تاریخ خواسته‌شده پانچی ثبت نشده است.</p>
          ) : (
            <table className="admin-table">
              <thead>
                <tr>
                  <th>پرسنل</th>
                  <th>روز</th>
                  <th>ورود</th>
                  <th>خروج</th>
                  <th>ساعت کارکرد</th>
                  <th>وضعیت</th>
                  <th>یادداشت</th>
                </tr>
              </thead>
              <tbody>
                {punches.map((punch) => {
                  const punchDate = punch.punchInAt ? new Date(punch.punchInAt) : null
                  return (
                    <tr key={punch.id}>
                      <td>{punch.employeeName}</td>
                      <td>{gregorianIsoToShamsiLabel(punch.workDate)}</td>
                      <td dir="ltr">{punchDate ? punchDate.toLocaleTimeString('fa-IR') : '—'}</td>
                      <td dir="ltr">{punch.punchOutAt ? new Date(punch.punchOutAt).toLocaleTimeString('fa-IR') : '—'}</td>
                      <td>{toPersianNumber(Math.round(punch.workedMinutes / 6) / 10)} ساعت</td>
                      <td>{punch.status === 'Open' ? 'پانچ باز' : punch.status === 'Closed' ? 'بسته' : 'ناتمام'}</td>
                      <td>{punch.note ?? '—'}</td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          )}
        </section>
      ) : null}

      {/* ─────────── Payroll tab ─────────── */}
      {tab === 'payroll' ? (
        <section className="panel">
          <div className="panel-head">
            <div>
              <h3>حقوق و دستمزد {<InfoTip termKey="hr.payroll.period" glossary={glossary} />}</h3>
              <p>انتخاب ماه شمسی → ساخت/بروزرسانی فیش‌ها از روی کارکردها → تعدیل دستی → قطعی‌کردن → پرداخت.</p>
            </div>
            <form
              className="inline-form"
              onSubmit={(event) => { event.preventDefault(); upsertPeriod() }}
            >
              <select value={payDefaultMonth} onChange={(event) => setPayDefaultMonth(Number(event.target.value))}>
                {MONTH_NAMES.map((name, index) => (
                  <option key={name} value={index + 1}>{name}</option>
                ))}
              </select>
              <select value={payDefaultYear} onChange={(event) => setPayDefaultYear(Number(event.target.value))}>
                {[payDefaultYear - 2, payDefaultYear - 1, payDefaultYear, payDefaultYear + 1]
                  .filter((year, index, list) => year > 1300 && list.indexOf(year) === index)
                  .map((year) => (
                    <option key={year} value={year}>{year}</option>
                  ))}
              </select>
              <button type="submit" className="ghost-button">
                ساخت / بروزرسانی دوره
              </button>
            </form>
          </div>

          {periods.length > 0 ? (
            <div className="hr-period-picker">
              <span>دوره‌های قبلی: </span>
              <select
                value={selectedPeriodId}
                onChange={(event) => { setSelectedPeriodId(event.target.value); loadPeriodDetail(event.target.value) }}
              >
                <option value="">انتخاب...</option>
                {periods.map((period) => (
                  <option key={period.id} value={period.id}>
                    {period.monthLabel} — {PAYROLL_STATUS_LABELS[period.status] ?? period.status}
                  </option>
                ))}
              </select>
            </div>
          ) : null}

          {periodDetail ? (
            <>
              <div className="stat-grid">
                <div className="stat-card">
                  <span>قابل پرداخت (مانده)</span>
                  <strong>{Number(periodDetail.totals.totalRemaining).toLocaleString('fa-IR')} تومان</strong>
                </div>
                <div className="stat-card">
                  <span>پرداخت‌شده</span>
                  <strong>{Number(periodDetail.totals.totalPaid).toLocaleString('fa-IR')} تومان</strong>
                </div>
                <div className="stat-card">
                  <span>فیش‌ها</span>
                  <strong>
                    {toPersianNumber(periodDetail.totals.paidCount)} از {toPersianNumber(periodDetail.totals.entryCount)} پرداخت‌شده
                  </strong>
                </div>
                <div className="stat-card">
                  <span>وضعیت دوره {<InfoTip termKey={`hr.payroll.${periodDetail.status.toLowerCase()}`} glossary={glossary} />}</span>
                  <strong>{PAYROLL_STATUS_LABELS[periodDetail.status] ?? periodDetail.status}</strong>
                </div>
              </div>

              <div className="hr-payroll-actions">
                {periodDetail.status === 'Draft' && canUpdate ? (
                  <button type="button" className="ghost-button" onClick={finalizePeriod}>
                    قطعی‌کردن دوره {<InfoTip termKey="hr.payroll.finalizeAction" glossary={glossary} />}
                  </button>
                ) : null}
                {periodDetail.status === 'Finalized' && canUpdate ? (
                  <>
                    <button type="button" className="ghost-button" onClick={payAll}>
                      پرداخت همه {<InfoTip termKey="hr.payroll.payAction" glossary={glossary} />}
                    </button>
                    <button type="button" className="ghost-button" onClick={reopenPeriod}>
                      بازگشایی {<InfoTip termKey="hr.payroll.reopenAction" glossary={glossary} />}
                    </button>
                  </>
                ) : null}
              </div>

              <table className="admin-table">
                <thead>
                  <tr>
                    <th>پرسنل</th>
                    <th>نوع پرداخت</th>
                    <th>کارکرد (ساعت)</th>
                    <th>اضافه‌کار (ساعت)</th>
                    <th>شیفت</th>
                    <th>شیفت مصوب {<InfoTip termKey="hr.scheduledHours" glossary={glossary} />}</th>
                    <th>کسرکار {<InfoTip termKey="hr.underwork" glossary={glossary} />}</th>
                    <th>پایه {<InfoTip termKey="hr.payroll.basePay" glossary={glossary} />}</th>
                    <th>مبلغ اضافه‌کار {<InfoTip termKey="hr.payroll.overtimePay" glossary={glossary} />}</th>
                    <th>پورسانت {<InfoTip termKey="hr.payroll.commissionPay" glossary={glossary} />}</th>
                    <th>پاداش {<InfoTip termKey="hr.payroll.bonus" glossary={glossary} />}</th>
                    <th>کسورات {<InfoTip termKey="hr.payroll.deductions" glossary={glossary} />}</th>
                    <th>مساعده {<InfoTip termKey="hr.payroll.advance" glossary={glossary} />}</th>
                    <th>جمع {<InfoTip termKey="hr.payroll.totalPay" glossary={glossary} />}</th>
                    <th>وضعیت</th>
                    <th>عملیات</th>
                  </tr>
                </thead>
                <tbody>
                  {periodDetail.entries.map((entry) => (
                    <tr key={entry.id}>
                      <td>
                        <strong>{entry.employeeName}</strong>
                        <span className="table-subtext">{entry.employeePosition}</span>
                      </td>
                      <td>{payKindLabel(entry.payPlanKind)}</td>
                      <td>{toPersianNumber(entry.workedHours)}</td>
                      <td>{toPersianNumber(entry.overtimeHours)}</td>
                      <td>{toPersianNumber(entry.shiftCount)}</td>
                      <td>{entry.scheduledHours ? toPersianNumber(entry.scheduledHours) : '—'}</td>
                      <td>
                        {entry.underworkHours ? `${toPersianNumber(entry.underworkHours)} ساعت / ${Number(entry.underworkDeduction).toLocaleString('fa-IR')} تومان` : '—'}
                      </td>
                      <td>{Number(entry.basePay).toLocaleString('fa-IR')}</td>
                      <td>{Number(entry.overtimePay).toLocaleString('fa-IR')}</td>
                      <td>{Number(entry.commissionPay).toLocaleString('fa-IR')}</td>
                      <td>{Number(entry.bonus).toLocaleString('fa-IR')}</td>
                      <td>{Number(entry.deductions).toLocaleString('fa-IR')}</td>
                      <td>{Number(entry.advance).toLocaleString('fa-IR')}</td>
                      <td className="hr-total-cell">{Number(entry.totalPay).toLocaleString('fa-IR')}</td>
                      <td>
                        {ENTRY_STATUS_LABELS[entry.status] ?? entry.status}
                        {entry.paidAt ? <span className="table-subtext">{formatDateTime(entry.paidAt)}</span> : null}
                      </td>
                      <td className="table-actions">
                        {entry.status === 'Draft' && canUpdate ? (
                          <button type="button" className="ghost-button" onClick={() => openEntryForm(entry)}>
                            ویرایش
                          </button>
                        ) : null}
                        {entry.status === 'Draft' && periodDetail.status !== 'Draft' && canUpdate ? (
                          <button type="button" className="ghost-button" onClick={() => payEntry(entry)}>
                            پرداخت
                          </button>
                        ) : null}
                      </td>
                    </tr>
                  ))}
                  {periodDetail.entries.length === 0 ? (
                    <tr><td colSpan={16}>فیشی برای این دوره وجود ندارد.</td></tr>
                  ) : null}
                </tbody>
              </table>
            </>
          ) : (
            <p>ماهی را انتخاب و «ساخت / بروزرسانی دوره» را بزنید تا فیش‌ها محاسبه شوند.</p>
          )}
        </section>
      ) : null}

      {/* ─────────── Glossary tab ─────────── */}
      {tab === 'glossary' ? (
        <section className="panel">
          <div className="panel-head">
            <div>
              <h3>تعریف اصطلاحات {<InfoTip termKey="hr.glossary.edit" glossary={glossary} />}</h3>
              <p>متن tooltip هر اصطلاح را به زبان خود کافه تغییر دهید — همان متن روی «؟» همهٔ این صفحه نمایش داده می‌شود.</p>
            </div>
          </div>

          {!glossary ? <p>در حال بارگذاری...</p> : null}

          {glossaryEntries.map(([termKey, definition]) => (
            <div key={termKey} className="hr-glossary-row">
              <label>
                <span className="hr-glossary-key">{termKey}</span>
                <textarea
                  defaultValue={definition}
                  rows={2}
                  onChange={(event) => {
                    // debounce-free simple local state
                    event.target.dataset.changed = String(event.target.value !== definition)
                  }}
                  ref={(node) => {
                    if (node) {
                      node.dataset.key = termKey
                    }
                  }}
                />
              </label>
              {canUpdate ? (
                <button
                  type="button"
                  className="ghost-button"
                  onClick={(event) => {
                    const textarea = event.target.parentElement.querySelector('textarea')
                    saveGlossaryTerm(termKey, textarea.value)
                  }}
                >
                  ذخیره
                </button>
              ) : null}
            </div>
          ))}
        </section>
      ) : null}

      {/* ─────────── Employee editor modal ─────────── */}
      {employeeForm && planForm ? (
        <div className="modal-backdrop" onClick={() => { setEmployeeForm(null); setPlanForm(null) }}>
          <div className="modal-card hr-editor-modal" onClick={(event) => event.stopPropagation()}>
            <h3>{employeeForm.id ? `ویرایش پرونده ${employeeForm.persianName}` : 'پرسنل جدید'}</h3>

            <div className="field-grid">
              <label>
                <span>نام فارسی *</span>
                <input
                  type="text"
                  value={employeeForm.persianName}
                  onChange={(event) => setEmployeeForm((form) => ({ ...form, persianName: event.target.value }))}
                />
              </label>
              <label>
                <span>نام انگلیسی</span>
                <input
                  type="text"
                  value={employeeForm.englishName}
                  onChange={(event) => setEmployeeForm((form) => ({ ...form, englishName: event.target.value }))}
                />
              </label>
              <label>
                <span>شماره تماس</span>
                <input
                  type="tel"
                  value={employeeForm.phoneNumber}
                  onChange={(event) => setEmployeeForm((form) => ({ ...form, phoneNumber: event.target.value }))}
                />
              </label>
              <label>
                <span>کد ملی</span>
                <input
                  type="text"
                  value={employeeForm.nationalId}
                  onChange={(event) => setEmployeeForm((form) => ({ ...form, nationalId: event.target.value }))}
                />
              </label>
              <label>
                <span>سمت شغلی * {<InfoTip termKey="hr.position" glossary={glossary} />}</span>
                <input
                  type="text"
                  value={employeeForm.position}
                  placeholder="باریستا / کاسیِر / سرآشپز..."
                  onChange={(event) => setEmployeeForm((form) => ({ ...form, position: event.target.value }))}
                />
              </label>
              <label>
                <span>تاریخ شروع به کار</span>
                <ShamsiDatePicker
                  value={employeeForm.hireDate}
                  onChange={(value) => setEmployeeForm((form) => ({ ...form, hireDate: value }))}
                />
              </label>
              <label className="hr-full-width">
                <span>یادداشت</span>
                <input
                  type="text"
                  value={employeeForm.notes}
                  onChange={(event) => setEmployeeForm((form) => ({ ...form, notes: event.target.value }))}
                />
              </label>
            </div>

            <h4 className="hr-section-title">ساختار پرداخت حقوق</h4>
            <div className="field-grid">
              <label>
                <span>نوع پرداخت *</span>
                <select
                  value={planForm.kind}
                  onChange={(event) => setPlanForm((form) => ({ ...form, kind: event.target.value }))}
                >
                  {PAY_KINDS.map((kind) => (
                    <option key={kind.value} value={kind.value}>{kind.label}</option>
                  ))}
                </select>
                <span className="hr-glossary-def">{glossary?.[`hr.plan.kind.${planForm.kind}`]}</span>
              </label>

              {['Monthly', 'MonthlyPlusCommission'].includes(planForm.kind) ? (
                <label>
                  <span>حقوق ثابت ماهانه (تومان) {<InfoTip termKey="hr.plan.salary" glossary={glossary} />}</span>
                  <input
                    type="number"
                    min="0"
                    value={planForm.salaryAmount}
                    onChange={(event) => setPlanForm((form) => ({ ...form, salaryAmount: event.target.value }))}
                  />
                </label>
              ) : null}

              {['Hourly', 'HourlyPlusCommission'].includes(planForm.kind) ? (
                <label>
                  <span>نرخ هر ساعت (تومان) {<InfoTip termKey="hr.plan.hourlyRate" glossary={glossary} />}</span>
                  <input
                    type="number"
                    min="0"
                    value={planForm.hourlyRate}
                    onChange={(event) => setPlanForm((form) => ({ ...form, hourlyRate: event.target.value }))}
                  />
                </label>
              ) : null}

              {planForm.kind === 'ShiftBased' ? (
                <label>
                  <span>نرخ هر شیفت (تومان) {<InfoTip termKey="hr.plan.shiftRate" glossary={glossary} />}</span>
                  <input
                    type="number"
                    min="0"
                    value={planForm.shiftRate}
                    onChange={(event) => setPlanForm((form) => ({ ...form, shiftRate: event.target.value }))}
                  />
                </label>
              ) : null}

              {['CommissionOnly', 'MonthlyPlusCommission', 'HourlyPlusCommission'].includes(planForm.kind) ? (
                <label>
                  <span>درصد پورسانت {<InfoTip termKey="hr.plan.commissionPercent" glossary={glossary} />}</span>
                  <input
                    type="number"
                    min="0"
                    max="100"
                    step="0.5"
                    value={planForm.commissionPercent}
                    onChange={(event) => setPlanForm((form) => ({ ...form, commissionPercent: event.target.value }))}
                  />
                </label>
              ) : null}

              <label>
                <span>ضریب اضافه‌کار {<InfoTip termKey="hr.plan.overtimeMultiplier" glossary={glossary} />}</span>
                <input
                  type="number"
                  min="0.5"
                  max="3"
                  step="0.1"
                  value={planForm.overtimeMultiplier}
                  onChange={(event) => setPlanForm((form) => ({ ...form, overtimeMultiplier: event.target.value }))}
                />
              </label>
              <label className="hr-full-width">
                <span>یادداشت ساختار پرداخت</span>
                <input
                  type="text"
                  value={planForm.notes}
                  onChange={(event) => setPlanForm((form) => ({ ...form, notes: event.target.value }))}
                />
              </label>
            </div>

            <div className="modal-actions">
              <button type="button" className="ghost-button" disabled={savingEmployee} onClick={saveEmployee}>
                {savingEmployee ? 'در حال ذخیره...' : 'ذخیره پرونده'}
              </button>
              <button type="button" className="ghost-button" onClick={() => { setEmployeeForm(null); setPlanForm(null) }}>
                انصراف
              </button>
            </div>
          </div>
        </div>
      ) : null}

      {/* ─────────── Payroll entry adjust modal ─────────── */}
      {entryForm && periodDetail ? (
        <div className="modal-backdrop" onClick={() => setEntryForm(null)}>
          <div className="modal-card hr-editor-modal" onClick={(event) => event.stopPropagation()}>
            <h3>تعدیل فیش: {entryForm.employeeName}</h3>
            <div className="field-grid">
              <label>
                <span>فروش منسوب به این نفر (تومان) {<InfoTip termKey="hr.plan.commissionSales" glossary={glossary} />}</span>
                <input
                  type="number"
                  min="0"
                  value={entryForm.commissionSalesAmount}
                  onChange={(event) => setEntryForm((form) => ({ ...form, commissionSalesAmount: event.target.value }))}
                />
                <span className="hr-glossary-def">
                  پورسانت = فروش × {toPersianNumber(entryForm.commissionPercent)}٪ ={' '}
                  {Number((Number(entryForm.commissionSalesAmount || 0) * Number(entryForm.commissionPercent)) / 100).toLocaleString('fa-IR')} تومان
                </span>
              </label>
              <label>
                <span>پاداش {<InfoTip termKey="hr.payroll.bonus" glossary={glossary} />}</span>
                <input
                  type="number"
                  min="0"
                  value={entryForm.bonus}
                  onChange={(event) => setEntryForm((form) => ({ ...form, bonus: event.target.value }))}
                />
              </label>
              <label>
                <span>کسورات {<InfoTip termKey="hr.payroll.deductions" glossary={glossary} />}</span>
                <input
                  type="number"
                  min="0"
                  value={entryForm.deductions}
                  onChange={(event) => setEntryForm((form) => ({ ...form, deductions: event.target.value }))}
                />
              </label>
              <label>
                <span>مساعده {<InfoTip termKey="hr.payroll.advance" glossary={glossary} />}</span>
                <input
                  type="number"
                  min="0"
                  value={entryForm.advance}
                  onChange={(event) => setEntryForm((form) => ({ ...form, advance: event.target.value }))}
                />
              </label>
              <label className="hr-full-width">
                <span>یادداشت</span>
                <input
                  type="text"
                  value={entryForm.note}
                  onChange={(event) => setEntryForm((form) => ({ ...form, note: event.target.value }))}
                />
              </label>
            </div>
            <div className="modal-actions">
              <button type="button" className="ghost-button" onClick={adjustEntry}>
                ذخیره تعدیل
              </button>
              <button type="button" className="ghost-button" onClick={() => setEntryForm(null)}>
                انصراف
              </button>
            </div>
          </div>
        </div>
      ) : null}
    </section>
  )
}
