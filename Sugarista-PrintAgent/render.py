#!/usr/bin/env python3
"""Render a Sugarista receipt (customer or barista copy) to a 384-dot PNG.

The GA-E200I is an 80 mm thermal printer: 384 dots printable width.
macOS does not shape Arabic text when spooling plain text, so the agent
renders each receipt as a 1-bit bitmap with proper Persian shaping
(arabic_reshaper + bidi) and sends that to CUPS.
"""

import json
import re
import sys

from PIL import Image, ImageDraw, ImageFont
import arabic_reshaper
from bidi.algorithm import get_display

WIDTH = 384
MARGIN = 10
CONTENT_W = WIDTH - MARGIN * 2

FONT_PATH = "/System/Library/Fonts/GeezaPro.ttc"
FONT_REGULAR = 0
FONT_BOLD = 1
# Geeza Pro has no Latin glyphs and misses —, •, ×; Helvetica covers them.
LATIN_FONT_PATH = "/System/Library/Fonts/Helvetica.ttc"
LATIN_REGULAR = 0
LATIN_BOLD = 1

_FONT_CACHE: dict[tuple, ImageFont.FreeTypeFont] = {}

RESHAPER = arabic_reshaper.ArabicReshaper(
    configuration={"delete_harakat": False, "support_ligatures": True}
)

# Latin/ASCII tokens (tracking codes, phone numbers with a leading +) must stay
# left-to-right inside an RTL line. python-bidi implements the older UBA without
# isolate support, so we bracket the token with LRM marks instead; the marks are
# stripped again before drawing because they have no glyphs.
_LTR_TOKEN = re.compile(r"[A-Za-z0-9+][A-Za-z0-9+\-_.:/@]*")
_CONTROL = re.compile("[\u200e\u200f\u202a\u202b\u202c\u2066\u2067\u2068\u2069]")


def _isolate_token(match: re.Match) -> str:
    token = match.group(0)
    # Persian digits / plain numbers already render correctly; only protect
    # tokens that contain Latin letters or a sign such as "+".
    if not re.search(r"[A-Za-z+]", token):
        return token
    return "\u200e" + token + "\u200e"


def rtl(text: str) -> str:
    """Shape Persian/Arabic and apply bidi ordering for drawing."""
    if not text:
        return ""
    prepared = _LTR_TOKEN.sub(_isolate_token, str(text))
    visual = get_display(RESHAPER.reshape(prepared))
    return _CONTROL.sub("", visual)


def is_arabic_char(ch: str) -> bool:
    code = ord(ch)
    return (
        0x0600 <= code <= 0x06FF
        or 0x0750 <= code <= 0x077F
        or 0xFB50 <= code <= 0xFDFF
        or 0xFE70 <= code <= 0xFEFF
    )


def char_font(ch: str, size: int, bold: bool) -> ImageFont.FreeTypeFont:
    """Per-character font: Geeza Pro for Persian/Arabic, Helvetica otherwise."""
    key = (size, bold, is_arabic_char(ch))
    cached = _FONT_CACHE.get(key)
    if cached is not None:
        return cached
    if is_arabic_char(ch):
        loaded = ImageFont.truetype(FONT_PATH, size, index=FONT_BOLD if bold else FONT_REGULAR)
    else:
        loaded = ImageFont.truetype(
            LATIN_FONT_PATH, size, index=LATIN_BOLD if bold else LATIN_REGULAR
        )
    _FONT_CACHE[key] = loaded
    return loaded


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
        self.height = 2000
        self.image = Image.new("1", (WIDTH, self.height), 1)
        self.draw = ImageDraw.Draw(self.image)
        self.y = MARGIN

    def _ensure(self, extra: int):
        if self.y + extra < self.height:
            return
        grown = Image.new("1", (WIDTH, self.height + 1200), 1)
        grown.paste(self.image, (0, 0))
        self.image = grown
        self.height += 1200
        self.draw = ImageDraw.Draw(self.image)

    def centered(self, text: str, size: int, bold=False, gap=4):
        for line in wrap(text, size, bold, CONTENT_W):
            self._ensure(size + gap)
            visual = rtl(line)
            w = visual_width(visual, size, bold)
            draw_visual(self.draw, (WIDTH - w) / 2, self.y, visual, size, bold)
            self.y += size + gap

    def row(self, right_text: str, left_text: str = "", size=20, bold=False, gap=6):
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

    def paragraph(self, text: str, size=18, bold=False, indent=0, gap=4):
        for line in wrap(text, size, bold, CONTENT_W - indent):
            self._ensure(size + gap)
            visual = rtl(line)
            w = visual_width(visual, size, bold)
            draw_visual(self.draw, WIDTH - MARGIN - indent - w, self.y, visual, size, bold)
            self.y += size + gap

    def divider(self, style="dashed"):
        self._ensure(14)
        y = self.y + 5
        if style == "double":
            self.draw.line([(MARGIN, y), (WIDTH - MARGIN, y)], fill=0, width=2)
            self.draw.line([(MARGIN, y + 4), (WIDTH - MARGIN, y + 4)], fill=0, width=1)
        else:
            x = MARGIN
            while x < WIDTH - MARGIN:
                self.draw.line([(x, y), (min(x + 6, WIDTH - MARGIN), y)], fill=0, width=1)
                x += 11
        self.y += 14

    def gap(self, pixels=6):
        self.y += pixels

    def save(self, path: str, bottom_pad=48):
        bbox = self.image.point(lambda p: 0 if p else 255).getbbox()
        if bbox is None:
            content_bottom = self.y
        else:
            content_bottom = min(bbox[3] + 8, self.y)
        height = max(content_bottom + bottom_pad, 120)
        self.image.crop((0, 0, WIDTH, min(height, self.height))).save(path, "PNG")


def money(value):
    return value or ""


def customer_receipt(payload) -> Receipt:
    r = Receipt()
    header = payload.get("header", {})
    order = payload.get("order", {})
    items = payload.get("items", [])
    totals = payload.get("totals", {})

    r.centered(header.get("cafeName") or "Sugarista", 30, bold=True, gap=8)
    if header.get("address"):
        r.centered(header["address"], 17, gap=2)
    if header.get("contactPhone"):
        r.centered(f"تلفن: {header['contactPhone']}", 17, gap=2)
    r.gap(2)
    r.divider("double")
    r.gap(2)

    r.row("کد سفارش", order.get("code", ""), size=22, bold=True, gap=8)
    r.row(order.get("createdAtLabel", ""), order.get("channelLabel", ""), size=17)
    service = order.get("serviceLabel") or ""
    if order.get("tableLabel"):
        service = f"{service} • میز {order['tableLabel']}"
    if service:
        r.row(service, "", size=17)
    if order.get("customerName"):
        r.row(order["customerName"], "", size=17)
    if order.get("customerPhone"):
        r.row(order["customerPhone"], "", size=17)
    if order.get("note"):
        r.paragraph(f"یادداشت: {order['note']}", size=17)

    r.gap(2)
    r.divider()
    r.gap(2)

    for item in items:
        qty = item.get("quantityLabel") or str(item.get("quantity", 1))
        name = item.get("name", "")
        r.row(f"{qty} × {name}", item.get("totalLabel", ""), size=21, bold=True, gap=6)
        details = []
        if item.get("sizeLabel"):
            details.append(str(item["sizeLabel"]))
        if item.get("modifierLabels"):
            details.extend(str(m) for m in item["modifierLabels"])
        if item.get("payerLabel"):
            details.append(f"سهم {item['payerLabel']}")
        if details:
            r.paragraph(" • ".join(details), size=16, indent=14, gap=3)
        if item.get("unitPriceLabel") and item.get("quantity", 1) > 1:
            r.paragraph(f"واحد: {item['unitPriceLabel']}", size=15, indent=14, gap=2)
        r.gap(2)

    r.divider()
    r.gap(2)

    if totals.get("subtotalLabel"):
        r.row("جمع اقلام", totals["subtotalLabel"], size=18)
    if totals.get("deliveryFeeLabel"):
        r.row("هزینه ارسال", totals["deliveryFeeLabel"], size=18)
    if totals.get("taxLabel"):
        r.row("مالیات", totals["taxLabel"], size=18)
    r.row("مبلغ نهایی", totals.get("totalLabel", ""), size=24, bold=True, gap=10)

    r.row("روش پرداخت", order.get("paymentLabel", ""), size=17)
    if order.get("reference"):
        r.paragraph(f"شماره پیگیری: {order['reference']}", size=16)
    if totals.get("amountPaidLabel") and totals.get("amountPaidLabel") != totals.get("totalLabel"):
        r.row("پرداخت شده", totals["amountPaidLabel"], size=17)

    r.gap(4)
    r.divider()
    r.gap(2)
    if payload.get("footerText"):
        r.centered(payload["footerText"], 17, gap=3)
    r.centered("از انتخاب شما سپاسگزاریم", 19, bold=True, gap=4)
    r.centered(header.get("cafeName") or "Sugarista", 15, gap=2)
    return r


def barista_receipt(payload) -> Receipt:
    r = Receipt()
    order = payload.get("order", {})
    items = payload.get("items", [])

    r.centered("فیش باریستا", 20, bold=True, gap=8)
    r.centered(order.get("code", ""), 40, bold=True, gap=10)
    r.row(order.get("paidAtLabel") or order.get("createdAtLabel", ""), order.get("channelLabel", ""), size=18)
    service = order.get("serviceLabel") or ""
    if order.get("tableLabel"):
        service = f"{service} • میز {order['tableLabel']}"
    if service:
        r.paragraph(service, size=19, bold=True)
    if order.get("customerName"):
        r.paragraph(f"مشتری: {order['customerName']}", size=18)

    r.gap(2)
    r.divider("double")
    r.gap(4)

    for item in items:
        qty = item.get("quantityLabel") or str(item.get("quantity", 1))
        name = item.get("name", "")
        r.row(f"{qty} × {name}", "", size=26, bold=True, gap=8)
        details = []
        if item.get("sizeLabel"):
            details.append(str(item["sizeLabel"]))
        if item.get("modifierLabels"):
            details.extend(str(m) for m in item["modifierLabels"])
        if details:
            r.paragraph(" • ".join(details), size=20, indent=14, gap=4)
        if item.get("baristaNote"):
            r.paragraph(f"! {item['baristaNote']}", size=20, bold=True, indent=14, gap=4)
        r.gap(4)

    r.divider()
    r.gap(2)
    if order.get("note"):
        r.paragraph(f"یادداشت سفارش: {order['note']}", size=20, bold=True)
    r.centered("تهیه و تحویل — شوگریستا", 16, gap=2)
    return r


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
