import DateObject from 'react-date-object'
import persian from 'react-date-object/calendars/persian'
import persianFa from 'react-date-object/locales/persian_fa'
import gregorian from 'react-date-object/calendars/gregorian'
import gregorianEn from 'react-date-object/locales/gregorian_en'

function normalizeGregorianInput(isoDate) {
  if (!isoDate) {
    return null
  }

  if (typeof isoDate === 'string') {
    return isoDate.slice(0, 10)
  }

  if (typeof isoDate === 'object' && isoDate.year && isoDate.month && isoDate.day) {
    const month = String(isoDate.month).padStart(2, '0')
    const day = String(isoDate.day).padStart(2, '0')
    return `${isoDate.year}-${month}-${day}`
  }

  return null
}

export function gregorianIsoToShamsiLabel(isoDate) {
  const normalized = normalizeGregorianInput(isoDate)
  if (!normalized) {
    return '—'
  }

  try {
    const date = new DateObject({
      date: normalized,
      calendar: gregorian,
      locale: gregorianEn,
    }).convert(persian, persianFa)

    return date.format('YYYY/MM/DD')
  } catch {
    return normalized
  }
}

export function shamsiTextToGregorianIso(text) {
  if (!text?.trim()) {
    return ''
  }

  try {
    const date = new DateObject({
      date: text.trim(),
      calendar: persian,
      locale: persianFa,
      format: 'YYYY/MM/DD',
    })

    if (!date.isValid) {
      return ''
    }

    return date.convert(gregorian, gregorianEn).format('YYYY-MM-DD')
  } catch {
    return ''
  }
}

export function gregorianIsoFromShamsiDateObject(dateObject) {
  if (!dateObject) {
    return ''
  }

  try {
    return dateObject.convert(gregorian, gregorianEn).format('YYYY-MM-DD')
  } catch {
    return ''
  }
}

export function shamsiDateObjectFromGregorianIso(isoDate) {
  const normalized = normalizeGregorianInput(isoDate)
  if (!normalized) {
    return undefined
  }

  try {
    return new DateObject({
      date: normalized,
      calendar: gregorian,
      locale: gregorianEn,
    }).convert(persian, persianFa)
  } catch {
    return undefined
  }
}

export { persian, persianFa }
