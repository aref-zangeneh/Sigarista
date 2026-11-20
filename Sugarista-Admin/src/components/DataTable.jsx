import { useMemo, useState } from 'react'
import {
  flexRender,
  getCoreRowModel,
  getFilteredRowModel,
  getPaginationRowModel,
  getSortedRowModel,
  useReactTable,
} from '@tanstack/react-table'
import './DataTable.css'

function toPersianNumber(value) {
  return new Intl.NumberFormat('fa-IR').format(value)
}

export function computeRowDisplayNumber(rowIndex, pageIndex = 0, pageSize = 25) {
  const safePageIndex = Math.max(0, Number(pageIndex) || 0)
  const safePageSize = Math.max(1, Number(pageSize) || 25)
  const safeRowIndex = Math.max(0, Number(rowIndex) || 0)
  return safePageIndex * safePageSize + safeRowIndex + 1
}

export function DataTable({
  data,
  columns,
  searchPlaceholder = 'جستجو در جدول...',
  pageSize = 25,
  pageSizeOptions = [7, 25, 50, 100],
  emptyMessage = 'موردی یافت نشد.',
  enableSearch = true,
  enablePagination = true,
  showRowNumber = true,
  compact = false,
  manualPagination = false,
  totalCount = 0,
  pageIndex = 0,
  onPageChange,
  onPageSizeChange,
  onSearchChange,
  searchValue = '',
  loading = false,
}) {
  const [localSearch, setLocalSearch] = useState('')
  const [sorting, setSorting] = useState([])
  const [localPagination, setLocalPagination] = useState({ pageIndex: 0, pageSize })

  const isServerMode = manualPagination
  const effectiveSearch = isServerMode ? searchValue : localSearch
  const effectivePageIndex = isServerMode ? pageIndex : localPagination.pageIndex
  const effectivePageSize = isServerMode ? pageSize : localPagination.pageSize
  const pageCount = isServerMode
    ? Math.max(1, Math.ceil(totalCount / Math.max(pageSize, 1)))
    : undefined

  const effectiveColumns = useMemo(() => {
    if (!showRowNumber) {
      return columns
    }

    const alreadyHasIndex = columns.some(
      (col) => col.id === '__rowNumber' || col.header === 'ردیف' || col.header === '#',
    )
    if (alreadyHasIndex) {
      return columns
    }

    const rowNumberColumn = {
      id: '__rowNumber',
      header: 'ردیف',
      enableSorting: false,
      size: 56,
      meta: { className: 'data-table-row-num' },
      cell: ({ row, table: tableInstance }) => {
        const currentPageIndex = isServerMode
          ? effectivePageIndex
          : tableInstance.getState().pagination.pageIndex
        const currentPageSize = isServerMode
          ? effectivePageSize
          : tableInstance.getState().pagination.pageSize
        const visibleRows = tableInstance.getRowModel().rows
        const visualIndex = visibleRows.findIndex((entry) => entry.id === row.id)
        const displayNum = computeRowDisplayNumber(
          visualIndex >= 0 ? visualIndex : row.index,
          currentPageIndex,
          currentPageSize,
        )
        return toPersianNumber(displayNum)
      },
    }

    return [rowNumberColumn, ...columns]
  }, [columns, showRowNumber, isServerMode, effectivePageIndex, effectivePageSize])

  const table = useReactTable({
    data,
    columns: effectiveColumns,
    getCoreRowModel: getCoreRowModel(),
    getSortedRowModel: getSortedRowModel(),
    ...(!isServerMode && enableSearch ? { getFilteredRowModel: getFilteredRowModel() } : {}),
    ...(!isServerMode && enablePagination ? { getPaginationRowModel: getPaginationRowModel() } : {}),
    manualPagination: isServerMode,
    pageCount,
    state: {
      sorting,
      ...(isServerMode
        ? {
            pagination: {
              pageIndex: effectivePageIndex,
              pageSize: effectivePageSize,
            },
          }
        : {
            globalFilter: localSearch,
            pagination: localPagination,
          }),
    },
    onSortingChange: setSorting,
    onGlobalFilterChange: isServerMode ? undefined : setLocalSearch,
    onPaginationChange: isServerMode
      ? (updater) => {
          const current = { pageIndex: effectivePageIndex, pageSize: effectivePageSize }
          const next = typeof updater === 'function' ? updater(current) : updater
          if (next.pageIndex !== current.pageIndex) {
            onPageChange?.(next.pageIndex + 1)
          }
          if (next.pageSize !== current.pageSize) {
            onPageSizeChange?.(next.pageSize)
            onPageChange?.(1)
          }
        }
      : setLocalPagination,
  })

  const rowCount = isServerMode ? totalCount : table.getFilteredRowModel().rows.length

  const paginationControls = enablePagination ? (
    <div className="data-table-pagination">
      <div className="data-table-page-size">
        <label>
          <span>نمایش</span>
          <select
            value={effectivePageSize}
            onChange={(event) => {
              const nextSize = Number(event.target.value)
              if (isServerMode) {
                onPageSizeChange?.(nextSize)
                onPageChange?.(1)
                return
              }

              setLocalPagination({ pageIndex: 0, pageSize: nextSize })
            }}
          >
            {pageSizeOptions.map((option) => (
              <option key={option} value={option}>
                {toPersianNumber(option)}
              </option>
            ))}
          </select>
          <span>ردیف</span>
        </label>
      </div>
      <button
        type="button"
        className="data-table-page-btn"
        onClick={() => {
          if (isServerMode) {
            onPageChange?.(effectivePageIndex ?? 0)
            return
          }

          table.previousPage()
        }}
        disabled={isServerMode ? (effectivePageIndex ?? 0) <= 0 : !table.getCanPreviousPage()}
      >
        قبلی
      </button>
      <span className="data-table-page-info">
        صفحه {toPersianNumber((isServerMode ? effectivePageIndex : table.getState().pagination.pageIndex) + 1)} از{' '}
        {toPersianNumber(isServerMode ? pageCount : Math.max(table.getPageCount(), 1))}
      </span>
      <button
        type="button"
        className="data-table-page-btn"
        onClick={() => {
          if (isServerMode) {
            onPageChange?.((effectivePageIndex ?? 0) + 2)
            return
          }

          table.nextPage()
        }}
        disabled={
          isServerMode
            ? (effectivePageIndex ?? 0) + 1 >= (pageCount ?? 1)
            : !table.getCanNextPage()
        }
      >
        بعدی
      </button>
    </div>
  ) : null

  return (
    <div className={`data-table ${compact ? 'data-table--compact' : ''}`}>
      {enableSearch ? (
        <div className="data-table-toolbar">
          <input
            className="data-table-search"
            type="search"
            value={effectiveSearch}
            onChange={(event) => {
              const value = event.target.value
              if (isServerMode) {
                onSearchChange?.(value)
                return
              }

              setLocalSearch(value)
            }}
            placeholder={searchPlaceholder}
          />
          <span className="data-table-count">
            {loading ? 'در حال بارگذاری...' : `${toPersianNumber(rowCount)} ردیف`}
          </span>
        </div>
      ) : null}

      <div className="data-table-scroll">
        <table className="data-table-grid">
          <thead>
            {table.getHeaderGroups().map((headerGroup) => (
              <tr key={headerGroup.id}>
                {headerGroup.headers.map((header) => {
                  const canSort = header.column.getCanSort()
                  const sorted = header.column.getIsSorted()

                  return (
                    <th
                      key={header.id}
                      className={canSort ? 'data-table-th--sortable' : undefined}
                      style={{ width: header.column.columnDef.size }}
                      tabIndex={canSort ? 0 : undefined}
                      aria-sort={
                        canSort
                          ? sorted === 'asc'
                            ? 'ascending'
                            : sorted === 'desc'
                              ? 'descending'
                              : 'none'
                          : undefined
                      }
                      onClick={canSort ? header.column.getToggleSortingHandler() : undefined}
                      onKeyDown={
                        canSort
                          ? (event) => {
                              if (event.key === 'Enter' || event.key === ' ') {
                                event.preventDefault()
                                header.column.getToggleSortingHandler()?.(event)
                              }
                            }
                          : undefined
                      }
                    >
                      <span className="data-table-th-content">
                        {header.isPlaceholder
                          ? null
                          : flexRender(header.column.columnDef.header, header.getContext())}
                        {canSort ? (
                          <span className={`data-table-sort ${sorted ? 'data-table-sort--active' : ''}`}>
                            {sorted === 'asc' ? '▲' : sorted === 'desc' ? '▼' : '↕'}
                          </span>
                        ) : null}
                      </span>
                    </th>
                  )
                })}
              </tr>
            ))}
          </thead>
          <tbody>
            {table.getRowModel().rows.length === 0 ? (
              <tr>
                <td
                  className="data-table-empty"
                  colSpan={table.getVisibleLeafColumns().length}
                  role="status"
                  aria-live="polite"
                >
                  {loading ? 'در حال بارگذاری...' : emptyMessage}
                </td>
              </tr>
            ) : (
              table.getRowModel().rows.map((row) => (
                <tr key={row.id}>
                  {row.getVisibleCells().map((cell) => (
                    <td key={cell.id} className={cell.column.columnDef.meta?.className}>
                      {flexRender(cell.column.columnDef.cell, cell.getContext())}
                    </td>
                  ))}
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>

      {paginationControls}
    </div>
  )
}

export function useDataTableColumns(factory, deps) {
  return useMemo(factory, deps)
}

export function formatMoney(value) {
  return new Intl.NumberFormat('fa-IR', {
    minimumFractionDigits: 0,
    maximumFractionDigits: 2,
  }).format(Number(value || 0))
}
