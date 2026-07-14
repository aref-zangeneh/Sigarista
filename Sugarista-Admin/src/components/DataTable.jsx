import { useMemo, useState } from 'react'
import {
  flexRender,
  getCoreRowModel,
  useReactTable,
} from '@tanstack/react-table'
import './DataTable.css'

function toPersianNumber(value) {
  return new Intl.NumberFormat('fa-IR').format(value)
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

  const isServerMode = manualPagination
  const effectiveSearch = isServerMode ? searchValue : localSearch
  const effectivePageIndex = isServerMode ? pageIndex : undefined
  const effectivePageSize = isServerMode ? pageSize : undefined
  const pageCount = isServerMode
    ? Math.max(1, Math.ceil(totalCount / Math.max(pageSize, 1)))
    : undefined

  const table = useReactTable({
    data,
    columns,
    getCoreRowModel: getCoreRowModel(),
    manualPagination: isServerMode,
    pageCount,
    state: isServerMode
      ? {
          pagination: {
            pageIndex: effectivePageIndex,
            pageSize: effectivePageSize,
          },
        }
      : undefined,
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
      : undefined,
  })

  const rowCount = isServerMode ? totalCount : data.length

  const paginationControls = enablePagination ? (
    <div className="data-table-pagination">
      <div className="data-table-page-size">
        <label>
          <span>نمایش</span>
          <select
            value={pageSize}
            onChange={(event) => {
              const nextSize = Number(event.target.value)
              if (isServerMode) {
                onPageSizeChange?.(nextSize)
                onPageChange?.(1)
                return
              }

              table.setPageSize(nextSize)
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
                      onClick={canSort ? header.column.getToggleSortingHandler() : undefined}
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
                <td className="data-table-empty" colSpan={columns.length}>
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
