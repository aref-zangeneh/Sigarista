import { useEffect, useMemo, useRef, useState } from 'react'
import { useAdmin } from '../context/AdminContext'
import { PERMISSIONS, hasPermission } from '../utils/adminPermissions'

const DASHBOARD_FRAME_URL = `${import.meta.env.BASE_URL}cost-controls/index.html`

export function CostDashboardPage() {
  const { session } = useAdmin()
  const iframeRef = useRef(null)
  const readyRef = useRef(false)
  const [loaded, setLoaded] = useState(false)

  const canView = useMemo(
    () => Boolean(session && hasPermission(session, PERMISSIONS['cost-dashboard'].view)),
    [session],
  )

  useEffect(() => {
    if (!canView || !session?.token) return
    if (readyRef.current && iframeRef.current?.contentWindow) {
      iframeRef.current.contentWindow.postMessage(
        { type: 'costdash:init', token: session.token },
        window.location.origin,
      )
    }
  }, [canView, session?.token])

  useEffect(() => {
    if (!canView) return undefined

    const handleMessage = (event) => {
      if (event.origin !== window.location.origin) return
      const data = event.data ?? {}
      if (data.type === 'costdash:ready' && session?.token) {
        readyRef.current = true
        event.source.postMessage(
          { type: 'costdash:init', token: session.token },
          event.origin,
        )
      }
    }

    window.addEventListener('message', handleMessage)
    return () => window.removeEventListener('message', handleMessage)
  }, [canView, session?.token])

  if (!session || !canView) {
    return (
      <div className="panel costdash-notice">
        <p>برای مشاهده‌ی کاست کنترل دسترسی ندارید.</p>
      </div>
    )
  }

  return (
    <div className="costdash-wrap">
      {!loaded ? (
        <div className="costdash-loading">
          <span className="spinner" aria-hidden="true" />
          <p>در حال بارگذاری کاست کنترل...</p>
        </div>
      ) : null}
      <iframe
        ref={iframeRef}
        src={DASHBOARD_FRAME_URL}
        title="کاست کنترل و قیمت‌گذاری"
        className={loaded ? 'costdash-frame' : 'costdash-frame costdash-frame--hidden'}
        onLoad={() => setLoaded(true)}
      />
    </div>
  )
}
