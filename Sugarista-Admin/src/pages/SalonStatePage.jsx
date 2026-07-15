import { useAdmin } from '../context/AdminContext'

export function SalonStatePage() {
  const {
    salonStatus,
    salonTableForm,
    setSalonTableForm,
    occupyForm,
    setOccupyForm,
    isSalonTableModalOpen,
    isOccupyModalOpen,
    saving,
    activeSalonTables,
    loadSalonStatus,
    handleSalonTableSubmit,
    handleDeleteSalonTable,
    handleOccupyTable,
    handleReleaseTable,
    editSalonTable,
    openCreateSalonTableModal,
    closeSalonTableModal,
    openOccupyTableModal,
    closeOccupyModal,
    toPersianNumber,
    formatDateTime,
    formatDuration,
  } = useAdmin()

  return (
      <section className="panel-stack">
        <section className="stat-grid">
          <article className="stat-card">
            <span>کل میزها</span>
            <strong>{toPersianNumber(salonStatus.totalTables)}</strong>
          </article>
          <article className="stat-card">
            <span>میزهای اشغال</span>
            <strong>{toPersianNumber(salonStatus.occupiedTables)}</strong>
          </article>
          <article className="stat-card">
            <span>میزهای آزاد</span>
            <strong>{toPersianNumber(salonStatus.freeTables)}</strong>
          </article>
          <article className="stat-card">
            <span>مشتریان حاضر</span>
            <strong>{toPersianNumber(salonStatus.currentCustomers)}</strong>
          </article>
        </section>
    
        <section className="panel">
          <div className="panel-head">
            <div>
              <h3>وضعیت زنده سالن</h3>
              <p>
                این بخش هر ۵ ثانیه تازه‌سازی می‌شود. آخرین بروزرسانی:{' '}
                {formatDateTime(salonStatus.timestamp)}
              </p>
            </div>
            <button type="button" className="ghost-button" onClick={() => loadSalonStatus()}>
              بروزرسانی دستی
            </button>
          </div>
    
          <div className="salon-grid">
            {activeSalonTables.map((table) => (
              <article
                key={table.id}
                className={table.isOccupied ? 'salon-card salon-card-occupied' : 'salon-card'}
              >
                <div className="salon-card-head">
                  <div>
                    <h4>{table.name}</h4>
                    <p>ظرفیت {toPersianNumber(table.capacity)} نفر</p>
                  </div>
                  <span className={table.isOccupied ? 'status-badge busy' : 'status-badge free'}>
                    {table.isOccupied ? 'اشغال' : 'آزاد'}
                  </span>
                </div>
    
                {table.isOccupied ? (
                  <div className="salon-card-body">
                    <div>
                      <span className="field-label">تعداد افراد</span>
                      <strong>{toPersianNumber(table.peopleCount)} نفر</strong>
                    </div>
                    <div>
                      <span className="field-label">شروع حضور</span>
                      <strong>{formatDateTime(table.startedAt)}</strong>
                    </div>
                    <div>
                      <span className="field-label">مدت استفاده</span>
                      <strong>{formatDuration(table.durationMinutes)}</strong>
                    </div>
                    {table.note ? (
                      <div className="full-width">
                        <span className="field-label">یادداشت</span>
                        <strong>{table.note}</strong>
                      </div>
                    ) : null}
                  </div>
                ) : (
                  <p className="salon-empty">این میز آماده پذیرش مشتری است.</p>
                )}
    
                <div className="salon-card-actions">
                  {table.isOccupied ? (
                    <button
                      type="button"
                      className="ghost-button"
                      onClick={() => handleReleaseTable(table.id)}
                      disabled={saving}
                    >
                      آزاد کردن میز
                    </button>
                  ) : (
                    <button
                      type="button"
                      className="ghost-button"
                      onClick={() => openOccupyTableModal(table)}
                    >
                      اشغال کردن
                    </button>
                  )}
                  <button type="button" className="link-button" onClick={() => editSalonTable(table)}>
                    ویرایش میز
                  </button>
                </div>
              </article>
            ))}
          </div>
        </section>
    
        <section className="panel">
          <div className="panel-head">
            <div>
              <h3>مدیریت میزها</h3>
              <p>برای افزودن میز جدید یا ویرایش میزهای موجود از پنجره popup استفاده کنید.</p>
            </div>
            <button type="button" className="ghost-button" onClick={openCreateSalonTableModal}>
              افزودن میز جدید
            </button>
          </div>
        </section>
    
        {isOccupyModalOpen ? (
          <div className="modal-backdrop" role="presentation">
            <form className="panel form-panel modal-card" onSubmit={handleOccupyTable}>
              <div className="panel-head">
                <div>
                  <h3>ثبت اشغال میز</h3>
                  <p>تعداد افراد را وارد کنید تا زمان شروع حضور مشتریان ذخیره شود.</p>
                </div>
                <button type="button" className="ghost-button" onClick={closeOccupyModal}>
                  بستن
                </button>
              </div>
              <label>
                <span>میز</span>
                <select
                  value={occupyForm.tableId}
                  onChange={(event) =>
                    setOccupyForm((currentForm) => ({
                      ...currentForm,
                      tableId: event.target.value,
                    }))
                  }
                >
                  <option value="">یک میز آزاد انتخاب کنید</option>
                  {activeSalonTables
                    .filter((table) => !table.isOccupied)
                    .map((table) => (
                      <option key={table.id} value={table.id}>
                        {table.name} - ظرفیت {toPersianNumber(table.capacity)} نفر
                      </option>
                    ))}
                </select>
              </label>
              <label>
                <span>تعداد افراد</span>
                <input
                  className="ltr-input"
                  type="number"
                  min="1"
                  value={occupyForm.peopleCount}
                  onChange={(event) =>
                    setOccupyForm((currentForm) => ({
                      ...currentForm,
                      peopleCount: Number(event.target.value),
                    }))
                  }
                />
              </label>
              <label>
                <span>یادداشت اختیاری</span>
                <textarea
                  rows="3"
                  value={occupyForm.note}
                  onChange={(event) =>
                    setOccupyForm((currentForm) => ({
                      ...currentForm,
                      note: event.target.value,
                    }))
                  }
                />
              </label>
              <button type="submit" disabled={saving || !occupyForm.tableId}>
                {saving ? 'در حال ثبت...' : 'ثبت اشغال میز'}
              </button>
            </form>
          </div>
        ) : null}
    
        {isSalonTableModalOpen ? (
          <div className="modal-backdrop" role="presentation">
            <form className="panel form-panel modal-card" onSubmit={handleSalonTableSubmit}>
            <div className="panel-head">
              <div>
                <h3>{salonTableForm.id ? 'ویرایش میز' : 'میز جدید'}</h3>
                <p>میزها، ظرفیت سالن و نقشه عملیاتی کافه را مشخص می‌کنند.</p>
              </div>
              <button type="button" className="ghost-button" onClick={closeSalonTableModal}>
                بستن
              </button>
            </div>
            <label>
              <span>نام میز</span>
              <input
                value={salonTableForm.name}
                onChange={(event) =>
                  setSalonTableForm((currentForm) => ({
                    ...currentForm,
                    name: event.target.value,
                  }))
                }
              />
            </label>
            <div className="field-grid">
              <label>
                <span>ظرفیت</span>
                <input
                  className="ltr-input"
                  type="number"
                  min="1"
                  value={salonTableForm.capacity}
                  onChange={(event) =>
                    setSalonTableForm((currentForm) => ({
                      ...currentForm,
                      capacity: Number(event.target.value),
                    }))
                  }
                />
              </label>
              <label>
                <span>ترتیب نمایش</span>
                <input
                  className="ltr-input"
                  type="number"
                  min="0"
                  value={salonTableForm.displayOrder}
                  onChange={(event) =>
                    setSalonTableForm((currentForm) => ({
                      ...currentForm,
                      displayOrder: Number(event.target.value),
                    }))
                  }
                />
              </label>
            </div>
            <label className="checkbox-row">
              <input
                type="checkbox"
                checked={salonTableForm.isActive}
                onChange={(event) =>
                  setSalonTableForm((currentForm) => ({
                    ...currentForm,
                    isActive: event.target.checked,
                  }))
                }
              />
              <span>میز فعال باشد</span>
            </label>
            <div className="form-actions">
              <button type="submit" disabled={saving}>
                {saving ? 'در حال ذخیره...' : 'ذخیره میز'}
              </button>
              {salonTableForm.id ? (
                <button
                  type="button"
                  className="link-button link-danger"
                  onClick={() => handleDeleteSalonTable(salonTableForm.id)}
                >
                  حذف میز
                </button>
              ) : null}
            </div>
          </form>
          </div>
        ) : null}
      </section>
  )
}
