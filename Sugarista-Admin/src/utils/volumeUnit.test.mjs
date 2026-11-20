import assert from 'node:assert/strict'
import test from 'node:test'
import {
  formatVolumeValue,
  inferDefaultVolumeUnit,
  parseVolumeValue,
} from './volumeUnit.js'

test('inferDefaultVolumeUnit chooses گرم for cakes/food and میلی‌لیتر for drinks', () => {
  assert.equal(inferDefaultVolumeUnit('بار سرد موهیتو Mojito'), 'میلی‌لیتر')
  assert.equal(inferDefaultVolumeUnit('بر پایه اسپرسو آمریکانو'), 'میلی‌لیتر')
  assert.equal(inferDefaultVolumeUnit('اسموتی آناناس'), 'میلی‌لیتر')
  assert.equal(inferDefaultVolumeUnit('کیک و دسر چیزکیک'), 'گرم')
  assert.equal(inferDefaultVolumeUnit('پای سیب'), 'گرم')
  assert.equal(inferDefaultVolumeUnit('صبحانه کروسان'), 'گرم')
})

test('parseVolumeValue parses bare numbers, English units, and Persian units', () => {
  assert.deepEqual(parseVolumeValue('360', 'میلی‌لیتر'), { amount: '360', unit: 'میلی‌لیتر' })
  assert.deepEqual(parseVolumeValue('۳۶۰', 'میلی‌لیتر'), { amount: '360', unit: 'میلی‌لیتر' })
  assert.deepEqual(parseVolumeValue('350 ml', 'گرم'), { amount: '350', unit: 'میلی‌لیتر' })
  assert.deepEqual(parseVolumeValue('180 gr', 'میلی‌لیتر'), { amount: '180', unit: 'گرم' })
  assert.deepEqual(parseVolumeValue('1.5 لیتر', 'میلی‌لیتر'), { amount: '1.5', unit: 'لیتر' })
  assert.deepEqual(parseVolumeValue('', 'گرم'), { amount: '', unit: 'گرم' })
})

test('formatVolumeValue outputs ASCII digits + canonical Persian unit for DB storage', () => {
  assert.equal(formatVolumeValue('۳۶۰', 'میلی‌لیتر'), '360 میلی‌لیتر')
  assert.equal(formatVolumeValue('200', 'گرم'), '200 گرم')
  assert.equal(formatVolumeValue('', 'میلی‌لیتر'), '')
})
