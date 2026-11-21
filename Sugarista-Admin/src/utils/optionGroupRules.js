/**
 * Keeps an option group's selection rule fields (`isRequired`, `selectionMode`,
 * `minSelections`, `maxSelections`) mutually consistent on every user interaction
 * so unchecking "اجباری" never silently snaps back to true on save.
 */
export function applyOptionGroupRuleChange(current, change) {
  const next = { ...current }

  if ('isRequired' in change) {
    const required = Boolean(change.isRequired)
    next.isRequired = required
    if (required) {
      const currMin = Number(current.minSelections)
      next.minSelections = Number.isFinite(currMin) && currMin >= 1 ? currMin : 1
      const currMax = Number(current.maxSelections)
      if (!Number.isFinite(currMax) || currMax < next.minSelections) {
        next.maxSelections = next.minSelections
      }
      if (next.selectionMode === 'Single') {
        next.minSelections = 1
        next.maxSelections = 1
      }
    } else {
      next.minSelections = 0
    }
    return next
  }

  if ('selectionMode' in change) {
    next.selectionMode = change.selectionMode === 'Multi' ? 'Multi' : 'Single'
    if (next.selectionMode === 'Single') {
      next.maxSelections = 1
      next.minSelections = next.isRequired ? 1 : 0
    }
    return next
  }

  if ('minSelections' in change) {
    const raw = change.minSelections
    if (raw === '' || raw === null || raw === undefined) {
      next.minSelections = ''
      next.isRequired = false
      return next
    }
    const parsed = Math.max(0, Math.floor(Number(raw) || 0))
    next.minSelections = parsed
    next.isRequired = parsed > 0
    const currMax = Math.max(1, Math.floor(Number(current.maxSelections) || 1))
    if (parsed > currMax) {
      next.maxSelections = parsed
    }
    if (parsed > 1 && next.selectionMode === 'Single') {
      next.selectionMode = 'Multi'
    }
    return next
  }

  if ('maxSelections' in change) {
    const raw = change.maxSelections
    if (raw === '' || raw === null || raw === undefined) {
      next.maxSelections = ''
      return next
    }
    const parsed = Math.max(1, Math.floor(Number(raw) || 1))
    next.maxSelections = parsed
    const currMin = Math.max(0, Math.floor(Number(current.minSelections) || 0))
    if (currMin > parsed) {
      next.minSelections = parsed
    }
    if (parsed > 1 && next.selectionMode === 'Single') {
      next.selectionMode = 'Multi'
    }
    return next
  }

  return { ...current, ...change }
}
