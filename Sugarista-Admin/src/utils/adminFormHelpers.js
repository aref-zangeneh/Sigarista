export function splitTags(tagValue) {
  return tagValue
    .split(',')
    .map((tag) => tag.trim())
    .filter(Boolean)
}

export function joinTags(tags) {
  return tags.join('، ')
}

export function splitList(value) {
  return value
    .split(/[\n,،]+/)
    .map((item) => item.trim())
    .filter(Boolean)
}

export function joinList(values) {
  return values.join('\n')
}

export function optionalNumber(value) {
  if (value === '' || value === null || value === undefined) {
    return null
  }

  return Number(value)
}
