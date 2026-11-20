import { Fragment, jsxDEV as reactJsxDev } from 'react/jsx-dev-runtime'
import { PersianFieldInput, PersianFieldTextarea } from './PersianFieldControls.js'
import { NumberedTable } from './NumberedTable.js'

const EXCLUDED_INPUT_TYPES = new Set([
  'checkbox',
  'radio',
  'file',
  'hidden',
  'password',
  'email',
  'url',
  'date',
  'time',
  'datetime-local',
  'month',
  'week',
  'color',
  'range',
  'submit',
  'button',
  'reset',
  'image',
])

function resolveControlType(type, props) {
  if (type === 'table') {
    return NumberedTable
  }

  if (!props || props['data-latin-digits'] === 'true' || props['data-latin-digits'] === true) {
    return type
  }

  if (type === 'input') {
    const inputType = props.type ?? 'text'
    if (
      EXCLUDED_INPUT_TYPES.has(inputType) ||
      'data-jdp' in props ||
      props.autoComplete === 'username' ||
      props.autoComplete === 'current-password' ||
      props.autoComplete === 'new-password'
    ) {
      return type
    }
    return PersianFieldInput
  }

  if (type === 'textarea') {
    return PersianFieldTextarea
  }

  return type
}

export { Fragment }

export function jsxDEV(type, props, key, isStaticChildren, source, self) {
  return reactJsxDev(resolveControlType(type, props), props, key, isStaticChildren, source, self)
}
