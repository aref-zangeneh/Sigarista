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
_CONTROL = re.compile("[\u200e\u200f\u202a\u202b\u202c\u2066\u2067\u2068\u2069]")


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
    prepared = _LTR_TOKEN.sub(_isolate_token, str(text))
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


def char_font(ch: str, size: int, bold: bool) -> ImageFont.FreeTypeFont:
    """Vazirmatn, falling back to Helvetica for glyphs it does not cover."""
    primary = FONT_BOLD_PATH if bold else FONT_REGULAR_PATH
    if _has_glyph(str(primary), ch):
        return font(size, bold)
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
        total += char_font(ch, size, bold).getlength(ch)
    return total


def draw_visual(draw: ImageDraw.ImageDraw, x: float, y: float, text: str, size: int, bold=False):
    """Draw a shaped/bidi visual string with per-character font fallback."""
    cursor = x
    for ch in text:
        f = char_font(ch, size, bold)
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

    def __init__(self):
        self.height = 2600
        self.image = Image.new("1", (WIDTH, self.height), 1)
        self.draw = ImageDraw.Draw(self.image)
        self.y = MARGIN

    def _ensure(self, extra: int):
        if self.y + extra < self.height:
            return
        grown = Image.new("1", (WIDTH, self.height + 1600), 1)
        grown.paste(self.image, (0, 0))
        self.image = grown
        self.height += 1600
        self.draw = ImageDraw.Draw(self.image)

    def centered(self, text: str, size: int, bold=False, gap=8):
        for line in wrap(text, size, bold, CONTENT_W):
            self._ensure(size + gap)
            visual = rtl(line)
            w = visual_width(visual, size, bold)
            draw_visual(self.draw, (WIDTH - w) / 2, self.y, visual, size, bold)
            self.y += size + gap

    def row(self, right_text: str, left_text: str = "", size=26, bold=False, gap=10):
        """RTL row: right_text hugs the right margin, left_text the left."""
        self._ensure(size + gap)
        if right_text:
            visual = rtl(right_text)
            w = visual_width(visual, size, bold)
            draw_visual(self.draw, WIDTH - MARGIN - w, self.y, visual, size, bold)
        if left_text:
            visual = rtl(left_text)
            draw_visual(self.draw, MARGIN, self.y, visual, size, bold)
        self.y += size + gap

    def paragraph(self, text: str, size=22, bold=False, indent=0, gap=6):
        for line in wrap(text, size, bold, CONTENT_W - indent):
            self._ensure(size + gap)
            visual = rtl(line)
            w = visual_width(visual, size, bold)
            draw_visual(self.draw, WIDTH - MARGIN - indent - w, self.y, visual, size, bold)
            self.y += size + gap

    def divider(self, style="dashed"):
        self._ensure(22)
        y = self.y + 8
        if style == "double":
            self.draw.line([(MARGIN, y), (WIDTH - MARGIN, y)], fill=0, width=3)
            self.draw.line([(MARGIN, y + 6), (WIDTH - MARGIN, y + 6)], fill=0, width=2)
        else:
            x = MARGIN
            while x < WIDTH - MARGIN:
                self.draw.line([(x, y), (min(x + 9, WIDTH - MARGIN), y)], fill=0, width=2)
                x += 16
        self.y += 22

    def gap(self, pixels=10):
        self.y += pixels

    def cropped_image(self, bottom_pad=90) -> Image.Image:
        """The drawn receipt cropped to its content plus trailing white space."""
        bbox = self.image.point(lambda p: 0 if p else 255).getbbox()
        if bbox is None:
            content_bottom = self.y
        else:
            content_bottom = min(bbox[3] + 12, self.y)
        height = max(content_bottom + bottom_pad, 160)
        return self.image.crop((0, 0, WIDTH, min(height, self.height)))

    def save(self, path: str, bottom_pad=90):
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


def receipt_to_escpos(receipt: Receipt, bottom_pad=90) -> bytes:
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
                lines.append(f"{name} (+{price})")
            else:
                lines.append(name)
        return lines
    # Legacy payload fallback.
    labels = item.get("optionLabels")
    if isinstance(labels, list):
        return [str(label) for label in labels if label]
    return []


def sub_lines(item: dict, sub_line_keys: list[str], layout_items: dict) -> list[str]:
    """Detail lines under an item name, rendered in admin-defined order."""
    lines: list[str] = []
    for key in sub_line_keys:
        if key == "size":
            if item.get("sizeLabel"):
                lines.append(str(item["sizeLabel"]))
        elif key == "options":
            lines.extend(option_text(item, layout_items))
        elif key == "modifiers":
            for modifier in item.get("modifierLabels") or []:
                lines.append(str(modifier))
        elif key == "customDrink":
            for summary in item.get("customDrinkSummary") or []:
                lines.append(str(summary))
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
    r, size = contract
    if key == "code":
        r.row("کد سفارش", order.get("code", ""), size=30, bold=True, gap=12)
    elif key == "createdAt":
        r.row(order.get("createdAtLabel", ""), "", size=size)
    elif key == "channel":
        r.row(order.get("channelLabel", ""), "", size=size)
    elif key == "service":
        service = service_with_table(order)
        if service:
            r.row(service, "", size=size)
    elif key == "table":
        if order.get("tableLabel"):
            r.row(f"میز {order['tableLabel']}", "", size=size)
    elif key == "pickupTime":
        if order.get("pickupAtLabel"):
            r.row("تحویل", order["pickupAtLabel"], size=size, bold=True)
    elif key == "customerName":
        if order.get("customerName"):
            r.row(order["customerName"], "", size=size)
    elif key == "customerPhone":
        if order.get("customerPhone"):
            r.row(order["customerPhone"], "", size=size)
    elif key == "note":
        if order.get("note"):
            r.paragraph(f"یادداشت: {order['note']}", size=22)


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
            joined = " • ".join(details)
            detail_width = max(name_width - 20, 120)
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
            r.paragraph(f"واحد: {item['unitPriceLabel']}", size=20, indent=20, gap=4)
        r.gap(6)


# Default detail lines per copy (classic layout).
CUSTOMER_DEFAULT_SUB_LINES = ["size", "options", "modifiers", "customDrink", "payer"]
BARISTA_DEFAULT_SUB_LINES = ["size", "modifiers", "options", "customDrink", "baristaNote"]


def customer_receipt(payload) -> Receipt:
    r = Receipt()
    layout_data = dict(copy_layout(payload, "customer"))
    layout_data["_barista"] = False
    header = payload.get("header", {})
    order = payload.get("order", {})
    items = payload.get("items", [])
    totals = payload.get("totals", {})
    fonts_header = font_size(layout_data, "header", 40)

    r.centered(header.get("cafeName") or "Sugarista", fonts_header, bold=True, gap=12)
    header_section = layout_data.get("header", {}) if isinstance(layout_data.get("header"), dict) else {}
    fonts_info = font_size(layout_data, "info", 22)
    if header_section.get("showAddress", True) and header.get("address"):
        r.centered(header["address"], fonts_info, gap=4)
    if header_section.get("showPhone", True) and header.get("contactPhone"):
        r.centered(f"تلفن: {header['contactPhone']}", fonts_info, gap=4)
    r.gap(4)
    r.divider("double")
    r.gap(4)

    for row_key in info_rows(layout_data, "headerRows", list(DEFAULT_CUSTOMER_HEADER_ROW_LABELS)):
        customer_header_row((r, fonts_info), payload, order, row_key)
    r.gap(4)
    r.divider()
    r.gap(4)

    draw_items_table(r, payload, items, layout_data)

    r.divider()
    r.gap(4)

    # Admin-configured totals rows.
    totals_section = layout_data.get("totals", {}) if isinstance(layout_data.get("totals"), dict) else {}
    rows = totals_section.get("rows")
    if not isinstance(rows, list) or not rows:
        rows = [
            {"key": "subtotal", "label": "جمع اقلام", "visible": True},
            {"key": "deliveryFee", "label": "هزینه ارسال", "visible": True},
            {"key": "tax", "label": "مالیات", "visible": True},
            {"key": "total", "label": "مبلغ نهایی", "visible": True},
        ]
    subtotal_font = font_size(layout_data, "subtotal", 24)
    total_font = font_size(layout_data, "total", 36)
    for row in rows:
        if not isinstance(row, dict) or not row.get("visible", True):
            continue
        key = row.get("key")
        label = str(row.get("label") or "")
        if key == "subtotal" and totals.get("subtotalLabel"):
            r.row(label or "جمع اقلام", totals["subtotalLabel"], size=subtotal_font)
        elif key == "deliveryFee" and totals.get("deliveryFeeLabel"):
            r.row(label or "هزینه ارسال", totals["deliveryFeeLabel"], size=subtotal_font)
        elif key == "tax" and totals.get("taxLabel"):
            r.row(label or "مالیات", totals["taxLabel"], size=subtotal_font)
        elif key == "total":
            r.row(label or "مبلغ نهایی", totals.get("totalLabel", ""), size=total_font, bold=True, gap=16)

    for row_key in info_rows(layout_data, "paymentRows", ["paymentMethod", "reference", "amountPaid"]):
        if row_key == "paymentMethod":
            r.row("روش پرداخت", order.get("paymentLabel", ""), size=fonts_info)
        elif row_key == "reference":
            if order.get("reference"):
                r.paragraph(f"شماره پیگیری: {order['reference']}", size=20)
        elif row_key == "amountPaid":
            amount_paid = totals.get("amountPaidLabel")
            if amount_paid and amount_paid != totals.get("totalLabel"):
                r.row("پرداخت شده", amount_paid, size=fonts_info)

    r.gap(8)
    r.divider()
    r.gap(4)
    footer = layout_data.get("footer", {}) if isinstance(layout_data.get("footer"), dict) else {}
    footer_text = footer.get("text") or payload.get("footerText")
    if footer_text:
        r.centered(footer_text, 22, gap=5)
    if footer.get("showThanks", True):
        r.centered("از انتخاب شما سپاسگزاریم", 26, bold=True, gap=8)
    if footer.get("showCafeName", True):
        r.centered(header.get("cafeName") or "Sugarista", 20, gap=4)
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
