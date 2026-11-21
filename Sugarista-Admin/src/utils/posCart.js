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
}) {
  const isNonRevenue =
    action === 'InternalUse' ||
    action === 'Complimentary' ||
    orderKind === 'InternalUse' ||
    orderKind === 'Complimentary'
  const resolvedOrderKind =
    action === 'InternalUse' || action === 'Complimentary' ? action : orderKind || 'Standard'

  return {
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
