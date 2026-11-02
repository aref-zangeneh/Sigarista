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

    def save(self, path: str, bottom_pad=90):
        bbox = self.image.point(lambda p: 0 if p else 255).getbbox()
        if bbox is None:
            content_bottom = self.y
        else:
            content_bottom = min(bbox[3] + 12, self.y)
        height = max(content_bottom + bottom_pad, 160)
        self.image.crop((0, 0, WIDTH, min(height, self.height))).save(path, "PNG")


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


def customer_receipt(payload) -> Receipt:
    r = Receipt()
    header = payload.get("header", {})
    order = payload.get("order", {})
    items = payload.get("items", [])
    totals = payload.get("totals", {})

    r.centered(header.get("cafeName") or "Sugarista", 40, bold=True, gap=12)
    if header.get("address"):
        r.centered(header["address"], 22, gap=4)
    if header.get("contactPhone"):
        r.centered(f"تلفن: {header['contactPhone']}", 22, gap=4)
    r.gap(4)
    r.divider("double")
    r.gap(4)

    r.row("کد سفارش", order.get("code", ""), size=30, bold=True, gap=12)
    r.row(order.get("createdAtLabel", ""), order.get("channelLabel", ""), size=22)
    service = order.get("serviceLabel") or ""
    if order.get("tableLabel"):
        service = f"{service} • میز {order['tableLabel']}"
    if service:
        r.row(service, "", size=22)
    if order.get("customerName"):
        r.row(order["customerName"], "", size=22)
    if order.get("customerPhone"):
        r.row(order["customerPhone"], "", size=22)
    if order.get("note"):
        r.paragraph(f"یادداشت: {order['note']}", size=22)

    r.gap(4)
    r.divider()
    r.gap(4)

    for item in items:
        qty = item.get("quantityLabel") or str(item.get("quantity", 1))
        name = item.get("name", "")
        r.row(f"{qty} × {name}", item.get("totalLabel", ""), size=30, bold=True, gap=10)
        details = []
        if item.get("sizeLabel"):
            details.append(str(item["sizeLabel"]))
        if item.get("modifierLabels"):
            details.extend(str(m) for m in item["modifierLabels"])
        if item.get("payerLabel"):
            details.append(f"سهم {item['payerLabel']}")
        if details:
            r.paragraph(" • ".join(details), size=22, indent=20, gap=5)
        if item.get("unitPriceLabel") and item.get("quantity", 1) > 1:
            r.paragraph(f"واحد: {item['unitPriceLabel']}", size=20, indent=20, gap=4)
        r.gap(6)

    r.divider()
    r.gap(4)

    if totals.get("subtotalLabel"):
        r.row("جمع اقلام", totals["subtotalLabel"], size=24)
    if totals.get("deliveryFeeLabel"):
        r.row("هزینه ارسال", totals["deliveryFeeLabel"], size=24)
    if totals.get("taxLabel"):
        r.row("مالیات", totals["taxLabel"], size=24)
    r.row("مبلغ نهایی", totals.get("totalLabel", ""), size=36, bold=True, gap=16)

    r.row("روش پرداخت", order.get("paymentLabel", ""), size=22)
    if order.get("reference"):
        r.paragraph(f"شماره پیگیری: {order['reference']}", size=20)
    if totals.get("amountPaidLabel") and totals.get("amountPaidLabel") != totals.get("totalLabel"):
        r.row("پرداخت شده", totals["amountPaidLabel"], size=22)

    r.gap(8)
    r.divider()
    r.gap(4)
    if payload.get("footerText"):
        r.centered(payload["footerText"], 22, gap=5)
    r.centered("از انتخاب شما سپاسگزاریم", 26, bold=True, gap=8)
    r.centered(header.get("cafeName") or "Sugarista", 20, gap=4)
    return r


def barista_receipt(payload) -> Receipt:
    r = Receipt()
    order = payload.get("order", {})
    items = payload.get("items", [])

    r.centered("فیش باریستا", 26, bold=True, gap=10)
    r.centered(order.get("code", ""), 56, bold=True, gap=16)
    r.row(order.get("paidAtLabel") or order.get("createdAtLabel", ""), order.get("channelLabel", ""), size=24)
    service = order.get("serviceLabel") or ""
    if order.get("tableLabel"):
        service = f"{service} • میز {order['tableLabel']}"
    if service:
        r.paragraph(service, size=26, bold=True)
    if order.get("customerName"):
        r.paragraph(f"مشتری: {order['customerName']}", size=24)

    r.gap(4)
    r.divider("double")
    r.gap(8)

    for item in items:
        qty = item.get("quantityLabel") or str(item.get("quantity", 1))
        name = item.get("name", "")
        r.row(f"{qty} × {name}", "", size=38, bold=True, gap=12)
        details = []
        if item.get("sizeLabel"):
            details.append(str(item["sizeLabel"]))
        if item.get("modifierLabels"):
            details.extend(str(m) for m in item["modifierLabels"])
        if details:
            r.paragraph(" • ".join(details), size=28, indent=20, gap=6)
        if item.get("baristaNote"):
            r.paragraph(f"! {item['baristaNote']}", size=28, bold=True, indent=20, gap=6)
        r.gap(10)

    r.divider()
    r.gap(4)
    if order.get("note"):
        r.paragraph(f"یادداشت سفارش: {order['note']}", size=28, bold=True)
    r.centered("تهیه و تحویل — شوگریستا", 22, gap=4)
    return r


def main():
    if len(sys.argv) < 3:
        print("usage: render.py <payload.json> <output.png>", file=sys.stderr)
        sys.exit(2)

    payload_path, output_path = sys.argv[1], sys.argv[2]
    with open(payload_path, "r", encoding="utf-8") as handle:
        payload = normalize_keys(json.load(handle))

    receipt = barista_receipt(payload) if copy_type_of(payload) == "barista" else customer_receipt(payload)
    receipt.save(output_path)
    print(output_path)


if __name__ == "__main__":
    main()
