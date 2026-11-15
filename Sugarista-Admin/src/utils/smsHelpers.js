// Shared helpers + labels for the SMS Management Center pages (dir=rtl, Persian).

export const SMS_STATUS_LABELS = {
  Pending: 'در انتظار',
  Queued: 'در صف',
  Processing: 'در حال ارسال',
  Accepted: 'پذیرفته‌شده',
  Sent: 'ارسال‌شده',
  Delivered: 'تحویل‌شده',
  Failed: 'ناموفق',
  Rejected: 'ردشده',
  Unknown: 'نامشخص',
}

export const SMS_STATUS_TONES = {
  Pending: 'tone-neutral',
  Queued: 'tone-neutral',
  Processing: 'tone-info',
  Accepted: 'tone-info',
  Sent: 'tone-ok',
  Delivered: 'tone-ok',
  Failed: 'tone-error',
  Rejected: 'tone-error',
  Unknown: 'tone-warn',
}

export const SMS_KIND_LABELS = {
  Service: 'خدماتی',
  Marketing: 'تبلیغاتی',
  Otp: 'کد یکبار مصرف',
}

export const SMS_SEND_TYPE_LABELS = {
  Single: 'تکی',
  Bulk: 'گروهی',
  System: 'سیستمی',
}

export const SMS_PROVIDER_TYPES = [
  { value: 'Logging', label: 'Logging (فقط ثبت)' },
  { value: 'Kavenegar', label: 'کاوه‌نگار' },
  { value: 'SmsIr', label: 'SMS.ir' },
  { value: 'Melipayamak', label: 'ملی پیامک' },
  { value: 'Ghasedak', label: 'قاصدک' },
  { value: 'IpPanel', label: 'آی‌پنل' },
]

export const SMS_CAMPAIGN_STATUS_LABELS = {
  Pending: 'در انتظار پردازش',
  Processing: 'در حال ارسال',
  Completed: 'تکمیل‌شده',
  Cancelled: 'لغوشده',
  Failed: 'ناموفق',
}

export function campaignStatusLabel(campaign) {
  if (campaign.status === 'Pending'
      && campaign.scheduledAt
      && new Date(campaign.scheduledAt) > new Date()) {
    return 'زمان‌بندی‌شده'
  }
  return SMS_CAMPAIGN_STATUS_LABELS[campaign.status] ?? campaign.status
}

export const SMS_TEMPLATE_PLACEHOLDERS = [
  'FirstName',
  'LastName',
  'Mobile',
  'OrderNumber',
  'Amount',
  'Date',
  'TrackingCode',
]

export function toPersian(value) {
  return new Intl.NumberFormat('fa-IR').format(value ?? 0)
}

export function toPersianNumberString(value) {
  if (value === null || value === undefined || value === '') return '—'
  return String(value).replace(/[0-9]/g, (digit) => '۰۱۲۳۴۵۶۷۸۹'[digit])
}

export function formatSmsDateTime(iso) {
  if (!iso) return '—'
  try {
    return new Intl.DateTimeFormat('fa-IR', {
      dateStyle: 'short',
      timeStyle: 'short',
    }).format(new Date(iso))
  } catch {
    return iso
  }
}

export function formatToman(value) {
  if (value === null || value === undefined) return '—'
  return `${toPersian(Math.round(value))} تومان`
}

export const toneClass = (tone) => `sms-badge ${SMS_STATUS_TONES[tone] ?? 'tone-neutral'}`
