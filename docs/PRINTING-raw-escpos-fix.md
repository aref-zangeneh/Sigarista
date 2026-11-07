# Print pipeline fix — raw ESC/POS instead of the CUPS raster filter

## Symptom

The Gainscha GA-E200I printed **garbled output** (hex-dump-like noise mixed
with partially rendered receipts). Diagnosis on the host (Nov 8, 2026):

1. The CUPS queue `GA-E200I` was **disabled** (`Unable to send data to
   printer`, usb backend dead) with **28 stuck PNG jobs** from Nov 4 sitting in
   the queue. When such a queue is force-resumed, a partially-sent job desyncs
   the ESC/POS stream and everything after it prints as garbage.
2. The driver chain was fragile by design:
   `PNG → cupsfilters → CUPS raster (3SaR) → rastertopdt` (the Gainscha
   vendor filter, which even segfaults standalone, exit 139).

## Fix

We no longer trust any filter chain. The pipeline is:

```
payload.json ──render.py──▶ receipt.escpos ──lp -o raw──▶ CUPS backend ──▶ printer
```

- **render.py** renders the receipt exactly as before (1-bit bitmap, Persian
  shaping via arabic_reshaper + bidi, dynamic TASK-014 layout) and then emits
  the raw ESC/POS byte stream itself:
  - output ends with `.escpos` → `ESC @` + `GS v 0` raster (576 dots =
    72 bytes/row — the printer's native 72 mm @ 203 dpi) + `ESC d 4` feed +
    `GS V 0` (full cut) after **every copy**
  - output ends with `.png` → unchanged PNG (backward compatibility, tests)
- **agent.js**
  - prints with `lp -o raw` — nothing scales, dithers or converts the stream;
  - groups pending jobs **of the same order claimed in one poll** into a
    single physical print job, so customer + barista copies print back-to-back
    with a cut between them, and can never be interleaved by anything else;
  - marks every copy of the group via `/printed` or `/failed` — no partial
    success states;
  - self-heals the queue before each attempt group:
    `lpstat -p` disabled → `cupsenable` + `cupsaccept`;
    and on a failed retry attempt: `cancel -a` (drop stuck jobs) then re-enable
    — a stuck partial job can no longer poison the paper stream.
- **config.json**: `printOptions` removed (PageSize/PageType/CutType options
  belonged to the raster-filter route; raw mode needs no options).

## Verification

- Raw test job (`ESC @` + text + `ESC d 4` + `GS V 0`) → printed cleanly and
  cut on the physical printer, left the queue with zero backend errors.
- E2E through the API: two `Pending` `PrintJob` rows (customer + barista) for
  order `SG-PKWD7J` → agent claimed both, printed ONE paper with the customer
  receipt + cut + barista receipt + cut, marked both `Printed`.
- Queue drained to idle after every test with no `Unable to send data`
  occurrences in `/var/log/cups/error_log`.

## Operative notes

- Recovery when paper runs out / USB glitches: the agent drops stale CUPS
  jobs and re-opens the queue before retrying, so recovery surfaces on the
  *next* paper, not as garbage glued to a half-printed one.
- Jobs for one order claimed in **different polls** still print as two
  adjacent jobs — each copy ends with its own cut, so the separation between
  customer and barista copies always holds.
- Runbook (if the printer misbehaves again):
  ```bash
  lpstat -W not-completed -o GA-E200I   # stuck jobs?
  cancel -a GA-E200I && cupsenable GA-E200I && cupsaccept GA-E200I
  launchctl kickstart -k gui/$(id -u)/com.sugarista.printagent
  ```
