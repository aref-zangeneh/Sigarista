import { Children, Fragment, cloneElement, createElement, isValidElement } from 'react'
import { toPersianDigits } from '../utils/persianDigits.js'

const EXISTING_ORDER_HEADERS = new Set(['ردیف', '#', 'شماره', 'شماره ردیف'])

function extractPlainText(node) {
  if (node === null || node === undefined || typeof node === 'boolean') {
    return ''
  }
  if (typeof node === 'string' || typeof node === 'number') {
    return String(node)
  }
  if (Array.isArray(node)) {
    return node.map(extractPlainText).join('')
  }
  if (isValidElement(node)) {
    return extractPlainText(node.props?.children)
  }
  return ''
}

function flattenElements(children) {
  const result = []
  Children.forEach(children, (child) => {
    if (child === null || child === undefined || typeof child === 'boolean') {
      return
    }
    if (isValidElement(child) && child.type === Fragment) {
      result.push(...flattenElements(child.props?.children))
      return
    }
    result.push(child)
  })
  return result
}

function hasExistingRowNumberColumn(theadElement) {
  const headerRows = flattenElements(theadElement.props?.children).filter(
    (child) => isValidElement(child) && child.type === 'tr',
  )
  if (headerRows.length === 0) {
    return true
  }
  const firstCells = flattenElements(headerRows[0].props?.children).filter(
    (child) => isValidElement(child) && (child.type === 'th' || child.type === 'td'),
  )
  if (firstCells.length === 0) {
    return true
  }
  const firstText = extractPlainText(firstCells[0]).trim()
  return EXISTING_ORDER_HEADERS.has(firstText)
}

function canSafelyNumberSection(sectionElement) {
  const items = flattenElements(sectionElement.props?.children)
  return items.every((item) => isValidElement(item) && item.type === 'tr')
}

function transformThead(theadElement) {
  const rows = flattenElements(theadElement.props?.children)
  const trCount = rows.filter((child) => isValidElement(child) && child.type === 'tr').length
  let added = false

  const nextRows = rows.map((child) => {
    if (!isValidElement(child) || child.type !== 'tr' || added) {
      return child
    }
    added = true
    const headerCell = createElement(
      'th',
      {
        key: '__auto_row_num_th',
        className: 'table-row-num-col',
        rowSpan: trCount > 1 ? trCount : undefined,
      },
      'ردیف',
    )
    const cells = Children.toArray(child.props?.children)
    return cloneElement(child, undefined, [headerCell, ...cells])
  })

  return cloneElement(theadElement, undefined, nextRows)
}

function transformTbodyChildren(children, counter) {
  return Children.map(children, (child) => {
    if (child === null || child === undefined || typeof child === 'boolean') {
      return child
    }
    if (isValidElement(child) && child.type === Fragment) {
      return cloneElement(child, undefined, transformTbodyChildren(child.props?.children, counter))
    }
    if (!isValidElement(child) || child.type !== 'tr') {
      return child
    }

    const cells = flattenElements(child.props?.children)
    if (
      cells.length === 1 &&
      isValidElement(cells[0]) &&
      (cells[0].type === 'td' || cells[0].type === 'th') &&
      Number(cells[0].props?.colSpan) > 1
    ) {
      const expandedCell = cloneElement(cells[0], {
        colSpan: Number(cells[0].props.colSpan) + 1,
      })
      return cloneElement(child, undefined, [expandedCell])
    }

    counter.value += 1
    const indexCell = createElement(
      'td',
      {
        key: '__auto_row_num_td',
        className: 'table-row-num-col',
      },
      toPersianDigits(counter.value),
    )
    return cloneElement(child, undefined, [indexCell, ...Children.toArray(child.props?.children)])
  })
}

function transformTfoot(tfootElement) {
  const rows = Children.map(tfootElement.props?.children, (child) => {
    if (!isValidElement(child) || child.type !== 'tr') {
      return child
    }
    const cells = flattenElements(child.props?.children)
    if (
      cells.length > 0 &&
      isValidElement(cells[0]) &&
      Number(cells[0].props?.colSpan) > 1
    ) {
      const firstExpanded = cloneElement(cells[0], {
        colSpan: Number(cells[0].props.colSpan) + 1,
      })
      return cloneElement(child, undefined, [firstExpanded, ...cells.slice(1)])
    }
    const spacer = createElement('td', {
      key: '__auto_row_num_tfoot',
      className: 'table-row-num-col',
    })
    return cloneElement(child, undefined, [spacer, ...Children.toArray(child.props?.children)])
  })
  return cloneElement(tfootElement, undefined, rows)
}

export function enhanceTableChildren(children) {
  const topLevel = flattenElements(children)
  const thead = topLevel.find((el) => isValidElement(el) && el.type === 'thead')
  if (!thead || hasExistingRowNumberColumn(thead)) {
    return children
  }

  const tbodies = topLevel.filter((el) => isValidElement(el) && el.type === 'tbody')
  if (tbodies.length > 0 && !tbodies.every(canSafelyNumberSection)) {
    return children
  }

  const counter = { value: 0 }

  function mapNode(node) {
    return Children.map(node, (child) => {
      if (!isValidElement(child)) {
        return child
      }
      if (child.type === Fragment) {
        return cloneElement(child, undefined, mapNode(child.props?.children))
      }
      if (child.type === 'thead') {
        return transformThead(child)
      }
      if (child.type === 'tbody') {
        return cloneElement(child, undefined, transformTbodyChildren(child.props?.children, counter))
      }
      if (child.type === 'tfoot') {
        return transformTfoot(child)
      }
      return child
    })
  }

  return mapNode(children)
}

export function NumberedTable(props) {
  const { children, 'data-no-row-number': optOut, ...rest } = props ?? {}
  if (optOut === 'true' || optOut === true) {
    return createElement('table', rest, children)
  }
  return createElement('table', rest, enhanceTableChildren(children))
}
