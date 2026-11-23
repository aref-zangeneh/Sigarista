#!/usr/bin/env python3
"""Render a Sugarista receipt (customer or barista copy) to a 576-dot PNG.

The GA-E200I is an 80 mm thermal printer: 72 mm of printable width, which at
203 dpi is exactly 576 dots. Rendering at the printer's native width keeps the
output crisp — CUPS does not have to scale or dither the bitmap.

macOS does not shape Arabic text when spooling plain text, so the agent renders
each receipt as a 1-bit bitmap with proper Persian shaping (arabic_reshaper +
bidi) and sends that to CUPS.

Font: Vazirmatn (SIL OFL) — bundled in ./fonts. It covers Persian, Latin,
Persian/Arabic digits and the punctuation receipts use (— • × ٬), so a single
font renders the whole receipt. A system font is only used for glyphs Vazirmatn
does not have.
"""

import json
import re
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont
import arabic_reshaper
from bidi.algorithm import get_display

try:
    from qr_matrix import encode_qr_matrix
except Exception:
    encode_qr_matrix = None  # type: ignore[assignment]

# 72 mm printable area at 203 dpi.
WIDTH = 576
MARGIN = 18
CONTENT_W = WIDTH - MARGIN * 2

FONT_DIR = Path(__file__).resolve().parent / "fonts"
FONT_REGULAR_PATH = FONT_DIR / "Vazirmatn-Regular.ttf"
FONT_BOLD_PATH = FONT_DIR / "Vazirmatn-Bold.ttf"

# Last-resort fallback for glyphs Vazirmatn lacks (rare symbols, emoji-ish text).
FALLBACK_FONT_PATH = "/System/Library/Fonts/Helvetica.ttc"

_FONT_CACHE: dict[tuple, ImageFont.FreeTypeFont] = {}
_COVERAGE: dict[str, set[int]] = {}

RESHAPER = arabic_reshaper.ArabicReshaper(
    configuration={"delete_harakat": False, "support_ligatures": True}
)

# Latin/ASCII tokens (tracking codes, phone numbers with a leading +) must stay
# left-to-right inside an RTL line. python-bidi implements the older UBA without
# isolate support, so we bracket the token with LRM marks instead; the marks are
# stripped again before drawing because they have no glyphs.
_LTR_TOKEN = re.compile(r"[0-9\u06F0-\u06F9\u0660-\u0669A-Za-z+][0-9\u06F0-\u06F9\u0660-\u0669A-Za-z+\-_.:/@]*")
_CONTROL = re.compile("[\u200e\u200f\u202a\u202b\u202c\u2066\u2067\u2068\u2069\ufe0e\ufe0f]")
_PERSIAN_DIGIT_MAP = str.maketrans("0123456789\u0660\u0661\u0662\u0663\u0664\u0665\u0666\u0667\u0668\u0669", "۰۱۲۳۴۵۶۷۸۹۰۱۲۳۴۵۶۷۸۹")
_LATIN_CODE_TOKEN = re.compile(r"[A-Za-z][A-Za-z0-9\-_.:/@]*|[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}")


def to_persian_digits_safe(text: str) -> str:
    """Convert numeric runs to Persian digits while keeping Latin codes (e.g. SG-AU2XNN) intact."""
    if not text:
        return ""
    s = str(text)
    out = []
    last = 0
    for m in _LATIN_CODE_TOKEN.finditer(s):
        if m.start() > last:
            out.append(s[last:m.start()].translate(_PERSIAN_DIGIT_MAP))
        out.append(m.group(0))
        last = m.end()
    if last < len(s):
        out.append(s[last:].translate(_PERSIAN_DIGIT_MAP))
    return "".join(out)


# ─────────────────────────────────────────────────────────────────────────────
# Custom 203-dpi monochrome vector micro-icons (zero tofu □ boxes on paper!)
# ─────────────────────────────────────────────────────────────────────────────

VECTOR_ICON_CHARS = {"✦", "◆", "☕", "✨", "♥", "❤", "✓", "✔"}


def _draw_four_point_star(draw: ImageDraw.ImageDraw, cx: float, cy: float, r: float):
    """Crisp boutique 4-point star (✦)."""
    inner = max(1.5, r * 0.34)
    pts = [
        (cx, cy - r),
        (cx + inner, cy - inner),
        (cx + r, cy),
        (cx + inner, cy + inner),
        (cx, cy + r),
        (cx - inner, cy + inner),
        (cx - r, cy),
        (cx - inner, cy - inner),
    ]
    draw.polygon(pts, fill=0)


def _draw_vector_icon(draw: ImageDraw.ImageDraw, ch: str, x: float, y: float, size: int) -> float:
    """Draw a crisp 1-bit vector icon aligned with text of height `size`; return advance width."""
    cy = y + size * 0.56
    if ch == "✦":
        w = max(12.0, size * 0.68)
        cx = x + w * 0.5
        r = max(4.5, size * 0.30)
        _draw_four_point_star(draw, cx, cy, r)
        return w

    if ch == "◆":
        w = max(11.0, size * 0.62)
        cx = x + w * 0.5
        r = max(4.0, size * 0.25)
        draw.polygon([(cx, cy - r), (cx + r, cy), (cx, cy + r), (cx - r, cy)], fill=0)
        return w

    if ch == "✨":
        w = max(18.0, size * 0.96)
        _draw_four_point_star(draw, x + w * 0.42, cy + size * 0.02, max(5.0, size * 0.30))
        _draw_four_point_star(draw, x + w * 0.80, cy - size * 0.24, max(3.0, size * 0.16))
        _draw_four_point_star(draw, x + w * 0.78, cy + size * 0.24, max(2.5, size * 0.13))
        return w

    if ch == "☕":
        w = max(22.0, size * 1.15)
        cx = x + w * 0.44
        cup_w = max(10.0, size * 0.52)
        cup_h = max(9.0, size * 0.44)
        top_y = cy - cup_h * 0.35
        bot_y = top_y + cup_h
        left_x = cx - cup_w * 0.5
        right_x = cx + cup_w * 0.5
        # Cup body (rounded bottom bowl)
        draw.rounded_rectangle([(left_x, top_y), (right_x, bot_y)], radius=max(3, int(size * 0.16)), fill=0)
        draw.rectangle([(left_x, top_y), (right_x, top_y + cup_h * 0.45)], fill=0)
        # Handle on right side
        h_w = max(4.0, size * 0.20)
        draw.arc(
            [(right_x - h_w * 0.4, top_y + 1), (right_x + h_w, bot_y - cup_h * 0.22)],
            start=-90,
            end=90,
            fill=0,
            width=max(2, int(size * 0.09)),
        )
        # Saucer line beneath the cup
        draw.line(
            [(left_x - 2, bot_y + 3), (right_x + 3, bot_y + 3)],
            fill=0,
            width=max(2, int(size * 0.08)),
        )
        # Two delicate steam curls above the rim
        sw = max(1, int(size * 0.07))
        for offset in (-cup_w * 0.18, cup_w * 0.18):
            sx = cx + offset
            draw.line([(sx, top_y - 2), (sx - 1.5, top_y - size * 0.15), (sx + 1.0, top_y - size * 0.28)], fill=0, width=sw)
        return w

    if ch in ("♥", "❤"):
        w = max(14.0, size * 0.74)
        cx = x + w * 0.5
        r = max(3.2, size * 0.19)
        top_y = cy - r * 0.6
        draw.ellipse([(cx - r * 1.9, top_y - r), (cx + r * 0.1, top_y + r)], fill=0)
        draw.ellipse([(cx - r * 0.1, top_y - r), (cx + r * 1.9, top_y + r)], fill=0)
        draw.polygon([(cx - r * 1.85, top_y + r * 0.25), (cx + r * 1.85, top_y + r * 0.25), (cx, cy + r * 2.1)], fill=0)
        return w

    if ch in ("✓", "✔"):
        w = max(14.0, size * 0.72)
        lw = max(2, int(size * 0.11))
        draw.line(
            [
                (x + w * 0.15, cy + size * 0.04),
                (x + w * 0.42, cy + size * 0.26),
                (x + w * 0.86, cy - size * 0.24),
            ],
            fill=0,
            width=lw,
        )
        return w

    return 0.0


def _vector_icon_width(ch: str, size: int) -> float:
    if ch == "✦":
        return max(12.0, size * 0.68)
    if ch == "◆":
        return max(11.0, size * 0.62)
    if ch == "✨":
        return max(18.0, size * 0.96)
    if ch == "☕":
        return max(22.0, size * 1.15)
    if ch in ("♥", "❤"):
        return max(14.0, size * 0.74)
    if ch in ("✓", "✔"):
        return max(14.0, size * 0.72)
    return 0.0


def _isolate_token(match: re.Match) -> str:
    token = match.group(0)
    # Persian digits / plain numbers already render correctly; only protect
    # tokens that contain Latin letters, a sign such as "+", or digit groups
    # joined by a hyphen (e.g. 021-12345678) — those would otherwise be
    # reordered as separate RTL number runs.
    if not re.search(r"[A-Za-z+]", token) and not re.search(r"\d-\d", token):
        return token
    return "\u200e" + token + "\u200e"


def rtl(text: str) -> str:
    """Shape Persian/Arabic and apply bidi ordering for drawing."""
    if not text:
        return ""
    localized = to_persian_digits_safe(str(text))
    prepared = _LTR_TOKEN.sub(_isolate_token, localized)
    visual = get_display(RESHAPER.reshape(prepared))
    return _CONTROL.sub("", visual)


def _coverage(font_path: str) -> set[int] | None:
    """Codepoints a font can draw, or None when fontTools is unavailable."""
    if font_path in _COVERAGE:
        return _COVERAGE[font_path]
    try:
        from fontTools.ttLib import TTFont
    except ImportError:
        _COVERAGE[font_path] = None  # type: ignore[assignment]
        return None
    try:
        cmap = TTFont(font_path, lazy=True).getBestCmap() or {}
        covered = set(cmap.keys())
    except Exception:
        covered = None  # type: ignore[assignment]
    _COVERAGE[font_path] = covered  # type: ignore[assignment]
    return covered


def _has_glyph(font_path: str, ch: str) -> bool:
    covered = _coverage(font_path)
    if covered is None:
        return True
    return ord(ch) in covered


def font(size: int, bold: bool = False) -> ImageFont.FreeTypeFont:
    """The bundled Vazirmatn face at the requested size."""
    key = (size, bold)
    cached = _FONT_CACHE.get(key)
    if cached is not None:
        return cached
    path = FONT_BOLD_PATH if bold else FONT_REGULAR_PATH
    loaded = ImageFont.truetype(str(path), size)
    _FONT_CACHE[key] = loaded
    return loaded


def char_font(ch: str, size: int, bold: bool) -> ImageFont.FreeTypeFont | None:
    """Vazirmatn, falling back to Helvetica only if Helvetica actually has the glyph."""
    primary = FONT_BOLD_PATH if bold else FONT_REGULAR_PATH
    if _has_glyph(str(primary), ch):
        return font(size, bold)
    if not _has_glyph(FALLBACK_FONT_PATH, ch):
        return None
    key = (size, bold, "fallback")
    cached = _FONT_CACHE.get(key)
    if cached is None:
        cached = ImageFont.truetype(FALLBACK_FONT_PATH, size, index=1 if bold else 0)
        _FONT_CACHE[key] = cached
    return cached


def visual_width(text: str, size: int, bold: bool = False) -> float:
    """Width of an already shaped/bidi text string, summing per-glyph widths."""
    total = 0.0
    for ch in text:
        if ch in VECTOR_ICON_CHARS:
            total += _vector_icon_width(ch, size)
            continue
        f = char_font(ch, size, bold)
        if f is not None:
            total += f.getlength(ch)
    return total


def draw_visual(draw: ImageDraw.ImageDraw, x: float, y: float, text: str, size: int, bold=False):
    """Draw a shaped/bidi visual string with custom vector icons & per-character fallback."""
    cursor = x
    for ch in text:
        if ch in VECTOR_ICON_CHARS:
            cursor += _draw_vector_icon(draw, ch, cursor, y, size)
            continue
        f = char_font(ch, size, bold)
        if f is None:
            continue
        draw.text((cursor, y), ch, font=f, fill=0)
        cursor += f.getlength(ch)


def wrap(text: str, size: int, bold: bool, max_width: int) -> list[str]:
    """Wrap logical (unshaped) text so each rendered line fits max_width."""
    if not text:
        return [""]
    words = str(text).split()
    lines, current = [], ""
    for word in words:
        candidate = (current + " " + word).strip()
        if visual_width(rtl(candidate), size, bold) <= max_width or not current:
            current = candidate
        else:
            lines.append(current)
            current = word
    if current:
        lines.append(current)
    return lines or [""]


class Receipt:
    """A tall canvas we draw on and then crop to the printed content."""

    def __init__(self, top_padding: int = 24, bottom_pad: int = 96):
        self.height = 2600
        self.image = Image.new("1", (WIDTH, self.height), 1)
        self.draw = ImageDraw.Draw(self.image)
        self.y = max(12, int(top_padding))
        self.bottom_pad = max(24, int(bottom_pad))

    def _ensure(self, extra: int):
        if self.y + extra < self.height:
            return
        grown = Image.new("1", (WIDTH, self.height + 1600), 1)
        grown.paste(self.image, (0, 0))
        self.image = grown
        self.height += 1600
        self.draw = ImageDraw.Draw(self.image)

    def _line_height(self, size: int, bold: bool = False) -> int:
        try:
            ascent, descent = font(size, bold).getmetrics()
            return max(size + 4, int(ascent + descent))
        except Exception:
            return int(size * 1.35)

    def centered(self, text: str, size: int, bold=False, gap=8, auto_fit: bool = True):
        if not text:
            return
        lines = wrap(text, size, bold, CONTENT_W)
        # If any single wrapped line still exceeds CONTENT_W (e.g. a very long single word
        # or large cafe brand name), gently step down font size so left/right edges never clip.
        effective_size = size
        if auto_fit:
            while effective_size > 16:
                max_w = max((visual_width(rtl(line), effective_size, bold) for line in lines), default=0.0)
                if max_w <= CONTENT_W:
                    break
                effective_size -= 2
                lines = wrap(text, effective_size, bold, CONTENT_W)

        lh = self._line_height(effective_size, bold)
        for line in lines:
            self._ensure(lh + gap + 6)
            visual = rtl(line)
            w = visual_width(visual, effective_size, bold)
            x = max(float(MARGIN), (WIDTH - w) / 2.0)
            draw_visual(self.draw, x, self.y, visual, effective_size, bold)
            self.y += lh + gap

    def row(self, right_text: str, left_text: str = "", size=26, bold=False, gap=10):
        """RTL row: right_text hugs the right margin, left_text the left."""
        lh = self._line_height(size, bold)
        self._ensure(lh + gap)
        if right_text:
            visual = rtl(right_text)
            w = visual_width(visual, size, bold)
            draw_visual(self.draw, max(float(MARGIN), WIDTH - MARGIN - w), self.y, visual, size, bold)
        if left_text:
            visual = rtl(left_text)
            draw_visual(self.draw, MARGIN, self.y, visual, size, bold)
        self.y += lh + gap

    def paragraph(self, text: str, size=22, bold=False, indent=0, gap=6):
        lh = self._line_height(size, bold)
        for line in wrap(text, size, bold, CONTENT_W - indent):
            self._ensure(lh + gap)
            visual = rtl(line)
            w = visual_width(visual, size, bold)
            draw_visual(self.draw, max(float(MARGIN), WIDTH - MARGIN - indent - w), self.y, visual, size, bold)
            self.y += lh + gap

    def divider(self, style="dashed"):
        self._ensure(22)
        y = self.y + 8
        if style == "double":
            self.draw.line([(MARGIN, y), (WIDTH - MARGIN, y)], fill=0, width=3)
            self.draw.line([(MARGIN, y + 6), (WIDTH - MARGIN, y + 6)], fill=0, width=2)
        elif style == "solid":
            self.draw.line([(MARGIN, y), (WIDTH - MARGIN, y)], fill=0, width=2)
        elif style == "dotted":
            x = MARGIN + 4
            while x < WIDTH - MARGIN - 4:
                self.draw.ellipse([(x, y - 1), (x + 3, y + 2)], fill=0)
                x += 10
        else:
            x = MARGIN
            while x < WIDTH - MARGIN:
                self.draw.line([(x, y), (min(x + 9, WIDTH - MARGIN), y)], fill=0, width=2)
                x += 16
        self.y += 22

    def badge_box(self, title: str, code: str, size: int = 28, gap: int = 12, box_style: str = "rounded"):
        """Draw an order-code highlight honoring the admin-selected box style."""
        full_text = f"{title}: {code}"
        if box_style == "minimal":
            self.centered(full_text, size, bold=True, gap=gap)
            return

        lh = self._line_height(size, True)
        box_h = lh + 22
        self._ensure(box_h + gap)
        y0 = self.y + 4
        y1 = y0 + box_h
        x0 = MARGIN + 4
        x1 = WIDTH - MARGIN - 4
        if box_style == "solid":
            self.draw.rectangle([(x0, y0), (x1, y1)], outline=0, width=3)
        else:
            self.draw.rounded_rectangle([(x0, y0), (x1, y1)], radius=12, outline=0, width=2)
        visual = rtl(full_text)
        w = visual_width(visual, size, True)
        draw_visual(self.draw, max(float(x0 + 6), (WIDTH - w) / 2.0), y0 + 9, visual, size, True)
        self.y = y1 + gap

    def boxed_card(self, lines: list[tuple[str, int, bool]], gap: int = 12, dashed: bool = False):
        """Draw a framed info/promo/Wi-Fi card centered on the receipt."""
        if not lines:
            return
        pad_x = 16
        pad_y = 12
        inner_w = CONTENT_W - pad_x * 2 - 8
        rendered_lines: list[tuple[str, int, bool, int]] = []
        total_h = pad_y * 2
        for text, sz, is_bold in lines:
            lh = self._line_height(sz, is_bold)
            for wrapped in wrap(text, sz, is_bold, inner_w):
                rendered_lines.append((wrapped, sz, is_bold, lh))
                total_h += lh + 4
        if rendered_lines:
            total_h -= 4

        self._ensure(total_h + gap + 8)
        x0 = MARGIN + 4
        x1 = WIDTH - MARGIN - 4
        y0 = self.y + 2
        y1 = y0 + total_h
        if dashed:
            self.draw.rounded_rectangle([(x0, y0), (x1, y1)], radius=10, outline=0, width=2)
            self.draw.rounded_rectangle([(x0 + 4, y0 + 4), (x1 - 4, y1 - 4)], radius=7, outline=0, width=1)
        else:
            self.draw.rounded_rectangle([(x0, y0), (x1, y1)], radius=10, outline=0, width=2)

        cy = y0 + pad_y
        for text, sz, is_bold, lh in rendered_lines:
            visual = rtl(text)
            w = visual_width(visual, sz, is_bold)
            draw_visual(self.draw, max(float(x0 + 8), (WIDTH - w) / 2.0), cy, visual, sz, is_bold)
            cy += lh + 4
        self.y = y1 + gap

    def qr_code(self, url: str, label: str = "", target_px: int = 196, gap: int = 12):
        """Draw a crisp 1-bit ISO/IEC 18004 QR code centered on the receipt."""
        if not url or encode_qr_matrix is None:
            return
        matrix = encode_qr_matrix(url)
        if not matrix:
            return
        n = len(matrix)
        scale = max(3, min(8, target_px // n))
        qr_px = n * scale
        quiet = scale * 2
        total_box = qr_px + quiet * 2
        self._ensure(total_box + gap + 60)

        x0 = (WIDTH - qr_px) // 2
        y0 = self.y + quiet // 2
        for r_idx, row in enumerate(matrix):
            for c_idx, dark in enumerate(row):
                if dark:
                    rx = x0 + c_idx * scale
                    ry = y0 + r_idx * scale
                    self.draw.rectangle([(rx, ry), (rx + scale - 1, ry + scale - 1)], fill=0)

        self.y = y0 + qr_px + 10
        if label:
            self.centered(label, 20, bold=False, gap=gap)
        else:
            self.y += gap

    def cafe_ornament(self, icon: str = "☕", gap: int = 10):
        """Draw a boutique cafe divider with centered vector icon flanked by stars & lines."""
        if not icon or icon == "none":
            return
        h = 34
        self._ensure(h + gap)
        center_seq = f"✦  {icon}  ✦"
        icon_size = 24
        seq_w = visual_width(center_seq, icon_size, False)
        cx_start = (WIDTH - seq_w) / 2
        line_y = self.y + icon_size * 0.56
        pad = 14
        if cx_start - pad > MARGIN:
            self.draw.line([(MARGIN, line_y), (cx_start - pad, line_y)], fill=0, width=2)
            self.draw.line([(cx_start + seq_w + pad, line_y), (WIDTH - MARGIN, line_y)], fill=0, width=2)
        draw_visual(self.draw, cx_start, self.y, center_seq, icon_size, False)
        self.y += h + gap

    def gap(self, pixels=10):
        self.y += pixels

    def cropped_image(self, bottom_pad: int | None = None) -> Image.Image:
        """The drawn receipt cropped to its full ink bounding box plus safe trailing space."""
        pad = self.bottom_pad if bottom_pad is None else int(bottom_pad)
        bbox = self.image.point(lambda p: 0 if p else 255).getbbox()
        if bbox is None:
            content_bottom = self.y
        else:
            # Always keep the full ink bottom (never min()) so bottom cafe name/descenders never clip!
            content_bottom = max(bbox[3] + 18, self.y)
        height = max(content_bottom + pad, 160)
        return self.image.crop((0, 0, WIDTH, min(height, self.height)))

    def save(self, path: str, bottom_pad: int | None = None):
        self.cropped_image(bottom_pad).save(path, "PNG")


# ─────────────────────────────────────────────────────────────────────────────
# Raw ESC/POS emission (printer-fix): we no longer trust the Gainscha CUPS
# raster filter (rastertopdt) — a damaged queue + that filter produced hex-
# garbage on paper. Instead render.py emits the exact ESC/POS byte stream:
#   ESC @ (init) → GS v 0 raster (576 dots = 72 bytes/line) → ESC d feed →
#   GS V cut. The agent prints it with `lp -o raw`, bypassing every filter.
# Mode "1": PIL pixel 0 = black; ESC/POS raster bit 1 = printed dot → same.
# ─────────────────────────────────────────────────────────────────────────────

ESC_INIT = b"\x1b@"          # initialize printer
ESC_FEED_LINES = b"\x1bd\x04"  # print and feed 4 lines (cutter clearance)
GS_CUT = b"\x1dV\x00"        # full cut at the paper's current position


def receipt_to_escpos(receipt: Receipt, bottom_pad: int | None = None) -> bytes:
    image = receipt.cropped_image(bottom_pad)
    width, height = image.size
    if width % 8 != 0:
        padded = Image.new("1", ((width + 7) // 8) * 8, 1)
        padded.paste(image, (0, 0))
        image = padded
        width = image.size[0]
    bytes_per_row = width // 8

    pixels = image.load()
    rows = bytearray()
    for y in range(height):
        row = bytearray(bytes_per_row)
        for x in range(width):
            if pixels[x, y] == 0:  # black dot
                row[x >> 3] |= 0x80 >> (x & 7)
        rows += row

    raster = b"\x1d\x76\x30\x00" + (bytes_per_row & 0xFF).to_bytes(1, "little") \
        + (bytes_per_row >> 8).to_bytes(1, "little") \
        + (height & 0xFF).to_bytes(1, "little") \
        + (height >> 8).to_bytes(1, "little")
    return raster + bytes(rows)


def normalize_keys(value):
    """Payload keys may arrive PascalCase (C# records) or camelCase — fold to camelCase."""
    if isinstance(value, dict):
        return {
            (key[:1].lower() + key[1:] if key else key): normalize_keys(item)
            for key, item in value.items()
        }
    if isinstance(value, list):
        return [normalize_keys(item) for item in value]
    return value


def copy_type_of(payload) -> str:
    value = payload.get("copyType", "customer")
    if isinstance(value, int):
        return "barista" if value == 1 else "customer"
    return str(value).lower()


# ─────────────────────────────────────────────────────────────────────────────
# Dynamic layout support (TASK-014): payload["layout"]["customer"|"barista"]
# drives which rows/columns are printed, their labels and font sizes. Any
# missing piece falls back to the classic layout.
# ─────────────────────────────────────────────────────────────────────────────

DEFAULT_CUSTOMER_HEADER_ROW_LABELS = {
    "code": "کد سفارش",
    "pickupTime": "تحویل",
    "note": "یادداشت",
    "service": "",
    "table": "میز",
    "createdAt": "",
    "channel": "",
    "customerName": "",
    "customerPhone": "",
}

DEFAULT_BARISTA_INFO_LABELS = {
    "paidAt": "",
    "createdAt": "",
    "channel": "",
    "service": "",
    "table": "میز",
    "customerName": "مشتری",
    "customerPhone": "",
    "paymentMethod": "روش پرداخت",
}

BAND_COLUMN_KEYS = ("unitPrice", "total")


def copy_layout(payload, copy: str) -> dict:
    layouts = payload.get("layout", {})
    if isinstance(layouts, dict):
        own = layouts.get(copy)
        if isinstance(own, dict):
            return own
    return {}


def font_size(copy_layout_data: dict, key: str, default: int) -> int:
    fonts = copy_layout_data.get("fonts", {})
    if isinstance(fonts, dict):
        value = fonts.get(key)
        if isinstance(value, (int, float)) and value:
            return int(value)
    return default


def option_text(item: dict, layout_items: dict) -> list[str]:
    """Chosen per-menu-item options for the line (TASK-013)."""
    options = item.get("options")
    if isinstance(options, list):
        show_prices = bool(layout_items.get("showOptionPrices"))
        lines = []
        for option in options:
            if not isinstance(option, dict):
                continue
            name = str(option.get("optionName") or "").strip()
            if not name:
                continue
            price = option.get("priceDeltaLabel")
            if show_prices and price:
                lines.append(f"✦ {name} (+{price} ت)")
            else:
                lines.append(f"✦ {name}")
        return lines
    # Legacy payload fallback.
    labels = item.get("optionLabels")
    if isinstance(labels, list):
        return [f"✦ {str(label)}" for label in labels if label]
    return []


def sub_lines(item: dict, sub_line_keys: list[str], layout_items: dict) -> list[str]:
    """Detail lines under an item name, rendered in admin-defined order."""
    lines: list[str] = []
    for key in sub_line_keys:
        if key == "size":
            if item.get("sizeLabel"):
                lines.append(f"اندازه: {item['sizeLabel']}")
        elif key == "options":
            lines.extend(option_text(item, layout_items))
        elif key == "modifiers":
            for modifier in item.get("modifierLabels") or []:
                lines.append(f"✦ {modifier}")
        elif key == "customDrink":
            for summary in item.get("customDrinkSummary") or []:
                lines.append(f"✦ {summary}")
        elif key == "payer":
            if item.get("payerLabel"):
                lines.append(f"سهم {item['payerLabel']}")
        elif key == "baristaNote":
            if item.get("baristaNote"):
                lines.append(f"! {item['baristaNote']}")
    return lines


def visible_columns(layout_items: dict, default_columns: list[dict]) -> list[dict]:
    columns = layout_items.get("columns")
    if not isinstance(columns, list) or not columns:
        columns = default_columns
    result = []
    for column in columns:
        if isinstance(column, dict) and column.get("visible", True):
            result.append(column)
    return result


def info_rows(layout_data: dict, rows_key: str, default_rows: list[str]) -> list[str]:
    rows = layout_data.get(rows_key)
    if not isinstance(rows, list) or not rows:
        rows = default_rows
    return [str(row) for row in rows if row]


def service_with_table(order: dict) -> str:
    return (order.get("serviceLabel") or "")


def customer_header_row(contract, payload, order: dict, key: str) -> None:
    r, size, code_box_style = contract
    if key == "code":
        code = order.get("code", "")
        if code:
            r.badge_box("شماره سفارش", f"#{code}", size=26, gap=10, box_style=code_box_style)
    elif key == "createdAt":
        created = order.get("createdAtLabel", "")
        if created:
            r.row("تاریخ و زمان ثبت", created, size=size)
    elif key == "channel":
        channel = order.get("channelLabel", "")
        if channel:
            r.row("نوع سفارش", channel, size=size)
    elif key == "service":
        service = service_with_table(order)
        if service:
            r.row("شیوهٔ پذیرایی", service, size=size, bold=True)
    elif key == "table":
        table = order.get("tableLabel")
        if table:
            r.row("شماره میز", f"میز {table}", size=size, bold=True)
    elif key == "pickupTime":
        pickup = order.get("pickupAtLabel")
        if pickup:
            r.row("زمان آماده‌سازی", pickup, size=size, bold=True)
    elif key == "customerName":
        name = order.get("customerName")
        if name:
            r.row("مشتری گرامی", name, size=size)
    elif key == "customerPhone":
        phone = order.get("customerPhone")
        if phone:
            r.row("شماره همراه", phone, size=size)
    elif key == "note":
        note = order.get("note")
        if note:
            r.paragraph(f"توضیحات مهمان: {note}", size=22, bold=False, indent=10)


def barista_info_row(contract, payload, order: dict, key: str) -> None:
    r, size = contract
    if key == "paidAt":
        r.row(order.get("paidAtLabel") or order.get("createdAtLabel", ""), "", size=size)
    elif key == "createdAt":
        r.row(order.get("createdAtLabel", ""), "", size=size)
    elif key == "channel":
        r.row(order.get("channelLabel", ""), "", size=size)
    elif key == "service":
        service = service_with_table(order)
        if service:
            r.paragraph(service, size=size, bold=True)
    elif key == "table":
        if order.get("tableLabel"):
            r.row(f"میز {order['tableLabel']}", "", size=size)
    elif key == "customerName":
        if order.get("customerName"):
            r.row(f"مشتری: {order['customerName']}", "", size=size)
    elif key == "customerPhone":
        if order.get("customerPhone"):
            r.row(order["customerPhone"], "", size=size)
    elif key == "paymentMethod":
        r.row("روش پرداخت", order.get("paymentLabel", ""), size=size)


def draw_items_table(r: Receipt, payload: dict, items: list[dict], layout_data: dict):
    """Items table honoring the admin-configured columns, widths and fonts.

    Geometry: `name` owns the flexible right zone (qty is drawn inline as
    "۲ × اسپرسو"); `unitPrice`/`total` form annotation bands stacked from the
    left margin in document order. An item row updates r.y normally; every
    band value is aligned with the item's first name line.
    """
    is_barista = bool(layout_data.get("_barista"))

    default_columns = (
        [{"key": "name", "visible": True}, {"key": "qty", "visible": True}]
        if is_barista
        else [
            {"key": "name", "visible": True},
            {"key": "qty", "visible": True},
            {"key": "unitPrice", "visible": False},
            {"key": "total", "visible": True, "label": "جمع"},
        ]
    )
    layout_items = layout_data.get("items") if isinstance(layout_data.get("items"), dict) else {}
    columns = visible_columns(layout_items, default_columns)
    default_sub_lines = BARISTA_DEFAULT_SUB_LINES if is_barista else CUSTOMER_DEFAULT_SUB_LINES
    sub_line_keys = info_rows(layout_items if not is_barista else layout_data, "subLines", default_sub_lines)

    size = font_size(layout_data, "item", 38 if is_barista else 30)
    show_header = bool(layout_items.get("showHeaderRow", False))

    qty_visible = any(column.get("key") == "qty" for column in columns)
    bands = [column for column in columns if column.get("key") in BAND_COLUMN_KEYS]

    # ── band widths and positions (left margin → rightwards) ────────────────
    band_values: dict[str, list[str]] = {}
    band_labels: dict[str, str] = {}
    for column in bands:
        key = column["key"]
        band_values[key] = [
            str(item.get("unitPriceLabel") or "" if key == "unitPrice" else item.get("totalLabel") or "")
            for item in items
        ]
        band_labels[key] = str(column.get("label") or ("واحد" if key == "unitPrice" else "جمع"))

    band_widths: dict[str, float] = {}
    for column in bands:
        key = column["key"]
        texts = band_values[key] + ([band_labels[key]] if show_header else [])
        measured = max(visual_width(rtl(text), size, False) for text in texts) if texts else 0.0
        pct = column.get("widthPct")
        assigned = (float(pct) / 100.0) * CONTENT_W if pct else max(measured + 24.0, 96.0)
        band_widths[key] = min(assigned, CONTENT_W * 0.9)

    band_positions: dict[str, tuple[float, float]] = {}
    x = MARGIN
    for column in bands:
        key = column["key"]
        band_positions[key] = (x, band_widths[key])
        x += band_widths[key]

    # The name zone spans from the end of the bands to the right margin.
    name_width = max((WIDTH - MARGIN) - x, int(CONTENT_W * 0.4))
    name_left = (WIDTH - MARGIN) - name_width

    def draw_band_text(band_y: float, key: str, text: str, bold=False):
        start, width = band_positions[key]
        if not text:
            return
        visual = rtl(text)
        w = min(visual_width(visual, size, bold), width - 4)
        draw_visual(r.draw, start + max(0.0, (width - w)), band_y, visual, size, bold)

    if show_header and bands:
        header_y = r.y
        r._ensure(size + 12)
        for column in bands:
            draw_band_text(header_y, column["key"], band_labels[column["key"]])
        r.y += size + 12

    for index, item in enumerate(items):
        qty = item.get("quantityLabel") or str(item.get("quantity", 1))
        name = item.get("name", "")
        name_text = f"{qty} × {name}" if qty_visible else name

        lines = wrap(name_text, size, True, name_width)
        if not lines:
            lines = [""]

        for line_index, line in enumerate(lines):
            r._ensure(size + 8)
            visual = rtl(line)
            w = visual_width(visual, size, True)
            if line_index == 0:
                for column in bands:
                    draw_band_text(r.y, column["key"], band_values[column["key"]][index])
            draw_visual(r.draw, (WIDTH - MARGIN) - w, r.y, visual, size, True)
            r.y += size + 8

        # Detail lines under the name, honoring the admin-defined order.
        details = sub_lines(item, sub_line_keys, layout_items)
        if details:
            joined = "   ".join(details)
            detail_width = max(name_width - 16, 120)
            for wrapped_detail in wrap(joined, max(20, size - 8), False, detail_width):
                r._ensure(size)
                visual = rtl(wrapped_detail)
                w = visual_width(visual, max(20, size - 8), False)
                draw_visual(r.draw, (WIDTH - MARGIN) - w, r.y, visual, max(20, size - 8), False)
                r.y += (max(20, size - 8)) + 6

        # Note of the unit price when it has no column of its own (customer copy).
        unit_price_column = any(column.get("key") == "unitPrice" for column in columns)
        show_note = bool(layout_items.get("showUnitPriceNote", True))
        if (
            not is_barista
            and show_note
            and not unit_price_column
            and item.get("unitPriceLabel")
            and item.get("quantity", 1) > 1
        ):
            r.paragraph(f"نرخ واحد (فی): {item['unitPriceLabel']} تومان", size=20, indent=16, gap=4)
        if index < len(items) - 1 and not is_barista:
            r.gap(4)
            r.divider("dotted")
            r.gap(2)
        else:
            r.gap(6)


# Default detail lines per copy (classic layout).
CUSTOMER_DEFAULT_SUB_LINES = ["size", "options", "modifiers", "customDrink", "payer"]
BARISTA_DEFAULT_SUB_LINES = ["size", "modifiers", "options", "customDrink", "baristaNote"]


def customer_receipt(payload) -> Receipt:
    layout_data = dict(copy_layout(payload, "customer"))
    layout_data["_barista"] = False
    header = payload.get("header", {})
    order = payload.get("order", {})
    items = payload.get("items", [])
    totals = payload.get("totals", {})

    fonts_section = layout_data.get("fonts", {}) if isinstance(layout_data.get("fonts"), dict) else {}
    top_padding = int(fonts_section.get("topPadding", 24) or 24)
    bottom_padding = int(fonts_section.get("bottomPadding", 96) or 96)
    r = Receipt(top_padding=top_padding, bottom_pad=bottom_padding)

    fonts_header = font_size(layout_data, "header", 40)
    fonts_info = font_size(layout_data, "info", 22)
    fonts_footer_cafe = int(fonts_section.get("footerCafe", 22) or 22)

    header_section = layout_data.get("header", {}) if isinstance(layout_data.get("header"), dict) else {}
    style_section = layout_data.get("style", {}) if isinstance(layout_data.get("style"), dict) else {}
    divider_style = str(style_section.get("dividerStyle") or "dashed")
    ornament_icon = str(style_section.get("ornamentIcon") or "☕")
    code_box_style = str(style_section.get("codeBoxStyle") or "rounded")

    cafe_name = (header_section.get("cafeName") or "").strip() or header.get("cafeName") or "کافه شوگاریستا"
    r.centered(cafe_name, fonts_header, bold=True, gap=6)

    subtitle = header_section.get("subtitle")
    if subtitle is None:
        subtitle = "صورت‌حساب سفارش"
    subtitle = str(subtitle).strip()
    if subtitle:
        r.centered(subtitle, fonts_info, bold=False, gap=6)

    resolved_address = (header_section.get("customAddress") or "").strip() or (header.get("address") or "").strip()
    resolved_phone = (header_section.get("customPhone") or "").strip() or (header.get("contactPhone") or "").strip()
    tax_id = (header_section.get("taxId") or "").strip()

    if header_section.get("showAddress", True) and resolved_address:
        r.centered(resolved_address, max(18, fonts_info - 2), gap=4)
    if header_section.get("showPhone", True) and resolved_phone:
        r.centered(f"تلفن تماس: {resolved_phone}", max(18, fonts_info - 2), gap=4)
    if tax_id:
        r.centered(f"شناسه اقتصادی / صنفی: {tax_id}", max(17, fonts_info - 3), gap=4)

    r.gap(4)
    if header_section.get("showOrnament", True) and ornament_icon != "none":
        r.cafe_ornament(ornament_icon, gap=8)
    else:
        r.divider(divider_style)
    r.gap(2)

    for row_key in info_rows(layout_data, "headerRows", list(DEFAULT_CUSTOMER_HEADER_ROW_LABELS)):
        customer_header_row((r, fonts_info, code_box_style), payload, order, row_key)
    r.gap(4)
    r.divider(divider_style)
    r.gap(6)

    draw_items_table(r, payload, items, layout_data)

    r.divider(divider_style)
    r.gap(4)

    # Admin-configured totals rows.
    totals_section = layout_data.get("totals", {}) if isinstance(layout_data.get("totals"), dict) else {}
    if totals_section.get("showItemCount", False) and items:
        total_qty = sum(int(it.get("quantity", 1) or 1) for it in items)
        r.row("تعداد کل اقلام", f"{to_persian_digits_safe(str(total_qty))} عدد", size=max(18, fonts_info))

    rows = totals_section.get("rows")
    if not isinstance(rows, list) or not rows:
        rows = [
            {"key": "subtotal", "label": "جمع کل اقلام", "visible": True},
            {"key": "deliveryFee", "label": "هزینه ارسال / پیک", "visible": True},
            {"key": "tax", "label": "مالیات بر ارزش افزوده", "visible": True},
            {"key": "discount", "label": "تخفیف", "visible": True},
            {"key": "total", "label": "مبلغ قابل پرداخت", "visible": True},
        ]
    subtotal_font = font_size(layout_data, "subtotal", 24)
    total_font = font_size(layout_data, "total", 36)
    has_explicit_discount_row = any(isinstance(row, dict) and row.get("key") == "discount" for row in rows)
    discount_rendered = False

    def render_discount_row(custom_label=""):
        nonlocal discount_rendered
        if discount_rendered or not totals.get("discountLabel"):
            return
        discount_rendered = True
        discount_title = totals.get("discountTitle") or custom_label or "تخفیف"
        r.row(discount_title, f"-{totals['discountLabel']} ت", size=subtotal_font, bold=True)
        discount_note = totals.get("discountNote")
        if discount_note:
            r.row("بابت تخفیف", str(discount_note), size=max(18, subtotal_font - 4))

    for row in rows:
        if not isinstance(row, dict) or not row.get("visible", True):
            continue
        key = row.get("key")
        label = str(row.get("label") or "")
        if key == "subtotal" and totals.get("subtotalLabel"):
            r.row(label or "جمع کل اقلام", f"{totals['subtotalLabel']} ت", size=subtotal_font)
        elif key == "deliveryFee" and totals.get("deliveryFeeLabel"):
            r.row(label or "هزینه ارسال / پیک", f"{totals['deliveryFeeLabel']} ت", size=subtotal_font)
        elif key == "tax" and totals.get("taxLabel"):
            r.row(label or "مالیات بر ارزش افزوده", f"{totals['taxLabel']} ت", size=subtotal_font)
        elif key == "discount":
            render_discount_row(label)
        elif key == "total":
            if not has_explicit_discount_row and totals.get("discountLabel"):
                render_discount_row()
            r.gap(4)
            r.divider("double")
            r.gap(2)
            total_val = totals.get("totalLabel", "")
            val_display = f"{total_val} تومان" if total_val and not str(total_val).endswith("تومان") else total_val
            r.row(label or "مبلغ قابل پرداخت", val_display, size=total_font, bold=True, gap=14)
            r.divider("double")
            r.gap(4)

    if totals_section.get("showSavingsBanner", True) and totals.get("discountLabel"):
        r.boxed_card(
            [(f"✨ سود شما از این سفارش: {totals['discountLabel']} تومان ✨", max(19, fonts_info), True)],
            gap=10,
        )

    for row_key in info_rows(layout_data, "paymentRows", ["paymentMethod", "reference", "amountPaid"]):
        if row_key == "paymentMethod":
            method = order.get("paymentLabel") or "کارت‌خوان (پوز)"
            r.row("شیوهٔ پرداخت", method, size=fonts_info)
        elif row_key == "reference":
            ref = order.get("reference")
            if ref:
                r.row("کد رهگیری بانکی", str(ref), size=fonts_info)
        elif row_key == "amountPaid":
            amount_paid = totals.get("amountPaidLabel")
            if amount_paid:
                paid_display = f"{amount_paid} تومان (تسویه شد)" if not str(amount_paid).endswith("تومان") else f"{amount_paid} (تسویه شد)"
                r.row("وضعیت پرداخت", paid_display, size=fonts_info, bold=True)

    r.gap(6)
    r.divider("dotted" if divider_style == "dashed" else divider_style)
    r.gap(6)
    footer = layout_data.get("footer", {}) if isinstance(layout_data.get("footer"), dict) else {}

    promo_banner = (footer.get("promoBanner") or "").strip()
    if promo_banner:
        r.boxed_card([(promo_banner, max(20, fonts_info), True)], gap=10, dashed=True)

    if footer.get("showWifi", False):
        wifi_ssid = (footer.get("wifiSsid") or "").strip()
        wifi_pass = (footer.get("wifiPassword") or "").strip()
        if wifi_ssid or wifi_pass:
            wifi_lines = [("وای‌فای رایگان مهمان (Wi-Fi)", max(18, fonts_info - 1), True)]
            if wifi_ssid:
                wifi_lines.append((f"شبکه: {wifi_ssid}", max(18, fonts_info - 2), False))
            if wifi_pass:
                wifi_lines.append((f"رمز عبور: {wifi_pass}", max(18, fonts_info - 2), False))
            r.boxed_card(wifi_lines, gap=10)

    if footer.get("showQrCode", False):
        qr_url = (footer.get("qrCodeUrl") or "").strip()
        qr_label = (footer.get("qrCodeLabel") or "").strip()
        if qr_url:
            r.qr_code(qr_url, qr_label, target_px=196, gap=10)

    ig_handle = (footer.get("instagramHandle") or "").strip()
    website_url = (footer.get("websiteUrl") or "").strip()
    if ig_handle:
        clean_ig = ig_handle if ig_handle.startswith("@") else f"@{ig_handle}"
        r.centered(f"اینستاگرام: {clean_ig}", max(18, fonts_info - 2), gap=4)
    if website_url:
        r.centered(f"وب‌سایت: {website_url}", max(18, fonts_info - 2), gap=6)

    footer_text = (footer.get("text") or "").strip() or (payload.get("footerText") or "").strip()
    if footer_text:
        r.centered(footer_text, 22, gap=6)
    if footer.get("showThanks", True):
        thanks_text = (footer.get("thanksText") or "").strip() or "نوش جان! از میزبانی شما خرسندیم ✨"
        r.centered(thanks_text, 24, bold=True, gap=8)
    if footer.get("showCafeName", True):
        footer_cafe_name = (footer.get("cafeNameText") or "").strip() or cafe_name
        r.centered(f"« {footer_cafe_name} »", fonts_footer_cafe, bold=True, gap=8)
    if header_section.get("showOrnament", True) and ornament_icon != "none":
        r.cafe_ornament(ornament_icon, gap=6)
    return r


def barista_receipt(payload) -> Receipt:
    r = Receipt()
    layout_data = dict(copy_layout(payload, "barista"))
    layout_data["_barista"] = True
    order = payload.get("order", {})
    items = payload.get("items", [])

    title = layout_data.get("title") or "فیش باریستا"
    fonts_info = font_size(layout_data, "info", 24)
    r.centered(title, font_size(layout_data, "title", 26), bold=True, gap=10)
    r.centered(order.get("code", ""), font_size(layout_data, "code", 56), bold=True, gap=16)

    for row_key in info_rows(layout_data, "infoRows", ["paidAt", "channel", "service", "table", "customerName"]):
        barista_info_row((r, fonts_info), payload, order, row_key)

    r.gap(4)
    r.divider("double")
    r.gap(8)

    draw_items_table(r, payload, items, layout_data)

    r.divider()
    r.gap(4)
    if bool(layout_data.get("showOrderNote", True)) and order.get("note"):
        r.paragraph(f"یادداشت سفارش: {order['note']}", size=28, bold=True)
    footer = layout_data.get("footer", {}) if isinstance(layout_data.get("footer"), dict) else {}
    # A custom footer text always prints; showSign only gates the default sign-off.
    footer_text = footer.get("text") or ""
    if not footer_text and footer.get("showSign", True):
        footer_text = "تهیه و تحویل — شوگریستا"
    if footer_text:
        r.centered(footer_text, 22, gap=4)
    return r



def main():
    if len(sys.argv) < 3:
        print("usage: render.py <payload.json> <output.png|output.escpos>", file=sys.stderr)
        sys.exit(2)

    payload_path, output_path = sys.argv[1], sys.argv[2]
    with open(payload_path, "r", encoding="utf-8") as handle:
        payload = normalize_keys(json.load(handle))

    receipt = barista_receipt(payload) if copy_type_of(payload) == "barista" else customer_receipt(payload)
    if output_path.lower().endswith(".escpos"):
        stream = ESC_INIT + receipt_to_escpos(receipt) + ESC_FEED_LINES + GS_CUT
        with open(output_path, "wb") as handle:
            handle.write(stream)
    else:
        receipt.save(output_path)
    print(output_path)


if __name__ == "__main__":
    main()
