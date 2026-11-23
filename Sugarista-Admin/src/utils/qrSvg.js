/**
 * Pure-JS ISO/IEC 18004 QR Code matrix & SVG generator (Versions 1..10, ECC-L, Byte mode).
 * Zero external dependencies — renders scannable SVG QR codes for the live receipt preview
 * and browser thermal print templates.
 */

const VERSION_SPEC = [
  [1, 26, 7, 1, 19, 0, 0, []],
  [2, 44, 10, 1, 34, 0, 0, [6, 18]],
  [3, 70, 15, 1, 55, 0, 0, [6, 22]],
  [4, 100, 20, 1, 80, 0, 0, [6, 26]],
  [5, 134, 26, 1, 108, 0, 0, [6, 30]],
  [6, 172, 18, 2, 68, 0, 0, [6, 34]],
  [7, 196, 20, 2, 78, 0, 0, [6, 22, 38]],
  [8, 242, 24, 2, 97, 0, 0, [6, 24, 42]],
  [9, 292, 30, 2, 116, 0, 0, [6, 26, 46]],
  [10, 346, 18, 2, 68, 2, 69, [6, 28, 50]],
]

const GF_EXP = new Uint8Array(512)
const GF_LOG = new Uint8Array(256)
{
  let v = 1
  for (let i = 0; i < 255; i += 1) {
    GF_EXP[i] = v
    GF_LOG[v] = i
    v <<= 1
    if (v & 0x100) v ^= 0x11d
  }
  for (let i = 255; i < 512; i += 1) {
    GF_EXP[i] = GF_EXP[i - 255]
  }
}

function gfMul(a, b) {
  return a === 0 || b === 0 ? 0 : GF_EXP[GF_LOG[a] + GF_LOG[b]]
}

function rsGenerator(degree) {
  let poly = [1]
  for (let i = 0; i < degree; i += 1) {
    const root = GF_EXP[i]
    const next = new Array(poly.length + 1).fill(0)
    for (let j = 0; j < poly.length; j += 1) {
      next[j] ^= poly[j]
      next[j + 1] ^= gfMul(poly[j], root)
    }
    poly = next
  }
  return poly
}

function rsRemainder(data, ecLen) {
  const gen = rsGenerator(ecLen)
  const rem = [...data, ...new Array(ecLen).fill(0)]
  for (let i = 0; i < data.length; i += 1) {
    const factor = rem[i]
    if (factor !== 0) {
      for (let j = 1; j < gen.length; j += 1) {
        rem[i + j] ^= gfMul(gen[j], factor)
      }
    }
  }
  return rem.slice(data.length)
}

function formatBits(mask) {
  const data = (0b01 << 3) | mask
  let rem = data << 10
  for (let i = 14; i >= 10; i -= 1) {
    if (rem & (1 << i)) rem ^= 0x537 << (i - 10)
  }
  return ((data << 10) | rem) ^ 0x5412
}

function versionBits(version) {
  let rem = version << 12
  for (let i = 17; i >= 12; i -= 1) {
    if (rem & (1 << i)) rem ^= 0x1f25 << (i - 12)
  }
  return (version << 12) | rem
}

function placeFinder(grid, reserved, r0, c0, size) {
  for (let dr = -1; dr <= 7; dr += 1) {
    for (let dc = -1; dc <= 7; dc += 1) {
      const r = r0 + dr
      const c = c0 + dc
      if (r >= 0 && r < size && c >= 0 && c < size) {
        const dark =
          dr >= 0 &&
          dr <= 6 &&
          dc >= 0 &&
          dc <= 6 &&
          (dr === 0 || dr === 6 || dc === 0 || dc === 6 || (dr >= 2 && dr <= 4 && dc >= 2 && dc <= 4))
        grid[r][c] = dark ? 1 : 0
        reserved[r][c] = true
      }
    }
  }
}

function placeAlignment(grid, reserved, r0, c0) {
  for (let dr = -2; dr <= 2; dr += 1) {
    for (let dc = -2; dc <= 2; dc += 1) {
      const d = Math.max(Math.abs(dr), Math.abs(dc))
      grid[r0 + dr][c0 + dc] = d === 0 || d === 2 ? 1 : 0
      reserved[r0 + dr][c0 + dc] = true
    }
  }
}

function maskFn(mask, r, c) {
  if (mask === 0) return (r + c) % 2 === 0
  if (mask === 1) return r % 2 === 0
  if (mask === 2) return c % 3 === 0
  if (mask === 3) return (r + c) % 3 === 0
  if (mask === 4) return (Math.floor(r / 2) + Math.floor(c / 3)) % 2 === 0
  if (mask === 5) return (r * c) % 2 + ((r * c) % 3) === 0
  if (mask === 6) return ((r * c) % 2 + ((r * c) % 3)) % 2 === 0
  return ((r + c) % 2 + ((r * c) % 3)) % 2 === 0
}

function penalty(grid) {
  const n = grid.length
  let score = 0
  for (let r = 0; r < n; r += 1) {
    let run = 1
    for (let c = 1; c < n; c += 1) {
      if (grid[r][c] === grid[r][c - 1]) run += 1
      else {
        if (run >= 5) score += run - 2
        run = 1
      }
    }
    if (run >= 5) score += run - 2
  }
  for (let c = 0; c < n; c += 1) {
    let run = 1
    for (let r = 1; r < n; r += 1) {
      if (grid[r][c] === grid[r - 1][c]) run += 1
      else {
        if (run >= 5) score += run - 2
        run = 1
      }
    }
    if (run >= 5) score += run - 2
  }
  for (let r = 0; r < n - 1; r += 1) {
    for (let c = 0; c < n - 1; c += 1) {
      const v = grid[r][c]
      if (v === grid[r][c + 1] && v === grid[r + 1][c] && v === grid[r + 1][c + 1]) score += 3
    }
  }
  let dark = 0
  for (let r = 0; r < n; r += 1) {
    for (let c = 0; c < n; c += 1) dark += grid[r][c]
  }
  score += Math.floor(Math.abs((dark * 100) / (n * n) - 50) / 5) * 10
  return score
}

export function encodeQrMatrix(text) {
  const trimmed = String(text ?? '').trim()
  if (!trimmed) return null
  const raw = new TextEncoder().encode(trimmed)

  let chosen = null
  for (const spec of VERSION_SPEC) {
    const [ver, , , g1b, g1d, g2b, g2d] = spec
    const dataCap = g1b * g1d + g2b * g2d
    const countBits = ver <= 9 ? 8 : 16
    if (Math.ceil((4 + countBits + raw.length * 8) / 8) <= dataCap) {
      chosen = spec
      break
    }
  }
  if (!chosen) return null

  const [ver, , ecLen, g1b, g1d, g2b, g2d, align] = chosen
  const dataCap = g1b * g1d + g2b * g2d
  const countBits = ver <= 9 ? 8 : 16

  const bits = []
  const pushBits = (val, count) => {
    for (let s = count - 1; s >= 0; s -= 1) bits.push((val >> s) & 1)
  }

  pushBits(0b0100, 4)
  pushBits(raw.length, countBits)
  for (const b of raw) pushBits(b, 8)

  const capBits = dataCap * 8
  pushBits(0, Math.min(4, capBits - bits.length))
  while (bits.length % 8 !== 0) bits.push(0)

  const dataBytes = []
  for (let i = 0; i < bits.length; i += 8) {
    let byte = 0
    for (let j = 0; j < 8; j += 1) byte = (byte << 1) | bits[i + j]
    dataBytes.push(byte)
  }
  const padCycle = [0xec, 0x11]
  for (let p = 0; dataBytes.length < dataCap; p += 1) dataBytes.push(padCycle[p % 2])

  const dataBlocks = []
  const ecBlocks = []
  let offset = 0
  for (let i = 0; i < g1b; i += 1) {
    const blk = dataBytes.slice(offset, offset + g1d)
    offset += g1d
    dataBlocks.push(blk)
    ecBlocks.push(rsRemainder(blk, ecLen))
  }
  for (let i = 0; i < g2b; i += 1) {
    const blk = dataBytes.slice(offset, offset + g2d)
    offset += g2d
    dataBlocks.push(blk)
    ecBlocks.push(rsRemainder(blk, ecLen))
  }

  const codewords = []
  const maxDataLen = Math.max(...dataBlocks.map((b) => b.length))
  for (let i = 0; i < maxDataLen; i += 1) {
    for (const blk of dataBlocks) if (i < blk.length) codewords.push(blk[i])
  }
  for (let i = 0; i < ecLen; i += 1) {
    for (const blk of ecBlocks) codewords.push(blk[i])
  }

  const bitstream = []
  for (const cw of codewords) {
    for (let s = 7; s >= 0; s -= 1) bitstream.push((cw >> s) & 1)
  }

  const size = 17 + ver * 4
  const base = Array.from({ length: size }, () => new Array(size).fill(0))
  const reserved = Array.from({ length: size }, () => new Array(size).fill(false))

  placeFinder(base, reserved, 0, 0, size)
  placeFinder(base, reserved, 0, size - 7, size)
  placeFinder(base, reserved, size - 7, 0, size)

  for (const r of align) {
    for (const c of align) {
      if (!reserved[r][c]) placeAlignment(base, reserved, r, c)
    }
  }

  for (let i = 8; i < size - 8; i += 1) {
    if (!reserved[6][i]) {
      base[6][i] = i % 2 === 0 ? 1 : 0
      reserved[6][i] = true
    }
    if (!reserved[i][6]) {
      base[i][6] = i % 2 === 0 ? 1 : 0
      reserved[i][6] = true
    }
  }

  base[size - 8][8] = 1
  reserved[size - 8][8] = true

  for (let i = 0; i < 9; i += 1) {
    if (i !== 6) {
      reserved[8][i] = true
      reserved[i][8] = true
    }
  }
  for (let i = 0; i < 8; i += 1) {
    reserved[8][size - 1 - i] = true
    reserved[size - 1 - i][8] = true
  }

  if (ver >= 7) {
    const vb = versionBits(ver)
    for (let i = 0; i < 18; i += 1) {
      const bit = (vb >> i) & 1
      const r = Math.floor(i / 3)
      const c = size - 11 + (i % 3)
      base[r][c] = bit
      reserved[r][c] = true
      base[c][r] = bit
      reserved[c][r] = true
    }
  }

  let bitIdx = 0
  let col = size - 1
  let upward = true
  while (col > 0) {
    if (col === 6) col -= 1
    for (let step = 0; step < size; step += 1) {
      const r = upward ? size - 1 - step : step
      for (const dc of [0, -1]) {
        const c = col + dc
        if (!reserved[r][c]) {
          base[r][c] = bitIdx < bitstream.length ? bitstream[bitIdx] : 0
          bitIdx += 1
        }
      }
    }
    upward = !upward
    col -= 2
  }

  let bestGrid = null
  let bestScore = Infinity
  for (let mask = 0; mask < 8; mask += 1) {
    const cand = Array.from({ length: size }, (_, r) =>
      Array.from({ length: size }, (__, c) => {
        let val = base[r][c]
        if (!reserved[r][c] && maskFn(mask, r, c)) val ^= 1
        return val
      }),
    )

    const fb = formatBits(mask)
    for (let i = 0; i < 6; i += 1) {
      const bit = (fb >> i) & 1
      cand[i][8] = bit
      cand[8][size - 1 - i] = bit
    }
    cand[7][8] = (fb >> 6) & 1
    cand[8][size - 7] = (fb >> 6) & 1
    cand[8][8] = (fb >> 7) & 1
    cand[8][size - 8] = (fb >> 7) & 1
    cand[8][7] = (fb >> 8) & 1
    cand[size - 7][8] = (fb >> 8) & 1
    for (let i = 9; i < 15; i += 1) {
      const bit = (fb >> i) & 1
      cand[8][14 - i] = bit
      cand[size - 15 + i][8] = bit
    }

    const sc = penalty(cand)
    if (sc < bestScore) {
      bestScore = sc
      bestGrid = cand
    }
  }

  return bestGrid ? bestGrid.map((row) => row.map(Boolean)) : null
}

export function buildQrSvg(text, sizePx = 112) {
  const matrix = encodeQrMatrix(text)
  if (!matrix) return ''
  const quiet = 2
  const total = matrix.length + quiet * 2
  const parts = []
  for (let r = 0; r < matrix.length; r += 1) {
    for (let c = 0; c < matrix.length; c += 1) {
      if (matrix[r][c]) {
        parts.push(`M${c + quiet},${r + quiet}h1v1h-1z`)
      }
    }
  }
  return `<svg xmlns="http://www.w3.org/2000/svg" width="${sizePx}" height="${sizePx}" viewBox="0 0 ${total} ${total}" shape-rendering="crispEdges" role="img" aria-label="QR Code"><rect width="${total}" height="${total}" fill="#fff"/><path d="${parts.join('')}" fill="#000"/></svg>`
}
