export function toPersianNumber(value) {
  return new Intl.NumberFormat('fa-IR').format(value)
}

export function formatDuration(minutes) {
  if (!minutes) {
    return 'کمتر از ۱ دقیقه'
  }

  const hours = Math.floor(minutes / 60)
  const remainingMinutes = minutes % 60

  if (hours === 0) {
    return `${toPersianNumber(remainingMinutes)} دقیقه`
  }

  if (remainingMinutes === 0) {
    return `${toPersianNumber(hours)} ساعت`
  }

  return `${toPersianNumber(hours)} ساعت و ${toPersianNumber(remainingMinutes)} دقیقه`
}

export function formatDateTime(value) {
  if (!value) {
    return 'ثبت نشده'
  }

  return new Intl.DateTimeFormat('fa-IR', {
    dateStyle: 'short',
    timeStyle: 'short',
  }).format(new Date(value))
}
