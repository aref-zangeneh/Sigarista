import { useCallback, useEffect, useState } from 'react'
import { useAdmin } from '../context/AdminContext'
import { hasPermission } from '../utils/adminPermissions'
import { formatDateTime } from '../utils/adminFormatters'
import './PunchWidget.css'

/** Panel-mounted punch in/out quick bar — actual presence (حضور واقعی). */
export function PunchWidget() {
  const { session, apiRequest, getReadableError } = useAdmin()
  const token = session?.token
  const can = hasPermission(session, 'admin.hr.update')

  const [status, setStatus] = useState([])
  const [message, setMessage] = useState('')
  const [error, setError] = useState('')

  const load = useCallback(async () => {
    if (!token || !can) return
    try {
      const data = await apiRequest('/admin/hr/punches/status', { token })
      setStatus(Array.isArray(data) ? data : [])
    } catch (requestError) {
      setError(getReadableError(requestError))
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [token])

  useEffect(() => {
    load()
  }, [load])

  if (!token || !can) return null

  const punch = async (employeeId, direction) => {
    setError('')
    setMessage('')
    try {
      await apiRequest(`/admin/hr/punches/${direction}`, {
        method: 'POST',
        token,
        body: direction === 'in' ? { employeeId, note: null } : { employeeId },
      })
      setMessage(direction === 'in' ? 'ورود ثبت شد.' : 'خروج ثبت شد.')
      await load()
    } catch (requestError) {
      setError(getReadableError(requestError))
    }
  }

  if (status.length === 0) return null

  return (
    <section className="panel punch-widget">
      <div className="punch-widget-head">
        <h3>حضور و غیاب (Punch)</h3>
        {message ? <span className="punch-widget-message">{message}</span> : null}
        {error ? <span className="punch-widget-message punch-widget-error">{error}</span> : null}
      </div>
      <div className="punch-widget-body">
        {status.map((item) => (
          <div key={item.employeeId} className={`punch-chip punch-chip-${item.state.toLowerCase().replace('notpunched', 'none')}`}>
            <span className="punch-chip-name">{item.employeeName}</span>
            <span className="punch-chip-sub">
              {item.punchInAt ? `ورود ${formatDateTime(item.punchInAt)}` : 'امروز پانچی ندارد'}
            </span>
            <button
              type="button"
              className={`punch-button${item.state === 'In' ? ' punch-button-out' : ''}`}
              onClick={() => punch(item.employeeId, item.state === 'In' ? 'out' : 'in')}
            >
              {item.state === 'In' ? 'ثبت خروج' : 'ثبت ورود'}
            </button>
          </div>
        ))}
      </div>
    </section>
  )
}
