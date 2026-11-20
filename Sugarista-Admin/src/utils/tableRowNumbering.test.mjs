import test from 'node:test'
import assert from 'node:assert/strict'
import { Fragment, createElement } from 'react'
import { enhanceTableChildren, NumberedTable } from '../jsx/NumberedTable.js'

function getChildrenArray(el) {
  const ch = el?.props?.children
  if (ch === undefined || ch === null) return []
  return Array.isArray(ch) ? ch.flat() : [ch]
}

test('enhanceTableChildren prepends ردیف header and Persian 1-based row numbers', () => {
  const tableChildren = [
    createElement(
      'thead',
      { key: 'h' },
      createElement('tr', null, [
        createElement('th', { key: 'a' }, 'نام'),
        createElement('th', { key: 'b' }, 'مبلغ'),
      ]),
    ),
    createElement('tbody', { key: 'b' }, [
      createElement('tr', { key: 'r1' }, [
        createElement('td', { key: '1' }, 'لاته'),
        createElement('td', { key: '2' }, '۱۲۰'),
      ]),
      createElement('tr', { key: 'r2' }, [
        createElement('td', { key: '1' }, 'موکا'),
        createElement('td', { key: '2' }, '۱۴۰'),
      ]),
    ]),
  ]

  const enhanced = enhanceTableChildren(tableChildren)
  const thead = enhanced[0]
  const tbody = enhanced[1]

  const headerCells = getChildrenArray(getChildrenArray(thead)[0])
  assert.equal(headerCells.length, 3)
  assert.equal(headerCells[0].props.children, 'ردیف')

  const bodyRows = getChildrenArray(tbody)
  assert.equal(bodyRows.length, 2)
  assert.equal(getChildrenArray(bodyRows[0])[0].props.children, '۱')
  assert.equal(getChildrenArray(bodyRows[1])[0].props.children, '۲')
})

test('enhanceTableChildren skips tables that already have a ردیف or # first column', () => {
  const original = [
    createElement(
      'thead',
      { key: 'h' },
      createElement('tr', null, [
        createElement('th', { key: '0' }, 'ردیف'),
        createElement('th', { key: '1' }, 'نام'),
      ]),
    ),
    createElement(
      'tbody',
      { key: 'b' },
      createElement('tr', null, [
        createElement('td', { key: '0' }, '۱'),
        createElement('td', { key: '1' }, 'آیتم'),
      ]),
    ),
  ]

  const result = enhanceTableChildren(original)
  assert.equal(result, original)
})

test('enhanceTableChildren expands colSpan on empty-state rows instead of numbering them', () => {
  const tableChildren = [
    createElement(
      'thead',
      { key: 'h' },
      createElement('tr', null, [
        createElement('th', { key: 'a' }, 'ستون ۱'),
        createElement('th', { key: 'b' }, 'ستون ۲'),
      ]),
    ),
    createElement(
      'tbody',
      { key: 'b' },
      createElement('tr', null, createElement('td', { colSpan: 2 }, 'موردی یافت نشد')),
    ),
  ]

  const enhanced = enhanceTableChildren(tableChildren)
  const emptyRowCells = getChildrenArray(getChildrenArray(enhanced[1])[0])
  assert.equal(emptyRowCells.length, 1)
  assert.equal(emptyRowCells[0].props.colSpan, 3)
})

test('enhanceTableChildren numbers continuously across multiple tbody sections and aligns tfoot', () => {
  const tableChildren = [
    createElement(
      'thead',
      { key: 'h' },
      createElement('tr', null, [
        createElement('th', { key: 'a' }, 'انبار'),
        createElement('th', { key: 'b' }, 'ترتیب نمایش'),
      ]),
    ),
    createElement('tbody', { key: 'b1' }, [
      createElement('tr', { key: 'r1' }, [
        createElement('td', { key: '1' }, 'اصلی'),
        createElement('td', { key: '2' }, '۱'),
      ]),
    ]),
    createElement(
      Fragment,
      { key: 'frag' },
      createElement('tbody', { key: 'b2' }, [
        createElement('tr', { key: 'r2' }, [
          createElement('td', { key: '1' }, 'انبارک بار'),
          createElement('td', { key: '2' }, '۲'),
        ]),
      ]),
    ),
    createElement(
      'tfoot',
      { key: 'f' },
      createElement('tr', null, [
        createElement('td', { key: '1' }, 'جمع'),
        createElement('td', { key: '2' }, '۲'),
      ]),
    ),
  ]

  const enhanced = enhanceTableChildren(tableChildren)
  const firstRowNum = getChildrenArray(getChildrenArray(enhanced[1])[0])[0].props.children
  const secondTbody = getChildrenArray(enhanced[2])[0]
  const secondRowNum = getChildrenArray(getChildrenArray(secondTbody)[0])[0].props.children
  const tfootCells = getChildrenArray(getChildrenArray(enhanced[3])[0])

  assert.equal(firstRowNum, '۱')
  assert.equal(secondRowNum, '۲')
  assert.equal(tfootCells.length, 3)
})

test('NumberedTable respects data-no-row-number opt-out', () => {
  const rawChildren = [
    createElement('thead', { key: 'h' }, createElement('tr', null, createElement('th', null, 'عنوان'))),
    createElement('tbody', { key: 'b' }, createElement('tr', null, createElement('td', null, 'متن'))),
  ]

  const el = NumberedTable({ 'data-no-row-number': 'true', children: rawChildren })
  assert.equal(el.type, 'table')
  assert.equal(getChildrenArray(getChildrenArray(el.props.children[0])[0]).length, 1)
})
