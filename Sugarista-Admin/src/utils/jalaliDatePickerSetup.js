import '@majidh1/jalalidatepicker/dist/jalalidatepicker.min.css'
import '@majidh1/jalalidatepicker/dist/jalalidatepicker.min.js'

let started = false

export function ensureJalaliDatePickerStarted() {
  if (started || typeof window === 'undefined' || !window.jalaliDatepicker) {
    return started
  }

  window.jalaliDatepicker.startWatch({
    targetValueInput: 'attr',
    targetValueType: 'attr',
    minDate: 'attr',
    maxDate: 'attr',
    persianDigits: true,
    zIndex: 4000,
    autoReadOnlyInput: true,
    showTodayBtn: true,
    showEmptyBtn: true,
    hideAfterChange: true,
    separatorChars: {
      date: '/',
      between: ' ',
      time: ':',
      targetDate: '-',
      targetBetween: ' ',
      targetTime: ':',
    },
  })

  started = true
  return true
}
