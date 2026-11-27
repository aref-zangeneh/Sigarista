import { toPersianDigits } from './persianDigits.js'

// Matches C# System.DayOfWeek bitmask: 1 << (int)DayOfWeek
// Sunday=1, Monday=2, Tuesday=4, Wednesday=8, Thursday=16, Friday=32, Saturday=64
export const PERSIAN_WEEKDAYS = [
  { bit: 64, short: 'ش', label: 'شنبه' },
  { bit: 1, short: 'ی', label: 'یکشنبه' },
  { bit: 2, short: 'د', label: 'دوشنبه' },
  { bit: 4, short: 'س', label: 'سه‌شنبه' },
  { bit: 8, short: 'چ', label: 'چهارشنبه' },
  { bit: 16, short: 'پ', label: 'پنجشنبه' },
  { bit: 32, short: 'ج', label: 'جمعه' },
]

export const ALL_DAYS_MASK = 127

export const STAFF_TASK_CATEGORIES = [
  'بستن شیفت',
  'باز کردن شیفت',
  'تجهیزات و دستگاه‌ها',
  'نظافت و بهداشت',
  'یخچال و انبار',
  'صندوق و مالی',
  'عملیات روزانه',
]

export const PRIORITY_META = {
  Normal: { label: 'عادی', tone: 'normal' },
  High: { label: 'مهم', tone: 'high' },
  Critical: { label: 'حیاتی', tone: 'critical' },
}

export const TODAY_STATE_META = {
  Due: { label: 'در انتظار انجام (الآن)', tone: 'due' },
  Done: { label: 'انجام شد ✓', tone: 'done' },
  Upcoming: { label: 'در پیش‌رو امروز', tone: 'upcoming' },
  NotScheduledToday: { label: 'خارج از برنامه امروز', tone: 'muted' },
  Inactive: { label: 'غیرفعال', tone: 'muted' },
}

export const ACTIVITY_ACTION_LABELS = {
  Created: 'تعریف تسک',
  Updated: 'ویرایش تسک',
  Activated: 'فعال‌سازی',
  Deactivated: 'غیرفعال‌سازی',
  Deleted: 'حذف تسک',
  Completed: 'انجام تسک',
  Reopened: 'بازگشایی مجدد',
}

export function toggleWeekdayBit(mask, bit) {
  const current = Number(mask) || 0
  return (current & bit) !== 0 ? current & ~bit : current | bit
}

export function formatScheduleLabel(task) {
  if (!task) return ''
  const timeFa = toPersianDigits(task.raiseTime || '00:00')
  if (task.recurrenceType === 'Daily') {
    return `هر روز ساعت ${timeFa}`
  }
  if (task.recurrenceType === 'OneTime') {
    const datePart = task.oneTimeDate ? formatShamsiDateOnly(task.oneTimeDate) : 'تاریخ نامشخص'
    return `یک‌باره — ${datePart} ساعت ${timeFa}`
  }
  const mask = Number(task.daysOfWeekMask) || 0
  if (mask === ALL_DAYS_MASK) {
    return `هر روز ساعت ${timeFa}`
  }
  const days = PERSIAN_WEEKDAYS.filter((d) => (mask & d.bit) !== 0).map((d) => d.label)
  return `${days.join('، ') || 'بدون روز'} — ساعت ${timeFa}`
}

export function formatOverdueLabel(minutes) {
  const mins = Math.max(0, Math.floor(Number(minutes) || 0))
  if (mins === 0) return 'هم‌اکنون وقت انجام است'
  if (mins < 60) return `${toPersianDigits(mins)} دقیقه از زمان مقرر گذشته`
  const hours = Math.floor(mins / 60)
  const rem = mins % 60
  if (rem === 0) return `${toPersianDigits(hours)} ساعت از زمان مقرر گذشته`
  return `${toPersianDigits(hours)} ساعت و ${toPersianDigits(rem)} دقیقه گذشته`
}

export function formatDelayBadge(delayMinutes) {
  const mins = Math.max(0, Math.floor(Number(delayMinutes) || 0))
  if (mins <= 5) return { label: mins === 0 ? 'دقیقاً به‌موقع' : `به‌موقع (${toPersianDigits(mins)} د)`, tone: 'ok' }
  if (mins <= 30) return { label: `${toPersianDigits(mins)} دقیقه تاخیر`, tone: 'warn' }
  const hours = Math.floor(mins / 60)
  const rem = mins % 60
  const text = hours > 0
    ? `${toPersianDigits(hours)} ساعت${rem > 0 ? ` و ${toPersianDigits(rem)} د` : ''} تاخیر`
    : `${toPersianDigits(mins)} دقیقه تاخیر`
  return { label: text, tone: 'bad' }
}

const PERSIAN_DATE_FMT = new Intl.DateTimeFormat('fa-IR-u-ca-persian', {
  year: 'numeric',
  month: '2-digit',
  day: '2-digit',
  timeZone: 'Asia/Tehran',
})

const PERSIAN_DATETIME_FMT = new Intl.DateTimeFormat('fa-IR-u-ca-persian', {
  year: 'numeric',
  month: '2-digit',
  day: '2-digit',
  hour: '2-digit',
  minute: '2-digit',
  hour12: false,
  timeZone: 'Asia/Tehran',
})

export function formatShamsiDateOnly(isoDate) {
  if (!isoDate) return '—'
  const str = String(isoDate).trim()
  const date = /^\d{4}-\d{2}-\d{2}$/.test(str) ? new Date(`${str}T12:00:00Z`) : new Date(str)
  if (Number.isNaN(date.getTime())) return str
  return PERSIAN_DATE_FMT.format(date)
}

export function formatShamsiDateTime(iso) {
  if (!iso) return '—'
  const date = new Date(iso)
  if (Number.isNaN(date.getTime())) return '—'
  return PERSIAN_DATETIME_FMT.format(date)
}
