/**
 * Pure bundle valuation, live pricing, quick-discount rounding, and receipt
 * summary helpers for Sugarista Product Bundles.
 */

export function resolveComponentRealPrice(optionItem, sizeKey) {
  if (!optionItem) {
    return { sizeKey: null, sizeLabel: null, unitPrice: 0 }
  }

  const normalized = typeof sizeKey === 'string' && sizeKey.trim()
    ? sizeKey.trim().toLowerCase()
    : null

  if (normalized === 'small' && Number(optionItem.smallPrice) > 0) {
    return {
      sizeKey: 'small',
      sizeLabel: optionItem.smallName || 'کوچک',
      unitPrice: Number(optionItem.smallPrice),
    }
  }

  if (normalized === 'medium' && Number(optionItem.mediumPrice) > 0) {
    return {
      sizeKey: 'medium',
      sizeLabel: optionItem.mediumName || 'متوسط',
      unitPrice: Number(optionItem.mediumPrice),
    }
  }

  if (normalized === 'large' && Number(optionItem.largePrice) > 0) {
    return {
      sizeKey: 'large',
      sizeLabel: optionItem.largeName || 'بزرگ',
      unitPrice: Number(optionItem.largePrice),
    }
  }

  if (Number(optionItem.mediumPrice) > 0) {
    return {
      sizeKey: 'medium',
      sizeLabel: optionItem.mediumName || 'متوسط',
      unitPrice: Number(optionItem.mediumPrice),
    }
  }

  if (Number(optionItem.smallPrice) > 0) {
    return {
      sizeKey: 'small',
      sizeLabel: optionItem.smallName || 'کوچک',
      unitPrice: Number(optionItem.smallPrice),
    }
  }

  if (Number(optionItem.largePrice) > 0) {
    return {
      sizeKey: 'large',
      sizeLabel: optionItem.largeName || 'بزرگ',
      unitPrice: Number(optionItem.largePrice),
    }
  }

  return {
    sizeKey: null,
    sizeLabel: null,
    unitPrice: Number(optionItem.effectivePrice ?? optionItem.price ?? 0) || 0,
  }
}

export function getAvailableSizesForOption(optionItem) {
  if (!optionItem) return []
  const sizes = []
  if (Number(optionItem.smallPrice) > 0) {
    sizes.push({
      key: 'small',
      label: optionItem.smallName || 'کوچک',
      price: Number(optionItem.smallPrice),
    })
  }
  if (Number(optionItem.mediumPrice) > 0) {
    sizes.push({
      key: 'medium',
      label: optionItem.mediumName || 'متوسط',
      price: Number(optionItem.mediumPrice),
    })
  }
  if (Number(optionItem.largePrice) > 0) {
    sizes.push({
      key: 'large',
      label: optionItem.largeName || 'بزرگ',
      price: Number(optionItem.largePrice),
    })
  }
  return sizes
}

/**
 * Evaluates a bundle draft in real time given component lines, menuItemOptions map,
 * server-supplied unit costs (if available), and the proposed bundle selling price.
 */
export function evaluateBundleDraft({
  components = [],
  optionsById = new Map(),
  costByComponentId = new Map(),
  bundlePrice = 0,
}) {
  let regularTotal = 0
  let totalCost = 0
  const resolvedRows = components.map((row) => {
    const qty = Math.max(1, Math.min(50, Math.round(Number(row.quantity) || 1)))
    const opt = optionsById.get(row.componentMenuItemId)
    const { sizeKey, sizeLabel, unitPrice } = opt
      ? resolveComponentRealPrice(opt, row.sizeKey)
      : {
          sizeKey: row.sizeKey ?? null,
          sizeLabel: row.sizeLabel ?? null,
          unitPrice: Number(row.unitRegularPrice) || 0,
        }
    const linePrice = unitPrice * qty
    const unitCost = Number(costByComponentId.get(row.componentMenuItemId) ?? row.unitCost ?? 0) || 0
    const lineCost = unitCost * qty
    regularTotal += linePrice
    totalCost += lineCost

    return {
      ...row,
      persianName: opt?.persianName ?? row.persianName ?? 'آیتم نامشخص',
      englishName: opt?.englishName ?? row.englishName ?? '',
      imageUrl: opt?.imageUrl ?? row.imageUrl ?? null,
      sizeKey,
      sizeLabel,
      quantity: qty,
      unitRegularPrice: unitPrice,
      lineRegularPrice: linePrice,
      unitCost,
      lineCost,
      isActive: opt ? opt.isActive !== false : true,
    }
  })

  const sellingPrice = Math.max(0, Number(bundlePrice) || 0)
  const savingsAmount = regularTotal > sellingPrice ? regularTotal - sellingPrice : 0
  const savingsPercent =
    regularTotal > 0 && savingsAmount > 0
      ? Math.round((savingsAmount / regularTotal) * 100)
      : 0
  const estimatedProfit = sellingPrice - totalCost
  const estimatedMarginPercent =
    sellingPrice > 0 && totalCost > 0
      ? Math.round((estimatedProfit / sellingPrice) * 1000) / 10
      : null

  const warnings = []
  if (resolvedRows.length > 0 && sellingPrice > 0 && regularTotal > 0 && sellingPrice >= regularTotal) {
    warnings.push('قیمت باندل از مجموع قیمت واقعی آیتم‌ها کمتر نیست؛ مشتری سودی در انتخاب باندل نمی‌بیند.')
  }
  if (totalCost > 0 && sellingPrice > 0 && sellingPrice < totalCost) {
    warnings.push('هشدار زیان: قیمت باندل کمتر از بهای تمام‌شده (COGS) مواد اولیه است.')
  }
  if (resolvedRows.some((r) => !r.isActive)) {
    warnings.push('یکی از آیتم‌های داخل باندل غیرفعال است و باعث غیرقابل‌سفارش شدن باندل می‌شود.')
  }

  return {
    rows: resolvedRows,
    regularTotal,
    bundlePrice: sellingPrice,
    savingsAmount,
    savingsPercent,
    totalCost: Math.round(totalCost),
    estimatedProfit: Math.round(estimatedProfit),
    estimatedMarginPercent,
    warnings,
  }
}

/**
 * Computes a clean Iranian-Toman bundle price rounded down to nearest 1,000 Toman
 * for a given discount percentage (e.g., 15% off 500,000 -> 425,000).
 */
export function computeDiscountedBundlePrice(regularTotal, discountPercent) {
  const total = Number(regularTotal) || 0
  const pct = Math.max(0, Math.min(95, Number(discountPercent) || 0))
  if (total <= 0) return 0
  const raw = total * (1 - pct / 100)
  return Math.max(1000, Math.floor(raw / 1000) * 1000)
}

/**
 * Formats bundle component breakdown lines for POS cart display and Barista/Customer receipts.
 * Example: ["باندل شامل: ۱ × آمریکانو", "باندل شامل: ۱ × چیزکیک سن‌سباستین"]
 */
export function formatBundleSummaryLabels(bundleItems = [], lineQuantity = 1) {
  if (!Array.isArray(bundleItems) || bundleItems.length === 0) {
    return []
  }

  const multiplier = Math.max(1, Math.round(Number(lineQuantity) || 1))
  return bundleItems
    .filter((comp) => comp && (comp.persianName || comp.name))
    .map((comp) => {
      const compQty = Math.max(1, Math.round(Number(comp.quantity) || 1))
      const totalQty = compQty * multiplier
      const name = comp.persianName || comp.name
      const sizeSuffix = comp.sizeLabel ? ` (${comp.sizeLabel})` : ''
      return `شامل: ${totalQty} × ${name}${sizeSuffix}`
    })
}
