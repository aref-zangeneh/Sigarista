(function (globalScope) {
  "use strict";

  const PERSIAN_DIGITS = "۰۱۲۳۴۵۶۷۸۹";
  const ARABIC_INDIC_DIGITS = "٠١٢٣٤٥٦٧٨٩";

  const TECHNICAL_TOKEN_REGEX =
    /(?:https?:\/\/|\/api\/|www\.)[^\s<>"'`]+|[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}|\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b|#(?=[0-9a-fA-F]{3,8}\b)(?:[0-9a-fA-F]*[a-fA-F][0-9a-fA-F]*|0{6})\b/g;

  const SENSITIVE_KEY_SUBSTRINGS = [
    "password",
    "passwd",
    "secret",
    "apikey",
    "api_key",
    "signingkey",
    "signing_key",
    "privatekey",
    "private_key",
    "connectionstring",
    "connection_string",
    "accesstoken",
    "access_token",
    "refreshtoken",
    "refresh_token",
    "idtoken",
    "id_token",
    "bearertoken",
    "bearer_token",
    "extrasettingsjson",
  ];

  function toEnglishDigits(value) {
    if (typeof value !== "string" || !value) {
      return value;
    }

    if (!/[۰-۹٠-٩]/.test(value)) {
      return value;
    }

    return value.replace(/[۰-۹٠-٩]/g, (digit) => {
      const persianIndex = PERSIAN_DIGITS.indexOf(digit);
      if (persianIndex >= 0) {
        return String(persianIndex);
      }
      return String(ARABIC_INDIC_DIGITS.indexOf(digit));
    });
  }

  function toPersianDigits(value) {
    if (value === null || value === undefined) {
      return "";
    }

    const text = String(value);
    if (!text || !/[0-9٠-٩]/.test(text)) {
      return text;
    }

    return text.replace(/[0-9٠-٩]/g, (digit) => {
      if (digit >= "0" && digit <= "9") {
        return PERSIAN_DIGITS[Number(digit)];
      }
      const arabicIndex = ARABIC_INDIC_DIGITS.indexOf(digit);
      return arabicIndex >= 0 ? PERSIAN_DIGITS[arabicIndex] : digit;
    });
  }

  function toPersianDisplayText(text) {
    if (typeof text !== "string" || !text || !/[0-9٠-٩]/.test(text)) {
      return text;
    }

    let result = "";
    let lastIndex = 0;
    TECHNICAL_TOKEN_REGEX.lastIndex = 0;

    let match;
    while ((match = TECHNICAL_TOKEN_REGEX.exec(text)) !== null) {
      if (match.index > lastIndex) {
        result += toPersianDigits(text.slice(lastIndex, match.index));
      }
      result += match[0];
      lastIndex = match.index + match[0].length;
    }

    if (lastIndex < text.length) {
      result += toPersianDigits(text.slice(lastIndex));
    }

    return result;
  }

  function sanitizeNumericInput(rawValue, options) {
    const opts = options || {};
    const allowDecimal = opts.allowDecimal !== undefined ? Boolean(opts.allowDecimal) : true;
    const allowNegative = Boolean(opts.allowNegative);
    const caret = opts.caret !== undefined ? opts.caret : null;

    const source = String(rawValue ?? "");
    let english = "";
    let seenDecimal = false;
    let seenSign = false;
    let nextCaret = 0;
    const hasCaret = typeof caret === "number" && !Number.isNaN(caret);

    for (let i = 0; i < source.length; i++) {
      const ch = source[i];
      let mapped = null;

      if (ch >= "0" && ch <= "9") {
        mapped = ch;
      } else if (ch >= "۰" && ch <= "۹") {
        mapped = String(ch.charCodeAt(0) - 0x06f0);
      } else if (ch >= "٠" && ch <= "٩") {
        mapped = String(ch.charCodeAt(0) - 0x0660);
      } else if ((ch === "." || ch === "٫") && allowDecimal && !seenDecimal) {
        mapped = ".";
        seenDecimal = true;
      } else if ((ch === "-" || ch === "−") && allowNegative && english.length === 0 && !seenSign) {
        mapped = "-";
        seenSign = true;
      }

      if (mapped !== null) {
        english += mapped;
        if (hasCaret && i < caret) {
          nextCaret += 1;
        }
      }
    }

    return {
      english,
      display: toPersianDigits(english),
      caret: hasCaret ? nextCaret : null,
    };
  }

  function isSensitiveFieldKey(key) {
    if (typeof key !== "string" || !key) {
      return false;
    }

    const lower = key.trim().toLowerCase();
    if (lower === "token" || lower === "hash" || lower === "signature") {
      return true;
    }

    return SENSITIVE_KEY_SUBSTRINGS.some((sub) => lower.includes(sub));
  }

  function isPlainObject(value) {
    if (!value || typeof value !== "object") {
      return false;
    }
    const proto = Object.getPrototypeOf(value);
    return proto === Object.prototype || proto === null;
  }

  function normalizePayloadDigits(payload) {
    if (payload === null || payload === undefined) {
      return payload;
    }

    if (typeof payload === "string") {
      return toEnglishDigits(payload);
    }

    if (Array.isArray(payload)) {
      let changed = false;
      const next = payload.map((item) => {
        const normalized = normalizePayloadDigits(item);
        if (normalized !== item) {
          changed = true;
        }
        return normalized;
      });
      return changed ? next : payload;
    }

    if (!isPlainObject(payload)) {
      return payload;
    }

    let changed = false;
    const next = {};
    for (const [key, value] of Object.entries(payload)) {
      if (isSensitiveFieldKey(key)) {
        next[key] = value;
        continue;
      }

      const normalized = normalizePayloadDigits(value);
      if (normalized !== value) {
        changed = true;
      }
      next[key] = normalized;
    }

    return changed ? next : payload;
  }

  const SKIP_TEXT_PARENT_TAGS = new Set([
    "SCRIPT",
    "STYLE",
    "NOSCRIPT",
    "TEMPLATE",
    "INPUT",
    "TEXTAREA",
    "SVG",
    "MATH",
    "CODE",
    "PRE",
    "KBD",
    "SAMP",
  ]);

  const EXCLUDED_INPUT_TYPES = new Set([
    "password",
    "email",
    "url",
    "date",
    "time",
    "datetime-local",
    "month",
    "week",
    "color",
    "range",
    "checkbox",
    "radio",
    "file",
    "hidden",
    "submit",
    "button",
    "reset",
    "image",
  ]);

  function shouldSkipDisplayElement(element) {
    if (!element || element.nodeType !== 1) {
      return true;
    }
    if (SKIP_TEXT_PARENT_TAGS.has(element.tagName)) {
      return true;
    }
    return Boolean(
      element.closest(
        '[data-latin-digits="true"], [data-persian-digits="off"], .keep-latin-digits, code, pre, kbd, samp'
      )
    );
  }

  function normalizeTextNode(textNode) {
    if (!textNode || textNode.nodeType !== 3) {
      return;
    }
    const parent = textNode.parentElement;
    if (!parent || shouldSkipDisplayElement(parent)) {
      return;
    }
    const raw = textNode.nodeValue;
    if (!raw || !/[0-9٠-٩]/.test(raw)) {
      return;
    }
    const converted = toPersianDisplayText(raw);
    if (converted !== raw) {
      textNode.nodeValue = converted;
    }
  }

  function normalizePlaceholder(element) {
    if (
      !(element instanceof HTMLInputElement || element instanceof HTMLTextAreaElement) ||
      !element.hasAttribute("placeholder")
    ) {
      return;
    }
    if (
      element.getAttribute("data-latin-digits") === "true" ||
      (element instanceof HTMLInputElement && EXCLUDED_INPUT_TYPES.has(element.type)) ||
      element.closest('[data-latin-digits="true"], [data-persian-digits="off"], .keep-latin-digits')
    ) {
      return;
    }
    const raw = element.getAttribute("placeholder");
    if (!raw || !/[0-9٠-٩]/.test(raw)) {
      return;
    }
    const converted = toPersianDisplayText(raw);
    if (converted !== raw) {
      element.setAttribute("placeholder", converted);
    }
  }

  function walkSubtree(node) {
    if (!node) {
      return;
    }
    if (node.nodeType === 3) {
      normalizeTextNode(node);
      return;
    }
    if (node.nodeType !== 1) {
      return;
    }

    normalizePlaceholder(node);
    if (shouldSkipDisplayElement(node)) {
      return;
    }

    const walker = document.createTreeWalker(node, 1 | 4);
    let current = walker.nextNode();
    while (current) {
      if (current.nodeType === 3) {
        normalizeTextNode(current);
      } else if (current.nodeType === 1) {
        normalizePlaceholder(current);
      }
      current = walker.nextNode();
    }
  }

  function setInputCaret(element, caret) {
    if (
      typeof caret !== "number" ||
      Number.isNaN(caret) ||
      typeof element.setSelectionRange !== "function"
    ) {
      return;
    }
    const clamped = Math.max(0, Math.min(caret, element.value.length));
    try {
      element.setSelectionRange(clamped, clamped);
    } catch {
      // Ignore selection errors on restricted input types.
    }
  }

  function installThemePersianDigits() {
    if (typeof document === "undefined" || !document.body || document.__themePersianDigitsInstalled) {
      return;
    }
    document.__themePersianDigitsInstalled = true;

    // 1. Display observer for visible DOM text, options, placeholders
    walkSubtree(document.body);
    const observer = new MutationObserver((mutations) => {
      for (const mutation of mutations) {
        if (mutation.type === "characterData") {
          normalizeTextNode(mutation.target);
        } else if (mutation.type === "attributes") {
          normalizePlaceholder(mutation.target);
        } else if (mutation.type === "childList") {
          for (const added of mutation.addedNodes) {
            walkSubtree(added);
          }
        }
      }
    });
    observer.observe(document.body, {
      childList: true,
      subtree: true,
      characterData: true,
      attributes: true,
      attributeFilter: ["placeholder"],
    });

    // 2. Capture-phase input listener so typed English/Arabic/Persian digits render
    // as Persian digits inside inputs before Alpine x-model/@input handlers read them.
    document.addEventListener(
      "input",
      (event) => {
        const target = event.target;
        if (!(target instanceof HTMLInputElement || target instanceof HTMLTextAreaElement)) {
          return;
        }
        if (
          target.getAttribute("data-latin-digits") === "true" ||
          target.closest('[data-latin-digits="true"]')
        ) {
          return;
        }
        if (target instanceof HTMLInputElement && EXCLUDED_INPUT_TYPES.has(target.type)) {
          return;
        }

        const raw = target.value;
        const rawCaret = typeof target.selectionStart === "number" ? target.selectionStart : null;

        if (target.dataset && target.dataset.numericInput === "true") {
          const allowDecimal = target.dataset.allowDecimal === "true";
          const allowNegative = target.dataset.allowNegative === "true";
          const result = sanitizeNumericInput(raw, {
            allowDecimal,
            allowNegative,
            caret: rawCaret,
          });
          if (target.value !== result.display) {
            target.value = result.display;
            setInputCaret(target, result.caret);
          }
          return;
        }

        if (!raw || !/[0-9٠-٩]/.test(raw)) {
          return;
        }

        const converted = toPersianDisplayText(raw);
        if (converted !== raw) {
          target.value = converted;
          setInputCaret(target, rawCaret);
        }
      },
      true
    );
  }

  const api = {
    toEnglishDigits,
    toPersianDigits,
    toPersianDisplayText,
    sanitizeNumericInput,
    isSensitiveFieldKey,
    normalizePayloadDigits,
    installThemePersianDigits,
  };

  if (globalScope) {
    globalScope.SugaristaDigits = api;
  }
})(typeof window !== "undefined" ? window : globalThis);
