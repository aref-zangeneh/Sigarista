import { useEffect, useMemo, useState } from 'react'
import './ProductWikiPanel.css'

const emptyGuideForm = {
  overview: '',
  qualityStandards: '',
  equipmentNotes: '',
  servingNotes: '',
  isPublished: false,
}

const emptyRecipeLineForm = {
  ingredientId: '',
  quantity: '',
  preparationNote: '',
}

const emptyStepForm = {
  title: '',
  instructions: '',
  durationMinutes: '',
  isQualityCheckpoint: false,
}

function toPersianNumber(value) {
  return new Intl.NumberFormat('fa-IR').format(value ?? 0)
}

function createEmptyStep(order) {
  return {
    stepOrder: order,
    title: '',
    instructions: '',
    durationMinutes: '',
    isQualityCheckpoint: false,
  }
}

export function ProductWikiPanel({
  session,
  menuItemOptions,
  ingredientOptions,
  apiRequest,
  getReadableError,
  setMessage,
  setError,
  saving,
  setSaving,
}) {
  const [selectedMenuItemId, setSelectedMenuItemId] = useState('')
  const [wiki, setWiki] = useState(null)
  const [guideForm, setGuideForm] = useState(emptyGuideForm)
  const [steps, setSteps] = useState([])
  const [recipeLines, setRecipeLines] = useState([])
  const [recipeLineForm, setRecipeLineForm] = useState(emptyRecipeLineForm)
  const [loading, setLoading] = useState(false)

  const selectedMenuItem = useMemo(
    () => menuItemOptions.find((item) => item.id === selectedMenuItemId) ?? null,
    [menuItemOptions, selectedMenuItemId],
  )

  useEffect(() => {
    if (!selectedMenuItemId || !session?.token) {
      setWiki(null)
      setGuideForm(emptyGuideForm)
      setSteps([])
      setRecipeLines([])
      return
    }

    let cancelled = false

    async function loadWiki() {
      setLoading(true)
      setError('')
      try {
        const data = await apiRequest(`/admin/product-wiki/${selectedMenuItemId}`, { token: session.token })
        if (cancelled) {
          return
        }

        setWiki(data)
        setGuideForm({
          overview: data.overview ?? '',
          qualityStandards: data.qualityStandards ?? '',
          equipmentNotes: data.equipmentNotes ?? '',
          servingNotes: data.servingNotes ?? '',
          isPublished: data.isPublished ?? false,
        })
        setSteps(
          data.steps.length > 0
            ? data.steps.map((step) => ({
                stepOrder: step.stepOrder,
                title: step.title,
                instructions: step.instructions,
                durationMinutes: step.durationMinutes ?? '',
                isQualityCheckpoint: step.isQualityCheckpoint,
              }))
            : [createEmptyStep(1)],
        )
        setRecipeLines(data.recipeLines ?? [])
      } catch (requestError) {
        if (!cancelled) {
          setError(getReadableError(requestError))
        }
      } finally {
        if (!cancelled) {
          setLoading(false)
        }
      }
    }

    loadWiki()

    return () => {
      cancelled = true
    }
  }, [selectedMenuItemId, session?.token])

  function updateStep(index, patch) {
    setSteps((current) => current.map((step, stepIndex) => (stepIndex === index ? { ...step, ...patch } : step)))
  }

  function moveStep(index, direction) {
    setSteps((current) => {
      const next = [...current]
      const targetIndex = index + direction
      if (targetIndex < 0 || targetIndex >= next.length) {
        return current
      }

      ;[next[index], next[targetIndex]] = [next[targetIndex], next[index]]
      return next.map((step, stepIndex) => ({ ...step, stepOrder: stepIndex + 1 }))
    })
  }

  function addStep() {
    setSteps((current) => [...current, createEmptyStep(current.length + 1)])
  }

  function removeStep(index) {
    setSteps((current) => {
      if (current.length <= 1) {
        return [createEmptyStep(1)]
      }

      return current
        .filter((_, stepIndex) => stepIndex !== index)
        .map((step, stepIndex) => ({ ...step, stepOrder: stepIndex + 1 }))
    })
  }

  async function reloadWiki() {
    const data = await apiRequest(`/admin/product-wiki/${selectedMenuItemId}`, { token: session.token })
    setWiki(data)
    setRecipeLines(data.recipeLines ?? [])
  }

  async function handleSaveGuide(event) {
    event.preventDefault()
    setSaving(true)
    setError('')
    setMessage('')

    try {
      const preparedSteps = steps
        .filter((step) => step.title.trim() && step.instructions.trim())
        .map((step, index) => ({
          stepOrder: index + 1,
          title: step.title.trim(),
          instructions: step.instructions.trim(),
          durationMinutes: step.durationMinutes ? Number(step.durationMinutes) : null,
          isQualityCheckpoint: step.isQualityCheckpoint,
        }))

      await apiRequest(`/admin/product-wiki/${selectedMenuItemId}`, {
        method: 'PUT',
        token: session.token,
        body: {
          menuItemId: selectedMenuItemId,
          overview: guideForm.overview.trim() || null,
          qualityStandards: guideForm.qualityStandards.trim() || null,
          equipmentNotes: guideForm.equipmentNotes.trim() || null,
          servingNotes: guideForm.servingNotes.trim() || null,
          isPublished: guideForm.isPublished,
          steps: preparedSteps,
        },
      })

      await reloadWiki()
      setMessage('ویکی محصول با موفقیت ذخیره شد.')
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setSaving(false)
    }
  }

  async function handleAddRecipeLine(event) {
    event.preventDefault()
    if (!recipeLineForm.ingredientId || !recipeLineForm.quantity) {
      setError('ماده اولیه و مقدار الزامی است.')
      return
    }

    setSaving(true)
    setError('')
    setMessage('')

    try {
      await apiRequest(`/admin/recipes/${selectedMenuItemId}`, {
        method: 'POST',
        token: session.token,
        body: {
          menuItemId: selectedMenuItemId,
          ingredientId: recipeLineForm.ingredientId,
          quantity: Number(recipeLineForm.quantity),
          preparationNote: recipeLineForm.preparationNote.trim() || null,
        },
      })

      setRecipeLineForm(emptyRecipeLineForm)
      await reloadWiki()
      setMessage('ماده اولیه به رسپی ویکی اضافه شد.')
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setSaving(false)
    }
  }

  async function handleDeleteRecipeLine(lineId) {
    setSaving(true)
    setError('')
    try {
      await apiRequest(`/admin/recipes/${lineId}`, { method: 'DELETE', token: session.token })
      await reloadWiki()
      setMessage('ماده اولیه از رسپی حذف شد.')
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setSaving(false)
    }
  }

  const ingredientUnit =
    ingredientOptions.find((item) => item.id === recipeLineForm.ingredientId)?.unit ?? ''

  return (
    <section className="panel-stack product-wiki">
      <section className="panel wiki-selector-panel">
        <div className="panel-head">
          <div>
            <h3>ویکی محصولات و QC</h3>
            <p>رسپی دقیق، مراحل آماده‌سازی و استانداردهای کیفیت هر محصول را مستند کنید.</p>
          </div>
        </div>
        <label>
          <span>انتخاب محصول</span>
          <select
            value={selectedMenuItemId}
            onChange={(event) => {
              setSelectedMenuItemId(event.target.value)
              setRecipeLineForm(emptyRecipeLineForm)
            }}
          >
            <option value="">-- انتخاب محصول --</option>
            {menuItemOptions.map((item) => (
              <option key={item.id} value={item.id}>
                {item.persianName} — {item.englishName}
              </option>
            ))}
          </select>
        </label>
      </section>

      {!selectedMenuItemId ? (
        <section className="panel">
          <p className="wiki-empty-hint">ابتدا یک محصول (مثلاً Ice Latte) را انتخاب کنید.</p>
        </section>
      ) : loading ? (
        <section className="panel">
          <p className="wiki-empty-hint">در حال بارگذاری ویکی محصول...</p>
        </section>
      ) : (
        <section className="two-column-layout">
          <div className="panel-stack">
            <form className="panel form-panel" onSubmit={handleSaveGuide}>
              <div className="panel-head">
                <div>
                  <h3>{selectedMenuItem?.persianName}</h3>
                  <p className="table-subtext ltr-text">{selectedMenuItem?.englishName}</p>
                </div>
                <label className="checkbox-row">
                  <input
                    type="checkbox"
                    checked={guideForm.isPublished}
                    onChange={(event) => setGuideForm((current) => ({ ...current, isPublished: event.target.checked }))}
                  />
                  <span>منتشر شده برای تیم</span>
                </label>
              </div>

              <div className="wiki-section">
                <h4 className="wiki-section-title">معرفی و استاندارد QC</h4>
                <label>
                  <span>خلاصه محصول</span>
                  <textarea
                    rows="3"
                    value={guideForm.overview}
                    onChange={(event) => setGuideForm((current) => ({ ...current, overview: event.target.value }))}
                  />
                </label>
                <label>
                  <span>استانداردهای کیفیت (QC)</span>
                  <textarea
                    rows="4"
                    value={guideForm.qualityStandards}
                    onChange={(event) => setGuideForm((current) => ({ ...current, qualityStandards: event.target.value }))}
                    placeholder="مثلاً: دمای سرو ۴ تا ۶ درجه، کرمای یکدست، طعم متعادل..."
                  />
                </label>
                <label>
                  <span>تجهیزات مورد نیاز</span>
                  <textarea
                    rows="2"
                    value={guideForm.equipmentNotes}
                    onChange={(event) => setGuideForm((current) => ({ ...current, equipmentNotes: event.target.value }))}
                  />
                </label>
                <label>
                  <span>نکات سرو</span>
                  <textarea
                    rows="2"
                    value={guideForm.servingNotes}
                    onChange={(event) => setGuideForm((current) => ({ ...current, servingNotes: event.target.value }))}
                  />
                </label>
              </div>

              <div className="wiki-section">
                <div className="panel-head">
                  <h4 className="wiki-section-title">مراحل آماده‌سازی</h4>
                  <button type="button" className="ghost-button" onClick={addStep}>
                    افزودن مرحله
                  </button>
                </div>

                {steps.map((step, index) => (
                  <article key={`step-${index}`} className="wiki-step-card">
                    <div className="wiki-step-head">
                      <span className="wiki-step-order">{toPersianNumber(index + 1)}</span>
                      <div className="wiki-step-actions">
                        <button type="button" className="ghost-button" onClick={() => moveStep(index, -1)} disabled={index === 0}>
                          ↑
                        </button>
                        <button
                          type="button"
                          className="ghost-button"
                          onClick={() => moveStep(index, 1)}
                          disabled={index === steps.length - 1}
                        >
                          ↓
                        </button>
                        <button type="button" className="ghost-button" onClick={() => removeStep(index)}>
                          حذف
                        </button>
                      </div>
                    </div>
                    <label>
                      <span>عنوان مرحله</span>
                      <input
                        value={step.title}
                        onChange={(event) => updateStep(index, { title: event.target.value })}
                        placeholder="مثلاً: آماده‌سازی اسپرسو"
                      />
                    </label>
                    <label>
                      <span>دستورالعمل دقیق</span>
                      <textarea
                        rows="3"
                        value={step.instructions}
                        onChange={(event) => updateStep(index, { instructions: event.target.value })}
                      />
                    </label>
                    <div className="field-grid">
                      <label>
                        <span>مدت (دقیقه)</span>
                        <input
                          className="ltr-input"
                          type="number"
                          min="1"
                          value={step.durationMinutes}
                          onChange={(event) => updateStep(index, { durationMinutes: event.target.value })}
                        />
                      </label>
                      <label className="checkbox-row">
                        <input
                          type="checkbox"
                          checked={step.isQualityCheckpoint}
                          onChange={(event) => updateStep(index, { isQualityCheckpoint: event.target.checked })}
                        />
                        <span>نقطه کنترل کیفیت (QC)</span>
                      </label>
                    </div>
                  </article>
                ))}
              </div>

              <button type="submit" disabled={saving}>
                {saving ? 'در حال ذخیره...' : 'ذخیره ویکی و مراحل'}
              </button>
            </form>

            <form className="panel form-panel" onSubmit={handleAddRecipeLine}>
              <div className="panel-head">
                <div>
                  <h3>رسپی — مواد اولیه</h3>
                  <p>مقدار دقیق هر ماده برای یک واحد محصول</p>
                </div>
              </div>
              <div className="field-grid">
                <label>
                  <span>ماده اولیه</span>
                  <select
                    required
                    value={recipeLineForm.ingredientId}
                    onChange={(event) => setRecipeLineForm((current) => ({ ...current, ingredientId: event.target.value }))}
                  >
                    <option value="">-- انتخاب --</option>
                    {ingredientOptions.map((item) => (
                      <option key={item.id} value={item.id}>
                        {item.persianName} ({item.unit})
                      </option>
                    ))}
                  </select>
                </label>
                <label>
                  <span>مقدار{ingredientUnit ? ` (${ingredientUnit})` : ''}</span>
                  <input
                    className="ltr-input"
                    type="number"
                    min="0.001"
                    step="0.001"
                    required
                    value={recipeLineForm.quantity}
                    onChange={(event) => setRecipeLineForm((current) => ({ ...current, quantity: event.target.value }))}
                  />
                </label>
              </div>
              <label>
                <span>نکته آماده‌سازی این ماده (اختیاری)</span>
                <textarea
                  rows="2"
                  value={recipeLineForm.preparationNote}
                  onChange={(event) => setRecipeLineForm((current) => ({ ...current, preparationNote: event.target.value }))}
                  placeholder="مثلاً: شیر ۳.۵٪، دمای ۴ درجه"
                />
              </label>
              <button type="submit" disabled={saving}>
                {saving ? 'در حال افزودن...' : 'افزودن / بروزرسانی ماده'}
              </button>
            </form>
          </div>

          <section className="panel list-panel">
            <div className="panel-head">
              <div>
                <h3>پیش‌نمایش QC</h3>
                <p>نمای کارتی برای تیم آشپزخانه / بار</p>
              </div>
            </div>

            <div className="wiki-preview">
              <div>
                <strong>{selectedMenuItem?.persianName}</strong>
                <div className="table-subtext ltr-text">{selectedMenuItem?.englishName}</div>
              </div>

              {guideForm.overview ? <p>{guideForm.overview}</p> : null}

              {guideForm.qualityStandards ? (
                <section>
                  <h4>استانداردهای QC</h4>
                  <p>{guideForm.qualityStandards}</p>
                </section>
              ) : null}

              <section>
                <h4>رسپی (مواد اولیه)</h4>
                {recipeLines.length === 0 ? (
                  <p className="wiki-empty-hint">هنوز ماده‌ای ثبت نشده.</p>
                ) : (
                  recipeLines.map((line) => (
                    <article key={line.id} className="wiki-recipe-row">
                      <div className="wiki-recipe-head">
                        <strong>
                          {line.ingredientPersianName}{' '}
                          <span className="ltr-text">
                            — {toPersianNumber(line.quantity)} {line.unit}
                          </span>
                        </strong>
                        <button type="button" className="link-button" onClick={() => handleDeleteRecipeLine(line.id)}>
                          حذف
                        </button>
                      </div>
                      {line.preparationNote ? <p>{line.preparationNote}</p> : null}
                    </article>
                  ))
                )}
              </section>

              <section>
                <h4>مراحل آماده‌سازی</h4>
                {steps.filter((step) => step.title.trim() && step.instructions.trim()).length === 0 ? (
                  <p className="wiki-empty-hint">مرحله‌ای ثبت نشده.</p>
                ) : (
                  steps
                    .filter((step) => step.title.trim() && step.instructions.trim())
                    .map((step, index) => (
                      <article key={`preview-step-${index}`} className="wiki-preview-step">
                        <span className="wiki-step-order">{toPersianNumber(index + 1)}</span>
                        <div>
                          <strong>
                            {step.title}
                            {step.isQualityCheckpoint ? <span className="wiki-qc-badge"> QC</span> : null}
                          </strong>
                          <p>{step.instructions}</p>
                          {step.durationMinutes ? (
                            <small>{toPersianNumber(step.durationMinutes)} دقیقه</small>
                          ) : null}
                        </div>
                      </article>
                    ))
                )}
              </section>

              {guideForm.equipmentNotes ? (
                <section>
                  <h4>تجهیزات</h4>
                  <p>{guideForm.equipmentNotes}</p>
                </section>
              ) : null}

              {guideForm.servingNotes ? (
                <section>
                  <h4>نکات سرو</h4>
                  <p>{guideForm.servingNotes}</p>
                </section>
              ) : null}

              {wiki?.isPublished ? null : (
                <p className="wiki-empty-hint">این ویکی هنوز برای تیم منتشر نشده است.</p>
              )}
            </div>
          </section>
        </section>
      )}
    </section>
  )
}
