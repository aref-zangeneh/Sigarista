import { useCallback, useEffect, useMemo, useState } from 'react'
import { useAdmin } from '../context/AdminContext'
import { DataTable } from '../components/DataTable'
import { ImageUploader } from '../components/ImageUploader.jsx'
import { hasPermission, PERMISSIONS } from '../utils/adminPermissions.js'
import { normalizeImageUrl } from '../utils/imageUrl.js'
import { toPersianDigits } from '../utils/persianDigits.js'
import {
  SERVING_TEMP_PRESETS,
  filterRecipeCards,
  formatIngredientQty,
  formatPrepDuration,
  normalizeBulletPoints,
  splitDescriptionToBullets,
} from '../utils/recipeCardHelpers.js'
import './RecipesPage.css'

export function RecipesPage() {
  const {
    session,
    menuItemOptions,
    ingredientOptions,
    recipeState,
    setRecipeState,
    saving,
    setSaving,
    setMessage,
    setError,
    recipeColumns,
    apiRequest,
    getReadableError,
    normalizeRecipeResponse,
    toPersianNumber,
    emptyRecipeLineForm,
  } = useAdmin()

  const canUpdate = hasPermission(session, PERMISSIONS.recipes?.update)
  const canCreate = hasPermission(session, PERMISSIONS.recipes?.create)

  const [activeTab, setActiveTab] = useState('gallery')
  const [gallery, setGallery] = useState({ categories: [], cards: [] })
  const [galleryLoading, setGalleryLoading] = useState(true)
  const [categoryFilter, setCategoryFilter] = useState('')
  const [modeFilter, setModeFilter] = useState('all')
  const [search, setSearch] = useState('')

  // Fullscreen Barista Card Viewer modal
  const [viewingCard, setViewingCard] = useState(null)

  // Card Editor modal
  const [editingCard, setEditingCard] = useState(null)
  const [cardForm, setCardForm] = useState(null)
  const [newBulletText, setNewBulletText] = useState('')
  const [cardSaving, setCardSaving] = useState(false)

  const loadGallery = useCallback(async () => {
    if (!session?.token) return
    setGalleryLoading(true)
    try {
      const data = await apiRequest('/admin/recipes/cards', { token: session.token })
      setGallery({
        categories: Array.isArray(data?.categories) ? data.categories : [],
        cards: Array.isArray(data?.cards) ? data.cards : [],
      })
    } catch (err) {
      setError(getReadableError(err))
    } finally {
      setGalleryLoading(false)
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [session?.token])

  useEffect(() => {
    loadGallery()
  }, [loadGallery])

  const filteredCards = useMemo(
    () => filterRecipeCards(gallery.cards, { categoryId: categoryFilter, search, mode: modeFilter }),
    [gallery.cards, categoryFilter, search, modeFilter],
  )

  const loadFormulaForMenuItem = async (menuItemId) => {
    setRecipeState((current) => ({
      ...current,
      selectedMenuItemId: menuItemId,
      description: '',
      lines: [],
      lineForm: emptyRecipeLineForm,
    }))
    if (!menuItemId) return
    try {
      const recipe = normalizeRecipeResponse(
        await apiRequest(`/admin/recipes/${menuItemId}`, { token: session.token }),
      )
      setRecipeState((current) => ({
        ...current,
        description: recipe.description,
        lines: recipe.lines,
      }))
    } catch (requestError) {
      setError(getReadableError(requestError))
    }
  }

  const openFormulaTabForItem = async (menuItemId) => {
    setActiveTab('formula')
    await loadFormulaForMenuItem(menuItemId)
  }

  const openCardEditor = (card) => {
    setEditingCard(card)
    setNewBulletText('')
    setCardForm({
      cardImageUrl: card.cardImageUrl || '',
      bulletPoints: Array.isArray(card.bulletPoints) ? [...card.bulletPoints] : [],
      servingTemp: card.servingTemp || '',
      glasswareOrPlate: card.glasswareOrPlate || '',
      prepSeconds: card.prepSeconds ? String(card.prepSeconds) : '',
      baristaTip: card.baristaTip || '',
      recipeDescription: card.recipeDescription || '',
    })
  }

  const addBulletPoint = () => {
    const clean = newBulletText.trim()
    if (!clean || !cardForm) return
    setCardForm((prev) => ({
      ...prev,
      bulletPoints: [...prev.bulletPoints, clean],
    }))
    setNewBulletText('')
  }

  const updateBulletPoint = (index, value) => {
    setCardForm((prev) => ({
      ...prev,
      bulletPoints: prev.bulletPoints.map((b, i) => (i === index ? value : b)),
    }))
  }

  const moveBulletPoint = (index, delta) => {
    setCardForm((prev) => {
      const target = index + delta
      if (target < 0 || target >= prev.bulletPoints.length) return prev
      const copy = [...prev.bulletPoints]
      const [item] = copy.splice(index, 1)
      copy.splice(target, 0, item)
      return { ...prev, bulletPoints: copy }
    })
  }

  const removeBulletPoint = (index) => {
    setCardForm((prev) => ({
      ...prev,
      bulletPoints: prev.bulletPoints.filter((_, i) => i !== index),
    }))
  }

  const handleSaveCard = async (event) => {
    event.preventDefault()
    if (!editingCard || !cardForm || cardSaving) return
    setCardSaving(true)
    setError('')
    setMessage('')
    try {
      const pendingBullet = newBulletText.trim()
      const allBullets = normalizeBulletPoints(
        pendingBullet ? [...cardForm.bulletPoints, pendingBullet] : cardForm.bulletPoints,
      )
      const prepSec = Number(cardForm.prepSeconds)
      const updated = await apiRequest(`/admin/recipes/${editingCard.menuItemId}/card`, {
        method: 'PUT',
        token: session.token,
        body: {
          cardImageUrl: cardForm.cardImageUrl.trim() || null,
          bulletPoints: allBullets,
          servingTemp: cardForm.servingTemp.trim() || null,
          glasswareOrPlate: cardForm.glasswareOrPlate.trim() || null,
          prepSeconds: Number.isFinite(prepSec) && prepSec > 0 ? Math.round(prepSec) : null,
          baristaTip: cardForm.baristaTip.trim() || null,
          recipeDescription: cardForm.recipeDescription.trim() || null,
          updateDescription: true,
        },
      })

      setGallery((prev) => ({
        ...prev,
        cards: prev.cards.map((c) => (c.menuItemId === updated.menuItemId ? updated : c)),
      }))
      if (viewingCard?.menuItemId === updated.menuItemId) {
        setViewingCard(updated)
      }
      setEditingCard(null)
      setCardForm(null)
      setNewBulletText('')
      setMessage(`رسپی کارت «${updated.persianName}» با موفقیت ذخیره شد (مقادیر مواد اولیه دست‌نخورده حفظ شدند).`)
    } catch (err) {
      setError(getReadableError(err))
    } finally {
      setCardSaving(false)
    }
  }

  return (
    <div className="rc-page" dir="rtl">
      <header className="rc-header">
        <div>
          <h2 className="rc-title">رسپی کارت‌ها و دستور پخت</h2>
          <p className="rc-subtitle">
            گالری مصور رسپی کارت‌های آنلاین برای باریستا و آشپزخانه — متصل به فرمولاسیون دقیق و مقادیر واقعی مواد اولیه.
          </p>
        </div>

        <div className="rc-tabs" role="tablist">
          <button
            type="button"
            role="tab"
            aria-selected={activeTab === 'gallery'}
            className={`rc-tab-btn ${activeTab === 'gallery' ? 'is-active' : ''}`}
            onClick={() => setActiveTab('gallery')}
          >
            گالری رسپی کارت‌ها ({toPersianDigits(gallery.cards.length)})
          </button>
          <button
            type="button"
            role="tab"
            aria-selected={activeTab === 'formula'}
            className={`rc-tab-btn ${activeTab === 'formula' ? 'is-active' : ''}`}
            onClick={() => setActiveTab('formula')}
          >
            فرمولاسیون و مقادیر مواد اولیه
          </button>
        </div>
      </header>

      {/* ═══════════════════════════════════════════════════════════════
          TAB 1: ONLINE RECIPE CARDS GALLERY
         ═══════════════════════════════════════════════════════════════ */}
      {activeTab === 'gallery' ? (
        <>
          <section className="rc-toolbar">
            <div className="rc-search-row">
              <input
                type="search"
                className="rc-search-input"
                placeholder="جستجو بر اساس نام نوشیدنی، دسر، ماده اولیه یا مرحله تهیه..."
                value={search}
                onChange={(e) => setSearch(e.target.value)}
              />
              <div className="rc-mode-pills">
                {[
                  { id: 'all', label: 'همه' },
                  { id: 'has-card', label: 'دارای رسپی' },
                  { id: 'has-formula', label: 'دارای فرمول مواد' },
                  { id: 'missing', label: 'بدون توضیحات' },
                ].map((m) => (
                  <button
                    key={m.id}
                    type="button"
                    className={`rc-mode-pill ${modeFilter === m.id ? 'is-active' : ''}`}
                    onClick={() => setModeFilter(m.id)}
                  >
                    {m.label}
                  </button>
                ))}
              </div>
            </div>

            {gallery.categories.length > 0 ? (
              <div className="rc-cat-rail" role="navigation" aria-label="فیلتر دسته‌بندی">
                <button
                  type="button"
                  className={`rc-cat-pill ${categoryFilter === '' ? 'is-active' : ''}`}
                  onClick={() => setCategoryFilter('')}
                >
                  همه دسته‌ها ({toPersianDigits(gallery.cards.length)})
                </button>
                {gallery.categories.map((cat) => (
                  <button
                    key={cat.id}
                    type="button"
                    className={`rc-cat-pill ${categoryFilter === cat.id ? 'is-active' : ''}`}
                    onClick={() => setCategoryFilter(cat.id)}
                  >
                    {cat.name} ({toPersianDigits(cat.itemCount)})
                  </button>
                ))}
              </div>
            ) : null}
          </section>

          {galleryLoading ? <p className="rc-empty">در حال بارگذاری گالری رسپی کارت‌ها…</p> : null}

          {!galleryLoading && filteredCards.length === 0 ? (
            <div className="rc-empty-card">
              <strong>آیتمی با این فیلتر یافت نشد.</strong>
            </div>
          ) : null}

          <section className="rc-gallery-grid" aria-label="گالری رسپی کارت‌ها">
            {filteredCards.map((card) => {
              const imgSrc = card.effectiveImageUrl ? normalizeImageUrl(card.effectiveImageUrl) : ''
              const prepText = formatPrepDuration(card.prepSeconds)
              const hasBullets = card.bulletPoints?.length > 0
              const previewBullets = hasBullets ? card.bulletPoints.slice(0, 4) : []

              return (
                <article key={card.menuItemId} className="rc-card">
                  {/* Top Photo Frame */}
                  <div
                    className="rc-card-media"
                    onClick={() => setViewingCard(card)}
                    role="button"
                    tabIndex={0}
                    onKeyDown={(e) => {
                      if (e.key === 'Enter' || e.key === ' ') {
                        e.preventDefault()
                        setViewingCard(card)
                      }
                    }}
                  >
                    {imgSrc ? (
                      <img src={imgSrc} alt={card.persianName} loading="lazy" />
                    ) : (
                      <div className="rc-card-placeholder">
                        <span>☕</span>
                        <small>{card.categoryName}</small>
                      </div>
                    )}
                    <div className="rc-card-media-overlay" />
                    <div className="rc-card-top-badges">
                      <span className="rc-media-badge is-cat">{card.categoryName}</span>
                      {card.servingTemp ? (
                        <span className="rc-media-badge is-temp">{card.servingTemp}</span>
                      ) : null}
                    </div>
                    {prepText ? <span className="rc-media-prep">⏱ {prepText}</span> : null}
                  </div>

                  {/* Body: Title + Live Ingredients + Bullet Instructions */}
                  <div className="rc-card-body">
                    <div className="rc-card-title-block">
                      <h3 onClick={() => setViewingCard(card)}>{card.persianName}</h3>
                      {card.englishName ? <span className="rc-en-name" dir="ltr">{card.englishName}</span> : null}
                    </div>

                    {card.glasswareOrPlate ? (
                      <div className="rc-meta-line">🥃 سرو در: <strong>{card.glasswareOrPlate}</strong></div>
                    ) : null}

                    {/* Live Formula Quantities (from RecipeItems — untouched!) */}
                    {card.ingredients?.length > 0 ? (
                      <div className="rc-ing-chips" aria-label="مقادیر مواد اولیه">
                        {card.ingredients.slice(0, 6).map((ing) => (
                          <span key={ing.id} className="rc-ing-chip">
                            <strong>{ing.ingredientName}</strong>
                            <em>{formatIngredientQty(ing.quantity, ing.unit)}</em>
                          </span>
                        ))}
                        {card.ingredients.length > 6 ? (
                          <span className="rc-ing-chip is-more">
                            +{toPersianDigits(card.ingredients.length - 6)} مورد
                          </span>
                        ) : null}
                      </div>
                    ) : null}

                    {/* Bullet Instructions */}
                    {hasBullets ? (
                      <ul className="rc-bullets">
                        {previewBullets.map((step, idx) => (
                          <li key={idx}>
                            <span className="rc-bullet-num">{toPersianDigits(idx + 1)}</span>
                            <span>{step}</span>
                          </li>
                        ))}
                        {card.bulletPoints.length > 4 ? (
                          <li className="rc-bullets-more">
                            +{toPersianDigits(card.bulletPoints.length - 4)} مرحله دیگر (کلیک برای نمایش کامل)
                          </li>
                        ) : null}
                      </ul>
                    ) : card.recipeDescription ? (
                      <p className="rc-legacy-desc">{card.recipeDescription}</p>
                    ) : (
                      <p className="rc-no-desc">هنوز توضیحات موردی برای این رسپی کارت ثبت نشده است.</p>
                    )}

                    {card.baristaTip ? (
                      <div className="rc-tip-box">
                        <span>💡</span>
                        <span>{card.baristaTip}</span>
                      </div>
                    ) : null}

                    <div className="rc-card-footer">
                      <button
                        type="button"
                        className="rc-btn-ghost"
                        onClick={() => setViewingCard(card)}
                      >
                        مشاهده کارت
                      </button>
                      {canUpdate ? (
                        <button
                          type="button"
                          className="rc-btn-primary"
                          onClick={() => openCardEditor(card)}
                        >
                          {card.hasCustomCard ? 'ویرایش کارت' : '+ تکمیل رسپی کارت'}
                        </button>
                      ) : null}
                      <button
                        type="button"
                        className="rc-btn-subtle"
                        onClick={() => openFormulaTabForItem(card.menuItemId)}
                        title="مدیریت مقادیر دقیق مواد اولیه و انبار"
                      >
                        فرمول ({toPersianDigits(card.ingredients?.length ?? 0)})
                      </button>
                    </div>
                  </div>
                </article>
              )
            })}
          </section>
        </>
      ) : null}

      {/* ═══════════════════════════════════════════════════════════════
          TAB 2: EXISTING INGREDIENT FORMULA EDITOR (100% PRESERVED!)
         ═══════════════════════════════════════════════════════════════ */}
      {activeTab === 'formula' ? (
        <section className="panel-stack">
          <section className="panel">
            <div className="panel-head">
              <div>
                <h3>انتخاب آیتم منو برای فرمولاسیون مواد اولیه</h3>
                <p>مقادیر مصرفی مواد اولیه برای کسر خودکار انبار و محاسبه بهای تمام‌شده (COGS).</p>
              </div>
            </div>
            <label>
              <span>آیتم منو</span>
              <select
                value={recipeState.selectedMenuItemId}
                onChange={(event) => loadFormulaForMenuItem(event.target.value)}
              >
                <option value="">-- انتخاب آیتم منو --</option>
                {menuItemOptions.map((item) => (
                  <option key={item.id} value={item.id}>
                    {item.persianName} — {item.englishName}
                  </option>
                ))}
              </select>
            </label>
          </section>

          {recipeState.selectedMenuItemId ? (
            <>
              <section className="panel form-panel">
                <div className="panel-head">
                  <div>
                    <h3>توضیحات دستور پخت</h3>
                    <p>شرح کامل آماده‌سازی محصول — مثلاً: «لاته با ۴۵ میلی‌لیتر اسپرسو و ۱۵۰ میلی‌لیتر شیر...»</p>
                  </div>
                  <button
                    type="button"
                    disabled={saving}
                    onClick={async () => {
                      setSaving(true)
                      setError('')
                      setMessage('')
                      try {
                        await apiRequest(`/admin/recipes/${recipeState.selectedMenuItemId}/description`, {
                          method: 'PUT',
                          token: session.token,
                          body: { description: recipeState.description.trim() || null },
                        })
                        setMessage('توضیحات دستور پخت ذخیره شد.')
                        loadGallery()
                      } catch (requestError) {
                        setError(getReadableError(requestError))
                      } finally {
                        setSaving(false)
                      }
                    }}
                  >
                    {saving ? 'در حال ذخیره...' : 'ذخیره توضیحات'}
                  </button>
                </div>
                <label>
                  <span>شرح دستور پخت</span>
                  <textarea
                    rows="4"
                    value={recipeState.description}
                    onChange={(event) =>
                      setRecipeState((current) => ({ ...current, description: event.target.value }))
                    }
                    placeholder="مثال: Ice Latte — 45ml espresso + 150ml milk + ice..."
                  />
                </label>
              </section>

              <section className="two-column-layout">
                {canCreate ? (
                  <form
                    className="panel form-panel"
                    onSubmit={async (event) => {
                      event.preventDefault()
                      if (!recipeState.lineForm.ingredientId || !recipeState.lineForm.quantity) return
                      setSaving(true)
                      setError('')
                      setMessage('')
                      try {
                        await apiRequest(`/admin/recipes/${recipeState.selectedMenuItemId}`, {
                          method: 'POST',
                          token: session.token,
                          body: {
                            menuItemId: recipeState.selectedMenuItemId,
                            ingredientId: recipeState.lineForm.ingredientId,
                            quantity: Number(recipeState.lineForm.quantity),
                            showToCustomer: recipeState.lineForm.showToCustomer !== false,
                          },
                        })
                        setRecipeState((current) => ({ ...current, lineForm: emptyRecipeLineForm }))
                        setMessage('خط دستور پخت ذخیره شد.')
                        const recipe = normalizeRecipeResponse(
                          await apiRequest(`/admin/recipes/${recipeState.selectedMenuItemId}`, {
                            token: session.token,
                          }),
                        )
                        setRecipeState((current) => ({ ...current, lines: recipe.lines }))
                        loadGallery()
                      } catch (requestError) {
                        setError(getReadableError(requestError))
                      } finally {
                        setSaving(false)
                      }
                    }}
                  >
                    <div className="panel-head">
                      <div>
                        <h3>افزودن / ویرایش ماده</h3>
                        <p>اگر این ماده قبلاً در دستور پخت بوده، مقدار آن به‌روزرسانی می‌شود.</p>
                      </div>
                    </div>
                    <label>
                      <span>ماده اولیه</span>
                      <select
                        required
                        value={recipeState.lineForm.ingredientId}
                        onChange={(event) =>
                          setRecipeState((current) => ({
                            ...current,
                            lineForm: { ...current.lineForm, ingredientId: event.target.value },
                          }))
                        }
                      >
                        <option value="">-- انتخاب قلم مصرفی --</option>
                        {(ingredientOptions ?? [])
                          .filter((ingredient) => ingredient.isActive)
                          .map((ingredient) => (
                            <option key={ingredient.id} value={ingredient.id}>
                              {ingredient.persianName} ({ingredient.unit})
                            </option>
                          ))}
                      </select>
                    </label>
                    <label>
                      <span>مقدار</span>
                      <input
                        className="ltr-input"
                        type="number"
                        min="0.001"
                        step="0.001"
                        required
                        value={recipeState.lineForm.quantity}
                        onChange={(event) =>
                          setRecipeState((current) => ({
                            ...current,
                            lineForm: { ...current.lineForm, quantity: event.target.value },
                          }))
                        }
                      />
                    </label>
                    <label className="checkbox-row">
                      <input
                        type="checkbox"
                        checked={recipeState.lineForm.showToCustomer !== false}
                        onChange={(event) =>
                          setRecipeState((current) => ({
                            ...current,
                            lineForm: { ...current.lineForm, showToCustomer: event.target.checked },
                          }))
                        }
                      />
                      <span>نمایش نام این ماده به مشتری در منو</span>
                    </label>
                    <button type="submit" disabled={saving}>
                      {saving ? 'در حال ذخیره...' : 'ذخیره در دستور پخت'}
                    </button>
                  </form>
                ) : null}

                <section className="panel list-panel">
                  <div className="panel-head">
                    <div>
                      <h3>اقلام فرمولاسیون (مواد اولیه و بسته‌بندی)</h3>
                      <p>
                        {recipeState.lines.length === 0
                          ? 'هنوز هیچ ماده‌ای ثبت نشده.'
                          : `${toPersianNumber(recipeState.lines.length)} ماده`}
                      </p>
                    </div>
                  </div>
                  <DataTable
                    data={recipeState.lines}
                    columns={recipeColumns}
                    searchPlaceholder="جستجو در دستور پخت..."
                    emptyMessage="هنوز هیچ ماده‌ای ثبت نشده."
                  />
                </section>
              </section>
            </>
          ) : null}
        </section>
      ) : null}

      {/* ═══════════════════════════════════════════════════════════════
          FULLSCREEN BARISTA RECIPE CARD VIEWER MODAL
         ═══════════════════════════════════════════════════════════════ */}
      {viewingCard ? (
        <div className="rc-modal-backdrop" onClick={() => setViewingCard(null)}>
          <div className="rc-viewer-modal" onClick={(e) => e.stopPropagation()} role="dialog" aria-modal="true">
            <div className="rc-viewer-media">
              {viewingCard.effectiveImageUrl ? (
                <img
                  src={normalizeImageUrl(viewingCard.effectiveImageUrl)}
                  alt={viewingCard.persianName}
                />
              ) : (
                <div className="rc-card-placeholder">
                  <span>☕</span>
                </div>
              )}
              <button
                type="button"
                className="rc-viewer-close"
                onClick={() => setViewingCard(null)}
                aria-label="بستن"
              >
                ×
              </button>
              <div className="rc-viewer-hero-caption">
                <span className="rc-media-badge is-cat">{viewingCard.categoryName}</span>
                {viewingCard.servingTemp ? (
                  <span className="rc-media-badge is-temp">{viewingCard.servingTemp}</span>
                ) : null}
                {viewingCard.prepSeconds ? (
                  <span className="rc-media-badge">⏱ {formatPrepDuration(viewingCard.prepSeconds)}</span>
                ) : null}
              </div>
            </div>

            <div className="rc-viewer-body">
              <div className="rc-viewer-head">
                <div>
                  <h3>{viewingCard.persianName}</h3>
                  {viewingCard.englishName ? <span className="rc-en-name" dir="ltr">{viewingCard.englishName}</span> : null}
                </div>
                {viewingCard.glasswareOrPlate ? (
                  <span className="rc-glass-pill">🥃 {viewingCard.glasswareOrPlate}</span>
                ) : null}
              </div>

              {viewingCard.ingredients?.length > 0 ? (
                <div className="rc-viewer-section">
                  <h4>مقادیر دقیق مواد اولیه</h4>
                  <div className="rc-ing-chips">
                    {viewingCard.ingredients.map((ing) => (
                      <span key={ing.id} className="rc-ing-chip is-lg">
                        <strong>{ing.ingredientName}</strong>
                        <em>{formatIngredientQty(ing.quantity, ing.unit)}</em>
                      </span>
                    ))}
                  </div>
                </div>
              ) : null}

              {viewingCard.bulletPoints?.length > 0 ? (
                <div className="rc-viewer-section">
                  <h4>مراحل و نکات تهیه (موردی)</h4>
                  <ul className="rc-bullets is-lg">
                    {viewingCard.bulletPoints.map((step, idx) => (
                      <li key={idx}>
                        <span className="rc-bullet-num">{toPersianDigits(idx + 1)}</span>
                        <span>{step}</span>
                      </li>
                    ))}
                  </ul>
                </div>
              ) : null}

              {viewingCard.recipeDescription ? (
                <div className="rc-viewer-section">
                  <h4>توضیحات تکمیلی</h4>
                  <p className="rc-viewer-desc">{viewingCard.recipeDescription}</p>
                </div>
              ) : null}

              {viewingCard.baristaTip ? (
                <div className="rc-tip-box is-lg">
                  <span>💡</span>
                  <div>
                    <strong>نکته کلیدی باریستا:</strong> {viewingCard.baristaTip}
                  </div>
                </div>
              ) : null}
            </div>

            <div className="rc-viewer-foot">
              <button type="button" className="rc-btn-ghost" onClick={() => setViewingCard(null)}>
                بستن
              </button>
              <button
                type="button"
                className="rc-btn-subtle"
                onClick={() => {
                  const id = viewingCard.menuItemId
                  setViewingCard(null)
                  openFormulaTabForItem(id)
                }}
              >
                ویرایش مقادیر مواد اولیه
              </button>
              {canUpdate ? (
                <button
                  type="button"
                  className="rc-btn-primary"
                  onClick={() => {
                    const card = viewingCard
                    setViewingCard(null)
                    openCardEditor(card)
                  }}
                >
                  ویرایش رسپی کارت
                </button>
              ) : null}
            </div>
          </div>
        </div>
      ) : null}

      {/* ═══════════════════════════════════════════════════════════════
          RECIPE CARD EDITOR MODAL
         ═══════════════════════════════════════════════════════════════ */}
      {editingCard && cardForm ? (
        <div className="rc-modal-backdrop" onClick={() => !cardSaving && setEditingCard(null)}>
          <form
            className="rc-editor-modal"
            onSubmit={handleSaveCard}
            onClick={(e) => e.stopPropagation()}
            role="dialog"
            aria-modal="true"
          >
            <div className="rc-editor-head">
              <div>
                <h3>ویرایش رسپی کارت: {editingCard.persianName}</h3>
                <p>مقادیر مواد اولیه در بخش فرمولاسیون محفوظ می‌مانند و دستکاری نمی‌شوند.</p>
              </div>
              <button
                type="button"
                className="rc-editor-close"
                onClick={() => setEditingCard(null)}
                aria-label="بستن"
              >
                ×
              </button>
            </div>

            <div className="rc-editor-body">
              <div className="rc-editor-image-box">
                <label className="rc-field-label">تصویر اختصاصی رسپی کارت (اختیاری — در صورت خالی بودن از عکس محصول استفاده می‌شود)</label>
                <ImageUploader
                  value={cardForm.cardImageUrl}
                  onChange={(url) => setCardForm((p) => ({ ...p, cardImageUrl: url || '' }))}
                  token={session?.token}
                />
              </div>

              <div className="rc-editor-row">
                <div className="rc-field">
                  <span className="rc-field-label">دمای سرو</span>
                  <div className="rc-temp-chips">
                    {SERVING_TEMP_PRESETS.map((t) => (
                      <button
                        key={t.value}
                        type="button"
                        className={`rc-temp-chip ${cardForm.servingTemp === t.value ? 'is-active' : ''}`}
                        onClick={() =>
                          setCardForm((p) => ({
                            ...p,
                            servingTemp: p.servingTemp === t.value ? '' : t.value,
                          }))
                        }
                      >
                        {t.icon} {t.label}
                      </button>
                    ))}
                  </div>
                </div>

                <label className="rc-field">
                  <span className="rc-field-label">ظرف / لیوان سرو</span>
                  <input
                    type="text"
                    maxLength={80}
                    placeholder="مثلاً: لیوان ۳۶۰ میلی‌لیتر / بشقاب دسر"
                    value={cardForm.glasswareOrPlate}
                    onChange={(e) => setCardForm((p) => ({ ...p, glasswareOrPlate: e.target.value }))}
                  />
                </label>

                <label className="rc-field">
                  <span className="rc-field-label">زمان آماده‌سازی (ثانیه)</span>
                  <input
                    type="number"
                    dir="ltr"
                    min="0"
                    max="86400"
                    placeholder="مثلاً: 120"
                    value={cardForm.prepSeconds}
                    onChange={(e) => setCardForm((p) => ({ ...p, prepSeconds: e.target.value }))}
                  />
                </label>
              </div>

              {/* Bullet Points Builder */}
              <div className="rc-bullets-builder">
                <div className="rc-bullets-builder-head">
                  <span className="rc-field-label">مراحل و توضیحات موردی رسپی</span>
                  {cardForm.recipeDescription && cardForm.bulletPoints.length === 0 ? (
                    <button
                      type="button"
                      className="rc-convert-btn"
                      onClick={() =>
                        setCardForm((p) => ({
                          ...p,
                          bulletPoints: splitDescriptionToBullets(p.recipeDescription),
                        }))
                      }
                    >
                      تبدیل توضیحات قبلی به موارد شماره‌دار ✨
                    </button>
                  ) : null}
                </div>

                <div className="rc-bullet-add-row">
                  <input
                    type="text"
                    maxLength={260}
                    placeholder="مرحله یا نکته جدید را بنویسید و Enter بزنید..."
                    value={newBulletText}
                    onChange={(e) => setNewBulletText(e.target.value)}
                    onKeyDown={(e) => {
                      if (e.key === 'Enter') {
                        e.preventDefault()
                        addBulletPoint()
                      }
                    }}
                  />
                  <button type="button" className="rc-btn-primary" onClick={addBulletPoint}>
                    + افزودن مورد
                  </button>
                </div>

                {cardForm.bulletPoints.length > 0 ? (
                  <div className="rc-bullet-edit-list">
                    {cardForm.bulletPoints.map((text, idx) => (
                      <div key={idx} className="rc-bullet-edit-item">
                        <span className="rc-bullet-num">{toPersianDigits(idx + 1)}</span>
                        <input
                          type="text"
                          maxLength={260}
                          value={text}
                          onChange={(e) => updateBulletPoint(idx, e.target.value)}
                        />
                        <button
                          type="button"
                          className="rc-icon-btn"
                          disabled={idx === 0}
                          onClick={() => moveBulletPoint(idx, -1)}
                          title="بالا"
                        >
                          ↑
                        </button>
                        <button
                          type="button"
                          className="rc-icon-btn"
                          disabled={idx === cardForm.bulletPoints.length - 1}
                          onClick={() => moveBulletPoint(idx, 1)}
                          title="پایین"
                        >
                          ↓
                        </button>
                        <button
                          type="button"
                          className="rc-icon-btn is-del"
                          onClick={() => removeBulletPoint(idx)}
                          title="حذف"
                        >
                          ×
                        </button>
                      </div>
                    ))}
                  </div>
                ) : null}
              </div>

              <label className="rc-field">
                <span className="rc-field-label">نکته کلیدی باریستا / سرآشپز (اختیاری)</span>
                <input
                  type="text"
                  maxLength={400}
                  placeholder="مثلاً: فوم شیر نباید بیش از ۵ میلی‌متر ضخامت داشته باشد..."
                  value={cardForm.baristaTip}
                  onChange={(e) => setCardForm((p) => ({ ...p, baristaTip: e.target.value }))}
                />
              </label>

              <label className="rc-field">
                <span className="rc-field-label">توضیحات متنی کلی (توضیحات قبلی محصول در صورت وجود اینجا نمایش داده می‌شود)</span>
                <textarea
                  rows={3}
                  value={cardForm.recipeDescription}
                  onChange={(e) => setCardForm((p) => ({ ...p, recipeDescription: e.target.value }))}
                  placeholder="توضیحات کلی دستور پخت..."
                />
              </label>
            </div>

            <div className="rc-editor-foot">
              <button
                type="button"
                className="rc-btn-ghost"
                disabled={cardSaving}
                onClick={() => setEditingCard(null)}
              >
                انصراف
              </button>
              <button type="submit" className="rc-btn-primary" disabled={cardSaving}>
                {cardSaving ? 'در حال ذخیره…' : 'ذخیره رسپی کارت'}
              </button>
            </div>
          </form>
        </div>
      ) : null}
    </div>
  )
}
