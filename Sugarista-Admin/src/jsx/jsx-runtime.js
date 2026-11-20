import { Fragment, jsx as reactJsx, jsxs as reactJsxs } from 'react/jsx-runtime'
import { PersianFieldInput, PersianFieldTextarea } from './PersianFieldControls.js'

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

export function jsx(type, props, key) {
  return reactJsx(resolveControlType(type, props), props, key)
}

export function jsxs(type, props, key) {
  return reactJsxs(resolveControlType(type, props), props, key)
}
