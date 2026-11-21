import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { DataTable } from '../components/DataTable'
import { ImageUploader } from '../components/ImageUploader'
import { PaginatedAdminTable } from '../components/PaginatedAdminTable'
import { useAdmin } from '../context/AdminContext'
import { apiRequest, getReadableError } from '../utils/adminApi'
import { toPersianNumber } from '../utils/adminFormatters'
import { hasPermission, PERMISSIONS } from '../utils/adminPermissions'
import {
  analyzeBlogSeo,
  normalizeBlogSlug,
  renderMarkdownPreview,
} from '../utils/blogSeoAnalyzer'
import { uploadImage, validateImageFile } from '../utils/imageUpload'
import './BlogPage.css'

const STATUS_LABELS = {
  Published: 'منتشرشده',
  Draft: 'پیش‌نویس',
  Scheduled: 'زمان‌بندی‌شده',
}

function emptyPostForm() {
  return {
    id: null,
    title: '',
    slug: '',
    blogCategoryId: '',
    authorName: 'تحریریه شوگریستا',
    status: 'Published',
    publishedAt: '',
    isFeatured: false,
    displayOrder: 0,
    coverImageUrl: '',
    coverImageAlt: '',
    tagsInput: '',
    excerpt: '',
    contentMarkdown: '',
    metaTitle: '',
    metaDescription: '',
    focusKeyword: '',
    canonicalUrl: '',
    noIndex: false,
    faqItems: [],
    relatedMenuItemIds: [],
  }
}

function emptyCategoryForm() {
  return {
    id: null,
    name: '',
    slug: '',
    description: '',
    metaTitle: '',
    metaDescription: '',
    accentColor: '#7c4a2d',
    displayOrder: 0,
    isActive: true,
  }
}

function toDatetimeLocal(value) {
  if (!value) return ''
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return ''
  const pad = (n) => String(n).padStart(2, '0')
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`
}

export function BlogPage() {
  const { session, menuItemOptions, setSiteSettings, setError, setMessage } = useAdmin()
  const [activeTab, setActiveTab] = useState('posts')
  const [summary, setSummary] = useState(null)
  const [isBlogEnabled, setIsBlogEnabled] = useState(true)
  const [visibilitySaving, setVisibilitySaving] = useState(false)
  const [categories, setCategories] = useState([])
  const [categoriesLoading, setCategoriesLoading] = useState(false)
  const [postsReloadToken, setPostsReloadToken] = useState(0)
  const [statusFilter, setStatusFilter] = useState('')
  const [categoryFilter, setCategoryFilter] = useState('')

  // Post editor state
  const [editorOpen, setEditorOpen] = useState(false)
  const [postForm, setPostForm] = useState(emptyPostForm)
  const [postSaving, setPostSaving] = useState(false)
  const [postLoading, setPostLoading] = useState(false)
  const [mdMode, setMdMode] = useState('edit')
  const [uploadingInline, setUploadingInline] = useState(false)
  const [menuSearch, setMenuSearch] = useState('')
  const mdTextareaRef = useRef(null)
  const inlineFileRef = useRef(null)
  const editorCardRef = useRef(null)

  // Category editor state
  const [categoryForm, setCategoryForm] = useState(emptyCategoryForm)
  const [categorySaving, setCategorySaving] = useState(false)

  const canCreate = hasPermission(session, PERMISSIONS.blog.create)
  const canUpdate = hasPermission(session, PERMISSIONS.blog.update)
  const canDelete = hasPermission(session, PERMISSIONS.blog.delete)
  const canToggleVisibility =
    canUpdate || hasPermission(session, PERMISSIONS['site-settings']?.update)

  const handleToggleBlogVisibility = async () => {
    if (!session?.token || !canToggleVisibility) return
    const nextEnabled = !isBlogEnabled
    setVisibilitySaving(true)
    setError('')
    setMessage('')
    try {
      const result = await apiRequest('/admin/blog/visibility', {
        method: 'PATCH',
        body: { isBlogEnabled: nextEnabled },
        token: session.token,
      })
      const persisted = Boolean(result?.isBlogEnabled)
      setIsBlogEnabled(persisted)
      setSummary((prev) => (prev ? { ...prev, isBlogEnabled: persisted } : prev))
      if (setSiteSettings) {
        setSiteSettings((current) => ({ ...current, isBlogEnabled: persisted }))
      }
      setMessage(
        persisted
          ? 'مجله قهوه (/mag) فعال شد؛ تمام صفحات، لینک‌های منو، فید RSS و نقشه سایت گوگل دوباره در دسترس هستند.'
          : 'مجله قهوه (/mag) غیرفعال شد؛ تمامی صفحات /mag و لینک‌های آن از سایت، منو و نقشه سایت حذف شدند.',
      )
    } catch (err) {
      setError(getReadableError(err))
    } finally {
      setVisibilitySaving(false)
    }
  }

  const loadMetadata = useCallback(async () => {
    if (!session?.token) return
    setCategoriesLoading(true)
    try {
      const [cats, stats] = await Promise.all([
        apiRequest('/admin/blog/categories', { token: session.token }),
        apiRequest('/admin/blog/summary', { token: session.token }).catch(() => null),
      ])
      setCategories(Array.isArray(cats) ? cats : [])
      if (stats) {
        setSummary(stats)
        if (typeof stats.isBlogEnabled === 'boolean') {
          setIsBlogEnabled(stats.isBlogEnabled)
        }
      }
    } catch (err) {
      setError(getReadableError(err))
    } finally {
      setCategoriesLoading(false)
    }
  }, [session?.token, setError])

  useEffect(() => {
    loadMetadata()
  }, [loadMetadata])

  const postsApiPath = useMemo(() => {
    if (!categoryFilter) return '/admin/blog/posts'
    return `/admin/blog/posts?categoryId=${encodeURIComponent(categoryFilter)}`
  }, [categoryFilter])

  const seoReport = useMemo(() => analyzeBlogSeo(postForm), [postForm])
  const previewHtml = useMemo(
    () => (mdMode === 'preview' ? renderMarkdownPreview(postForm.contentMarkdown) : ''),
    [mdMode, postForm.contentMarkdown],
  )

  const filteredMenuItems = useMemo(() => {
    const q = menuSearch.trim().toLowerCase()
    const list = Array.isArray(menuItemOptions) ? menuItemOptions : []
    if (!q) return list.slice(0, 48)
    return list
      .filter((item) => {
        const fa = (item.persianName ?? item.name ?? '').toLowerCase()
        const en = (item.englishName ?? '').toLowerCase()
        return fa.includes(q) || en.includes(q)
      })
      .slice(0, 48)
  }, [menuItemOptions, menuSearch])

  const openNewPost = () => {
    setError('')
    setMessage('')
    setPostForm(emptyPostForm())
    setMdMode('edit')
    setEditorOpen(true)
    setTimeout(() => editorCardRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' }), 40)
  }

  const openEditPost = useCallback(
    async (row) => {
      if (!session?.token) return
      setPostLoading(true)
      setError('')
      setMessage('')
      try {
        const detail = await apiRequest(`/admin/blog/posts/${row.id}`, { token: session.token })
        setPostForm({
          id: detail.id,
          title: detail.title ?? '',
          slug: detail.slug ?? '',
          blogCategoryId: detail.blogCategoryId ?? '',
          authorName: detail.authorName ?? 'تحریریه شوگریستا',
          status: detail.status ?? 'Draft',
          publishedAt: toDatetimeLocal(detail.publishedAt),
          isFeatured: Boolean(detail.isFeatured),
          displayOrder: detail.displayOrder ?? 0,
          coverImageUrl: detail.coverImageUrl ?? '',
          coverImageAlt: detail.coverImageAlt ?? '',
          tagsInput: Array.isArray(detail.tags) ? detail.tags.join('، ') : '',
          excerpt: detail.excerpt ?? '',
          contentMarkdown: detail.contentMarkdown ?? '',
          metaTitle: detail.metaTitle ?? '',
          metaDescription: detail.metaDescription ?? '',
          focusKeyword: detail.focusKeyword ?? '',
          canonicalUrl: detail.canonicalUrl ?? '',
          noIndex: Boolean(detail.noIndex),
          faqItems: Array.isArray(detail.faqItems) ? detail.faqItems : [],
          relatedMenuItemIds: Array.isArray(detail.relatedMenuItemIds)
            ? detail.relatedMenuItemIds
            : [],
        })
        setMdMode('edit')
        setEditorOpen(true)
        setTimeout(
          () => editorCardRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' }),
          40,
        )
      } catch (err) {
        setError(getReadableError(err))
      } finally {
        setPostLoading(false)
      }
    },
    [session?.token, setError, setMessage],
  )

  const handleSavePost = async (event) => {
    event.preventDefault()
    if (!session?.token) return
    if (!postForm.title.trim()) {
      setError('عنوان مقاله الزامی است.')
      return
    }
    if (!postForm.contentMarkdown.trim()) {
      setError('متن مقاله نمی‌تواند خالی باشد.')
      return
    }
    if (postForm.status === 'Scheduled' && !postForm.publishedAt) {
      setError('برای انتشار زمان‌بندی‌شده، تاریخ و ساعت انتشار را مشخص کنید.')
      return
    }

    setPostSaving(true)
    setError('')
    setMessage('')
    try {
      const tags = postForm.tagsInput
        .split(/[،,]/)
        .map((t) => t.trim())
        .filter(Boolean)

      const payload = {
        title: postForm.title.trim(),
        slug: postForm.slug.trim() || normalizeBlogSlug(postForm.title),
        contentMarkdown: postForm.contentMarkdown,
        excerpt: postForm.excerpt.trim() || null,
        blogCategoryId: postForm.blogCategoryId || null,
        coverImageUrl: postForm.coverImageUrl.trim() || null,
        coverImageAlt: postForm.coverImageAlt.trim() || null,
        authorName: postForm.authorName.trim() || 'تحریریه شوگریستا',
        tags,
        status: postForm.status,
        publishedAt: postForm.publishedAt ? new Date(postForm.publishedAt).toISOString() : null,
        isFeatured: Boolean(postForm.isFeatured),
        readingTimeMinutes: seoReport.readingTimeMinutes,
        displayOrder: Number(postForm.displayOrder) || 0,
        metaTitle: postForm.metaTitle.trim() || null,
        metaDescription: postForm.metaDescription.trim() || null,
        focusKeyword: postForm.focusKeyword.trim() || null,
        canonicalUrl: postForm.canonicalUrl.trim() || null,
        noIndex: Boolean(postForm.noIndex),
        faqItems: postForm.faqItems
          .filter((item) => item.question?.trim() && item.answer?.trim())
          .map((item) => ({ question: item.question.trim(), answer: item.answer.trim() })),
        relatedMenuItemIds: postForm.relatedMenuItemIds,
      }

      const isEdit = Boolean(postForm.id)
      const saved = await apiRequest(
        isEdit ? `/admin/blog/posts/${postForm.id}` : '/admin/blog/posts',
        {
          method: isEdit ? 'PUT' : 'POST',
          body: payload,
          token: session.token,
        },
      )

      setMessage(
        isEdit
          ? `مقاله «${saved?.title ?? postForm.title}» با موفقیت ویرایش شد.`
          : `مقاله «${saved?.title ?? postForm.title}» با موفقیت ثبت شد.`,
      )
      setEditorOpen(false)
      setPostForm(emptyPostForm())
      setPostsReloadToken((prev) => prev + 1)
      loadMetadata()
    } catch (err) {
      setError(getReadableError(err))
    } finally {
      setPostSaving(false)
    }
  }

  const handleDeletePost = useCallback(
    async (row) => {
      if (!session?.token) return
      if (!window.confirm(`آیا از حذف مقاله «${row.title}» مطمئن هستید؟`)) return
      setError('')
      setMessage('')
      try {
        await apiRequest(`/admin/blog/posts/${row.id}`, {
          method: 'DELETE',
          token: session.token,
        })
        setMessage('مقاله با موفقیت حذف شد.')
        setPostsReloadToken((prev) => prev + 1)
        loadMetadata()
      } catch (err) {
        setError(getReadableError(err))
      }
    },
    [session?.token, setError, setMessage, loadMetadata],
  )

  const insertMarkdownSnippet = (before, after = '', placeholder = '') => {
    const textarea = mdTextareaRef.current
    if (!textarea) {
      setPostForm((prev) => ({
        ...prev,
        contentMarkdown: `${prev.contentMarkdown}${before}${placeholder}${after}`,
      }))
      return
    }
    const start = textarea.selectionStart ?? 0
    const end = textarea.selectionEnd ?? 0
    const current = postForm.contentMarkdown
    const selected = current.slice(start, end) || placeholder
    const next = `${current.slice(0, start)}${before}${selected}${after}${current.slice(end)}`
    setPostForm((prev) => ({ ...prev, contentMarkdown: next }))
    setTimeout(() => {
      textarea.focus()
      const cursor = start + before.length + selected.length + after.length
      textarea.setSelectionRange(cursor, cursor)
    }, 10)
  }

  const handleInlineImageUpload = async (event) => {
    const file = event.target.files?.[0]
    event.target.value = ''
    if (!file || !session?.token) return
    const validationError = validateImageFile(file)
    if (validationError) {
      setError(validationError)
      return
    }
    setUploadingInline(true)
    setError('')
    try {
      const result = await uploadImage('/admin/uploads/images', file, session.token)
      if (result?.url) {
        insertMarkdownSnippet(`\n![توضیح تصویر قهوه](${result.url})\n`)
      }
    } catch (err) {
      setError(getReadableError(err))
    } finally {
      setUploadingInline(false)
    }
  }

  const toggleRelatedMenuItem = (id) => {
    setPostForm((prev) => {
      const exists = prev.relatedMenuItemIds.includes(id)
      return {
        ...prev,
        relatedMenuItemIds: exists
          ? prev.relatedMenuItemIds.filter((item) => item !== id)
          : [...prev.relatedMenuItemIds, id].slice(0, 8),
      }
    })
  }

  const handleSaveCategory = async (event) => {
    event.preventDefault()
    if (!session?.token) return
    if (!categoryForm.name.trim()) {
      setError('نام دسته‌بندی الزامی است.')
      return
    }
    setCategorySaving(true)
    setError('')
    setMessage('')
    try {
      const payload = {
        name: categoryForm.name.trim(),
        slug: categoryForm.slug.trim() || normalizeBlogSlug(categoryForm.name),
        description: categoryForm.description.trim(),
        metaTitle: categoryForm.metaTitle.trim() || null,
        metaDescription: categoryForm.metaDescription.trim() || null,
        accentColor: categoryForm.accentColor.trim() || null,
        displayOrder: Number(categoryForm.displayOrder) || 0,
        isActive: Boolean(categoryForm.isActive),
      }
      const isEdit = Boolean(categoryForm.id)
      await apiRequest(
        isEdit ? `/admin/blog/categories/${categoryForm.id}` : '/admin/blog/categories',
        {
          method: isEdit ? 'PUT' : 'POST',
          body: payload,
          token: session.token,
        },
      )
      setMessage(isEdit ? 'دسته‌بندی با موفقیت ویرایش شد.' : 'دسته‌بندی جدید ایجاد شد.')
      setCategoryForm(emptyCategoryForm())
      loadMetadata()
    } catch (err) {
      setError(getReadableError(err))
    } finally {
      setCategorySaving(false)
    }
  }

  const handleDeleteCategory = useCallback(
    async (cat) => {
      if (!session?.token) return
      if (!window.confirm(`دسته‌بندی «${cat.name}» حذف شود؟ (مقالات آن بدون دسته باقی می‌مانند)`)) {
        return
      }
      setError('')
      setMessage('')
      try {
        await apiRequest(`/admin/blog/categories/${cat.id}`, {
          method: 'DELETE',
          token: session.token,
        })
        setMessage('دسته‌بندی حذف شد.')
        loadMetadata()
      } catch (err) {
        setError(getReadableError(err))
      }
    },
    [session?.token, setError, setMessage, loadMetadata],
  )

  const postColumns = useMemo(
    () => [
      {
        accessorKey: 'displayOrder',
        header: 'ترتیب نمایش',
        cell: ({ getValue }) => toPersianNumber(getValue() ?? 0),
      },
      {
        accessorKey: 'title',
        header: 'عنوان مقاله',
        cell: ({ row }) => {
          const item = row.original
          return (
            <div className="blog-title-cell">
              <div>
                <strong>{item.title}</strong>
                {item.isFeatured ? (
                  <span className="blog-badge blog-badge--featured">برگزیده</span>
                ) : null}
              </div>
              <span className="blog-slug-code">/mag/{item.slug}</span>
            </div>
          )
        },
      },
      {
        accessorKey: 'categoryName',
        header: 'دسته‌بندی',
        cell: ({ getValue }) => getValue() || '—',
      },
      {
        accessorKey: 'status',
        header: 'وضعیت',
        cell: ({ getValue }) => {
          const val = getValue() || 'Draft'
          const mod = val.toLowerCase()
          return (
            <span className={`blog-badge blog-badge--${mod}`}>
              {STATUS_LABELS[val] ?? val}
            </span>
          )
        },
      },
      {
        accessorKey: 'focusKeyword',
        header: 'کلیدواژه سئو',
        cell: ({ getValue }) => getValue() || '—',
      },
      {
        accessorKey: 'readingTimeMinutes',
        header: 'مطالعه',
        cell: ({ getValue }) => `${toPersianNumber(getValue() ?? 1)} دقیقه`,
      },
      {
        accessorKey: 'viewCount',
        header: 'بازدید',
        cell: ({ getValue }) => toPersianNumber(getValue() ?? 0),
      },
      {
        id: 'actions',
        header: 'عملیات',
        enableSorting: false,
        cell: ({ row }) => {
          const item = row.original
          return (
            <div className="table-actions">
              <a
                className="link-button"
                href={`/mag/${encodeURIComponent(item.slug)}`}
                target="_blank"
                rel="noreferrer"
              >
                نمایش
              </a>
              {canUpdate ? (
                <button
                  type="button"
                  className="link-button"
                  disabled={postLoading}
                  onClick={() => openEditPost(item)}
                >
                  ویرایش
                </button>
              ) : null}
              {canDelete ? (
                <button
                  type="button"
                  className="link-button link-danger"
                  onClick={() => handleDeletePost(item)}
                >
                  حذف
                </button>
              ) : null}
            </div>
          )
        },
      },
    ],
    [canUpdate, canDelete, postLoading, openEditPost, handleDeletePost],
  )

  const categoryColumns = useMemo(
    () => [
      {
        accessorKey: 'displayOrder',
        header: 'ترتیب نمایش',
        cell: ({ getValue }) => toPersianNumber(getValue() ?? 0),
      },
      {
        accessorKey: 'name',
        header: 'نام دسته‌بندی',
        cell: ({ row }) => (
          <div>
            <span
              className="blog-color-swatch"
              style={{ background: row.original.accentColor || '#7c4a2d' }}
            />
            <strong>{row.original.name}</strong>
            <div className="blog-slug-code">/mag/category/{row.original.slug}</div>
          </div>
        ),
      },
      {
        accessorKey: 'postCount',
        header: 'مقالات',
        cell: ({ getValue }) => toPersianNumber(getValue() ?? 0),
      },
      {
        accessorKey: 'isActive',
        header: 'وضعیت',
        cell: ({ getValue }) => (
          <span
            className={`blog-badge ${
              getValue() ? 'blog-badge--published' : 'blog-badge--draft'
            }`}
          >
            {getValue() ? 'فعال' : 'غیرفعال'}
          </span>
        ),
      },
      {
        id: 'actions',
        header: 'عملیات',
        enableSorting: false,
        cell: ({ row }) => {
          const cat = row.original
          return (
            <div className="table-actions">
              <a
                className="link-button"
                href={`/mag/category/${encodeURIComponent(cat.slug)}`}
                target="_blank"
                rel="noreferrer"
              >
                نمایش
              </a>
              {canUpdate ? (
                <button
                  type="button"
                  className="link-button"
                  onClick={() =>
                    setCategoryForm({
                      id: cat.id,
                      name: cat.name ?? '',
                      slug: cat.slug ?? '',
                      description: cat.description ?? '',
                      metaTitle: cat.metaTitle ?? '',
                      metaDescription: cat.metaDescription ?? '',
                      accentColor: cat.accentColor || '#7c4a2d',
                      displayOrder: cat.displayOrder ?? 0,
                      isActive: Boolean(cat.isActive),
                    })
                  }
                >
                  ویرایش
                </button>
              ) : null}
              {canDelete ? (
                <button
                  type="button"
                  className="link-button link-danger"
                  onClick={() => handleDeleteCategory(cat)}
                >
                  حذف
                </button>
              ) : null}
            </div>
          )
        },
      },
    ],
    [canUpdate, canDelete, handleDeleteCategory],
  )

  return (
    <div className="blog-shell">
      {/* ── هدر و دسترسی سریع ─────────────────────────────── */}
      <section className="panel">
        <div className="blog-hero-bar">
          <div>
            <h3 style={{ margin: '0 0 4px', color: 'var(--heading)' }}>
              مجله قهوه و مدیریت بلاگ (SEO)
            </h3>
            <p style={{ margin: 0, color: 'var(--muted)', fontSize: '0.86rem' }}>
              مدیریت مقالات تخصصی قهوه با رندر سمت سرور (SSR)، اسکیما JSON-LD، نقشه سایت خودکار و تحلیل‌گر زنده گوگل.
            </p>
          </div>
          <div className="blog-hero-actions">
            {isBlogEnabled ? (
              <>
                <a className="blog-link-btn" href="/mag" target="_blank" rel="noreferrer">
                  مشاهده مجله (/mag)
                </a>
                <a className="blog-link-btn" href="/sitemap.xml" target="_blank" rel="noreferrer">
                  Sitemap XML
                </a>
                <a className="blog-link-btn" href="/mag/feed.xml" target="_blank" rel="noreferrer">
                  فید RSS
                </a>
              </>
            ) : null}
            {activeTab === 'posts' && canCreate ? (
              <button type="button" className="blog-primary-btn" onClick={openNewPost}>
                + مقاله جدید
              </button>
            ) : null}
          </div>
        </div>

        <div className={`blog-visibility-banner ${isBlogEnabled ? '' : 'is-disabled'}`}>
          <div className="blog-visibility-info">
            <span className={`blog-badge ${isBlogEnabled ? 'blog-badge--published' : 'blog-badge--scheduled'}`}>
              {isBlogEnabled ? 'فعال روی سایت' : 'غیرفعال و مخفی'}
            </span>
            <div>
              <strong>
                {isBlogEnabled
                  ? 'مجله قهوه (/mag) برای بازدیدکنندگان و گوگل فعال است'
                  : 'مجله قهوه (/mag) به‌طور کامل غیرفعال و از کل سایت محو شده است'}
              </strong>
              <small>
                {isBlogEnabled
                  ? 'لینک‌های مجله در منو، صفحات /mag، فید RSS و نقشه سایت (/sitemap.xml) همگی فعال‌اند.'
                  : 'مسیر /mag و زیرصفحه‌ها 404 برمی‌گردانند، لینک‌های مجله از منو حذف شده‌اند و در نقشه سایت رندر نمی‌شوند. همچنان می‌توانید مقالات را اینجا آماده کنید.'}
              </small>
            </div>
          </div>
          {canToggleVisibility ? (
            <button
              type="button"
              className={isBlogEnabled ? 'blog-danger-ghost-btn' : 'blog-enable-btn'}
              disabled={visibilitySaving}
              onClick={handleToggleBlogVisibility}
            >
              {visibilitySaving
                ? 'در حال اعمال...'
                : isBlogEnabled
                  ? 'غیرفعال کردن مجله (/mag)'
                  : 'فعال کردن مجله (/mag)'}
            </button>
          ) : null}
        </div>
      </section>

      {/* ── آمار خلاصه مجله ───────────────────────────────── */}
      {summary ? (
        <section className="stat-grid">
          <div className="stat-card">
            <span className="stat-label">کل مقالات</span>
            <strong className="stat-value">{toPersianNumber(summary.totalPosts)}</strong>
          </div>
          <div className="stat-card stat-card--ok">
            <span className="stat-label">منتشرشده در گوگل</span>
            <strong className="stat-value">{toPersianNumber(summary.publishedPosts)}</strong>
          </div>
          <div className="stat-card">
            <span className="stat-label">پیش‌نویس / زمان‌بندی</span>
            <strong className="stat-value">
              {toPersianNumber((summary.draftPosts ?? 0) + (summary.scheduledPosts ?? 0))}
            </strong>
          </div>
          <div className="stat-card">
            <span className="stat-label">کل بازدید مقالات</span>
            <strong className="stat-value">{toPersianNumber(summary.totalViews)}</strong>
          </div>
        </section>
      ) : null}

      {/* ── تب‌ها و فیلترها ───────────────────────────────── */}
      <div className="blog-tabs-bar">
        <div className="blog-tab-strip" role="tablist" aria-label="بخش‌های بلاگ">
          <button
            type="button"
            role="tab"
            aria-selected={activeTab === 'posts'}
            className={`blog-tab-btn ${activeTab === 'posts' ? 'is-active' : ''}`}
            onClick={() => setActiveTab('posts')}
          >
            مقالات مجله ({toPersianNumber(summary?.totalPosts ?? 0)})
          </button>
          <button
            type="button"
            role="tab"
            aria-selected={activeTab === 'categories'}
            className={`blog-tab-btn ${activeTab === 'categories' ? 'is-active' : ''}`}
            onClick={() => setActiveTab('categories')}
          >
            دسته‌بندی‌ها ({toPersianNumber(categories.length)})
          </button>
        </div>

        {activeTab === 'posts' && !editorOpen ? (
          <div className="blog-filters-bar">
            <select
              aria-label="فیلتر وضعیت"
              value={statusFilter}
              onChange={(e) => setStatusFilter(e.target.value)}
            >
              <option value="">همه وضعیت‌ها</option>
              <option value="Published">منتشرشده</option>
              <option value="Draft">پیش‌نویس</option>
              <option value="Scheduled">زمان‌بندی‌شده</option>
            </select>
            <select
              aria-label="فیلتر دسته‌بندی"
              value={categoryFilter}
              onChange={(e) => setCategoryFilter(e.target.value)}
            >
              <option value="">همه دسته‌بندی‌ها</option>
              {categories.map((cat) => (
                <option key={cat.id} value={cat.id}>
                  {cat.name}
                </option>
              ))}
            </select>
          </div>
        ) : null}
      </div>

      {activeTab === 'posts' ? (
        <>
          {editorOpen ? (
            <form ref={editorCardRef} className="blog-editor-grid" onSubmit={handleSavePost}>
              {/* ── ستون اصلی ویرایشگر ───────────────────── */}
              <div className="blog-editor-col">
                <section className="panel form-panel">
                  <div className="panel-head">
                    <div>
                      <h3>{postForm.id ? 'ویرایش مقاله' : 'نگارش مقاله جدید'}</h3>
                      <p>از زیرتیترهای H2 و H3 برای ساخت خودکار فهرست مطالب (TOC) استفاده کنید.</p>
                    </div>
                    <button
                      type="button"
                      className="ghost-button"
                      onClick={() => {
                        setEditorOpen(false)
                        setPostForm(emptyPostForm())
                      }}
                    >
                      بازگشت به لیست
                    </button>
                  </div>

                  <label>
                    <span>عنوان مقاله (H1)</span>
                    <input
                      type="text"
                      required
                      maxLength={220}
                      placeholder="مثلاً: راهنمای کامل قهوه تخصصی؛ از دانه عربیکا تا دم‌آوری"
                      value={postForm.title}
                      onChange={(e) => setPostForm((prev) => ({ ...prev, title: e.target.value }))}
                    />
                  </label>

                  <label>
                    <span>اسلاگ (آدرس URL در /mag/...)</span>
                    <div className="blog-slug-input-row">
                      <input
                        className="ltr-input"
                        type="text"
                        maxLength={200}
                        placeholder="complete-coffee-guide"
                        value={postForm.slug}
                        onChange={(e) => setPostForm((prev) => ({ ...prev, slug: e.target.value }))}
                      />
                      <button
                        type="button"
                        className="ghost-button"
                        onClick={() =>
                          setPostForm((prev) => ({ ...prev, slug: normalizeBlogSlug(prev.title) }))
                        }
                      >
                        تولید از عنوان
                      </button>
                    </div>
                  </label>

                  <div className="field-grid">
                    <label>
                      <span>دسته‌بندی</span>
                      <select
                        value={postForm.blogCategoryId}
                        onChange={(e) =>
                          setPostForm((prev) => ({ ...prev, blogCategoryId: e.target.value }))
                        }
                      >
                        <option value="">بدون دسته‌بندی</option>
                        {categories.map((cat) => (
                          <option key={cat.id} value={cat.id}>
                            {cat.name}
                          </option>
                        ))}
                      </select>
                    </label>

                    <label>
                      <span>نویسنده</span>
                      <input
                        type="text"
                        maxLength={120}
                        value={postForm.authorName}
                        onChange={(e) =>
                          setPostForm((prev) => ({ ...prev, authorName: e.target.value }))
                        }
                      />
                    </label>
                  </div>

                  <label>
                    <span>خلاصه کوتاه (لید مقاله)</span>
                    <textarea
                      rows={3}
                      maxLength={600}
                      placeholder="چکیده‌ای جذاب برای کارت‌های مجله و شبکه‌های اجتماعی..."
                      value={postForm.excerpt}
                      onChange={(e) => setPostForm((prev) => ({ ...prev, excerpt: e.target.value }))}
                    />
                  </label>

                  <div>
                    <span
                      style={{
                        display: 'block',
                        fontSize: '0.84rem',
                        fontWeight: 600,
                        marginBottom: 6,
                        color: 'var(--heading)',
                      }}
                    >
                      محتوای مقاله (Markdown)
                    </span>
                    <div className="blog-md-wrap">
                      <div className="blog-md-toolbar">
                        <div className="blog-md-group">
                          <button
                            type="button"
                            className="blog-md-btn"
                            onClick={() => insertMarkdownSnippet('\n## ', '\n', 'تیتر بخش جدید')}
                          >
                            H2
                          </button>
                          <button
                            type="button"
                            className="blog-md-btn"
                            onClick={() => insertMarkdownSnippet('\n### ', '\n', 'زیرتیتر')}
                          >
                            H3
                          </button>
                          <button
                            type="button"
                            className="blog-md-btn"
                            onClick={() => insertMarkdownSnippet('**', '**', 'متن مهم')}
                          >
                            بولد
                          </button>
                          <button
                            type="button"
                            className="blog-md-btn"
                            onClick={() => insertMarkdownSnippet('\n> ', '\n', 'نکته کلیدی باریستا')}
                          >
                            نقل‌قول
                          </button>
                          <button
                            type="button"
                            className="blog-md-btn"
                            onClick={() => insertMarkdownSnippet('\n- ', '\n- ', 'مورد اول')}
                          >
                            لیست
                          </button>
                          <button
                            type="button"
                            className="blog-md-btn"
                            onClick={() =>
                              insertMarkdownSnippet(
                                '\n| روش دم‌آوری | نسبت قهوه به آب | دمای آب |\n| --- | --- | --- |\n| وی ۶۰ | ۱:۱۶ | ۹۳ درجه |\n',
                              )
                            }
                          >
                            جدول
                          </button>
                          <button
                            type="button"
                            className="blog-md-btn"
                            onClick={() => insertMarkdownSnippet('[', '](/)', 'منوی کافه شوگریستا')}
                          >
                            لینک
                          </button>
                          <button
                            type="button"
                            className="blog-md-btn"
                            disabled={uploadingInline}
                            onClick={() => inlineFileRef.current?.click()}
                          >
                            {uploadingInline ? 'در حال آپلود...' : 'تصویر درون متن'}
                          </button>
                          <input
                            ref={inlineFileRef}
                            type="file"
                            accept="image/*"
                            hidden
                            onChange={handleInlineImageUpload}
                          />
                        </div>

                        <div className="blog-md-group">
                          <button
                            type="button"
                            className={`blog-md-btn ${mdMode === 'edit' ? 'is-active' : ''}`}
                            onClick={() => setMdMode('edit')}
                          >
                            ویرایش
                          </button>
                          <button
                            type="button"
                            className={`blog-md-btn ${mdMode === 'preview' ? 'is-active' : ''}`}
                            onClick={() => setMdMode('preview')}
                          >
                            پیش‌نمایش
                          </button>
                        </div>
                      </div>

                      {mdMode === 'edit' ? (
                        <textarea
                          ref={mdTextareaRef}
                          className="blog-md-textarea"
                          required
                          placeholder="## قهوه تخصصی چیست؟&#10;متن خود را اینجا بنویسید..."
                          value={postForm.contentMarkdown}
                          onChange={(e) =>
                            setPostForm((prev) => ({ ...prev, contentMarkdown: e.target.value }))
                          }
                        />
                      ) : (
                        <div
                          className="blog-md-preview"
                          dangerouslySetInnerHTML={{
                            __html:
                              previewHtml ||
                              '<p style="color:var(--muted)">هنوز متنی وارد نشده است.</p>',
                          }}
                        />
                      )}
                    </div>
                  </div>
                </section>

                {/* ── سوالات متداول (FAQPage Schema) ────────── */}
                <section className="panel form-panel">
                  <div className="panel-head">
                    <div>
                      <h3>سوالات متداول مقاله (اسکیما FAQPage گوگل)</h3>
                      <p>افزودن ۲ تا ۵ سوال متداول به نمایش آکاردئونی و دریافت Rich Result در گوگل کمک می‌کند.</p>
                    </div>
                    <button
                      type="button"
                      className="ghost-button"
                      onClick={() =>
                        setPostForm((prev) => ({
                          ...prev,
                          faqItems: [...prev.faqItems, { question: '', answer: '' }],
                        }))
                      }
                    >
                      + افزودن سوال
                    </button>
                  </div>

                  {postForm.faqItems.length > 0 ? (
                    <div className="blog-faq-list">
                      {postForm.faqItems.map((faq, index) => (
                        <div key={index} className="blog-faq-card">
                          <div className="blog-faq-card-head">
                            <span>سوال {toPersianNumber(index + 1)}</span>
                            <button
                              type="button"
                              className="link-button link-danger"
                              onClick={() =>
                                setPostForm((prev) => ({
                                  ...prev,
                                  faqItems: prev.faqItems.filter((_, idx) => idx !== index),
                                }))
                              }
                            >
                              حذف
                            </button>
                          </div>
                          <input
                            type="text"
                            placeholder="مثلاً: بهترین دانه قهوه برای اسپرسو کدام است؟"
                            value={faq.question}
                            onChange={(e) =>
                              setPostForm((prev) => ({
                                ...prev,
                                faqItems: prev.faqItems.map((item, idx) =>
                                  idx === index ? { ...item, question: e.target.value } : item,
                                ),
                              }))
                            }
                          />
                          <textarea
                            rows={2}
                            placeholder="پاسخ کوتاه، دقیق و کاربردی..."
                            value={faq.answer}
                            onChange={(e) =>
                              setPostForm((prev) => ({
                                ...prev,
                                faqItems: prev.faqItems.map((item, idx) =>
                                  idx === index ? { ...item, answer: e.target.value } : item,
                                ),
                              }))
                            }
                          />
                        </div>
                      ))}
                    </div>
                  ) : null}
                </section>

                {/* ── اتصال به آیتم‌های منوی کافه ────────────── */}
                <section className="panel form-panel">
                  <div className="panel-head">
                    <div>
                      <h3>
                        آیتم‌های پیشنهادی از منوی کافه ({toPersianNumber(postForm.relatedMenuItemIds.length)} انتخاب)
                      </h3>
                      <p>در پایین صفحه مقاله به خواننده نمایش داده می‌شوند تا مستقیم وارد منوی سفارش شود.</p>
                    </div>
                  </div>
                  <input
                    type="search"
                    placeholder="جستجوی نوشیدنی یا دسر از منو..."
                    value={menuSearch}
                    onChange={(e) => setMenuSearch(e.target.value)}
                  />
                  <div className="blog-menu-picker-grid">
                    {filteredMenuItems.length === 0 ? (
                      <span className="table-subtext">آیتمی پیدا نشد.</span>
                    ) : (
                      filteredMenuItems.map((item) => {
                        const selected = postForm.relatedMenuItemIds.includes(item.id)
                        const label = item.persianName || item.name || item.englishName
                        return (
                          <button
                            key={item.id}
                            type="button"
                            className={`blog-menu-chip ${selected ? 'is-selected' : ''}`}
                            onClick={() => toggleRelatedMenuItem(item.id)}
                          >
                            <span>{selected ? '✓' : '+'}</span>
                            <span>{label}</span>
                          </button>
                        )
                      })
                    )}
                  </div>
                </section>
              </div>

              {/* ── ستون کناری: انتشار، تصویر شاخص و تحلیل زنده سئو ── */}
              <div className="blog-editor-col blog-editor-sticky">
                <section className="panel form-panel">
                  <div className="panel-head">
                    <div>
                      <h3>انتشار و زمان‌بندی</h3>
                    </div>
                  </div>

                  <label>
                    <span>وضعیت انتشار</span>
                    <select
                      value={postForm.status}
                      onChange={(e) => setPostForm((prev) => ({ ...prev, status: e.target.value }))}
                    >
                      <option value="Published">منتشرشده (نمایش فوری در /mag)</option>
                      <option value="Draft">پیش‌نویس (مخفی)</option>
                      <option value="Scheduled">زمان‌بندی‌شده</option>
                    </select>
                  </label>

                  <label>
                    <span>
                      تاریخ و ساعت انتشار
                      {postForm.status === 'Scheduled' ? ' (الزامی برای زمان‌بندی)' : ' (اختیاری)'}
                    </span>
                    <input
                      className="ltr-input"
                      type="datetime-local"
                      required={postForm.status === 'Scheduled'}
                      value={postForm.publishedAt}
                      onChange={(e) =>
                        setPostForm((prev) => ({ ...prev, publishedAt: e.target.value }))
                      }
                    />
                  </label>

                  <div className="field-grid">
                    <label>
                      <span>ترتیب نمایش</span>
                      <input
                        className="ltr-input"
                        type="number"
                        min={0}
                        value={postForm.displayOrder}
                        onChange={(e) =>
                          setPostForm((prev) => ({ ...prev, displayOrder: e.target.value }))
                        }
                      />
                    </label>
                    <label className="checkbox-row" style={{ alignSelf: 'end', paddingBottom: 8 }}>
                      <input
                        type="checkbox"
                        checked={postForm.isFeatured}
                        onChange={(e) =>
                          setPostForm((prev) => ({ ...prev, isFeatured: e.target.checked }))
                        }
                      />
                      <span>مقاله برگزیده (Hero)</span>
                    </label>
                  </div>

                  <label>
                    <span>برچسب‌ها (با ویرگول جدا کنید)</span>
                    <input
                      type="text"
                      placeholder="قهوه، اسپرسو، عربیکا، وی ۶۰"
                      value={postForm.tagsInput}
                      onChange={(e) =>
                        setPostForm((prev) => ({ ...prev, tagsInput: e.target.value }))
                      }
                    />
                  </label>

                  <button type="submit" disabled={postSaving} style={{ width: '100%' }}>
                    {postSaving
                      ? 'در حال ذخیره...'
                      : postForm.id
                        ? 'ذخیره تغییرات مقاله'
                        : 'ذخیره و انتشار مقاله'}
                  </button>
                </section>

                {/* ── تصویر شاخص با ImageUploader بومی ───────── */}
                <section className="panel form-panel">
                  <div className="panel-head">
                    <div>
                      <h3>تصویر شاخص مقاله (WebP)</h3>
                      <p>نسبت ۱۶:۹ بهترین نمایش را در مجله و گوگل دیسکاور دارد.</p>
                    </div>
                  </div>
                  <ImageUploader
                    label="آپلود تصویر شاخص"
                    hint="تصویر به‌طور خودکار به WebP فشرده و بهینه می‌شود."
                    value={postForm.coverImageUrl}
                    token={session?.token}
                    onChange={(url) =>
                      setPostForm((prev) => ({
                        ...prev,
                        coverImageUrl: url ?? '',
                        coverImageAlt: prev.coverImageAlt || prev.title,
                      }))
                    }
                  />
                  <label>
                    <span>متن جایگزین (Alt) برای سئو تصاویر</span>
                    <input
                      type="text"
                      maxLength={240}
                      placeholder="فنجان اسپرسو تخصصی کافه شوگریستا"
                      value={postForm.coverImageAlt}
                      onChange={(e) =>
                        setPostForm((prev) => ({ ...prev, coverImageAlt: e.target.value }))
                      }
                    />
                  </label>
                </section>

                {/* ── تحلیل‌گر زنده سئو و پیش‌نمایش گوگل ─────── */}
                <section className="panel form-panel">
                  <div className="panel-head">
                    <div>
                      <h3>تحلیل زنده سئو گوگل</h3>
                    </div>
                  </div>

                  <div className="blog-seo-score-box">
                    <div className={`blog-seo-ring blog-seo-ring--${seoReport.grade.level}`}>
                      {toPersianNumber(seoReport.score)}
                    </div>
                    <div>
                      <strong style={{ color: 'var(--heading)', display: 'block' }}>
                        {seoReport.grade.label}
                      </strong>
                      <span className="table-subtext">
                        {toPersianNumber(seoReport.wordCount)} کلمه ·{' '}
                        {toPersianNumber(seoReport.readingTimeMinutes)} دقیقه مطالعه
                      </span>
                    </div>
                  </div>

                  <div className="blog-serp-card">
                    <div className="blog-serp-url">{seoReport.serp.url}</div>
                    <div className="blog-serp-title">{seoReport.serp.title}</div>
                    <div className="blog-serp-desc">{seoReport.serp.description}</div>
                  </div>

                  <label>
                    <span>کلیدواژه کانونی (Focus Keyword)</span>
                    <input
                      type="text"
                      maxLength={120}
                      placeholder="مثلاً: قهوه تخصصی"
                      value={postForm.focusKeyword}
                      onChange={(e) =>
                        setPostForm((prev) => ({ ...prev, focusKeyword: e.target.value }))
                      }
                    />
                  </label>

                  <label>
                    <span>عنوان متا گوگل (Meta Title)</span>
                    <input
                      type="text"
                      maxLength={160}
                      placeholder="در صورت خالی بودن از عنوان اصلی استفاده می‌شود"
                      value={postForm.metaTitle}
                      onChange={(e) =>
                        setPostForm((prev) => ({ ...prev, metaTitle: e.target.value }))
                      }
                    />
                  </label>

                  <label>
                    <span>توضیحات متا گوگل (Meta Description)</span>
                    <textarea
                      rows={3}
                      maxLength={320}
                      placeholder="۱۱۰ تا ۱۶۵ کاراکتر برای بالاترین نرخ کلیک (CTR) در گوگل..."
                      value={postForm.metaDescription}
                      onChange={(e) =>
                        setPostForm((prev) => ({ ...prev, metaDescription: e.target.value }))
                      }
                    />
                  </label>

                  <label>
                    <span>آدرس Canonical سفارشی (اختیاری)</span>
                    <input
                      className="ltr-input"
                      type="text"
                      placeholder="https://sugarista.ir/mag/..."
                      value={postForm.canonicalUrl}
                      onChange={(e) =>
                        setPostForm((prev) => ({ ...prev, canonicalUrl: e.target.value }))
                      }
                    />
                  </label>

                  <label className="checkbox-row">
                    <input
                      type="checkbox"
                      checked={postForm.noIndex}
                      onChange={(e) =>
                        setPostForm((prev) => ({ ...prev, noIndex: e.target.checked }))
                      }
                    />
                    <span>عدم ایندکس در گوگل (noindex)</span>
                  </label>

                  <ul className="blog-seo-checklist">
                    {seoReport.checks.map((check) => (
                      <li key={check.id} className="blog-seo-check-row">
                        <span className={`blog-seo-dot blog-seo-dot--${check.status}`} />
                        <div>
                          <div>{check.label}</div>
                          <div className="table-subtext">{check.hint}</div>
                        </div>
                      </li>
                    ))}
                  </ul>
                </section>
              </div>
            </form>
          ) : null}

          <section className="panel list-panel">
            <div className="panel-head">
              <div>
                <h3>لیست مقالات مجله</h3>
                <p>مقالات منتشرشده بلافاصله در مسیر /mag و نقشه سایت گوگل (/sitemap.xml) قرار می‌گیرند.</p>
              </div>
            </div>
            <PaginatedAdminTable
              apiPath={postsApiPath}
              token={session?.token}
              columns={postColumns}
              status={statusFilter}
              reloadToken={postsReloadToken}
              searchPlaceholder="جستجو بر اساس عنوان، اسلاگ یا کلیدواژه سئو..."
              emptyMessage="هیچ مقاله‌ای یافت نشد."
            />
          </section>
        </>
      ) : (
        <section className="two-column-layout">
          {canCreate || (canUpdate && categoryForm.id) ? (
            <form className="panel form-panel" onSubmit={handleSaveCategory}>
              <div className="panel-head">
                <div>
                  <h3>{categoryForm.id ? 'ویرایش دسته‌بندی مجله' : 'دسته‌بندی جدید مجله'}</h3>
                  <p>دسته‌بندی‌ها برای ساختاردهی موضوعی سئو (Topic Clusters) و فیلتر مقالات استفاده می‌شوند.</p>
                </div>
                {categoryForm.id ? (
                  <button
                    type="button"
                    className="ghost-button"
                    onClick={() => setCategoryForm(emptyCategoryForm())}
                  >
                    انصراف
                  </button>
                ) : null}
              </div>

              <label>
                <span>نام دسته‌بندی</span>
                <input
                  type="text"
                  required
                  maxLength={120}
                  placeholder="مثلاً: روش‌های دم‌آوری قهوه"
                  value={categoryForm.name}
                  onChange={(e) => setCategoryForm((prev) => ({ ...prev, name: e.target.value }))}
                />
              </label>

              <label>
                <span>اسلاگ (آدرس URL)</span>
                <div className="blog-slug-input-row">
                  <input
                    className="ltr-input"
                    type="text"
                    maxLength={120}
                    placeholder="brewing-methods"
                    value={categoryForm.slug}
                    onChange={(e) => setCategoryForm((prev) => ({ ...prev, slug: e.target.value }))}
                  />
                  <button
                    type="button"
                    className="ghost-button"
                    onClick={() =>
                      setCategoryForm((prev) => ({
                        ...prev,
                        slug: normalizeBlogSlug(prev.name),
                      }))
                    }
                  >
                    تولید
                  </button>
                </div>
              </label>

              <label>
                <span>توضیحات دسته</span>
                <textarea
                  rows={3}
                  maxLength={500}
                  value={categoryForm.description}
                  onChange={(e) =>
                    setCategoryForm((prev) => ({ ...prev, description: e.target.value }))
                  }
                />
              </label>

              <label>
                <span>عنوان سئو دسته (اختیاری)</span>
                <input
                  type="text"
                  maxLength={160}
                  value={categoryForm.metaTitle}
                  onChange={(e) =>
                    setCategoryForm((prev) => ({ ...prev, metaTitle: e.target.value }))
                  }
                />
              </label>

              <label>
                <span>توضیحات متا دسته (اختیاری)</span>
                <textarea
                  rows={2}
                  maxLength={320}
                  value={categoryForm.metaDescription}
                  onChange={(e) =>
                    setCategoryForm((prev) => ({ ...prev, metaDescription: e.target.value }))
                  }
                />
              </label>

              <div className="field-grid">
                <label>
                  <span>ترتیب نمایش</span>
                  <input
                    className="ltr-input"
                    type="number"
                    min={0}
                    value={categoryForm.displayOrder}
                    onChange={(e) =>
                      setCategoryForm((prev) => ({ ...prev, displayOrder: e.target.value }))
                    }
                  />
                </label>

                <label>
                  <span>رنگ برچسب</span>
                  <div className="blog-color-picker-row">
                    <input
                      type="color"
                      value={categoryForm.accentColor || '#7c4a2d'}
                      onChange={(e) =>
                        setCategoryForm((prev) => ({ ...prev, accentColor: e.target.value }))
                      }
                    />
                    <input
                      className="ltr-input"
                      type="text"
                      maxLength={30}
                      value={categoryForm.accentColor || '#7c4a2d'}
                      onChange={(e) =>
                        setCategoryForm((prev) => ({ ...prev, accentColor: e.target.value }))
                      }
                    />
                  </div>
                </label>
              </div>

              <label className="checkbox-row">
                <input
                  type="checkbox"
                  checked={categoryForm.isActive}
                  onChange={(e) =>
                    setCategoryForm((prev) => ({ ...prev, isActive: e.target.checked }))
                  }
                />
                <span>دسته‌بندی فعال و قابل نمایش در مجله</span>
              </label>

              <button type="submit" disabled={categorySaving}>
                {categorySaving
                  ? 'در حال ذخیره...'
                  : categoryForm.id
                    ? 'ذخیره دسته‌بندی'
                    : 'ایجاد دسته‌بندی'}
              </button>
            </form>
          ) : null}

          <section className="panel list-panel">
            <div className="panel-head">
              <div>
                <h3>دسته‌بندی‌های مجله</h3>
                <p>ترتیب نمایش دسته‌بندی‌ها در نوار بالای مجله (/mag) از ستون «ترتیب نمایش» خوانده می‌شود.</p>
              </div>
            </div>
            <DataTable
              data={categories}
              columns={categoryColumns}
              loading={categoriesLoading}
              emptyMessage="هنوز دسته‌بندی بلاگ ثبت نشده است."
            />
          </section>
        </section>
      )}
    </div>
  )
}
