import { useCallback, useEffect, useState } from 'react'

const DEFAULT_PAGE_SIZE = 25

export function usePaginatedResource(fetcher, { enabled = true, initialPageSize = DEFAULT_PAGE_SIZE } = {}) {
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(initialPageSize)
  const [search, setSearch] = useState('')
  const [debouncedSearch, setDebouncedSearch] = useState('')
  const [data, setData] = useState([])
  const [totalCount, setTotalCount] = useState(0)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')

  useEffect(() => {
    const timeoutId = window.setTimeout(() => {
      setDebouncedSearch(search.trim())
      setPage(1)
    }, 300)

    return () => window.clearTimeout(timeoutId)
  }, [search])

  const reload = useCallback(async () => {
    if (!enabled) {
      return
    }

    setLoading(true)
    setError('')

    try {
      const result = await fetcher({ page, pageSize, search: debouncedSearch })
      setData(result.items ?? [])
      setTotalCount(result.totalCount ?? 0)
    } catch (requestError) {
      setError(requestError.message || 'بارگذاری داده‌ها با خطا مواجه شد.')
      setData([])
      setTotalCount(0)
    } finally {
      setLoading(false)
    }
  }, [debouncedSearch, enabled, fetcher, page, pageSize])

  useEffect(() => {
    reload()
  }, [reload])

  return {
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
    reload,
  }
}
