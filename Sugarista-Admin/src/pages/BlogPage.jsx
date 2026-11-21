import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { DataTable } from '../components/DataTable'
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
    status: 'Draft',
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
  const { session, menuItemOptions, setError, setMessage } = useAdmin()
  const [activeTab, setActiveTab] = useState('posts')
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
  const [uploadingCover, setUploadingCover] = useState(false)
  const [uploadingInline, setUploadingInline] = useState(false)
  const [menuSearch, setMenuSearch] = useState('')
  const mdTextareaRef = useRef(null)
  const inlineFileRef = useRef(null)
  const coverFileRef = useRef(null)
  const editorCardRef = useRef(null)

  // Category editor state
  const [categoryForm, setCategoryForm] = useState(emptyCategoryForm)
  const [categorySaving, setCategorySaving] = useState(false)

  const canCreate = hasPermission(session, PERMISSIONS.blog.create)
  const canUpdate = hasPermission(session, PERMISSIONS.blog.update)
  const canDelete = hasPermission(session, PERMISSIONS.blog.delete)

  const loadCategories = useCallback(async () => {
    if (!session?.token) return
    setCategoriesLoading(true)
    try {
      const list = await apiRequest('/admin/blog/categories', { token: session.token })
      setCategories(Array.isArray(list) ? list : [])
    } catch (err) {
      setError(getReadableError(err))
    } finally {
      setCategoriesLoading(false)
    }
  }, [session?.token, setError])

  useEffect(() => {
    loadCategories()
  }, [loadCategories])

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
    if (!q) return list.slice(0, 40)
    return list
      .filter(
        (item) =>
          (item.name ?? '').toLowerCase().includes(q) ||
          (item.englishName ?? '').toLowerCase().includes(q),
      )
      .slice(0, 40)
  }, [menuItemOptions, menuSearch])

  const openNewPost = () => {
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
    [session?.token, setError],
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
      await apiRequest(isEdit ? `/admin/blog/posts/${postForm.id}` : '/admin/blog/posts', {
        method: isEdit ? 'PUT' : 'POST',
        body: payload,
        token: session.token,
      })

      setMessage(isEdit ? 'مقاله با موفقیت ویرایش و منتشر شد.' : 'مقاله جدید با موفقیت ثبت شد.')
      setEditorOpen(false)
      setPostForm(emptyPostForm())
      setPostsReloadToken((prev) => prev + 1)
      loadCategories()
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
        loadCategories()
      } catch (err) {
        setError(getReadableError(err))
      }
    },
    [session?.token, setError, setMessage, loadCategories],
  )

  const insertMarkdownSnippet = (before, after = '', placeholder = '') => {
    const textarea = mdTextareaRef.current
    if (!textarea) {
      setPostForm((prev) => ({ ...prev, contentMarkdown: `${prev.contentMarkdown}${before}${placeholder}${after}` }))
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

  const handleCoverUpload = async (event) => {
    const file = event.target.files?.[0]
    event.target.value = ''
    if (!file || !session?.token) return
    const validationError = validateImageFile(file)
    if (validationError) {
      setError(validationError)
      return
    }
    setUploadingCover(true)
    setError('')
    try {
      const result = await uploadImage('/admin/uploads/images', file, session.token)
      if (result?.url) {
        setPostForm((prev) => ({
          ...prev,
          coverImageUrl: result.url,
          coverImageAlt: prev.coverImageAlt || prev.title,
        }))
        setMessage('تصویر شاخص به صورت خودکار به WebP فشرده و آپلود شد.')
      }
    } catch (err) {
      setError(getReadableError(err))
    } finally {
      setUploadingCover(false)
    }
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
      setMessage(isEdit ? 'دسته‌بندی ویرایش شد.' : 'دسته‌بندی جدید ایجاد شد.')
      setCategoryForm(emptyCategoryForm())
      loadCategories()
    } catch (err) {
      setError(getReadableError(err))
    } finally {
      setCategorySaving(false)
    }
  }

  const handleDeleteCategory = useCallback(
    async (cat) => {
      if (!session?.token) return
      if (!window.confirm(`دسته‌بندی «${cat.name}» حذف شود؟ (مقالات آن بدون دسته می‌مانند)`)) return
      setError('')
      try {
        await apiRequest(`/admin/blog/categories/${cat.id}`, {
          method: 'DELETE',
          token: session.token,
        })
        setMessage('دسته‌بندی حذف شد.')
        loadCategories()
      } catch (err) {
        setError(getReadableError(err))
      }
    },
    [session?.token, setError, setMessage, loadCategories],
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
                {item.isFeatured ? <span className="blog-featured-tag">برگزیده</span> : null}
              </div>
              <span className="blog-slug-pill">/mag/{item.slug}</span>
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
            <span className={`blog-status-badge blog-status-badge--${mod}`}>
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
        header: 'زمان مطالعه',
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
        cell: ({ row }) => {
          const item = row.original
          return (
            <div className="action-group">
              <a
                className="btn-secondary btn-sm"
                href={`/mag/${encodeURIComponent(item.slug)}`}
                target="_blank"
                rel="noreferrer"
              >
                نمایش زنده
              </a>
              {canUpdate ? (
                <button
                  type="button"
                  className="btn-secondary btn-sm"
                  disabled={postLoading}
                  onClick={() => openEditPost(item)}
                >
                  ویرایش
                </button>
              ) : null}
              {canDelete ? (
                <button
                  type="button"
                  className="btn-danger btn-sm"
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
          <span>
            <span
              className="blog-color-swatch"
              style={{ background: row.original.accentColor || '#7c4a2d' }}
            />
            <strong>{row.original.name}</strong>
          </span>
        ),
      },
      {
        accessorKey: 'slug',
        header: 'اسلاگ',
        cell: ({ getValue }) => <span className="blog-slug-pill">/mag/category/{getValue()}</span>,
      },
      {
        accessorKey: 'postCount',
        header: 'تعداد مقالات',
        cell: ({ getValue }) => toPersianNumber(getValue() ?? 0),
      },
      {
        accessorKey: 'isActive',
        header: 'وضعیت',
        cell: ({ getValue }) => (
          <span
            className={`blog-status-badge ${
              getValue() ? 'blog-status-badge--published' : 'blog-status-badge--draft'
            }`}
          >
            {getValue() ? 'فعال' : 'غیرفعال'}
          </span>
        ),
      },
      {
        id: 'actions',
        header: 'عملیات',
        cell: ({ row }) => {
          const cat = row.original
          return (
            <div className="action-group">
              <a
                className="btn-secondary btn-sm"
                href={`/mag/category/${encodeURIComponent(cat.slug)}`}
                target="_blank"
                rel="noreferrer"
              >
                صفحه دسته
              </a>
              {canUpdate ? (
                <button
                  type="button"
                  className="btn-secondary btn-sm"
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
                  className="btn-danger btn-sm"
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
    <div className="admin-page">
      <div className="page-header-row">
        <div>
          <h2>مجله قهوه و مدیریت بلاگ (SEO)</h2>
          <p className="muted-text">
            مدیریت مقالات تخصصی قهوه با رندر سمت سرور (SSR)، اسکیما JSON-LD، نقشه سایت خودکار و تحلیل‌گر زنده سئو گوگل.
          </p>
        </div>
        <div className="blog-page-head-actions">
          <a className="btn-secondary" href="/mag" target="_blank" rel="noreferrer">
            مشاهده مجله (/mag)
          </a>
          <a className="btn-secondary" href="/sitemap.xml" target="_blank" rel="noreferrer">
            Sitemap XML
          </a>
          <a className="btn-secondary" href="/mag/feed.xml" target="_blank" rel="noreferrer">
            فید RSS
          </a>
          {activeTab === 'posts' && canCreate ? (
            <button type="button" className="btn-primary" onClick={openNewPost}>
              + مقاله جدید
            </button>
          ) : null}
        </div>
      </div>

      <div className="blog-tabs" role="tablist" aria-label="بخش‌های بلاگ">
        <button
          type="button"
          role="tab"
          aria-selected={activeTab === 'posts'}
          className={`blog-tab ${activeTab === 'posts' ? 'is-active' : ''}`}
          onClick={() => setActiveTab('posts')}
        >
          مقالات مجله
        </button>
        <button
          type="button"
          role="tab"
          aria-selected={activeTab === 'categories'}
          className={`blog-tab ${activeTab === 'categories' ? 'is-active' : ''}`}
          onClick={() => setActiveTab('categories')}
        >
          دسته‌بندی‌ها ({toPersianNumber(categories.length)})
        </button>
      </div>

      {activeTab === 'posts' ? (
        <>
          {editorOpen ? (
            <form ref={editorCardRef} className="blog-editor-grid" onSubmit={handleSavePost}>
              <div className="blog-editor-main">
                <div className="blog-section-card">
                  <h3>
                    <span>{postForm.id ? 'ویرایش مقاله' : 'نگارش مقاله جدید'}</span>
                    <button
                      type="button"
                      className="btn-secondary btn-sm"
                      onClick={() => {
                        setEditorOpen(false)
                        setPostForm(emptyPostForm())
                      }}
                    >
                      بستن ویرایشگر
                    </button>
                  </h3>

                  <div className="form-group">
                    <label htmlFor="blog-title">عنوان مقاله (H1)</label>
                    <input
                      id="blog-title"
                      type="text"
                      required
                      maxLength={220}
                      placeholder="مثلاً: راهنمای کامل قهوه تخصصی؛ از دانه عربیکا تا دم‌آوری"
                      value={postForm.title}
                      onChange={(e) => setPostForm((prev) => ({ ...prev, title: e.target.value }))}
                    />
                  </div>

                  <div className="form-group">
                    <label htmlFor="blog-slug">اسلاگ (آدرس URL در /mag/...)</label>
                    <div className="blog-slug-row">
                      <input
                        id="blog-slug"
                        type="text"
                        maxLength={200}
                        placeholder="complete-coffee-guide"
                        value={postForm.slug}
                        onChange={(e) => setPostForm((prev) => ({ ...prev, slug: e.target.value }))}
                      />
                      <button
                        type="button"
                        className="btn-secondary btn-sm"
                        onClick={() =>
                          setPostForm((prev) => ({ ...prev, slug: normalizeBlogSlug(prev.title) }))
                        }
                      >
                        تولید از عنوان
                      </button>
                    </div>
                  </div>

                  <div className="blog-form-grid-2">
                    <div className="form-group">
                      <label htmlFor="blog-cat">دسته‌بندی</label>
                      <select
                        id="blog-cat"
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
                    </div>

                    <div className="form-group">
                      <label htmlFor="blog-author">نویسنده</label>
                      <input
                        id="blog-author"
                        type="text"
                        maxLength={120}
                        value={postForm.authorName}
                        onChange={(e) =>
                          setPostForm((prev) => ({ ...prev, authorName: e.target.value }))
                        }
                      />
                    </div>
                  </div>

                  <div className="form-group">
                    <label htmlFor="blog-excerpt">خلاصه کوتاه (لید مقاله)</label>
                    <textarea
                      id="blog-excerpt"
                      rows={3}
                      maxLength={600}
                      placeholder="چکیده‌ای جذاب برای کارت‌های مجله و شبکه‌های اجتماعی..."
                      value={postForm.excerpt}
                      onChange={(e) => setPostForm((prev) => ({ ...prev, excerpt: e.target.value }))}
                    />
                  </div>

                  <div className="form-group">
                    <label htmlFor="blog-md">محتوای مقاله (Markdown — ساخت خودکار فهرست مطالب از ## و ###)</label>
                    <div className="blog-md-toolbar">
                      <div className="blog-md-tools">
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

                      <div className="blog-md-tools">
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
                        id="blog-md"
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
                          __html: previewHtml || '<p class="muted-text">هنوز متنی وارد نشده است.</p>',
                        }}
                      />
                    )}
                  </div>
                </div>

                {/* FAQ Builder for FAQPage JSON-LD */}
                <div className="blog-section-card">
                  <h3>
                    <span>سوالات متداول مقاله (اسکیما FAQPage گوگل)</span>
                    <button
                      type="button"
                      className="btn-secondary btn-sm"
                      onClick={() =>
                        setPostForm((prev) => ({
                          ...prev,
                          faqItems: [...prev.faqItems, { question: '', answer: '' }],
                        }))
                      }
                    >
                      + افزودن سوال
                    </button>
                  </h3>

                  {postForm.faqItems.length === 0 ? (
                    <p className="muted-text">
                      افزودن ۲ تا ۵ سوال متداول باعث نمایش آکاردئونی و دریافت Rich Result در نتایج گوگل می‌شود.
                    </p>
                  ) : (
                    postForm.faqItems.map((faq, index) => (
                      <div key={index} className="blog-faq-item">
                        <input
                          type="text"
                          placeholder={`سوال ${toPersianNumber(index + 1)}`}
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
                          placeholder="پاسخ دقیق و کوتاه..."
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
                        <div>
                          <button
                            type="button"
                            className="btn-danger btn-sm"
                            onClick={() =>
                              setPostForm((prev) => ({
                                ...prev,
                                faqItems: prev.faqItems.filter((_, idx) => idx !== index),
                              }))
                            }
                          >
                            حذف سوال
                          </button>
                        </div>
                      </div>
                    ))
                  )}
                </div>

                {/* Related Cafe Menu Items */}
                <div className="blog-section-card">
                  <h3>
                    <span>آیتم‌های پیشنهادی از منوی کافه ({toPersianNumber(postForm.relatedMenuItemIds.length)} انتخاب)</span>
                  </h3>
                  <div className="form-group">
                    <input
                      type="search"
                      placeholder="جستجوی نوشیدنی یا دسر از منو..."
                      value={menuSearch}
                      onChange={(e) => setMenuSearch(e.target.value)}
                    />
                  </div>
                  <div className="blog-menu-picker-list">
                    {filteredMenuItems.map((item) => {
                      const selected = postForm.relatedMenuItemIds.includes(item.id)
                      return (
                        <button
                          key={item.id}
                          type="button"
                          className={`blog-menu-chip ${selected ? 'is-selected' : ''}`}
                          onClick={() => toggleRelatedMenuItem(item.id)}
                        >
                          <span>{selected ? '✓' : '+'}</span>
                          <span>{item.name}</span>
                        </button>
                      )
                    })}
                  </div>
                </div>
              </div>

              {/* Right Sidebar: Publish + Cover + Live SEO Analyzer */}
              <div className="blog-editor-side">
                <div className="blog-section-card">
                  <h3>انتشار و زمان‌بندی</h3>
                  <div className="form-group">
                    <label htmlFor="blog-status">وضعیت</label>
                    <select
                      id="blog-status"
                      value={postForm.status}
                      onChange={(e) => setPostForm((prev) => ({ ...prev, status: e.target.value }))}
                    >
                      <option value="Draft">پیش‌نویس</option>
                      <option value="Published">منتشرشده</option>
                      <option value="Scheduled">زمان‌بندی‌شده</option>
                    </select>
                  </div>

                  <div className="form-group">
                    <label htmlFor="blog-pub">تاریخ انتشار (اختیاری)</label>
                    <input
                      id="blog-pub"
                      type="datetime-local"
                      value={postForm.publishedAt}
                      onChange={(e) =>
                        setPostForm((prev) => ({ ...prev, publishedAt: e.target.value }))
                      }
                    />
                  </div>

                  <div className="blog-form-grid-2">
                    <div className="form-group">
                      <label htmlFor="blog-order">ترتیب نمایش</label>
                      <input
                        id="blog-order"
                        type="number"
                        min={0}
                        value={postForm.displayOrder}
                        onChange={(e) =>
                          setPostForm((prev) => ({ ...prev, displayOrder: e.target.value }))
                        }
                      />
                    </div>
                    <div className="form-group" style={{ justifyContent: 'flex-end' }}>
                      <label className="checkbox-label">
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
                  </div>

                  <div className="form-group">
                    <label htmlFor="blog-tags">برچسب‌ها (با ویرگول جدا کنید)</label>
                    <input
                      id="blog-tags"
                      type="text"
                      placeholder="قهوه، اسپرسو، عربیکا، وی ۶۰"
                      value={postForm.tagsInput}
                      onChange={(e) =>
                        setPostForm((prev) => ({ ...prev, tagsInput: e.target.value }))
                      }
                    />
                  </div>

                  <button type="submit" className="btn-primary" style={{ width: '100%' }} disabled={postSaving}>
                    {postSaving ? 'در حال ذخیره...' : postForm.id ? 'ذخیره تغییرات مقاله' : 'انتشار / ذخیره مقاله'}
                  </button>
                </div>

                {/* Cover Image */}
                <div className="blog-section-card">
                  <h3>تصویر شاخص (WebP بهینه)</h3>
                  {postForm.coverImageUrl ? (
                    <img
                      className="blog-cover-preview"
                      src={postForm.coverImageUrl}
                      alt={postForm.coverImageAlt || postForm.title}
                    />
                  ) : null}
                  <div className="form-group">
                    <button
                      type="button"
                      className="btn-secondary"
                      style={{ width: '100%' }}
                      disabled={uploadingCover}
                      onClick={() => coverFileRef.current?.click()}
                    >
                      {uploadingCover ? 'در حال آپلود و فشرده‌سازی...' : 'آپلود تصویر شاخص'}
                    </button>
                    <input
                      ref={coverFileRef}
                      type="file"
                      accept="image/*"
                      hidden
                      onChange={handleCoverUpload}
                    />
                  </div>
                  <div className="form-group">
                    <label htmlFor="blog-cover-url">آدرس تصویر</label>
                    <input
                      id="blog-cover-url"
                      type="text"
                      dir="ltr"
                      placeholder="/api/files/..."
                      value={postForm.coverImageUrl}
                      onChange={(e) =>
                        setPostForm((prev) => ({ ...prev, coverImageUrl: e.target.value }))
                      }
                    />
                  </div>
                  <div className="form-group">
                    <label htmlFor="blog-cover-alt">متن جایگزین (Alt) برای سئو تصاویر</label>
                    <input
                      id="blog-cover-alt"
                      type="text"
                      maxLength={240}
                      placeholder="فنجان اسپرسو تخصصی کافه شوگریستا"
                      value={postForm.coverImageAlt}
                      onChange={(e) =>
                        setPostForm((prev) => ({ ...prev, coverImageAlt: e.target.value }))
                      }
                    />
                  </div>
                </div>

                {/* Live SEO Analyzer & Google SERP Preview */}
                <div className="blog-section-card">
                  <h3>تحلیل زنده سئو گوگل</h3>
                  <div className="blog-seo-meter">
                    <div className={`blog-seo-ring blog-seo-ring--${seoReport.grade.level}`}>
                      {toPersianNumber(seoReport.score)}
                    </div>
                    <div>
                      <strong>{seoReport.grade.label}</strong>
                      <div className="muted-text" style={{ fontSize: '0.8rem' }}>
                        {toPersianNumber(seoReport.wordCount)} کلمه · {toPersianNumber(seoReport.readingTimeMinutes)} دقیقه مطالعه
                      </div>
                    </div>
                  </div>

                  <div className="blog-serp-box">
                    <div className="blog-serp-url">{seoReport.serp.url}</div>
                    <div className="blog-serp-title">{seoReport.serp.title}</div>
                    <div className="blog-serp-desc">{seoReport.serp.description}</div>
                  </div>

                  <div className="form-group">
                    <label htmlFor="blog-kw">کلیدواژه کانونی (Focus Keyword)</label>
                    <input
                      id="blog-kw"
                      type="text"
                      maxLength={120}
                      placeholder="مثلاً: قهوه تخصصی"
                      value={postForm.focusKeyword}
                      onChange={(e) =>
                        setPostForm((prev) => ({ ...prev, focusKeyword: e.target.value }))
                      }
                    />
                  </div>

                  <div className="form-group">
                    <label htmlFor="blog-meta-title">عنوان متا گوگل (Meta Title)</label>
                    <input
                      id="blog-meta-title"
                      type="text"
                      maxLength={160}
                      placeholder="در صورت خالی بودن از عنوان اصلی استفاده می‌شود"
                      value={postForm.metaTitle}
                      onChange={(e) =>
                        setPostForm((prev) => ({ ...prev, metaTitle: e.target.value }))
                      }
                    />
                  </div>

                  <div className="form-group">
                    <label htmlFor="blog-meta-desc">توضیحات متا گوگل (Meta Description)</label>
                    <textarea
                      id="blog-meta-desc"
                      rows={3}
                      maxLength={320}
                      placeholder="۱۱۰ تا ۱۶۵ کاراکتر برای افزایش نرخ کلیک (CTR) در گوگل..."
                      value={postForm.metaDescription}
                      onChange={(e) =>
                        setPostForm((prev) => ({ ...prev, metaDescription: e.target.value }))
                      }
                    />
                  </div>

                  <div className="form-group">
                    <label htmlFor="blog-canonical">آدرس Canonical سفارشی (اختیاری)</label>
                    <input
                      id="blog-canonical"
                      type="text"
                      dir="ltr"
                      placeholder="https://sugarista.ir/mag/..."
                      value={postForm.canonicalUrl}
                      onChange={(e) =>
                        setPostForm((prev) => ({ ...prev, canonicalUrl: e.target.value }))
                      }
                    />
                  </div>

                  <label className="checkbox-label" style={{ marginBottom: '0.85rem' }}>
                    <input
                      type="checkbox"
                      checked={postForm.noIndex}
                      onChange={(e) =>
                        setPostForm((prev) => ({ ...prev, noIndex: e.target.checked }))
                      }
                    />
                    <span>عدم ایندکس در گوگل (noindex)</span>
                  </label>

                  <ul className="blog-seo-checks">
                    {seoReport.checks.map((check) => (
                      <li key={check.id} className="blog-seo-check">
                        <span className={`blog-seo-dot blog-seo-dot--${check.status}`} />
                        <div>
                          <div>{check.label}</div>
                          <small className="muted-text">{check.hint}</small>
                        </div>
                      </li>
                    ))}
                  </ul>
                </div>
              </div>
            </form>
          ) : null}

          <div className="card" style={{ marginTop: editorOpen ? '1.2rem' : 0 }}>
            <div className="blog-filters-row">
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

            <PaginatedAdminTable
              apiPath={postsApiPath}
              token={session?.token}
              columns={postColumns}
              status={statusFilter}
              reloadToken={postsReloadToken}
              searchPlaceholder="جستجو بر اساس عنوان، اسلاگ یا کلیدواژه سئو..."
              emptyMessage="هیچ مقاله‌ای یافت نشد."
            />
          </div>
        </>
      ) : (
        <div className="blog-editor-grid">
          <div className="blog-editor-main">
            <div className="card">
              <DataTable
                data={categories}
                columns={categoryColumns}
                loading={categoriesLoading}
                emptyMessage="هنوز دسته‌بندی بلاگ ثبت نشده است."
              />
            </div>
          </div>

          <div className="blog-editor-side">
            {(canCreate || (canUpdate && categoryForm.id)) ? (
              <form className="blog-section-card" onSubmit={handleSaveCategory}>
                <h3>
                  <span>{categoryForm.id ? 'ویرایش دسته‌بندی' : 'دسته‌بندی جدید مجله'}</span>
                  {categoryForm.id ? (
                    <button
                      type="button"
                      className="btn-secondary btn-sm"
                      onClick={() => setCategoryForm(emptyCategoryForm())}
                    >
                      انصراف
                    </button>
                  ) : null}
                </h3>

                <div className="form-group">
                  <label htmlFor="cat-name">نام دسته‌بندی</label>
                  <input
                    id="cat-name"
                    type="text"
                    required
                    maxLength={120}
                    placeholder="مثلاً: دم‌آوری قهوه"
                    value={categoryForm.name}
                    onChange={(e) => setCategoryForm((prev) => ({ ...prev, name: e.target.value }))}
                  />
                </div>

                <div className="form-group">
                  <label htmlFor="cat-slug">اسلاگ (آدرس URL)</label>
                  <div className="blog-slug-row">
                    <input
                      id="cat-slug"
                      type="text"
                      maxLength={120}
                      placeholder="brewing-methods"
                      value={categoryForm.slug}
                      onChange={(e) =>
                        setCategoryForm((prev) => ({ ...prev, slug: e.target.value }))
                      }
                    />
                    <button
                      type="button"
                      className="btn-secondary btn-sm"
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
                </div>

                <div className="form-group">
                  <label htmlFor="cat-desc">توضیحات دسته</label>
                  <textarea
                    id="cat-desc"
                    rows={3}
                    maxLength={500}
                    value={categoryForm.description}
                    onChange={(e) =>
                      setCategoryForm((prev) => ({ ...prev, description: e.target.value }))
                    }
                  />
                </div>

                <div className="form-group">
                  <label htmlFor="cat-mtitle">عنوان سئو دسته (اختیاری)</label>
                  <input
                    id="cat-mtitle"
                    type="text"
                    maxLength={160}
                    value={categoryForm.metaTitle}
                    onChange={(e) =>
                      setCategoryForm((prev) => ({ ...prev, metaTitle: e.target.value }))
                    }
                  />
                </div>

                <div className="form-group">
                  <label htmlFor="cat-mdesc">توضیحات متا دسته (اختیاری)</label>
                  <textarea
                    id="cat-mdesc"
                    rows={2}
                    maxLength={320}
                    value={categoryForm.metaDescription}
                    onChange={(e) =>
                      setCategoryForm((prev) => ({ ...prev, metaDescription: e.target.value }))
                    }
                  />
                </div>

                <div className="blog-form-grid-2">
                  <div className="form-group">
                    <label htmlFor="cat-order">ترتیب نمایش</label>
                    <input
                      id="cat-order"
                      type="number"
                      min={0}
                      value={categoryForm.displayOrder}
                      onChange={(e) =>
                        setCategoryForm((prev) => ({ ...prev, displayOrder: e.target.value }))
                      }
                    />
                  </div>
                  <div className="form-group">
                    <label htmlFor="cat-color">رنگ برچسب</label>
                    <input
                      id="cat-color"
                      type="color"
                      value={categoryForm.accentColor || '#7c4a2d'}
                      onChange={(e) =>
                        setCategoryForm((prev) => ({ ...prev, accentColor: e.target.value }))
                      }
                    />
                  </div>
                </div>

                <label className="checkbox-label" style={{ marginBottom: '0.9rem' }}>
                  <input
                    type="checkbox"
                    checked={categoryForm.isActive}
                    onChange={(e) =>
                      setCategoryForm((prev) => ({ ...prev, isActive: e.target.checked }))
                    }
                  />
                  <span>دسته‌بندی فعال و قابل نمایش در مجله</span>
                </label>

                <button
                  type="submit"
                  className="btn-primary"
                  style={{ width: '100%' }}
                  disabled={categorySaving}
                >
                  {categorySaving ? 'در حال ذخیره...' : categoryForm.id ? 'ذخیره دسته‌بندی' : 'ایجاد دسته‌بندی'}
                </button>
              </form>
            ) : null}
          </div>
        </div>
      )}
    </div>
  )
}
