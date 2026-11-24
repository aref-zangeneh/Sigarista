export const POS_TENDER_METHODS = [
  { key: 'Cash', label: 'نقدی', icon: '💵' },
  { key: 'Card', label: 'کارت‌خوان (POS)', icon: '💳' },
  { key: 'CardToCard', label: 'کارت به کارت', icon: '📲' },
]

export function formatPrice(value) {
  const amount = Number(value) || 0
  return amount.toLocaleString('fa-IR')
}

export function buildCartLineKey(menuItemId, sizeKey, modifierIds = [], optionIds = []) {
  const modifiers = [...modifierIds].sort().join(',')
  const options = [...optionIds].sort().join(',')
  return `${menuItemId}:${sizeKey || 'default'}:${modifiers}:${options}`
}

export function resolveItemPrice(item, sizeKey) {
  const variants = item.variants ?? []
  const variant =
    variants.find((entry) => entry.key === sizeKey) ||
    variants.find((entry) => entry.key === 'medium') ||
    variants[0]

  if (variant) {
    return Number(variant.specialOfferPrice ?? variant.price ?? item.price) || 0
  }

  return Number(item.specialOfferPrice ?? item.price) || 0
}

export function resolveDefaultSizeKey(item) {
  const variants = item.variants ?? []
  return (
    variants.find((entry) => entry.key === 'medium')?.key ||
    variants[0]?.key ||
    null
  )
}

export function parseNumericInput(raw) {
  if (raw === null || raw === undefined || raw === '') return 0
  const ascii = String(raw)
    .replace(/[۰-۹]/g, (d) => String('۰۱۲۳۴۵۶۷۸۹'.indexOf(d)))
    .replace(/[٠-٩]/g, (d) => String('٠١٢٣٤٥٦٧٨٩'.indexOf(d)))
    .replace(/[,٬\s]/g, '')
  const parsed = Number(ascii)
  return Number.isFinite(parsed) && parsed > 0 ? parsed : 0
}

export function computeSplitPaymentSummary(payableTotal, rawAllocations = {}) {
  const target = Math.max(0, Math.round(Number(payableTotal) || 0))
  const cashAmount = Math.round(parseNumericInput(rawAllocations.Cash ?? rawAllocations.cash))
  const cardAmount = Math.round(parseNumericInput(rawAllocations.Card ?? rawAllocations.card))
  const cardToCardAmount = Math.round(
    parseNumericInput(rawAllocations.CardToCard ?? rawAllocations.cardToCard),
  )
  const cardToCardReference = (rawAllocations.cardToCardReference ?? '').toString().trim() || null
  const cardReference = (rawAllocations.cardReference ?? '').toString().trim() || null

  const legs = []
  if (cashAmount > 0) {
    legs.push({ method: 'Cash', label: 'نقدی', amount: cashAmount, reference: null })
  }
  if (cardAmount > 0) {
    legs.push({
      method: 'Card',
      label: 'کارت‌خوان (پوز)',
      amount: cardAmount,
      reference: cardReference,
    })
  }
  if (cardToCardAmount > 0) {
    legs.push({
      method: 'CardToCard',
      label: 'کارت به کارت',
      amount: cardToCardAmount,
      reference: cardToCardReference,
    })
  }

  const allocatedTotal = cashAmount + cardAmount + cardToCardAmount
  const diff = target - allocatedTotal
  const remainingAmount = Math.max(0, diff)
  const excessAmount = Math.max(0, -diff)
  const isExactMatch = target === 0 ? allocatedTotal === 0 : allocatedTotal === target && legs.length > 0

  return {
    payableTotal: target,
    cashAmount,
    cardAmount,
    cardToCardAmount,
    allocatedTotal,
    remainingAmount,
    excessAmount,
    isExactMatch,
    legs,
    effectiveMethod:
      legs.length === 0 ? null : legs.length === 1 ? legs[0].method : 'Split',
  }
}

export function computeCustomerClubPreview({
  itemsTotal = 0,
  grandTotalBeforeDiscounts = 0,
  customerLookup = null,
  personalCup = false,
  siteSettings = null,
  redeemPoints = 0,
  discountCode = '',
}) {
  const safeItemsTotal = Math.max(0, Number(itemsTotal) || 0)
  let remainingOrderTotal = Math.max(0, Number(grandTotalBeforeDiscounts) || safeItemsTotal)
  let remainingCap =
    customerLookup?.remainingDailyDiscountAllowance !== null &&
    customerLookup?.remainingDailyDiscountAllowance !== undefined
      ? Math.max(0, Number(customerLookup.remainingDailyDiscountAllowance))
      : null

  const breakdown = []

  function applyRow(key, label, rawAmount) {
    let amount = Math.max(0, Math.round(Number(rawAmount) || 0))
    if (amount <= 0 || remainingOrderTotal <= 0) return 0
    amount = Math.min(amount, remainingOrderTotal)
    if (remainingCap !== null) {
      amount = Math.min(amount, remainingCap)
    }
    if (amount <= 0) return 0
    remainingOrderTotal -= amount
    if (remainingCap !== null) {
      remainingCap = Math.max(0, remainingCap - amount)
    }
    breakdown.push({ key, label, amount })
    return amount
  }

  let cappedRedeemPoints = 0
  const requestedPoints = Math.floor(parseNumericInput(redeemPoints))
  const redeemRate = Number(customerLookup?.loyaltyRedeemTomanPerPoint || siteSettings?.loyaltyRedeemTomanPerPoint || 0)
  const availablePoints = Math.floor(Number(customerLookup?.loyaltyPoints || 0))
  const loyaltyEnabled = Boolean(customerLookup?.loyaltyEnabled ?? siteSettings?.loyaltyEnabled)

  if (customerLookup?.exists && loyaltyEnabled && redeemRate > 0 && requestedPoints > 0 && availablePoints > 0) {
    let pts = Math.min(requestedPoints, availablePoints)
    pts = Math.min(pts, Math.floor(remainingOrderTotal / redeemRate))
    if (remainingCap !== null) {
      pts = Math.min(pts, Math.floor(remainingCap / redeemRate))
    }
    if (pts > 0) {
      const applied = applyRow('loyalty', 'امتیاز باشگاه مشتریان', pts * redeemRate)
      if (applied > 0) {
        cappedRedeemPoints = pts
      }
    }
  }

  if (
    customerLookup?.exists &&
    customerLookup.hasActiveStampReward &&
    Number(customerLookup.stampRewardPercent) > 0 &&
    !String(discountCode || '').trim()
  ) {
    applyRow(
      'stamp',
      `پاداش کارت مهر (${customerLookup.stampRewardPercent}٪)`,
      Math.round((safeItemsTotal * Number(customerLookup.stampRewardPercent)) / 100),
    )
  }

  if (customerLookup?.exists && Number(customerLookup.membershipTierDiscountPercent) > 0) {
    applyRow(
      'tier',
      `${customerLookup.membershipTierName || 'سطح عضویت'} (${customerLookup.membershipTierDiscountPercent}٪)`,
      Math.round((safeItemsTotal * Number(customerLookup.membershipTierDiscountPercent)) / 100),
    )
  }

  if (customerLookup?.exists && customerLookup.isBirthdayDiscountActive && Number(customerLookup.birthdayDiscountPercent) > 0) {
    applyRow(
      'birthday',
      `هدیهٔ تولد (${customerLookup.birthdayDiscountPercent}٪)`,
      Math.round((safeItemsTotal * Number(customerLookup.birthdayDiscountPercent)) / 100),
    )
  }

  if (customerLookup?.exists && Number(customerLookup.activeGroupDiscountPercent) > 0) {
    applyRow(
      'group',
      `${customerLookup.activeGroupName || 'گروه مشتریان'} (${customerLookup.activeGroupDiscountPercent}٪)`,
      Math.round((safeItemsTotal * Number(customerLookup.activeGroupDiscountPercent)) / 100),
    )
  }

  if (personalCup && siteSettings?.personalCupDiscountEnabled && Number(siteSettings.personalCupDiscountAmount) > 0) {
    applyRow(
      'cup',
      'لیوان شخصی',
      Math.min(safeItemsTotal, Number(siteSettings.personalCupDiscountAmount)),
    )
  }

  const totalClubDiscount = breakdown.reduce((sum, item) => sum + item.amount, 0)
  return {
    totalClubDiscount,
    afterClubTotal: remainingOrderTotal,
    breakdown,
    cappedRedeemPoints,
  }
}

export function buildUpsertPayload({
  orderId,
  customerName,
  customerPhone,
  note,
  serviceType,
  salonTableId,
  deliveryMethodId,
  cart,
  action,
  splitBill,
  splitBillMode,
  guestCount,
  splitShares,
  personalCup,
  useSubscription,
  orderKind = 'Standard',
  complimentaryReason = '',
  excludeFromAccounting = false,
  manualDiscountType = 'Amount',
  manualDiscountValue = null,
  manualDiscountReason = '',
  paymentAllocations = null,
  paymentReference = '',
  discountCode = '',
  redeemPoints = 0,
  managerNote = undefined,
}) {
  const isNonRevenue =
    action === 'InternalUse' ||
    action === 'Complimentary' ||
    orderKind === 'InternalUse' ||
    orderKind === 'Complimentary'
  const resolvedOrderKind =
    action === 'InternalUse' || action === 'Complimentary' ? action : orderKind || 'Standard'
  const parsedManualValue = parseNumericInput(manualDiscountValue)
  const hasManualDiscount = !isNonRevenue && parsedManualValue > 0
  const resolvedManualType = manualDiscountType === 'Percent' ? 'Percent' : 'Amount'
  const parsedRedeemPoints = !isNonRevenue ? Math.floor(parseNumericInput(redeemPoints)) : 0
  const normalizedAllocations =
    action === 'PaySplit' && Array.isArray(paymentAllocations)
      ? paymentAllocations
          .filter((leg) => leg && Number(leg.amount) > 0)
          .map((leg) => ({
            method: leg.method,
            amount: Math.round(Number(leg.amount)),
            reference: leg.reference?.toString().trim() || null,
          }))
      : null

  const payload = {
    orderId: orderId ?? null,
    customerName: customerName?.trim() || null,
    customerPhone: customerPhone?.trim() || null,
    note: note?.trim() || null,
    serviceType,
    salonTableId: serviceType === 'DineIn' ? salonTableId : null,
    deliveryMethodId: deliveryMethodId ?? null,
    items: cart.map((line) => ({
      menuItemId: line.menuItemId,
      sizeKey: line.sizeKey,
      quantity: line.quantity,
      modifierIds: line.modifierIds ?? [],
      optionIds: line.optionIds ?? [],
      baristaNote: line.baristaNote?.trim() || null,
      payerKey: line.payerKey ?? null,
      payerLabel: line.payerLabel ?? null,
    })),
    action,
    isSplitBill: splitBill,
    splitBillMode: splitBill ? splitBillMode : null,
    guestCount: splitBill ? guestCount : null,
    splitShares: splitBill ? splitShares : null,
    personalCup: !isNonRevenue && personalCup === true,
    useSubscription: !isNonRevenue && useSubscription === true,
    orderKind: resolvedOrderKind,
    complimentaryReason: isNonRevenue ? complimentaryReason?.trim() || null : null,
    excludeFromAccounting: excludeFromAccounting === true,
    manualDiscountType: hasManualDiscount ? resolvedManualType : null,
    manualDiscountValue: hasManualDiscount ? parsedManualValue : null,
    manualDiscountReason: hasManualDiscount ? manualDiscountReason?.trim() || null : null,
    paymentAllocations: normalizedAllocations,
    paymentReference: !isNonRevenue ? paymentReference?.toString().trim() || null : null,
    discountCode: !isNonRevenue ? discountCode?.toString().trim() || null : null,
    redeemPoints: parsedRedeemPoints,
  }

  if (managerNote !== undefined && managerNote !== null) {
    payload.managerNote = String(managerNote).trim()
  }

  return payload
}

export function computeManualDiscount(baseAmount, type = 'Amount', rawValue = 0) {
  const safeBase = Math.max(0, Number(baseAmount) || 0)
  const numericValue = parseNumericInput(rawValue)
  const resolvedType = type === 'Percent' ? 'Percent' : 'Amount'

  if (safeBase <= 0 || numericValue <= 0) {
    return {
      type: resolvedType,
      value: 0,
      amount: 0,
      finalTotal: safeBase,
      exceedsLimit: false,
    }
  }

  if (resolvedType === 'Percent') {
    const exceedsLimit = numericValue > 100
    const clampedPct = Math.min(100, numericValue)
    const amount = Math.min(safeBase, Math.round((safeBase * clampedPct) / 100))
    return {
      type: resolvedType,
      value: numericValue,
      amount,
      finalTotal: Math.max(0, safeBase - amount),
      exceedsLimit,
    }
  }

  const exceedsLimit = numericValue > safeBase
  const amount = Math.min(safeBase, Math.round(numericValue))
  return {
    type: resolvedType,
    value: numericValue,
    amount,
    finalTotal: Math.max(0, safeBase - amount),
    exceedsLimit,
  }
}

export function computeCartTotals(cart, deliveryFee = 0) {
  const itemsTotal = cart.reduce((total, line) => total + line.unitPrice * line.quantity, 0)
  // unit prices carry no modifier charge, so tax base matches the backend line total exactly
  const taxTotal = cart.reduce(
    (total, line) => total + (line.unitPrice * line.quantity * (line.taxPercent ?? 0)) / 100,
    0,
  )
  return {
    itemsTotal,
    taxTotal,
    grandTotal: itemsTotal + deliveryFee + taxTotal,
  }
}

export function findMissingRequiredOptionGroup(menuItem, selectedOptionIds = []) {
  const groups = menuItem?.optionGroups ?? []
  if (!groups.length) {
    return null
  }

  const selectedSet = new Set(selectedOptionIds ?? [])
  const ordered = [...groups].sort((a, b) => {
    if (a.isRequired !== b.isRequired) return a.isRequired ? -1 : 1
    return (a.displayOrder ?? 0) - (b.displayOrder ?? 0)
  })

  for (const group of ordered) {
    const min = group.minSelections > 0 ? group.minSelections : (group.isRequired ? 1 : 0)
    if (min <= 0) {
      continue
    }
    const count = (group.options ?? []).filter((opt) => selectedSet.has(opt.id)).length
    if (count < min) {
      return { group, min, count }
    }
  }

  return null
}

export function orderToCart(order, menuItems) {
  return (order.lines ?? []).map((line) => {
    const menuItem = menuItems.find((item) => item.id === line.menuItemId)
    const optionIds = line.optionIds ?? []
    const resolvedOptions =
      optionIds.length && menuItem?.optionGroups?.length
        ? menuItem.optionGroups.flatMap((group) =>
            (group.options ?? [])
              .filter((opt) => optionIds.includes(opt.id))
              .map((opt) => ({
                id: opt.id,
                groupId: group.id,
                groupName: group.persianName,
                optionName: opt.persianName,
                priceDelta: Number(opt.priceDelta) || 0,
                priceDeltaLabel: Number(opt.priceDelta) > 0 ? formatPrice(opt.priceDelta) : null,
              })),
          )
        : []
    const options = resolvedOptions.length
      ? resolvedOptions
      : (line.optionLabels?.map((label) => ({ optionName: label })) ?? [])

    return {
      cartKey: buildCartLineKey(line.menuItemId, line.sizeKey, line.modifierIds, optionIds),
      menuItemId: line.menuItemId,
      name: line.persianName,
      imageUrl: menuItem?.imageUrl ?? null,
      sizeKey: line.sizeKey,
      sizeLabel: line.sizeLabel,
      quantity: line.quantity,
      unitPrice: line.unitPrice,
      taxPercent: line.taxPercent ?? menuItem?.taxPercent ?? 0,
      modifierIds: line.modifierIds ?? [],
      modifierLabels: line.modifierLabels ?? [],
      optionIds,
      options,
      isBundle: Boolean(line.bundle?.components?.length || menuItem?.isBundle),
      bundleItems: line.bundle?.components ?? menuItem?.bundleItems ?? [],
      bundleRegularTotal: line.bundle?.regularTotal ?? menuItem?.bundleRegularTotal ?? null,
      bundleSavingsAmount: line.bundle?.savingsAmount ?? menuItem?.bundleSavingsAmount ?? null,
      baristaNote: line.baristaNote ?? '',
      payerKey: line.payerKey,
      payerLabel: line.payerLabel,
    }
  })
}
