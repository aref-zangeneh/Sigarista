export function formatPrice(value) {
  const amount = Number(value) || 0
  return amount.toLocaleString('fa-IR')
}

export function buildCartLineKey(menuItemId, sizeKey, modifierIds = []) {
  const modifiers = [...modifierIds].sort().join(',')
  return `${menuItemId}:${sizeKey || 'default'}:${modifiers}`
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
}) {
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
      baristaNote: line.baristaNote?.trim() || null,
      payerKey: line.payerKey ?? null,
      payerLabel: line.payerLabel ?? null,
    })),
    action,
    isSplitBill: splitBill,
    splitBillMode: splitBill ? splitBillMode : null,
    guestCount: splitBill ? guestCount : null,
    splitShares: splitBill ? splitShares : null,
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

export function orderToCart(order, menuItems) {
  return (order.lines ?? []).map((line) => {
    const menuItem = menuItems.find((item) => item.id === line.menuItemId)
    return {
      cartKey: buildCartLineKey(line.menuItemId, line.sizeKey, line.modifierIds),
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
      baristaNote: line.baristaNote ?? '',
      payerKey: line.payerKey,
      payerLabel: line.payerLabel,
    }
  })
}
