import { useCallback } from 'react'
import { DataTable } from './DataTable'
import { usePaginatedResource } from '../hooks/usePaginatedResource'

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? '/api'

async function fetchPaginated(path, { page, pageSize, search, status, token }) {
  const params = new URLSearchParams({
    page: String(page),
    pageSize: String(pageSize),
  })

  if (search) {
    params.set('search', search)
  }

  if (status) {
    params.set('status', status)
  }

  const separator = path.includes('?') ? '&' : '?'
  const response = await fetch(`${API_BASE_URL}${path}${separator}${params.toString()}`, {
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
  })

  if (!response.ok) {
    let message = `درخواست با خطای ${response.status} مواجه شد.`
    try {
      const payload = await response.json()
      message = payload.detail || payload.title || message
    } catch {
      // keep fallback
    }

    throw new Error(message)
  }

  return response.json()
}

export function PaginatedAdminTable({
  apiPath,
  token,
  columns,
  enabled = true,
  searchPlaceholder,
  reloadToken = 0,
  emptyMessage,
  status,
  onStatusChange,
}) {
  const fetchPage = useCallback(
    async ({ page, pageSize, search }) => {
      const result = await fetchPaginated(apiPath, { page, pageSize, search, status, token })
      return {
        items: result.items ?? [],
        totalCount: result.totalCount ?? 0,
      }
    },
    [apiPath, token, reloadToken, status],
  )

  const {
    data,
    totalCount,
    page,
    pageSize,
    search,
    loading,
    error,
    setPage,
    setPageSize,
    setSearch,
  } = usePaginatedResource(fetchPage, { enabled: enabled && Boolean(token) })

  return (
    <div className="paginated-admin-table">
      {error ? <p className="feedback feedback-error">{error}</p> : null}
      {onStatusChange ? (
        <label className="paginated-admin-table-status">
          <span>وضعیت</span>
          <select
            value={status ?? ''}
            onChange={(event) => onStatusChange(event.target.value)}
          >
            <option value="">همه</option>
            <option value="active">فعال</option>
            <option value="scheduled">زمان‌بندی‌شده</option>
            <option value="expired">منقضی</option>
            <option value="inactive">غیرفعال</option>
          </select>
        </label>
      ) : null}
      <DataTable
      manualPagination
      data={data}
      columns={columns}
      totalCount={totalCount}
      pageIndex={page - 1}
      pageSize={pageSize}
      onPageChange={setPage}
      onPageSizeChange={setPageSize}
      onSearchChange={setSearch}
      searchValue={search}
      loading={loading}
      searchPlaceholder={searchPlaceholder}
      emptyMessage={emptyMessage}
    />
    </div>
  )
}
