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

export function DataTable({
  data,
  columns,
  searchPlaceholder = 'جستجو در جدول...',
  pageSize = 10,
  emptyMessage = 'موردی یافت نشد.',
  enableSearch = true,
  enablePagination = true,
  compact = false,
}) {
  const [globalFilter, setGlobalFilter] = useState('')

  const table = useReactTable({
    data,
    columns,
    state: { globalFilter },
    onGlobalFilterChange: setGlobalFilter,
    getCoreRowModel: getCoreRowModel(),
    getFilteredRowModel: getFilteredRowModel(),
    getSortedRowModel: getSortedRowModel(),
    getPaginationRowModel: getPaginationRowModel(),
    initialState: {
      pagination: { pageSize },
    },
  })

  const filteredCount = table.getFilteredRowModel().rows.length
  const pageCount = table.getPageCount()
  const pageIndex = table.getState().pagination.pageIndex

  return (
    <div className={`data-table ${compact ? 'data-table--compact' : ''}`}>
      {enableSearch ? (
        <div className="data-table-toolbar">
          <input
            className="data-table-search"
            type="search"
            value={globalFilter}
            onChange={(event) => setGlobalFilter(event.target.value)}
            placeholder={searchPlaceholder}
          />
          <span className="data-table-count">
            {toPersianNumber(filteredCount)} ردیف
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
                  {emptyMessage}
                </td>
              </tr>
            ) : (
              table.getRowModel().rows.map((row) => (
                <tr key={row.id}>
                  {row.getVisibleCells().map((cell) => (
                    <td
                      key={cell.id}
                      className={cell.column.columnDef.meta?.className}
                    >
                      {flexRender(cell.column.columnDef.cell, cell.getContext())}
                    </td>
                  ))}
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>

      {enablePagination && filteredCount > pageSize ? (
        <div className="data-table-pagination">
          <button
            type="button"
            className="data-table-page-btn"
            onClick={() => table.previousPage()}
            disabled={!table.getCanPreviousPage()}
          >
            قبلی
          </button>
          <span className="data-table-page-info">
            صفحه {toPersianNumber(pageIndex + 1)} از {toPersianNumber(Math.max(pageCount, 1))}
          </span>
          <button
            type="button"
            className="data-table-page-btn"
            onClick={() => table.nextPage()}
            disabled={!table.getCanNextPage()}
          >
            بعدی
          </button>
        </div>
      ) : null}
    </div>
  )
}

export function useDataTableColumns(factory, deps) {
  return useMemo(factory, deps)
}
