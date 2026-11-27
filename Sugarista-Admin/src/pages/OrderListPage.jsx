import { useCallback, useEffect, useMemo, useState } from 'react'
import { useAdmin } from '../context/AdminContext'
import { hasPermission, PERMISSIONS } from '../utils/adminPermissions'
import {
  ORDER_LIST_UNITS,
  buildWhatsAppOrderMessage,
  buildWhatsAppUrl,
  formatQuantity,
  normalizeWhatsAppPhone,
  openOrderListPdfWindow,
  toPersianDigits,
} from '../utils/orderListHelpers'
import './MenuSectionMinimal.css'
import './OrderListPage.css'

const STATUS_TABS = [
  { key: 'Pending', label: 'در انتظار سفارش' },
  { key: 'Ordered', label: 'سفارش داده‌شده' },
  { key: 'Received', label: 'تحویل‌گرفته‌شده' },
  { key: 'All', label: 'همه' },
]

export function OrderListPage() {
  const {
    session,
    apiRequest,
    getReadableError,
    setMessage,
    setError,
    menuItemOptions,
    toPersianNumber,
    formatDateTime,
  } = useAdmin()

  const canCreate = hasPermission(session, PERMISSIONS['order-list']?.create)
  const canUpdate = hasPermission(session, PERMISSIONS['order-list']?.update)
  const canDelete = hasPermission(session, PERMISSIONS['order-list']?.delete)

  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)
  const [activeTab, setActiveTab] = useState('Pending')
  const [supplierFilter, setSupplierFilter] = useState('')
  const [searchTerm, setSearchTerm] = useState('')

  const [summary, setSummary] = useState({ pendingCount: 0, orderedCount: 0, receivedCount: 0 })
  const [items, setItems] = useState([])
  const [suggestions, setSuggestions] = useState([])
  const [suppliers, setSuppliers] = useState([])

  // Quick-add form state
  const [selectedMenuItemId, setSelectedMenuItemId] = useState('')
  const [customItemName, setCustomItemName] = useState('')
  const [addSupplierId, setAddSupplierId] = useState('')
  const [addQuantity, setAddQuantity] = useState('4')
  const [addUnit, setAddUnit] = useState('عدد')
  const [addNotes, setAddNotes] = useState('')

  // Selection & Export dock state
  const [selectedIds, setSelectedIds] = useState([])
  const [exportSupplierName, setExportSupplierName] = useState('')
  const [exportPhone, setExportPhone] = useState('')
  const [deliveryNote, setDeliveryNote] = useState('')
  const [generalNote, setGeneralNote] = useState('')
  const [customMessage, setCustomMessage] = useState('')
  const [isMessageEdited, setIsMessageEdited] = useState(false)
  const [copied, setCopied] = useState(false)

  const loadSuppliers = useCallback(async () => {
    if (!session?.token) return
    try {
      const data = await apiRequest('/admin/suppliers', { token: session.token })
      const list = Array.isArray(data) ? data : (data?.items ?? [])
      setSuppliers(list.filter((s) => s.isActive !== false))
    } catch {
      // Suppliers list is optional if user lacks suppliers permission
      setSuppliers([])
    }
  }, [session?.token, apiRequest])

  const loadOrderList = useCallback(async () => {
    if (!session?.token) return
    try {
      const params = new URLSearchParams()
      if (activeTab !== 'All') params.set('status', activeTab)
      if (supplierFilter) params.set('supplierId', supplierFilter)
      if (searchTerm.trim()) params.set('search', searchTerm.trim())
      const qs = params.toString()

      const [listRes, suggestionsRes] = await Promise.all([
        apiRequest(`/admin/supplier-order-list${qs ? `?${qs}` : ''}`, { token: session.token }),
        apiRequest('/admin/supplier-order-list/low-stock-suggestions', { token: session.token }),
      ])

      setSummary({
        pendingCount: listRes?.pendingCount ?? 0,
        orderedCount: listRes?.orderedCount ?? 0,
        receivedCount: listRes?.receivedCount ?? 0,
      })
      setItems(Array.isArray(listRes?.items) ? listRes.items : [])
      setSuggestions(Array.isArray(suggestionsRes) ? suggestionsRes : [])
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setLoading(false)
    }
  }, [session?.token, activeTab, supplierFilter, searchTerm, apiRequest, getReadableError, setError])

  useEffect(() => {
    loadSuppliers()
  }, [loadSuppliers])

  useEffect(() => {
    loadOrderList()
  }, [loadOrderList])

  // Keep selectedIds valid when list changes
  useEffect(() => {
    const visibleIds = new Set(items.map((i) => i.id))
    setSelectedIds((prev) => prev.filter((id) => visibleIds.has(id)))
  }, [items])

  const exportItems = useMemo(() => {
    if (selectedIds.length > 0) {
      const idSet = new Set(selectedIds)
      return items.filter((i) => idSet.has(i.id))
    }
    return items.filter((i) => i.status === 'Pending')
  }, [items, selectedIds])

  // Auto-fill supplier name & phone when exportItems share a single supplier
  useEffect(() => {
    const distinctSuppliers = Array.from(
      new Set(exportItems.map((i) => i.supplierName).filter(Boolean)),
    )
    if (distinctSuppliers.length === 1) {
      const match = exportItems.find((i) => i.supplierName === distinctSuppliers[0])
      setExportSupplierName(match?.supplierName ?? '')
      if (match?.supplierPhone) {
        setExportPhone(match.supplierPhone)
      }
    } else if (supplierFilter) {
      const sup = suppliers.find((s) => s.id === supplierFilter)
      if (sup) {
        setExportSupplierName(sup.persianName ?? '')
        setExportPhone(sup.phoneNumber ?? '')
      }
    }
  }, [exportItems, supplierFilter, suppliers])

  const generatedMessage = useMemo(
    () =>
      buildWhatsAppOrderMessage({
        items: exportItems,
        supplierName: exportSupplierName,
        cafeName: 'کافه شوگاریستا',
        deliveryNote,
        generalNote,
      }),
    [exportItems, exportSupplierName, deliveryNote, generalNote],
  )

  useEffect(() => {
    if (!isMessageEdited) {
      setCustomMessage(generatedMessage)
    }
  }, [generatedMessage, isMessageEdited])

  const effectiveMessage = isMessageEdited ? customMessage : generatedMessage

  const handleSelectMenuItem = (nextId) => {
    setSelectedMenuItemId(nextId)
    if (nextId) {
      const found = (menuItemOptions ?? []).find((m) => m.id === nextId)
      if (found) {
        setCustomItemName(found.persianName || found.name || '')
      }
    }
  }

  const handleAddItem = async (event) => {
    event.preventDefault()
    const trimmedName = customItemName.trim()
    if (!selectedMenuItemId && !trimmedName) {
      setError('لطفاً یک آیتم از منو انتخاب کنید یا نام محصول را وارد کنید.')
      return
    }

    setBusy(true)
    setError('')
    setMessage('')
    try {
      await apiRequest('/admin/supplier-order-list', {
        method: 'POST',
        token: session.token,
        body: {
          menuItemId: selectedMenuItemId || null,
          itemName: trimmedName || null,
          supplierId: addSupplierId || null,
          quantity: Math.max(1, Number(addQuantity) || 1),
          unit: addUnit || 'عدد',
          notes: addNotes.trim() || null,
          mergeIfPending: true,
        },
      })

      setSelectedMenuItemId('')
      setCustomItemName('')
      setAddQuantity('4')
      setAddNotes('')
      setIsMessageEdited(false)
      setMessage('محصول به لیست سفارش تأمین‌کننده اضافه شد.')
      await loadOrderList()
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setBusy(false)
    }
  }

  const handleAddSuggestion = async (suggestion) => {
    if (!canCreate) return
    setBusy(true)
    setError('')
    setMessage('')
    try {
      await apiRequest('/admin/supplier-order-list', {
        method: 'POST',
        token: session.token,
        body: {
          menuItemId: suggestion.menuItemId,
          itemName: suggestion.itemName,
          categoryName: suggestion.categoryName,
          supplierId: addSupplierId || null,
          quantity: suggestion.suggestedOrderQuantity || 4,
          unit: 'عدد',
          mergeIfPending: true,
        },
      })
      setIsMessageEdited(false)
      setMessage(`«${suggestion.itemName}» به لیست سفارش اضافه شد.`)
      await loadOrderList()
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setBusy(false)
    }
  }

  const handleAddAllOutOfStock = async () => {
    const targets = suggestions.filter((s) => !s.alreadyInOrderList)
    if (!targets.length || !canCreate) return

    setBusy(true)
    setError('')
    setMessage('')
    try {
      const res = await apiRequest('/admin/supplier-order-list/bulk-add', {
        method: 'POST',
        token: session.token,
        body: {
          defaultSupplierId: addSupplierId || null,
          mergeIfPending: true,
          items: targets.map((s) => ({
            menuItemId: s.menuItemId,
            itemName: s.itemName,
            categoryName: s.categoryName,
            quantity: s.suggestedOrderQuantity || 4,
            unit: 'عدد',
          })),
        },
      })
      setIsMessageEdited(false)
      setMessage(
        `${toPersianNumber(res?.addedCount ?? targets.length)} قلم تمام‌شده / کم‌موجودی به لیست سفارش اضافه شد.`,
      )
      await loadOrderList()
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setBusy(false)
    }
  }

  const handleInlineUpdate = async (item, patch) => {
    if (!canUpdate) return
    try {
      const updated = await apiRequest(`/admin/supplier-order-list/${item.id}`, {
        method: 'PUT',
        token: session.token,
        body: patch,
      })
      setItems((prev) => prev.map((row) => (row.id === item.id ? updated : row)))
      setIsMessageEdited(false)
    } catch (requestError) {
      setError(getReadableError(requestError))
    }
  }

  const handleDeleteItem = async (id) => {
    if (!canDelete) return
    try {
      await apiRequest(`/admin/supplier-order-list/${id}`, {
        method: 'DELETE',
        token: session.token,
      })
      setItems((prev) => prev.filter((row) => row.id !== id))
      setSelectedIds((prev) => prev.filter((sid) => sid !== id))
      setIsMessageEdited(false)
      await loadOrderList()
    } catch (requestError) {
      setError(getReadableError(requestError))
    }
  }

  const handleMarkOrdered = async () => {
    const pendingTargets = exportItems.filter((i) => i.status === 'Pending')
    if (!pendingTargets.length || !canUpdate) return

    setBusy(true)
    setError('')
    setMessage('')
    try {
      const res = await apiRequest('/admin/supplier-order-list/mark-ordered', {
        method: 'POST',
        token: session.token,
        body: { ids: pendingTargets.map((i) => i.id) },
      })
      setSelectedIds([])
      setIsMessageEdited(false)
      setMessage(
        `${toPersianNumber(res?.updatedCount ?? pendingTargets.length)} قلم با کد سفارش ${res?.batchCode ?? ''} به‌عنوان «سفارش داده‌شده» ثبت شد.`,
      )
      await loadOrderList()
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setBusy(false)
    }
  }

  const handleMarkReceived = async (targetIds, restockMenuItems = true) => {
    if (!targetIds.length || !canUpdate) return
    setBusy(true)
    setError('')
    setMessage('')
    try {
      const res = await apiRequest('/admin/supplier-order-list/mark-received', {
        method: 'POST',
        token: session.token,
        body: { ids: targetIds, restockMenuItems },
      })
      setSelectedIds([])
      setMessage(
        res?.restockedCount > 0
          ? `تحویل ثبت شد و موجودی ${toPersianNumber(res.restockedCount)} آیتم در منو به‌روزرسانی شد.`
          : 'تحویل سفارش با موفقیت ثبت شد.',
      )
      await loadOrderList()
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setBusy(false)
    }
  }

  const handleReorder = async (id) => {
    if (!canCreate) return
    setBusy(true)
    setError('')
    setMessage('')
    try {
      await apiRequest(`/admin/supplier-order-list/${id}/reorder`, {
        method: 'POST',
        token: session.token,
      })
      setMessage('محصول مجدداً به لیست «در انتظار سفارش» اضافه شد.')
      setActiveTab('Pending')
      await loadOrderList()
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setBusy(false)
    }
  }

  const handleSendWhatsApp = () => {
    if (!exportItems.length) {
      setError('هیچ قلمی برای ارسال به واتساپ انتخاب نشده است.')
      return
    }
    const url = buildWhatsAppUrl(exportPhone, effectiveMessage)
    window.open(url, '_blank', 'noopener,noreferrer')
  }

  const handleCopyWhatsApp = async () => {
    if (!effectiveMessage.trim()) return
    try {
      await navigator.clipboard.writeText(effectiveMessage)
      setCopied(true)
      setMessage('متن سفارش در کلیپ‌بورد کپی شد — می‌توانید مستقیم در واتساپ پیست کنید.')
      setTimeout(() => setCopied(false), 2200)
    } catch {
      setError('دسترسی به کلیپ‌بورد مرورگر ممکن نشد؛ لطفاً متن را دستی کپی کنید.')
    }
  }

  const handleExportPdf = () => {
    if (!exportItems.length) {
      setError('هیچ قلمی برای خروجی PDF وجود ندارد.')
      return
    }
    const opened = openOrderListPdfWindow({
      items: exportItems,
      supplierName: exportSupplierName,
      supplierPhone: exportPhone,
      cafeName: 'کافه شوگاریستا',
      deliveryNote,
      generalNote,
    })
    if (!opened) {
      setError('مرورگر پنجره چاپ/PDF را مسدود کرد. لطفاً پاپ‌آپ را برای این صفحه مجاز کنید.')
    }
  }

  const toggleSelectAll = () => {
    if (selectedIds.length === items.length && items.length > 0) {
      setSelectedIds([])
    } else {
      setSelectedIds(items.map((i) => i.id))
    }
  }

  const toggleRowSelect = (id) => {
    setSelectedIds((prev) =>
      prev.includes(id) ? prev.filter((item) => item !== id) : [...prev, id],
    )
  }

  const normalizedPhonePreview = normalizeWhatsAppPhone(exportPhone)
  const unqueuedSuggestions = suggestions.filter((s) => !s.alreadyInOrderList)

  return (
    <section className="ol-page menu-minimal-scope">
      {/* Top Summary & Smart Suggestions */}
      <div className="panel ol-top-panel">
        <div className="ol-top-head">
          <div>
            <h2>لیست سفارش به تأمین‌کننده (Order List)</h2>
            <p>
              کیک‌ها، دسرها و محصولاتی که تمام می‌شوند را اینجا جمع کنید و مستقیم به صورت متن مرتب در واتساپ یا فایل PDF برای تأمین‌کننده بفرستید.
            </p>
          </div>
        </div>

        <div className="ol-kpi-strip">
          <button
            type="button"
            className={`ol-kpi-card ${activeTab === 'Pending' ? 'is-active' : ''}`}
            onClick={() => setActiveTab('Pending')}
          >
            <span className="ol-kpi-label">در انتظار سفارش</span>
            <span className="ol-kpi-value">{toPersianNumber(summary.pendingCount)}</span>
          </button>
          <button
            type="button"
            className={`ol-kpi-card ${activeTab === 'Ordered' ? 'is-active' : ''}`}
            onClick={() => setActiveTab('Ordered')}
          >
            <span className="ol-kpi-label">سفارش داده‌شده</span>
            <span className="ol-kpi-value">{toPersianNumber(summary.orderedCount)}</span>
          </button>
          <button
            type="button"
            className={`ol-kpi-card ${activeTab === 'Received' ? 'is-active' : ''}`}
            onClick={() => setActiveTab('Received')}
          >
            <span className="ol-kpi-label">تحویل‌گرفته‌شده</span>
            <span className="ol-kpi-value">{toPersianNumber(summary.receivedCount)}</span>
          </button>
        </div>

        {suggestions.length > 0 ? (
          <div className="ol-suggestions-box">
            <div className="ol-suggestions-head">
              <div className="ol-suggestions-title">
                <span>⚡ آیتم‌های تمام‌شده یا کم‌موجودی ({toPersianNumber(suggestions.length)} مورد)</span>
              </div>
              {canCreate && unqueuedSuggestions.length > 0 ? (
                <button
                  type="button"
                  className="ghost-button"
                  disabled={busy}
                  onClick={handleAddAllOutOfStock}
                >
                  + افزودن همه ({toPersianNumber(unqueuedSuggestions.length)}) به لیست سفارش
                </button>
              ) : null}
            </div>
            <div className="ol-suggestions-chips">
              {suggestions.map((sug) => (
                <button
                  key={sug.menuItemId}
                  type="button"
                  className={`ol-suggestion-chip ${sug.alreadyInOrderList ? 'is-queued' : ''}`}
                  disabled={busy || !canCreate}
                  onClick={() => handleAddSuggestion(sug)}
                  title={
                    sug.alreadyInOrderList
                      ? 'این آیتم در لیست سفارش هست؛ کلیک برای افزایش تعداد'
                      : 'کلیک برای افزودن فوری به لیست سفارش'
                  }
                >
                  <span className={`ol-stock-dot ${sug.isOutOfStock ? 'is-out' : 'is-low'}`} />
                  <span>{sug.itemName}</span>
                  <span style={{ color: 'var(--muted)', fontSize: '0.72rem' }}>
                    (موجودی: {formatQuantity(sug.currentQuantity)})
                  </span>
                  {sug.alreadyInOrderList ? <span>✓</span> : <span>+</span>}
                </button>
              ))}
            </div>
          </div>
        ) : null}
      </div>

      {/* Quick Add Bar */}
      {canCreate ? (
        <form className="panel ol-quick-add" onSubmit={handleAddItem}>
          <div className="panel-head">
            <div>
              <h3>افزودن سریع به لیست سفارش</h3>
              <p>از آیتم‌های منو انتخاب کنید یا نام محصول دلخواه را تایپ کنید.</p>
            </div>
          </div>
          <div className="ol-quick-add-grid">
            <label>
              <span>انتخاب از منو (اختیاری)</span>
              <select
                value={selectedMenuItemId}
                onChange={(e) => handleSelectMenuItem(e.target.value)}
              >
                <option value="">— انتخاب آیتم منو —</option>
                {(menuItemOptions ?? []).map((item) => (
                  <option key={item.id} value={item.id}>
                    {item.persianName || item.name}
                  </option>
                ))}
              </select>
            </label>

            <label>
              <span>نام محصول *</span>
              <input
                placeholder="مثلاً کیک اپرا، دنیش توت‌فرنگی..."
                value={customItemName}
                onChange={(e) => setCustomItemName(e.target.value)}
              />
            </label>

            <label>
              <span>تأمین‌کننده (پرووایدر)</span>
              <select
                value={addSupplierId}
                onChange={(e) => setAddSupplierId(e.target.value)}
              >
                <option value="">— بدون انتخاب —</option>
                {suppliers.map((sup) => (
                  <option key={sup.id} value={sup.id}>
                    {sup.persianName}
                  </option>
                ))}
              </select>
            </label>

            <label>
              <span>تعداد</span>
              <input
                className="ltr-input"
                type="number"
                min="1"
                step="any"
                value={addQuantity}
                onChange={(e) => setAddQuantity(e.target.value)}
              />
            </label>

            <label>
              <span>واحد</span>
              <select value={addUnit} onChange={(e) => setAddUnit(e.target.value)}>
                {ORDER_LIST_UNITS.map((unit) => (
                  <option key={unit} value={unit}>
                    {unit}
                  </option>
                ))}
              </select>
            </label>
          </div>

          <div className="ol-quick-add-row2">
            <label>
              <span>یادداشت برای تأمین‌کننده (اختیاری)</span>
              <input
                placeholder="مثلاً تازه و بدون گردو، برش ۱۲ اسلایس..."
                value={addNotes}
                onChange={(e) => setAddNotes(e.target.value)}
              />
            </label>
            <button type="submit" className="primary-button" disabled={busy}>
              {busy ? 'در حال ثبت...' : '+ افزودن به لیست سفارش'}
            </button>
          </div>
        </form>
      ) : null}

      {/* Main Workspace: Order Items List + WhatsApp & PDF Export Dock */}
      <div className="ol-workspace">
        <div className="panel">
          <div className="ol-toolbar">
            <div className="ol-tabs">
              {STATUS_TABS.map((tab) => (
                <button
                  key={tab.key}
                  type="button"
                  className={`ol-tab-btn ${activeTab === tab.key ? 'is-active' : ''}`}
                  onClick={() => setActiveTab(tab.key)}
                >
                  {tab.label}
                </button>
              ))}
            </div>

            <div className="ol-filter-controls">
              <select
                aria-label="فیلتر تأمین‌کننده"
                value={supplierFilter}
                onChange={(e) => setSupplierFilter(e.target.value)}
              >
                <option value="">همه تأمین‌کنندگان</option>
                {suppliers.map((sup) => (
                  <option key={sup.id} value={sup.id}>
                    {sup.persianName}
                  </option>
                ))}
              </select>
              <input
                placeholder="جستجو در اقلام..."
                value={searchTerm}
                onChange={(e) => setSearchTerm(e.target.value)}
              />
              {items.length > 0 ? (
                <button type="button" className="ghost-button" onClick={toggleSelectAll}>
                  {selectedIds.length === items.length ? 'لغو انتخاب همه' : 'انتخاب همه'}
                </button>
              ) : null}
            </div>
          </div>

          {loading ? (
            <div className="ol-empty-state">در حال بارگذاری لیست سفارش...</div>
          ) : items.length === 0 ? (
            <div className="ol-empty-state">
              {activeTab === 'Pending'
                ? 'لیست سفارش در انتظار خالی است. از بخش بالا یا صفحه «موجودی و انقضا سریع» آیتم‌ها را اضافه کنید.'
                : 'موردی در این بخش ثبت نشده است.'}
            </div>
          ) : (
            <div className="ol-items-list">
              {items.map((item) => {
                const isSelected = selectedIds.includes(item.id)
                const isPending = item.status === 'Pending'
                const isOrdered = item.status === 'Ordered'
                const statusClass =
                  item.status === 'Pending'
                    ? 'is-pending'
                    : item.status === 'Ordered'
                      ? 'is-ordered'
                      : 'is-received'
                const statusLabel =
                  item.status === 'Pending'
                    ? 'در انتظار'
                    : item.status === 'Ordered'
                      ? `سفارش شد${item.batchCode ? ` • ${item.batchCode}` : ''}`
                      : 'تحویل شد'

                return (
                  <div key={item.id} className={`ol-item-row ${isSelected ? 'is-selected' : ''}`}>
                    <div className="ol-item-top">
                      <div className="ol-item-identity">
                        <input
                          type="checkbox"
                          checked={isSelected}
                          onChange={() => toggleRowSelect(item.id)}
                          aria-label={`انتخاب ${item.itemName}`}
                        />
                        <span className="ol-item-name">{item.itemName}</span>
                        {item.categoryName ? (
                          <span className="ol-item-cat">{item.categoryName}</span>
                        ) : null}
                        {item.currentStock != null ? (
                          <span className="ol-item-cat">
                            موجودی فعلی: {formatQuantity(item.currentStock)}
                          </span>
                        ) : null}
                      </div>
                      <span className={`ol-status-pill ${statusClass}`}>{statusLabel}</span>
                    </div>

                    <div className="ol-item-controls">
                      <div className="ol-qty-stepper">
                        <button
                          type="button"
                          className="ol-qty-btn"
                          disabled={!canUpdate || !isPending || Number(item.quantity) <= 1}
                          onClick={() =>
                            handleInlineUpdate(item, {
                              quantity: Math.max(1, Number(item.quantity) - 1),
                            })
                          }
                          aria-label="کاهش تعداد"
                        >
                          −
                        </button>
                        <input
                          className="ltr-input ol-qty-input"
                          type="number"
                          min="1"
                          step="any"
                          disabled={!canUpdate || !isPending}
                          value={item.quantity}
                          onChange={(e) => {
                            const val = Number(e.target.value)
                            if (val > 0) {
                              setItems((prev) =>
                                prev.map((r) => (r.id === item.id ? { ...r, quantity: val } : r)),
                              )
                            }
                          }}
                          onBlur={(e) => {
                            const val = Number(e.target.value)
                            if (val > 0) {
                              handleInlineUpdate(item, { quantity: val })
                            }
                          }}
                        />
                        <button
                          type="button"
                          className="ol-qty-btn"
                          disabled={!canUpdate || !isPending}
                          onClick={() =>
                            handleInlineUpdate(item, { quantity: Number(item.quantity) + 1 })
                          }
                          aria-label="افزایش تعداد"
                        >
                          +
                        </button>
                      </div>

                      <select
                        disabled={!canUpdate || !isPending}
                        value={item.unit || 'عدد'}
                        onChange={(e) => handleInlineUpdate(item, { unit: e.target.value })}
                        aria-label="واحد"
                      >
                        {ORDER_LIST_UNITS.map((unit) => (
                          <option key={unit} value={unit}>
                            {unit}
                          </option>
                        ))}
                      </select>

                      <select
                        disabled={!canUpdate || !isPending}
                        value={item.supplierId ?? ''}
                        onChange={(e) => {
                          const nextSupId = e.target.value
                          handleInlineUpdate(
                            item,
                            nextSupId ? { supplierId: nextSupId } : { clearSupplier: true },
                          )
                        }}
                        aria-label="تأمین‌کننده"
                      >
                        <option value="">— تأمین‌کننده —</option>
                        {suppliers.map((sup) => (
                          <option key={sup.id} value={sup.id}>
                            {sup.persianName}
                          </option>
                        ))}
                      </select>

                      <input
                        disabled={!canUpdate || !isPending}
                        placeholder="یادداشت..."
                        defaultValue={item.notes ?? ''}
                        onBlur={(e) => {
                          if (e.target.value !== (item.notes ?? '')) {
                            handleInlineUpdate(item, { notes: e.target.value })
                          }
                        }}
                      />

                      <div className="ol-row-actions">
                        {isOrdered && canUpdate ? (
                          <button
                            type="button"
                            className="ghost-button"
                            disabled={busy}
                            onClick={() => handleMarkReceived([item.id], true)}
                            title="ثبت تحویل و اضافه کردن خودکار به موجودی منو"
                          >
                            تحویل شد ✓
                          </button>
                        ) : null}
                        {!isPending && canCreate ? (
                          <button
                            type="button"
                            className="ghost-button"
                            disabled={busy}
                            onClick={() => handleReorder(item.id)}
                            title="افزودن مجدد این قلم به لیست در انتظار"
                          >
                            سفارش مجدد
                          </button>
                        ) : null}
                        {canDelete ? (
                          <button
                            type="button"
                            className="ol-icon-danger"
                            onClick={() => handleDeleteItem(item.id)}
                            title="حذف از لیست"
                            aria-label="حذف"
                          >
                            ✕
                          </button>
                        ) : null}
                      </div>
                    </div>

                    {item.orderedAt || item.receivedAt ? (
                      <div style={{ fontSize: '0.72rem', color: 'var(--muted)' }}>
                        {item.orderedAt ? `ثبت سفارش: ${formatDateTime(item.orderedAt)}` : ''}
                        {item.orderedAt && item.receivedAt ? ' • ' : ''}
                        {item.receivedAt ? `تحویل: ${formatDateTime(item.receivedAt)}` : ''}
                      </div>
                    ) : null}
                  </div>
                )
              })}
            </div>
          )}
        </div>

        {/* Right/Left Sticky WhatsApp & PDF Export Panel */}
        <aside className="panel ol-export-panel">
          <div className="panel-head">
            <div>
              <h3>ارسال به واتساپ و خروجی PDF</h3>
              <p>
                {selectedIds.length > 0
                  ? `${toPersianDigits(exportItems.length)} قلم انتخاب‌شده آماده ارسال است.`
                  : `${toPersianDigits(exportItems.length)} قلم در انتظار آماده ارسال است.`}
              </p>
            </div>
          </div>

          <div className="ol-export-fields">
            <label>
              <span>نام تأمین‌کننده (در متن/PDF)</span>
              <input
                placeholder="مثلاً قنادی شوگاریستا"
                value={exportSupplierName}
                onChange={(e) => {
                  setExportSupplierName(e.target.value)
                  setIsMessageEdited(false)
                }}
              />
            </label>
            <label>
              <span>شماره واتساپ تأمین‌کننده</span>
              <input
                className="ltr-input"
                placeholder="09123456789"
                value={exportPhone}
                onChange={(e) => setExportPhone(e.target.value)}
              />
            </label>
          </div>

          <div className="ol-export-fields">
            <label>
              <span>زمان تحویل پیشنهادی</span>
              <input
                placeholder="مثلاً فردا صبح ساعت ۹"
                value={deliveryNote}
                onChange={(e) => {
                  setDeliveryNote(e.target.value)
                  setIsMessageEdited(false)
                }}
              />
            </label>
            <label>
              <span>توضیحات کلی سفارش</span>
              <input
                placeholder="مثلاً فاکتور همراه کالا ارسال شود"
                value={generalNote}
                onChange={(e) => {
                  setGeneralNote(e.target.value)
                  setIsMessageEdited(false)
                }}
              />
            </label>
          </div>

          <label>
            <span>پیش‌نمایش متن واتساپ (قابل ویرایش)</span>
            <textarea
              className="ol-whatsapp-preview"
              value={effectiveMessage}
              onChange={(e) => {
                setCustomMessage(e.target.value)
                setIsMessageEdited(true)
              }}
            />
          </label>

          <div className="ol-export-actions">
            <button
              type="button"
              className="ol-whatsapp-btn"
              disabled={exportItems.length === 0}
              onClick={handleSendWhatsApp}
            >
              <span>💬 ارسال مستقیم به واتساپ</span>
              {normalizedPhonePreview ? (
                <span style={{ fontSize: '0.74rem', opacity: 0.9 }} dir="ltr">
                  (+{normalizedPhonePreview})
                </span>
              ) : null}
            </button>

            <div className="ol-export-secondary-row">
              <button
                type="button"
                className="ghost-button"
                disabled={exportItems.length === 0}
                onClick={handleCopyWhatsApp}
              >
                {copied ? 'کپی شد ✓' : '📋 کپی متن واتساپ'}
              </button>
              <button
                type="button"
                className="ghost-button"
                disabled={exportItems.length === 0}
                onClick={handleExportPdf}
              >
                📄 خروجی PDF / چاپ
              </button>
            </div>

            {canUpdate && exportItems.some((i) => i.status === 'Pending') ? (
              <button
                type="button"
                className="primary-button"
                disabled={busy}
                onClick={handleMarkOrdered}
              >
                ✓ ثبت به‌عنوان «سفارش داده شد»
              </button>
            ) : null}
          </div>
        </aside>
      </div>
    </section>
  )
}
