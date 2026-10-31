import { useCallback, useEffect, useMemo, useState } from 'react'
import { useAdmin } from '../context/AdminContext'
import { apiRequest, getReadableError } from '../utils/adminApi'
import { toPersianNumber } from '../utils/adminFormatters'
import { gregorianIsoToShamsiLabel } from '../utils/shamsiDate'
import { ShamsiDatePicker } from '../components/ShamsiDatePicker'
import { useEscapeKey } from '../hooks/useEscapeKey'
import './EquipmentMaintenancePage.css'

const CATEGORY_PRESETS = [
  'اسپرسوساز',
  'آسیاب و میکسر',
  'یخچال و فریزر',
  'تاسیسات (آب/برق/هوا)',
  'Laptop و نرم‌افزار',
  'سایر',
]

function emptyEquipmentForm() {
  return {
    id: null,
    persianName: '',
    category: CATEGORY_PRESETS[0],
    brandModel: '',
    serialNumber: '',
    location: '',
    purchaseDate: '',
    notes: '',
    isActive: true,
  }
}

function emptyScheduleForm(equipmentId) {
  const now = new Date()
  const nextWeek = new Date(now.getTime() + 7 * 24 * 60 * 60 * 1000)
  const iso = nextWeek.toISOString().slice(0, 10)
  return {
    equipmentId,
    id: null,
    title: '',
    intervalDays: 30,
    nextDueDate: iso,
    isActive: true,
    recipients: [{ channel: 'Sms', target: '', label: '' }],
  }
}

function emptyLogForm(equipmentId, scheduleId = '') {
  return {
    equipmentId,
    id: null,
    scheduleId,
    servicedOn: new Date().toISOString().slice(0, 10),
    cost: '',
    performedBy: '',
    description: '',
    closeCycle: true,
  }
}

function formatToman(value) {
  return `${Number(value || 0).toLocaleString('fa-IR')} تومان`
}

function dueLabel(schedule) {
  if (!schedule.isActive) return { text: 'غیرفعال', tone: 'off' }
  const days = schedule.daysRemaining ?? 0
  if (schedule.isOverdue) {
    return { text: `${toPersianNumber(Math.abs(days))} روز تأخیر`, tone: 'overdue' }
  }
  if (days <= 3) {
    return { text: 'نزدیک موعد', tone: 'soon' }
  }
  return { text: `${toPersianNumber(days)} روز مانده`, tone: 'ok' }
}

export function EquipmentMaintenancePage() {
  const { session, setMessage, setError } = useAdmin()
  const token = session?.token

  function has(permissionKey) {
    if (!session) return false
    if (session.isSuperAdmin) return true
    return (session.permissions ?? []).includes(permissionKey)
  }

  const canCreate = has('admin.maintenance.create')
  const canUpdate = has('admin.maintenance.update')
  const canDelete = has('admin.maintenance.delete')

  const [equipments, setEquipments] = useState([])
  const [loading, setLoading] = useState(true)
  const [includeInactive, setIncludeInactive] = useState(true)

  const [defaultRecipients, setDefaultRecipients] = useState([])
  const [defaultRecipientsOpen, setDefaultRecipientsOpen] = useState(false)
  const [defaultRecipientsForm, setDefaultRecipientsForm] = useState([])

  const [equipmentFormOpen, setEquipmentFormOpen] = useState(false)
  const [equipmentForm, setEquipmentForm] = useState(emptyEquipmentForm)
  const [scheduleFormOpen, setScheduleFormOpen] = useState(false)
  const [scheduleForm, setScheduleForm] = useState(emptyScheduleForm(''))
  const [logFormOpen, setLogFormOpen] = useState(false)
  const [logForm, setLogForm] = useState(emptyLogForm(''))
  const [detailEquipmentId, setDetailEquipmentId] = useState(null)
  const [detailLogs, setDetailLogs] = useState([])
  const [detailLogsLoading, setDetailLogsLoading] = useState(false)
  const [sendingTestFor, setSendingTestFor] = useState(null)
  const [savingGlobal, setSavingGlobal] = useState(false)

  useEscapeKey(() => {
    setEquipmentFormOpen(false)
    setScheduleFormOpen(false)
    setLogFormOpen(false)
    setDefaultRecipientsOpen(false)
    setDetailEquipmentId(null)
  }, Boolean(equipmentFormOpen || scheduleFormOpen || logFormOpen || defaultRecipientsOpen || detailEquipmentId))

  const loadEquipments = useCallback(async () => {
    if (!token) return
    setLoading(true)
    try {
      const data = await apiRequest(
        `/admin/maintenance/equipments?includeInactive=${includeInactive}`,
        { token },
      )
      setEquipments(Array.isArray(data) ? data : [])
    } catch (loadError) {
      setError(getReadableError(loadError))
    } finally {
      setLoading(false)
    }
  }, [includeInactive, setError, token])

  useEffect(() => {
    loadEquipments()
  }, [loadEquipments])

  const loadDefaultRecipients = useCallback(async () => {
    if (!token) return
    try {
      const data = await apiRequest('/admin/maintenance/default-recipients', { token })
      setDefaultRecipients(Array.isArray(data) ? data : [])
    } catch (loadError) {
      setError(getReadableError(loadError))
    }
  }, [setError, token])

  useEffect(() => {
    loadDefaultRecipients()
  }, [loadDefaultRecipients])

  const loadDetailLogs = useCallback(async (equipmentId) => {
    if (!token || !equipmentId) return
    setDetailLogsLoading(true)
    try {
      const data = await apiRequest(
        `/admin/maintenance/service-logs?equipmentId=${equipmentId}`,
        { token },
      )
      setDetailLogs(Array.isArray(data) ? data : [])
    } catch (loadError) {
      setError(getReadableError(loadError))
    } finally {
      setDetailLogsLoading(false)
    }
  }, [setError, token])

  const refreshDetail = useCallback(async (equipmentId) => {
    await Promise.all([loadEquipments(), loadDetailLogs(equipmentId)])
  }, [loadDetailLogs, loadEquipments])

  const summary = useMemo(
    () => computeSummary(equipments),
    [equipments],
  )

  /* ── equipment CRUD ── */
  const openEquipmentForm = (equipment = null) => {
    setEquipmentForm(equipment
      ? {
          id: equipment.id,
          persianName: equipment.persianName ?? '',
          category: equipment.category ?? CATEGORY_PRESETS[0],
          brandModel: equipment.brandModel ?? '',
          serialNumber: equipment.serialNumber ?? '',
          location: equipment.location ?? '',
          purchaseDate: equipment.purchaseDate ?? '',
          notes: equipment.notes ?? '',
          isActive: equipment.isActive,
        }
      : emptyEquipmentForm())
    setEquipmentFormOpen(true)
  }

  const submitEquipment = async (event) => {
    event.preventDefault()
    setSavingGlobal(true)
    try {
      const body = {
        persianName: equipmentForm.persianName.trim(),
        category: equipmentForm.category.trim() || null,
        brandModel: equipmentForm.brandModel.trim() || null,
        serialNumber: equipmentForm.serialNumber.trim() || null,
        location: equipmentForm.location.trim() || null,
        purchaseDate: equipmentForm.purchaseDate || null,
        notes: equipmentForm.notes.trim() || null,
        isActive: equipmentForm.isActive,
      }
      if (equipmentForm.id) {
        await apiRequest(`/admin/maintenance/equipments/${equipmentForm.id}`, {
          method: 'PUT', token, body,
        })
        setMessage('تجهیز به‌روزرسانی شد.')
      } else {
        await apiRequest('/admin/maintenance/equipments', { method: 'POST', token, body })
        setMessage('تجهیز ثبت شد.')
      }
      setEquipmentFormOpen(false)
      await loadEquipments()
    } catch (saveError) {
      setError(getReadableError(saveError))
    } finally {
      setSavingGlobal(false)
    }
  }

  const deleteEquipment = async (equipment) => {
    if (!window.confirm(`«${equipment.persianName}» و تمام زمان‌بندی‌ها و سوابق آن حذف شود؟`)) return
    try {
      await apiRequest(`/admin/maintenance/equipments/${equipment.id}`, { method: 'DELETE', token })
      setMessage('تجهیز حذف شد.')
      if (detailEquipmentId === equipment.id) setDetailEquipmentId(null)
      await loadEquipments()
    } catch (deleteError) {
      setError(getReadableError(deleteError))
    }
  }

  /* ── default (fallback) recipients — the site admin's mobiles/emails ── */
  const openDefaultRecipients = () => {
    setDefaultRecipientsForm(
      defaultRecipients.length
        ? defaultRecipients.map((recipient) => ({
            channel: recipient.channel,
            target: recipient.target,
            label: recipient.label ?? '',
          }))
        : [{ channel: 'Email', target: '', label: 'مدیر سایت' }],
    )
    setDefaultRecipientsOpen(true)
  }

  const setDefaultRecipientRow = (index, patch) => {
    setDefaultRecipientsForm((current) => current.map((row, rowIndex) => (
      rowIndex === index ? { ...row, ...patch } : row
    )))
  }

  const addDefaultRecipientRow = () => {
    setDefaultRecipientsForm((current) => [
      ...current,
      { channel: 'Sms', target: '', label: '' },
    ])
  }

  const removeDefaultRecipientRow = (index) => {
    setDefaultRecipientsForm((current) => {
      const next = current.filter((_, rowIndex) => rowIndex !== index)
      return next.length ? next : [{ channel: 'Email', target: '', label: '' }]
    })
  }

  const submitDefaultRecipients = async (event) => {
    event.preventDefault()
    setSavingGlobal(true)
    try {
      const body = {
        recipients: defaultRecipientsForm
          .filter((row) => row.target.trim())
          .map((row) => ({
            channel: row.channel,
            target: row.target.trim(),
            label: row.label.trim() || null,
          })),
      }
      const saved = await apiRequest('/admin/maintenance/default-recipients', {
        method: 'PUT', token, body,
      })
      setDefaultRecipients(Array.isArray(saved) ? saved : [])
      setMessage('دریافت‌کننده‌های پیش‌فرض یادآور ذخیره شد.')
      setDefaultRecipientsOpen(false)
    } catch (saveError) {
      setError(getReadableError(saveError))
    } finally {
      setSavingGlobal(false)
    }
  }

  /* ── schedules ── */
  const openScheduleForm = (equipment, schedule = null) => {
    setScheduleForm(schedule
      ? {
          equipmentId: equipment.id,
          id: schedule.id,
          title: schedule.title,
          intervalDays: schedule.intervalDays,
          nextDueDate: schedule.nextDueDate,
          isActive: schedule.isActive,
          recipients: schedule.recipients.length
            ? schedule.recipients.map((recipient) => ({
                channel: recipient.channel,
                target: recipient.target,
                label: recipient.label ?? '',
              }))
            : [{ channel: 'Sms', target: '', label: '' }],
        }
      : emptyScheduleForm(equipment.id))
    setScheduleFormOpen(true)
  }

  const setRecipientRow = (index, patch) => {
    setScheduleForm((current) => ({
      ...current,
      recipients: current.recipients.map((row, rowIndex) => (
        rowIndex === index ? { ...row, ...patch } : row
      )),
    }))
  }

  const addRecipientRow = () => {
    setScheduleForm((current) => ({
      ...current,
      recipients: [...current.recipients, { channel: 'Email', target: '', label: '' }],
    }))
  }

  const removeRecipientRow = (index) => {
    setScheduleForm((current) => {
      const next = current.recipients.filter((_, rowIndex) => rowIndex !== index)
      return { ...current, recipients: next.length ? next : [{ channel: 'Sms', target: '', label: '' }] }
    })
  }

  const submitSchedule = async (event) => {
    event.preventDefault()
    setSavingGlobal(true)
    try {
      const body = {
        equipmentId: scheduleForm.equipmentId,
        id: scheduleForm.id,
        title: scheduleForm.title.trim(),
        intervalDays: Number(scheduleForm.intervalDays) || 0,
        nextDueDate: scheduleForm.nextDueDate,
        isActive: scheduleForm.isActive,
        recipients: scheduleForm.recipients
          .filter((row) => row.target.trim())
          .map((row) => ({ channel: row.channel, target: row.target.trim(), label: row.label.trim() || null })),
      }
      const path = scheduleForm.id
        ? `/admin/maintenance/equipments/${scheduleForm.equipmentId}/schedules/${scheduleForm.id}`
        : `/admin/maintenance/equipments/${scheduleForm.equipmentId}/schedules`
      await apiRequest(path, { method: scheduleForm.id ? 'PUT' : 'POST', token, body })
      setMessage(scheduleForm.id ? 'زمان‌بندی به‌روزرسانی شد.' : 'زمان‌بندی سرویس ثبت شد.')
      setScheduleFormOpen(false)
      await refreshDetail(scheduleForm.equipmentId)
    } catch (saveError) {
      setError(getReadableError(saveError))
    } finally {
      setSavingGlobal(false)
    }
  }

  const deleteSchedule = async (equipmentId, schedule) => {
    if (!window.confirm(`زمان‌بندی «${schedule.title}» و دریافت‌کننده‌های آن حذف شود؟`)) return
    try {
      await apiRequest(
        `/admin/maintenance/equipments/${equipmentId}/schedules/${schedule.id}`,
        { method: 'DELETE', token },
      )
      setMessage('زمان‌بندی حذف شد.')
      await refreshDetail(equipmentId)
    } catch (deleteError) {
      setError(getReadableError(deleteError))
    }
  }

  const sendTestReminder = async (schedule) => {
    setSendingTestFor(schedule.id)
    try {
      const result = await apiRequest(
        `/admin/maintenance/schedules/${schedule.id}/test-reminder`,
        { method: 'POST', token },
      )
      const delivered = result?.delivered ?? 0
      const failures = result?.failures ?? []
      const source = result?.usedDefaultRecipients ? ' (از لیست پیش‌فرض مدیر سایت)' : ''
      if (failures.length) {
        setError(`پیام آزمایشی${source}: ${toPersianNumber(delivered)} موفق؛ ناموفق: ${failures.join('، ')}`)
      } else {
        setMessage(`پیام آزمایشی${source} به ${toPersianNumber(delivered)} دریافت‌کننده ارسال شد.`)
      }
    } catch (testError) {
      setError(getReadableError(testError))
    } finally {
      setSendingTestFor(null)
    }
  }

  /* ── service logs ── */
  const openLogForm = (equipmentId, scheduleId = '') => {
    setLogForm(emptyLogForm(equipmentId, scheduleId))
    setLogFormOpen(true)
  }

  const openDetail = (equipment) => {
    setDetailEquipmentId(equipment.id)
    loadDetailLogs(equipment.id)
  }

  const detailEquipment = equipments.find((equipment) => equipment.id === detailEquipmentId) ?? null

  const submitLog = async (event) => {
    event.preventDefault()
    setSavingGlobal(true)
    try {
      const body = {
        equipmentId: logForm.equipmentId,
        scheduleId: logForm.scheduleId || null,
        servicedOn: logForm.servicedOn,
        cost: Number(logForm.cost) || 0,
        performedBy: logForm.performedBy.trim() || null,
        description: logForm.description.trim() || null,
        closeScheduleCycle: Boolean(logForm.closeCycle),
      }
      await apiRequest(
        `/admin/maintenance/equipments/${logForm.equipmentId}/service-logs`,
        { method: 'POST', token, body },
      )
      setMessage('سابقه سرویس ثبت شد.')
      setLogFormOpen(false)
      await refreshDetail(logForm.equipmentId)
    } catch (saveError) {
      setError(getReadableError(saveError))
    } finally {
      setSavingGlobal(false)
    }
  }

  const deleteLog = async (log) => {
    if (!window.confirm(`سابقه سرویس تاریخ ${gregorianIsoToShamsiLabel(log.servicedOn)} حذف شود؟`)) return
    try {
      await apiRequest(
        `/admin/maintenance/equipments/${log.equipmentId}/service-logs/${log.id}`,
        { method: 'DELETE', token },
      )
      setMessage('سابقه سرویس حذف شد.')
      await refreshDetail(log.equipmentId)
    } catch (deleteError) {
      setError(getReadableError(deleteError))
    }
  }

  const scheduleOptions = detailEquipment?.schedules ?? []

  return (
    <section className="panel equipment-maintenance-page">
      <div className="panel-head">
        <div>
          <h3>سرویس دوره‌ای تجهیزات</h3>
          <p>ثبت تجهیزاتی که به سرویس دوره‌ای نیاز دارند، زمان‌بندی یادآور (پیامک/ایمیل)، سوابق و هزینه‌های سرویس و تعمیر.</p>
        </div>
        {canCreate ? (
          <button type="button" className="primary-button" onClick={() => openEquipmentForm()}>
            تجهیز جدید
          </button>
        ) : null}
      </div>

      <div className="mnx-cards">
        <div className="mnx-card">
          <span>تجهیزات فعال</span>
          <strong>{toPersianNumber(summary.activeEquipments)}</strong>
        </div>
        <div className="mnx-card">
          <span>زمان‌بندی‌های فعال</span>
          <strong>{toPersianNumber(summary.activeSchedules)}</strong>
        </div>
        <div className="mnx-card mnx-card-alert">
          <span>سرویس سررسیده / تأخیر</span>
          <strong>{toPersianNumber(summary.overdue)}</strong>
        </div>
        <div className="mnx-card">
          <span>هزینه کل سرویس و تعمیرات</span>
          <strong>{formatToman(summary.totalCost)}</strong>
        </div>
      </div>

      <div className="list-toolbar-row">
        <label className="checkbox-row">
          <input
            type="checkbox"
            checked={includeInactive}
            onChange={(event) => setIncludeInactive(event.target.checked)}
          />
          <span>نمایش از رده خارج‌ها</span>
        </label>
        <div className="mnx-toolbar-actions">
          <span className="mnx-default-hint">
            {defaultRecipients.length
              ? `${toPersianNumber(defaultRecipients.length)} دریافت‌کننده پیش‌فرض (مدیر سایت)`
              : 'دریافت‌کننده پیش‌فرض ثبت نشده'}
          </span>
          {canUpdate ? (
            <button type="button" className="secondary-button" onClick={openDefaultRecipients}>
              دریافت‌کننده‌های پیش‌فرض
            </button>
          ) : null}
        </div>
      </div>

      {loading ? (
        <p className="feedback">در حال بارگذاری تجهیزات...</p>
      ) : (
        <table>
          <thead>
            <tr>
              <th>تجهیز</th>
              <th>شماره سریال</th>
              <th>زمان‌بندی‌ها</th>
              <th>نزدیک‌ترین سرویس</th>
              <th>هزینه تجمعی</th>
              <th>وضعیت</th>
              <th>عملیات</th>
            </tr>
          </thead>
          <tbody>
            {equipments.map((equipment) => {
              const due = nextDueSchedule(equipment)
              return (
                <tr key={equipment.id} className={equipment.isActive ? '' : 'mnx-row-off'}>
                  <td>
                    <strong>{equipment.persianName}</strong>
                    {equipment.category ? <div className="table-subtext">دسته: {equipment.category}</div> : null}
                    {equipment.brandModel ? <div className="table-subtext ltr-text">{equipment.brandModel}</div> : null}
                  </td>
                  <td className="ltr-text">{equipment.serialNumber || '—'}</td>
                  <td>{toPersianNumber(equipment.schedules?.length ?? 0)}</td>
                  <td>
                    {due ? (
                      <>
                        <span className={`mnx-due mnx-due--${dueLabel(due).tone}`}>{dueLabel(due).text}</span>
                        <div className="table-subtext">{gregorianIsoToShamsiLabel(due.nextDueDate)} — {due.title}</div>
                      </>
                    ) : '—'}
                  </td>
                  <td>{formatToman(equipment.totalServiceCost)}</td>
                  <td>{equipment.isActive ? 'فعال' : 'از رده خارج'}</td>
                  <td>
                    <div className="table-actions">
                      <button type="button" className="link-button" onClick={() => openDetail(equipment)}>
                        جزئیات
                      </button>
                      {canUpdate ? (
                        <button type="button" className="link-button" onClick={() => openEquipmentForm(equipment)}>
                          ویرایش
                        </button>
                      ) : null}
                      {canDelete ? (
                        <button type="button" className="link-button link-danger" onClick={() => deleteEquipment(equipment)}>
                          حذف
                        </button>
                      ) : null}
                    </div>
                  </td>
                </tr>
              )
            })}
            {equipments.length === 0 ? (
              <tr>
                <td colSpan={7}>هنوز تجهیزی برای سرویس دوره‌ای ثبت نشده است.</td>
              </tr>
            ) : null}
          </tbody>
        </table>
      )}

      {/* ── equipment detail (schedules + recipients + logs) ── */}
      {detailEquipment ? (
        <div
          className="modal-backdrop"
          onClick={(event) => { if (event.target === event.currentTarget) setDetailEquipmentId(null) }}
        >
          <div className="modal mnx-detail-modal" role="dialog" aria-modal="true" aria-labelledby="mnx-detail-title">
            <div className="panel-head">
              <div>
                <h3 id="mnx-detail-title">{detailEquipment.persianName}</h3>
                <p>
                  {detailEquipment.category ? `${detailEquipment.category} • ` : ''}
                  {detailEquipment.serialNumber ? `سریال ${detailEquipment.serialNumber} • ` : ''}
                  {detailEquipment.location ?? ''}
                </p>
              </div>
              <button type="button" className="secondary-button" onClick={() => setDetailEquipmentId(null)}>
                بستن
              </button>
            </div>

            <div className="mnx-section-head">
              <h4>زمان‌بندی‌های سرویس دوره‌ای</h4>
              {canCreate ? (
                <button type="button" className="link-button" onClick={() => openScheduleForm(detailEquipment)}>
                  + زمان‌بندی جدید
                </button>
              ) : null}
            </div>

            {detailEquipment.schedules.length === 0 ? (
              <p className="feedback">برای این تجهیز هنوز زمان‌بندی‌ای ثبت نشده است.</p>
            ) : (
              <div className="mnx-schedule-list">
                {detailEquipment.schedules.map((schedule) => {
                  const due = dueLabel(schedule)
                  return (
                    <div key={schedule.id} className={`mnx-schedule${schedule.isActive ? '' : ' mnx-schedule--off'}`}>
                      <div className="mnx-schedule-head">
                        <strong>{schedule.title}</strong>
                        <span className={`mnx-due mnx-due--${due.tone}`}>{due.text}</span>
                      </div>
                      <div className="mnx-schedule-meta">
                        <span>هر {toPersianNumber(schedule.intervalDays)} روز یک‌بار</span>
                        <span>سررسید: {gregorianIsoToShamsiLabel(schedule.nextDueDate)}</span>
                        {schedule.lastCompletedOn ? (
                          <span>آخرین سرویس: {gregorianIsoToShamsiLabel(schedule.lastCompletedOn)}</span>
                        ) : null}
                      </div>
                      <div className="mnx-recipients">
                        {schedule.recipients.length === 0 ? (
                          <span className="mnx-recipient-empty">
                            {defaultRecipients.length
                              ? 'دریافت‌کننده اختصاصی ندارد؛ یادآوری به دریافت‌کننده‌های پیش‌فرض (مدیر سایت) ارسال می‌شود.'
                              : 'دریافت‌کننده‌ای ثبت نشده — یادآوری ارسال نمی‌شود.'}
                          </span>
                        ) : schedule.recipients.map((recipient) => (
                          <span key={recipient.id} className={`mnx-recipient mnx-recipient--${recipient.channel.toLowerCase()}`}>
                            <b>{recipient.channel === 'Sms' ? 'پیامک' : 'ایمیل'}</b>
                            <span className="ltr-text">{recipient.target}</span>
                            {recipient.label ? <em>{recipient.label}</em> : null}
                          </span>
                        ))}
                      </div>
                      <div className="table-actions">
                        {canUpdate ? (
                          <>
                            <button type="button" className="link-button" onClick={() => openScheduleForm(detailEquipment, schedule)}>
                              ویرایش
                            </button>
                            <button
                              type="button"
                              className="link-button"
                              disabled={sendingTestFor === schedule.id}
                              onClick={() => sendTestReminder(schedule)}
                            >
                              {sendingTestFor === schedule.id ? 'در حال ارسال...' : 'ارسال آزمایشی'}
                            </button>
                            <button type="button" className="link-button" onClick={() => openLogForm(detailEquipment.id, schedule.id)}>
                              ثبت سرویس انجام‌شده
                            </button>
                          </>
                        ) : null}
                        {canDelete ? (
                          <button type="button" className="link-button link-danger" onClick={() => deleteSchedule(detailEquipment.id, schedule)}>
                            حذف
                          </button>
                        ) : null}
                      </div>
                    </div>
                  )
                })}
              </div>
            )}

            <div className="mnx-section-head">
              <h4>سوابق سرویس و تعمیرات</h4>
              {canUpdate ? (
                <button type="button" className="link-button" onClick={() => openLogForm(detailEquipment.id)}>
                  + ثبت سرویس / تعمیر
                </button>
              ) : null}
            </div>

            {detailLogsLoading ? (
              <p className="feedback">در حال بارگذاری سوابق...</p>
            ) : (
              <table className="mnx-logs-table">
                <thead>
                  <tr>
                    <th>تاریخ</th>
                    <th>زمان‌بندی</th>
                    <th>اقدام</th>
                    <th>هزینه</th>
                    <th>عملیات</th>
                  </tr>
                </thead>
                <tbody>
                  {detailLogs.map((log) => (
                    <tr key={log.id}>
                      <td>{gregorianIsoToShamsiLabel(log.servicedOn)}</td>
                      <td>{log.scheduleTitle ?? '— (مستقل)'}</td>
                      <td>
                        {log.description || '—'}
                        {log.performedBy ? <div className="table-subtext">توسط: {log.performedBy}</div> : null}
                      </td>
                      <td>{formatToman(log.cost)}</td>
                      <td>
                        {canDelete ? (
                          <button type="button" className="link-button link-danger" onClick={() => deleteLog(log)}>
                            حذف
                          </button>
                        ) : null}
                      </td>
                    </tr>
                  ))}
                  {detailLogs.length === 0 ? (
                    <tr>
                      <td colSpan={5}>سابقه‌ای ثبت نشده است.</td>
                    </tr>
                  ) : null}
                </tbody>
              </table>
            )}
          </div>
        </div>
      ) : null}

      {/* ── equipment form ── */}
      {equipmentFormOpen ? (
        <div className="modal-backdrop" onClick={(event) => { if (event.target === event.currentTarget) setEquipmentFormOpen(false) }}>
          <div className="modal" role="dialog" aria-modal="true" aria-labelledby="mnx-equipment-title">
            <div className="panel-head">
              <div>
                <h3 id="mnx-equipment-title">{equipmentForm.id ? 'ویرایش تجهیز' : 'ثبت تجهیز جدید'}</h3>
                <p>جزئیات دقیق تجهیز برای پیگیری سرویس.</p>
              </div>
            </div>
            <form onSubmit={submitEquipment} className="field-grid">
              <label>
                <span>نام تجهیز *</span>
                <input
                  value={equipmentForm.persianName}
                  onChange={(event) => setEquipmentForm((current) => ({ ...current, persianName: event.target.value }))}
                  required
                />
              </label>
              <label>
                <span>دستهٔ تجهیز</span>
                <input
                  list="mnx-categories"
                  value={equipmentForm.category}
                  onChange={(event) => setEquipmentForm((current) => ({ ...current, category: event.target.value }))}
                />
                <datalist id="mnx-categories">
                  {CATEGORY_PRESETS.map((category) => (
                    <option key={category} value={category} />
                  ))}
                </datalist>
              </label>
              <label>
                <span>برند / مدل</span>
                <input
                  className="ltr-input"
                  value={equipmentForm.brandModel}
                  onChange={(event) => setEquipmentForm((current) => ({ ...current, brandModel: event.target.value }))}
                />
              </label>
              <label>
                <span>شماره سریال / شناسه</span>
                <input
                  className="ltr-input"
                  value={equipmentForm.serialNumber}
                  onChange={(event) => setEquipmentForm((current) => ({ ...current, serialNumber: event.target.value }))}
                />
              </label>
              <label>
                <span>محل استقرار</span>
                <input
                  value={equipmentForm.location}
                  onChange={(event) => setEquipmentForm((current) => ({ ...current, location: event.target.value }))}
                />
              </label>
              <label>
                <span>تاریخ خرید</span>
                <ShamsiDatePicker
                  value={equipmentForm.purchaseDate ?? ''}
                  onChange={(purchaseDate) => setEquipmentForm((current) => ({ ...current, purchaseDate }))}
                />
              </label>
              <label className="mnx-full">
                <span>توضیحات</span>
                <textarea
                  rows={2}
                  value={equipmentForm.notes}
                  onChange={(event) => setEquipmentForm((current) => ({ ...current, notes: event.target.value }))}
                />
              </label>
              <label className="checkbox-row">
                <input
                  type="checkbox"
                  checked={equipmentForm.isActive}
                  onChange={(event) => setEquipmentForm((current) => ({ ...current, isActive: event.target.checked }))}
                />
                <span>فعال (یادآوری طبق برنامه ارسال شود)</span>
              </label>
              <div className="modal-actions mnx-full">
                <button type="button" className="secondary-button" onClick={() => setEquipmentFormOpen(false)}>
                  انصراف
                </button>
                <button type="submit" className="primary-button" disabled={savingGlobal}>ذخیره</button>
              </div>
            </form>
          </div>
        </div>
      ) : null}

      {/* ── schedule form ── */}
      {scheduleFormOpen ? (
        <div className="modal-backdrop" onClick={(event) => { if (event.target === event.currentTarget) setScheduleFormOpen(false) }}>
          <div className="modal mnx-schedule-modal" role="dialog" aria-modal="true" aria-labelledby="mnx-schedule-title">
            <div className="panel-head">
              <div>
                <h3 id="mnx-schedule-title">{scheduleForm.id ? 'ویرایش زمان‌بندی سرویس' : 'زمان‌بندی سرویس جدید'}</h3>
                <p>یادآور در سررسید (طبق تقویم تهران) به همهٔ دریافت‌کننده‌ها ارسال می‌شود و چرخه به دورهٔ بعد می‌رود.</p>
              </div>
            </div>
            <form onSubmit={submitSchedule} className="field-grid">
              <label>
                <span>عنوان کار سرویس *</span>
                <input
                  value={scheduleForm.title}
                  onChange={(event) => setScheduleForm((current) => ({ ...current, title: event.target.value }))}
                  required
                  placeholder="مثلا: روغن‌کاری دف میکسر"
                />
              </label>
              <label>
                <span>فاصله دورهٔ سرویس (روز) *</span>
                <input
                  className="ltr-input"
                  type="number"
                  min="1"
                  max="3650"
                  value={scheduleForm.intervalDays}
                  onChange={(event) => setScheduleForm((current) => ({ ...current, intervalDays: event.target.value }))}
                  required
                />
              </label>
              <label>
                <span>سررسید بعدی *</span>
                <ShamsiDatePicker
                  value={scheduleForm.nextDueDate ?? ''}
                  onChange={(nextDueDate) => setScheduleForm((current) => ({ ...current, nextDueDate }))}
                />
              </label>
              <label className="checkbox-row">
                <input
                  type="checkbox"
                  checked={scheduleForm.isActive}
                  onChange={(event) => setScheduleForm((current) => ({ ...current, isActive: event.target.checked }))}
                />
                <span>زمان‌بندی فعال</span>
              </label>

              <div className="mnx-full">
                <div className="mnx-section-head">
                  <h4>دریافت‌کننده‌های یادآور</h4>
                  <button type="button" className="link-button" onClick={addRecipientRow}>
                    + افزودن دریافت‌کننده
                  </button>
                </div>
                <p className="table-subtext">
                  هر تعداد ایمیل و شماره موبایل که بخواهید، با برچسب دلخواه (مثلا «مدیر عملیات»).
                  اگر خالی بماند، یادآوری به دریافت‌کننده‌های پیش‌فرض (مدیر سایت) ارسال می‌شود.
                </p>
                {scheduleForm.recipients.map((row, index) => (
                  <div key={index} className="mnx-recipient-row">
                    <select
                      value={row.channel}
                      onChange={(event) => setRecipientRow(index, { channel: event.target.value })}
                    >
                      <option value="Sms">پیامک</option>
                      <option value="Email">ایمیل</option>
                    </select>
                    <input
                      className="ltr-input"
                      type={row.channel === 'Email' ? 'email' : 'tel'}
                      dir={row.channel === 'Email' ? 'ltr' : 'ltr'}
                      value={row.target}
                      onChange={(event) => setRecipientRow(index, { target: event.target.value })}
                      placeholder={row.channel === 'Email' ? 'name@example.com' : '0912xxxxxxx'}
                    />
                    <input
                      value={row.label}
                      onChange={(event) => setRecipientRow(index, { label: event.target.value })}
                      placeholder="برچسب (اختیاری)"
                    />
                    <button
                      type="button"
                      className="link-button link-danger"
                      onClick={() => removeRecipientRow(index)}
                      aria-label="حذف دریافت‌کننده"
                    >
                      حذف
                    </button>
                  </div>
                ))}
              </div>

              <div className="modal-actions mnx-full">
                <button type="button" className="secondary-button" onClick={() => setScheduleFormOpen(false)}>
                  انصراف
                </button>
                <button
                  type="submit"
                  className="primary-button"
                  disabled={savingGlobal}
                >
                  {scheduleForm.id ? 'ذخیره زمان‌بندی' : 'ثبت زمان‌بندی'}
                </button>
              </div>
            </form>
          </div>
        </div>
      ) : null}

      {/* ── service log form ── */}
      {logFormOpen ? (
        <div className="modal-backdrop" onClick={(event) => { if (event.target === event.currentTarget) setLogFormOpen(false) }}>
          <div className="modal" role="dialog" aria-modal="true" aria-labelledby="mnx-log-title">
            <div className="panel-head">
              <div>
                <h3 id="mnx-log-title">ثبت سرویس / تعمیر انجام‌شده</h3>
                <p>اگر به یک زمان‌بندی وصل شود، سررسید بعدی به‌طور خودکار جلو می‌رود.</p>
              </div>
            </div>
            <form onSubmit={submitLog} className="field-grid">
              <label>
                <span>تاریخ انجام *</span>
                <ShamsiDatePicker
                  value={logForm.servicedOn ?? ''}
                  onChange={(servicedOn) => setLogForm((current) => ({ ...current, servicedOn }))}
                />
              </label>
              <label>
                <span>زمان‌بندی مرتبط</span>
                <select
                  value={logForm.scheduleId}
                  onChange={(event) => setLogForm((current) => ({ ...current, scheduleId: event.target.value }))}
                >
                  <option value="">— بدون اتصال (تعمیر مستقل) —</option>
                  {scheduleOptions.map((schedule) => (
                    <option key={schedule.id} value={schedule.id}>{schedule.title}</option>
                  ))}
                </select>
              </label>
              <label>
                <span>هزینه (تومان)</span>
                <input
                  className="ltr-input"
                  type="number"
                  min="0"
                  value={logForm.cost}
                  onChange={(event) => setLogForm((current) => ({ ...current, cost: event.target.value }))}
                />
              </label>
              <label>
                <span>سرویس‌کار / تعمیرکار</span>
                <input
                  value={logForm.performedBy}
                  onChange={(event) => setLogForm((current) => ({ ...current, performedBy: event.target.value }))}
                />
              </label>
              <label className="mnx-full">
                <span>شرح اقدام</span>
                <textarea
                  rows={2}
                  value={logForm.description}
                  onChange={(event) => setLogForm((current) => ({ ...current, description: event.target.value }))}
                />
              </label>
              {logForm.scheduleId ? (
                <label className="checkbox-row">
                  <input
                    type="checkbox"
                    checked={logForm.closeCycle}
                    onChange={(event) => setLogForm((current) => ({ ...current, closeCycle: event.target.checked }))}
                  />
                  <span>این سرویس چرخهٔ زمان‌بندی را ببندد (سررسید بعدی جلو برود)</span>
                </label>
              ) : null}
              <div className="modal-actions mnx-full">
                <button type="button" className="secondary-button" onClick={() => setLogFormOpen(false)}>
                  انصراف
                </button>
                <button type="submit" className="primary-button" disabled={savingGlobal}>ثبت سابقه</button>
              </div>
            </form>
          </div>
        </div>
      ) : null}

      {/* ── default (fallback) recipients — site admin ── */}
      {defaultRecipientsOpen ? (
        <div className="modal-backdrop" onClick={(event) => { if (event.target === event.currentTarget) setDefaultRecipientsOpen(false) }}>
          <div className="modal mnx-schedule-modal" role="dialog" aria-modal="true" aria-labelledby="mnx-default-title">
            <div className="panel-head">
              <div>
                <h3 id="mnx-default-title">دریافت‌کننده‌های پیش‌فرض یادآور</h3>
                <p>
                  پیامک/ایمیل مدیر سایت. هر زمان‌بندی که دریافت‌کننده اختصاصی نداشته باشد،
                  یادآور را به این لیست می‌فرستد. هر تعداد ایمیل و شماره موبایل مجاز است.
                </p>
              </div>
            </div>
            <form onSubmit={submitDefaultRecipients} className="field-grid">
              <div className="mnx-full">
                <div className="mnx-section-head">
                  <h4>لیست دریافت‌کننده‌ها</h4>
                  <button type="button" className="link-button" onClick={addDefaultRecipientRow}>
                    + افزودن دریافت‌کننده
                  </button>
                </div>
                {defaultRecipientsForm.map((row, index) => (
                  <div key={index} className="mnx-recipient-row">
                    <select
                      value={row.channel}
                      onChange={(event) => setDefaultRecipientRow(index, { channel: event.target.value })}
                    >
                      <option value="Sms">پیامک</option>
                      <option value="Email">ایمیل</option>
                    </select>
                    <input
                      className="ltr-input"
                      type={row.channel === 'Email' ? 'email' : 'tel'}
                      dir="ltr"
                      value={row.target}
                      onChange={(event) => setDefaultRecipientRow(index, { target: event.target.value })}
                      placeholder={row.channel === 'Email' ? 'name@example.com' : '0912xxxxxxx'}
                    />
                    <input
                      value={row.label}
                      onChange={(event) => setDefaultRecipientRow(index, { label: event.target.value })}
                      placeholder="برچسب (اختیاری)"
                    />
                    <button
                      type="button"
                      className="link-button link-danger"
                      onClick={() => removeDefaultRecipientRow(index)}
                      aria-label="حذف دریافت‌کننده"
                    >
                      حذف
                    </button>
                  </div>
                ))}
                {defaultRecipientsForm.length === 0 ? (
                  <p className="table-subtext">لیست خالی است؛ با «افزودن دریافت‌کننده» شروع کنید.</p>
                ) : null}
              </div>
              <div className="modal-actions mnx-full">
                <button type="button" className="secondary-button" onClick={() => setDefaultRecipientsOpen(false)}>
                  انصراف
                </button>
                <button type="submit" className="primary-button" disabled={savingGlobal}>ذخیره</button>
              </div>
            </form>
          </div>
        </div>
      ) : null}
    </section>
  )
}

/* ── small helpers ── */

function computeSummary(equipments) {
  const activeEquipments = equipments.filter((equipment) => equipment.isActive).length
  const activeSchedules = equipments.reduce(
    (count, equipment) => count + (equipment.schedules ?? []).filter((schedule) => schedule.isActive).length,
    0,
  )
  const overdue = equipments.reduce(
    (count, equipment) => count + (equipment.schedules ?? []).filter((schedule) => schedule.isOverdue).length,
    0,
  )
  const totalCost = equipments.reduce(
    (sum, equipment) => sum + Number(equipment.totalServiceCost ?? 0),
    0,
  )
  return { activeEquipments, activeSchedules, overdue, totalCost }
}

function nextDueSchedule(equipment) {
  const schedules = (equipment.schedules ?? []).filter((schedule) => schedule.isActive)
  if (!schedules.length) return null
  return schedules.reduce((earliest, schedule) => (
    schedule.nextDueDate < earliest.nextDueDate ? schedule : earliest
  ))
}
