"""Pure-Python ISO/IEC 18004 QR Code matrix generator (Versions 1..10, ECC-L, Byte mode).

Zero external dependencies — produces exact 2D boolean modules (True = dark)
for crisp 1-bit rendering on 203 dpi thermal printers.
"""

from __future__ import annotations

# (version, total_codewords, ec_codewords_per_block, num_blocks_g1, data_cw_g1, num_blocks_g2, data_cw_g2, align_centers)
_VERSION_SPEC = [
    (1, 26, 7, 1, 19, 0, 0, []),
    (2, 44, 10, 1, 34, 0, 0, [6, 18]),
    (3, 70, 15, 1, 55, 0, 0, [6, 22]),
    (4, 100, 20, 1, 80, 0, 0, [6, 26]),
    (5, 134, 26, 1, 108, 0, 0, [6, 30]),
    (6, 172, 18, 2, 68, 0, 0, [6, 34]),
    (7, 196, 20, 2, 78, 0, 0, [6, 22, 38]),
    (8, 242, 24, 2, 97, 0, 0, [6, 24, 42]),
    (9, 292, 30, 2, 116, 0, 0, [6, 26, 46]),
    (10, 346, 18, 2, 68, 2, 69, [6, 28, 50]),
]

# Precomputed GF(256) exp/log tables with primitive polynomial 0x11D
_GF_EXP = [0] * 512
_GF_LOG = [0] * 256
_v = 1
for _i in range(255):
    _GF_EXP[_i] = _v
    _GF_LOG[_v] = _i
    _v <<= 1
    if _v & 0x100:
        _v ^= 0x11D
for _i in range(255, 512):
    _GF_EXP[_i] = _GF_EXP[_i - 255]


def _gf_mul(a: int, b: int) -> int:
    if a == 0 or b == 0:
        return 0
    return _GF_EXP[_GF_LOG[a] + _GF_LOG[b]]


def _rs_generator(degree: int) -> list[int]:
    poly = [1]
    for i in range(degree):
        root = _GF_EXP[i]
        next_poly = [0] * (len(poly) + 1)
        for j, coef in enumerate(poly):
            next_poly[j] ^= coef
            next_poly[j + 1] ^= _gf_mul(coef, root)
        poly = next_poly
    return poly


def _rs_remainder(data: list[int], ec_len: int) -> list[int]:
    gen = _rs_generator(ec_len)
    rem = list(data) + [0] * ec_len
    for i in range(len(data)):
        factor = rem[i]
        if factor != 0:
            for j in range(1, len(gen)):
                rem[i + j] ^= _gf_mul(gen[j], factor)
    return rem[len(data):]


def _format_bits(mask: int) -> int:
    # ECC-L = 01 (2 bits) + mask (3 bits) = 5 data bits
    data = (0b01 << 3) | mask
    rem = data << 10
    for i in range(14, 9, -1):
        if rem & (1 << i):
            rem ^= 0x537 << (i - 10)
    return ((data << 10) | rem) ^ 0x5412


def _version_bits(version: int) -> int:
    rem = version << 12
    for i in range(17, 11, -1):
        if rem & (1 << i):
            rem ^= 0x1F25 << (i - 12)
    return (version << 12) | rem


def _place_finder(grid: list[list[int | None]], reserved: list[list[bool]], r0: int, c0: int, size: int):
    for dr in range(-1, 8):
        for dc in range(-1, 8):
            r, c = r0 + dr, c0 + dc
            if 0 <= r < size and 0 <= c < size:
                dark = (0 <= dr <= 6 and 0 <= dc <= 6) and (
                    dr in (0, 6) or dc in (0, 6) or (2 <= dr <= 4 and 2 <= dc <= 4)
                )
                grid[r][c] = 1 if dark else 0
                reserved[r][c] = True


def _place_alignment(grid: list[list[int | None]], reserved: list[list[bool]], r0: int, c0: int):
    for dr in range(-2, 3):
        for dc in range(-2, 3):
            dark = max(abs(dr), abs(dc)) in (0, 2)
            grid[r0 + dr][c0 + dc] = 1 if dark else 0
            reserved[r0 + dr][c0 + dc] = True


def _mask_fn(mask: int, r: int, c: int) -> bool:
    if mask == 0:
        return (r + c) % 2 == 0
    if mask == 1:
        return r % 2 == 0
    if mask == 2:
        return c % 3 == 0
    if mask == 3:
        return (r + c) % 3 == 0
    if mask == 4:
        return (r // 2 + c // 3) % 2 == 0
    if mask == 5:
        return ((r * c) % 2 + (r * c) % 3) == 0
    if mask == 6:
        return (((r * c) % 2 + (r * c) % 3) % 2) == 0
    return (((r + c) % 2 + (r * c) % 3) % 2) == 0


def _penalty(grid: list[list[int]]) -> int:
    n = len(grid)
    score = 0
    for r in range(n):
        run = 1
        for c in range(1, n):
            if grid[r][c] == grid[r][c - 1]:
                run += 1
            else:
                if run >= 5:
                    score += run - 2
                run = 1
        if run >= 5:
            score += run - 2
    for c in range(n):
        run = 1
        for r in range(1, n):
            if grid[r][c] == grid[r - 1][c]:
                run += 1
            else:
                if run >= 5:
                    score += run - 2
                run = 1
        if run >= 5:
            score += run - 2
    for r in range(n - 1):
        for c in range(n - 1):
            v = grid[r][c]
            if v == grid[r][c + 1] == grid[r + 1][c] == grid[r + 1][c + 1]:
                score += 3
    dark = sum(sum(row) for row in grid)
    pct = (dark * 100) / (n * n)
    score += int(abs(pct - 50) // 5) * 10
    return score


def encode_qr_matrix(text: str) -> list[list[bool]] | None:
    """Return a 2D boolean matrix (True = dark) for `text`, or None if empty/too long."""
    if not text or not str(text).strip():
        return None
    raw = str(text).strip().encode("utf-8")

    chosen = None
    for spec in _VERSION_SPEC:
        ver, _total_cw, _ec_len, g1_b, g1_d, g2_b, g2_d, _align = spec
        data_cap = g1_b * g1_d + g2_b * g2_d
        char_count_bits = 8 if ver <= 9 else 16
        total_bits_needed = 4 + char_count_bits + len(raw) * 8
        if (total_bits_needed + 7) // 8 <= data_cap:
            chosen = spec
            break

    if chosen is None:
        return None

    ver, _total_cw, ec_len, g1_b, g1_d, g2_b, g2_d, align = chosen
    data_cap = g1_b * g1_d + g2_b * g2_d
    char_count_bits = 8 if ver <= 9 else 16

    bits: list[int] = []

    def push_bits(val: int, count: int):
        for shift in range(count - 1, -1, -1):
            bits.append((val >> shift) & 1)

    push_bits(0b0100, 4)  # Byte mode
    push_bits(len(raw), char_count_bits)
    for b in raw:
        push_bits(b, 8)

    cap_bits = data_cap * 8
    terminator = min(4, cap_bits - len(bits))
    push_bits(0, terminator)
    while len(bits) % 8 != 0:
        bits.append(0)

    data_bytes = []
    for i in range(0, len(bits), 8):
        byte = 0
        for j in range(8):
            byte = (byte << 1) | bits[i + j]
        data_bytes.append(byte)

    pad_cycle = (0xEC, 0x11)
    pad_idx = 0
    while len(data_bytes) < data_cap:
        data_bytes.append(pad_cycle[pad_idx % 2])
        pad_idx += 1

    # Split into blocks and compute Reed-Solomon EC codewords
    data_blocks: list[list[int]] = []
    ec_blocks: list[list[int]] = []
    offset = 0
    for _ in range(g1_b):
        blk = data_bytes[offset:offset + g1_d]
        offset += g1_d
        data_blocks.append(blk)
        ec_blocks.append(_rs_remainder(blk, ec_len))
    for _ in range(g2_b):
        blk = data_bytes[offset:offset + g2_d]
        offset += g2_d
        data_blocks.append(blk)
        ec_blocks.append(_rs_remainder(blk, ec_len))

    codewords: list[int] = []
    max_data_len = max(len(b) for b in data_blocks)
    for i in range(max_data_len):
        for blk in data_blocks:
            if i < len(blk):
                codewords.append(blk[i])
    for i in range(ec_len):
        for blk in ec_blocks:
            codewords.append(blk[i])

    bitstream: list[int] = []
    for cw in codewords:
        for shift in range(7, -1, -1):
            bitstream.append((cw >> shift) & 1)

    size = 17 + ver * 4
    base: list[list[int | None]] = [[None] * size for _ in range(size)]
    reserved = [[False] * size for _ in range(size)]

    _place_finder(base, reserved, 0, 0, size)
    _place_finder(base, reserved, 0, size - 7, size)
    _place_finder(base, reserved, size - 7, 0, size)

    # Alignment patterns
    for r in align:
        for c in align:
            if not reserved[r][c]:
                _place_alignment(base, reserved, r, c)

    # Timing patterns
    for i in range(8, size - 8):
        if not reserved[6][i]:
            base[6][i] = 1 if i % 2 == 0 else 0
            reserved[6][i] = True
        if not reserved[i][6]:
            base[i][6] = 1 if i % 2 == 0 else 0
            reserved[i][6] = True

    # Dark module
    base[size - 8][8] = 1
    reserved[size - 8][8] = True

    # Reserve format info cells
    for i in range(9):
        if i != 6:
            reserved[8][i] = True
            reserved[i][8] = True
    for i in range(8):
        reserved[8][size - 1 - i] = True
        reserved[size - 1 - i][8] = True

    # Version info for V7+
    if ver >= 7:
        vbits = _version_bits(ver)
        for i in range(18):
            bit = (vbits >> i) & 1
            r = i // 3
            c = size - 11 + (i % 3)
            base[r][c] = bit
            reserved[r][c] = True
            base[c][r] = bit
            reserved[c][r] = True

    # Place data bits in upward/downward 2-column zig-zag
    bit_idx = 0
    col = size - 1
    upward = True
    while col > 0:
        if col == 6:
            col -= 1
        rows_iter = range(size - 1, -1, -1) if upward else range(size)
        for r in rows_iter:
            for dc in (0, -1):
                c = col + dc
                if not reserved[r][c]:
                    base[r][c] = bitstream[bit_idx] if bit_idx < len(bitstream) else 0
                    bit_idx += 1
        upward = not upward
        col -= 2

    best_grid = None
    best_score = None
    for mask in range(8):
        cand = [[0] * size for _ in range(size)]
        for r in range(size):
            for c in range(size):
                val = base[r][c] or 0
                if not reserved[r][c] and _mask_fn(mask, r, c):
                    val ^= 1
                cand[r][c] = val

        # Write format info
        fb = _format_bits(mask)
        for i in range(6):
            bit = (fb >> i) & 1
            cand[i][8] = bit
            cand[8][size - 1 - i] = bit
        cand[7][8] = (fb >> 6) & 1
        cand[8][size - 7] = (fb >> 6) & 1
        cand[8][8] = (fb >> 7) & 1
        cand[8][size - 8] = (fb >> 7) & 1
        cand[8][7] = (fb >> 8) & 1
        cand[size - 7][8] = (fb >> 8) & 1
        for i in range(9, 15):
            bit = (fb >> i) & 1
            cand[8][14 - i] = bit
            cand[size - 15 + i][8] = bit

        sc = _penalty(cand)
        if best_score is None or sc < best_score:
            best_score = sc
            best_grid = cand

    return [[bool(cell) for cell in row] for row in best_grid] if best_grid is not None else None
