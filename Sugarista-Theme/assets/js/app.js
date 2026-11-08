document.addEventListener("alpine:init", () => {
  const CART_STORAGE_KEY = "sugarista_cart";
  const CART_TOKEN_STORAGE_KEY = "sugarista_cart_token";
  const CART_TRACK_DEBOUNCE_MS = 1200;
  const PROFILE_STORAGE_KEY = "sugarista_profile_phone";

  Alpine.data("cafeMenu", () => ({
    apiBaseUrl: window.SUGARISTA_API_BASE_URL || "/api",
    loading: true,
    error: "",
    search: "",
    selectedCategory: "all",
    categories: [],
    items: [],
    addons: [],
    modifiers: [],
    drinkBuilders: [],
    cart: [],
    cartDrawerOpen: false,
    cartView: "cart",
    cartTrackTimer: null,
    cartProgressTimer: null,
    cartTokenValue: null,
    checkoutForm: {
      customerName: "",
      customerPhone: "",
      email: "",
      note: "",
      discountCode: "",
      redeemPoints: "",
      useWallet: false,
      personalCup: false,
      useSubscription: false,
      isSplitBill: false,
      splitBillMode: "ByItem",
      guestCount: 2,
      splitShares: []
    },
    checkoutLoading: false,
    checkoutError: "",
    appliedDiscount: null,
    checkoutWallet: null,
    checkoutWalletLoading: false,
    birthdayDraft: "",
    birthdaySaving: false,
    cartProgress: null,
    cartProgressTimer: null,
    pickupSchedule: null,
    pickupLoading: false,
    pickupSelectionActive: false,
    selectedPickupDate: null,
    selectedDeliverySlot: null,
    pickupError: "",
    checkoutConflict: null,
    acceptPriceChanges: false,
    checkoutIdempotencyKey: null,
    orderStatus: null,
    orderStatusLoading: false,
    orderStatusTimer: null,
    actionMessage: "",
    actionError: "",
    cancelConfirmOpen: false,
    rescheduleOpen: false,
    rescheduleDate: null,
    rescheduleSlot: null,
    rescheduleSaving: false,
    rescheduleError: "",
    profileOpen: false,
    profileLoading: false,
    profileError: "",
    profilePhoneInput: "",
    profilePhone: null,
    profile: null,
    flashSale: null,
    flashSaleTimer: null,
    flashSaleRemaining: "",
    addToCartModal: null,
    mockPayment: null,
    splitOrder: null,
    splitMockPayment: null,
    splitLoading: false,
    orderSuccess: null,
    paymentLoading: false,
    settings: {
      browserTitle: "Sugarista Cafe | Digital Menu",
      metaDescription: "منوی دیجیتال کافه شوگریستا",
      eyebrow: "Sugarista Cafe",
      brandTitle: "منوی دیجیتال شوگریستا",
      brandSubtitle: "Mobile-first QR menu for fast in-cafe browsing",
      heroBadgeText: "Scan, choose, then order at the counter",
      heroTitle: "منو را سریع ببینید، دسته را انتخاب کنید و راحت سفارش دهید",
      heroDescription: "",
      heroPrimaryButtonLabel: "شروع مرور منو",
      heroSecondaryButtonLabel: "پرفروش‌ها",
      heroHours: "",
      howToOrderLabel: "How To Order",
      howToOrderTitle: "سفارش در 3 قدم",
      howToOrderStatus: "Fast UX",
      stepOneTitle: "",
      stepOneDescription: "",
      stepTwoTitle: "",
      stepTwoDescription: "",
      stepThreeTitle: "",
      stepThreeDescription: "",
      quickAccessLabel: "Quick Access",
      quickAccessTitle: "مسیر سریع برای انتخاب",
      quickAccessDescription: "",
      popularLabel: "Popular Now",
      popularTitle: "پیشنهادهای سریع برای مهمان‌های جدید",
      popularDescription: "",
      menuLabel: "Browse Menu",
      menuTitle: "منوی کامل",
      menuToolbarHint: "",
      searchPlaceholder: "مثلا: لاته، چیزکیک، کروسان...",
      visitLabel: "Visit Sugarista",
      visitTitle: "اطلاعات کافه",
      visitDescription: "",
      contactPhone: "",
      address: "",
      breakfastHours: "",
      takeawayStatus: "",
      instagramUrl: "",
      footerText: "Sugarista Cafe Digital Menu Theme",
      footerPopularLabel: "پرفروش‌ها",
      footerMenuLabel: "منو",
      footerCallLabel: "تماس",
      isOnlineSalesEnabled: true,
      isOnlineOrderingAvailable: true,
      onlineSalesNotice: "",
      themeMode: "auto",
      timeZoneId: "Asia/Tehran",
      lightThemeStartHour: 6,
      darkThemeStartHour: 20,
      allowManualThemeOverride: true
    },
    // --- theme (light/dark by café time, manual override persisted) ---
    themePref: null,
    themeTimer: null,
    themeIsDark: false,
    // --- search box typewriter placeholder ---
    placeholderText: "مثلا: لاته، چیزکیک، کروسان...",
    searchTypeTimer: null,
    searchUserTyping: false,
    async init() {
      this.loadThemePref();
      this.applyTheme();
      this.loadCart();
      this.restoreProfilePhone();
      this.handleRouteState();
      this.loading = true;
      this.error = "";

      try {
        const [menuResponse, settingsResponse] = await Promise.all([
          fetch(`${this.apiBaseUrl}/menu`),
          fetch(`${this.apiBaseUrl}/site-settings`)
        ]);

        if (!menuResponse.ok || !settingsResponse.ok) {
          throw new Error("خطا در دریافت اطلاعات منو از سرور.");
        }

        const menuData = await menuResponse.json();
        const settingsData = await settingsResponse.json();

        this.categories = menuData.categories.map((category) => ({
          id: category.slug,
          label: category.label,
          order: category.displayOrder
        }));
        this.items = menuData.items.map((item) => ({
          ...item,
          tags: item.tags || [],
          allergens: item.allergens || [],
          variants: item.variants || [],
          galleryImageUrls: item.galleryImageUrls || []
        }));
        this.addons = (menuData.addons || []).map((addon) => ({
          ...addon,
          tags: addon.tags || []
        }));
        this.modifiers = menuData.modifiers || [];
        // Empty when the admin has switched the whole feature off — the theme
        // then renders no customisation controls at all.
        this.drinkBuilders = menuData.drinkBuilders || [];
        this.settings = { ...this.settings, ...settingsData };
        this.applyPageMetadata();
        this.setupTheme();
        this.initSearchTypewriter();
        await this.loadSplitOrderFromRoute();
        this.flashSale = menuData.flashSale || null;
        if (this.flashSale) {
          this.startFlashSaleCountdown();
        }
        if (this.cart.length) {
          this.refreshCartProgress();
        }
        await this.loadPickupSchedule();
      } catch (error) {
        this.error = error.message || "دریافت اطلاعات با خطا مواجه شد.";
      } finally {
        this.loading = false;
      }
    },
    /* ── theme: light/dark by café local time, manual override persisted ── */
    setupTheme() {
      this.loadThemePref();

      if (this.settings.allowManualThemeOverride === false && this.themePref?.mode === "manual") {
        // The café has disabled manual overrides — clear any stale preference.
        this.themePref = null;
        try { localStorage.removeItem("sugarista_theme_pref"); } catch {}
      }

      this.applyTheme();
      this.startThemeClock();
    },
    loadThemePref() {
      try {
        const raw = JSON.parse(localStorage.getItem("sugarista_theme_pref") || "null");
        if (raw && (raw.mode === "manual" || raw.mode === "auto")) {
          this.themePref = raw;
        }
      } catch {
        this.themePref = null;
      }
    },
    /** Cafe-local hour (0-23) resolved through the configured IANA time zone. */
    cafeLocalHour() {
      const timeZone = this.settings.timeZoneId || "Asia/Tehran";
      try {
        const formatted = new Intl.DateTimeFormat("en-US", {
          timeZone,
          hour: "numeric",
          hourCycle: "h23"
        }).format(new Date());
        return Number.parseInt(formatted, 10);
      } catch {
        // Unknown time zone id — fall back to the visitor's own clock.
        return new Date().getHours();
      }
    },
    scheduledTheme() {
      const hour = this.cafeLocalHour();
      const lightStart = Number(this.settings.lightThemeStartHour ?? 6);
      const darkStart = Number(this.settings.darkThemeStartHour ?? 20);

      if (lightStart === darkStart) return "light";

      const finalTheme = (hour >= darkStart || hour < lightStart) ? "dark" : "light";
      return finalTheme;
    },
    /** Resolves what theme should be active right now, and applies it live. */
    applyTheme() {
      let next;
      if (this.themePref?.mode === "manual" && (this.themePref.value === "light" || this.themePref.value === "dark")) {
        next = this.themePref.value;
      } else if (this.settings.themeMode === "light") {
        next = "light";
      } else if (this.settings.themeMode === "dark") {
        next = "dark";
      } else {
        next = this.scheduledTheme();
      }

      const root = document.documentElement;
      if (root.dataset.theme !== next) {
        // Attribute swap only — CSS variables transition, no reload, no flash.
        root.dataset.theme = next;
        root.dataset.bsTheme = next;
        root.style.colorScheme = next;
      } else {
        root.dataset.bsTheme = next;
        root.style.colorScheme = next;
      }
      this.themeIsDark = next === "dark";
      this.$nextTick?.();
    },
    toggleTheme() {
      const root = document.documentElement;
      const current = root.dataset.theme === "dark" ? "dark" : "light";
      const next = current === "dark" ? "light" : "dark";

      this.themePref = { mode: "manual", value: next };
      try { localStorage.setItem("sugarista_theme_pref", JSON.stringify(this.themePref)); } catch {}
      this.applyTheme();
    },
    resetThemeToAuto() {
      this.themePref = { mode: "auto", value: null };
      try {
        localStorage.setItem("sugarista_theme_pref", JSON.stringify(this.themePref));
      } catch {}
      this.applyTheme();
    },
    // Re-check the schedule every minute so the theme moves with the clock —
    // attribute swap only, never a reload.
    startThemeClock() {
      if (this.themeTimer) clearInterval(this.themeTimer);
      this.themeTimer = setInterval(() => {
        if (this.themePref?.mode !== "manual") {
          this.applyTheme();
        }
      }, 60_000);
    },
    destroy() {
      if (this.themeTimer) clearInterval(this.themeTimer);
      if (this.searchTypeTimer) clearTimeout(this.searchTypeTimer);
    },
    /* ── search placeholder typewriter ──
       Types real item names from the menu inside the search box placeholder,
       then deletes them, word by word. Pauses (static placeholder) whenever
       the visitor has anything typed, and resumes when the field is emptied. */
    initSearchTypewriter() {
      if (this.searchTypeTimer) {
        clearTimeout(this.searchTypeTimer);
        this.searchTypeTimer = null;
      }

      const names = (this.items || [])
        .map((item) => (item.name || "").trim())
        .filter((name) => name)
        .filter((name, index, all) => all.indexOf(name) === index);

      if (!names.length) {
        this.placeholderText = this.settings.searchPlaceholder || "جستجو در منو";
        return;
      }

      // Start from a random word so repeat visitors see variety.
      let wordIndex = Math.floor(Math.random() * names.length);
      let chars = 0;
      let deleting = false;

      const TICK = 90;           // typing speed per character
      const DELETE_TICK = 45;    // deleting speed per character
      const HOLD_FULL = 1800;    // pause once a whole word is visible
      const HOLD_EMPTY = 400;    // pause after a word is fully erased

      const schedule = (delay) => {
        this.searchTypeTimer = setTimeout(() => {
          // Never let one bad tick kill the loop for good.
          try {
            step();
          } catch (error) {
            console.warn("search typewriter tick failed, retrying", error);
            schedule(500);
          }
        }, delay);
      };

      const step = () => {
        // Visitor is searching — show the calm static placeholder, keep idling.
        // searchUserTyping flips instantly on every keystroke (the x-model
        // debounced `search` updates ~200ms later and covers resets/filters).
        if (this.searchUserTyping || (this.search || "").trim() !== "") {
          this.placeholderText = this.settings.searchPlaceholder || "جستجو در منو";
          schedule(500);
          return;
        }

        const word = names[wordIndex];
        if (!deleting) {
          chars += 1;
          this.placeholderText = word.slice(0, chars);
          if (chars >= word.length) {
            deleting = true;
            schedule(HOLD_FULL);
          } else {
            schedule(TICK);
          }
        } else {
          chars -= 1;
          this.placeholderText = chars > 0 ? word.slice(0, chars) : word.slice(0, 1);
          if (chars <= 0) {
            // Erased fully — move to the next word after a small beat.
            deleting = false;
            chars = 0;
            this.placeholderText = "";
            wordIndex = (wordIndex + 1) % names.length;
            schedule(HOLD_EMPTY);
          } else {
            schedule(DELETE_TICK);
          }
        }
      };

      schedule(1200);
    },
    handleRouteState() {
      const params = new URLSearchParams(window.location.search);

      const successCode = params.get("code");
      if (params.get("order") === "success" && successCode) {
        const discountCode = params.get("discount");
        const discountAmount = Number(params.get("discountAmount")) || 0;
        this.orderSuccess = discountCode && discountAmount > 0
          ? { trackingCode: successCode, discountCode, discountAmount }
          : { trackingCode: successCode };
        this.startOrderStatusPolling(successCode);
        return;
      }

      const splitOrderId = params.get("split");
      const splitPaymentId = params.get("payment");
      const authority = params.get("authority");
      if (splitOrderId && splitPaymentId && authority) {
        this.splitMockPayment = {
          orderId: splitOrderId,
          paymentId: splitPaymentId,
          authority
        };
        this.restorePendingPayment();
        return;
      }

      const payOrderId = params.get("pay");
      if (payOrderId && authority) {
        this.mockPayment = {
          orderId: payOrderId,
          authority
        };
        this.restorePendingPayment();
      }
    },
    restorePendingPayment() {
      try {
        const raw = window.sessionStorage.getItem("sugarista_pending_payment");
        if (!raw) {
          return;
        }

        this.pendingPayment = JSON.parse(raw);
        window.sessionStorage.removeItem("sugarista_pending_payment");
      } catch {
        this.pendingPayment = null;
      }
    },
    async loadSplitOrderFromRoute() {
      const params = new URLSearchParams(window.location.search);
      const splitOrderId = params.get("split");
      if (!splitOrderId || this.splitMockPayment) {
        return;
      }

      await this.refreshSplitOrder(splitOrderId);
    },
    async refreshSplitOrder(orderId) {
      this.splitLoading = true;
      try {
        const response = await fetch(`${this.apiBaseUrl}/orders/${orderId}/split-status`);
        if (!response.ok) {
          throw new Error("سفارش یافت نشد.");
        }

        this.splitOrder = await response.json();
        if (this.splitOrder.status === "Paid") {
          this.orderSuccess = { trackingCode: this.splitOrder.trackingCode };
        }
      } catch (error) {
        this.checkoutError = error.message || "بارگذاری وضعیت تسویه جداگانه با خطا مواجه شد.";
      } finally {
        this.splitLoading = false;
      }
    },
    get guestOptions() {
      const count = Number(this.checkoutForm.guestCount) || 2;
      return Array.from({ length: count }, (_, index) => index + 1);
    },
    cartTotal() {
      return this.cart.reduce((total, line) => total + line.unitPrice * line.quantity, 0);
    },
    get automaticDiscountAmount() {
      return Number(this.cartProgress?.automaticDiscountTotal) || 0;
    },
    get tierProgress() {
      return this.cartProgress?.tier || null;
    },
    get discountedAddons() {
      return (this.addons || []).filter((addon) => Number(addon.specialOfferPrice) > 0);
    },
    get tierProgressPercent() {
      return Math.max(0, Math.min(100, Number(this.tierProgress?.progressPercent) || 0));
    },
    get tierProgressMessage() {
      const tier = this.tierProgress;
      if (!tier) {
        return "";
      }

      if (tier.nextThresholdAmount != null) {
        const reward =
          tier.nextValueKind === "Percentage"
            ? `${this.toPersianNumber(tier.nextValue)}٪ تخفیف`
            : `${this.formatPrice(tier.nextValue)} تخفیف`;
        return `${this.formatPrice(tier.remainingAmount)} دیگر بخرید تا ${reward} بگیرید`;
      }

      if (tier.appliedThresholdAmount != null) {
        const reward =
          tier.appliedValueKind === "Percentage"
            ? `${this.toPersianNumber(tier.appliedValue)}٪ تخفیف`
            : `${this.formatPrice(tier.appliedValue)} تخفیف`;
        return `تبریک! تخفیف پلکانی ${reward} فعال شد`;
      }

      return "";
    },
    get finalAmount() {
      const discount = Math.max(
        Number(this.appliedDiscount?.amount) || 0,
        this.automaticDiscountAmount
      );
      const final = this.cartTotal - discount;
      return Math.max(0, Math.round(final * 100) / 100);
    },
    get walletBalance() {
      const digits = (this.checkoutForm.customerPhone || "").replace(/\D/g, "");
      if (this.checkoutWallet?.phone === digits) {
        return Math.max(0, Number(this.checkoutWallet.wallet?.balance) || 0);
      }
      return 0;
    },
    get walletPayable() {
      return this.cart.length > 0 && !this.checkoutForm.isSplitBill && this.walletBalance > 0;
    },
    get walletPaymentMaxPercent() {
      const digits = (this.checkoutForm.customerPhone || "").replace(/\D/g, "");
      if (this.checkoutWallet?.phone === digits) {
        const percent = Number(this.checkoutWallet.wallet?.paymentMaxPercent);
        return percent > 0 && percent <= 100 ? percent : 100;
      }
      return 100;
    },
    get walletSpendAmount() {
      if (!this.checkoutForm.useWallet || !this.walletPayable) {
        return 0;
      }
      const capped = Math.floor(this.finalAmount * this.walletPaymentMaxPercent / 100);
      return Math.min(this.walletBalance, this.finalAmount, capped);
    },
    get checkoutPersonalCup() {
      const digits = (this.checkoutForm.customerPhone || "").replace(/\D/g, "");
      if (this.checkoutWallet?.phone === digits && this.checkoutWallet.personalCup?.enabled) {
        return this.checkoutWallet.personalCup;
      }
      return null;
    },
    get checkoutSubscription() {
      const digits = (this.checkoutForm.customerPhone || "").replace(/\D/g, "");
      if (this.checkoutWallet?.phone === digits && this.checkoutWallet.subscription) {
        return this.checkoutWallet.subscription;
      }
      return null;
    },
    get checkoutGroup() {
      const digits = (this.checkoutForm.customerPhone || "").replace(/\D/g, "");
      if (this.checkoutWallet?.phone === digits && this.checkoutWallet.group) {
        return this.checkoutWallet.group;
      }
      return null;
    },
    get payableAfterWallet() {
      const final = this.finalAmount - this.walletSpendAmount;
      return Math.max(0, Math.round(final * 100) / 100);
    },
    get paymentAmount() {
      return this.pendingPayment?.amount ?? this.finalAmount;
    },
    get paymentDiscount() {
      if (this.pendingPayment?.discountAmount > 0) {
        return { code: this.pendingPayment.code, amount: this.pendingPayment.discountAmount };
      }
      return this.appliedDiscount;
    },
    get splitPercentageTotal() {
      return this.checkoutForm.splitShares.reduce(
        (total, share) => total + Number(share.sharePercentage || 0),
        0
      );
    },
    get splitAmountTotal() {
      return this.checkoutForm.splitShares.reduce(
        (total, share) => total + Number(share.shareAmount || 0),
        0
      );
    },
    get splitAmountRemaining() {
      return this.cartTotal - this.splitAmountTotal;
    },
    get canSubmitCheckout() {
      if (this.salesClosed) {
        return false;
      }
      if (!this.checkoutForm.customerName.trim() || !this.checkoutForm.customerPhone.trim()) {
        return false;
      }

      if (!this.checkoutForm.isSplitBill) {
        return true;
      }

      if (this.checkoutForm.splitBillMode === "ByPercentage") {
        return this.splitPercentageTotal === 100;
      }

      if (this.checkoutForm.splitBillMode === "ByCustomAmount") {
        return Math.abs(this.splitAmountRemaining) < 0.01 && this.splitAmountTotal > 0;
      }

      return true;
    },
    guestKey(guestNumber) {
      return `guest-${guestNumber}`;
    },
    guestLabel(guestNumber) {
      return `مهمان ${this.toPersianNumber(guestNumber)}`;
    },
    assignCartPayer(cartKey, guestNumber) {
      const line = this.cart.find((entry) => entry.cartKey === cartKey);
      if (!line) {
        return;
      }

      line.payerKey = this.guestKey(guestNumber);
      line.payerLabel = this.guestLabel(guestNumber);
      this.saveCart();
    },
    ensureCartPayerAssignments() {
      this.cart.forEach((line, index) => {
        if (!line.payerKey) {
          const guestNumber = (index % this.guestOptions.length) + 1;
          line.payerKey = this.guestKey(guestNumber);
          line.payerLabel = this.guestLabel(guestNumber);
        }
      });
      this.saveCart();
    },
    initSplitShares() {
      const count = Number(this.checkoutForm.guestCount) || 2;
      const mode = this.checkoutForm.splitBillMode;
      const basePercent = Math.floor(100 / count);
      const remainder = 100 - basePercent * count;
      const shares = [];

      for (let guestNumber = 1; guestNumber <= count; guestNumber += 1) {
        const payerKey = this.guestKey(guestNumber);
        const payerLabel = this.guestLabel(guestNumber);

        if (mode === "ByPercentage") {
          shares.push({
            payerKey,
            payerLabel,
            sharePercentage: guestNumber === count ? basePercent + remainder : basePercent,
            shareAmount: 0
          });
        } else if (mode === "ByCustomAmount") {
          shares.push({
            payerKey,
            payerLabel,
            sharePercentage: 0,
            shareAmount: 0
          });
        } else {
          shares.push({
            payerKey,
            payerLabel,
            sharePercentage: 0,
            shareAmount: 0
          });
        }
      }

      this.checkoutForm.splitShares = shares;
    },
    handleSplitBillToggle() {
      if (this.checkoutForm.isSplitBill) {
        this.initSplitShares();
        if (this.checkoutForm.splitBillMode === "ByItem") {
          this.ensureCartPayerAssignments();
        }
        return;
      }

      this.checkoutForm.splitShares = [];
    },
    handleSplitGuestCountChange() {
      this.initSplitShares();
      if (this.checkoutForm.splitBillMode === "ByItem") {
        this.ensureCartPayerAssignments();
      }
    },
    handleSplitBillModeChange() {
      this.initSplitShares();
      if (this.checkoutForm.splitBillMode === "ByItem") {
        this.ensureCartPayerAssignments();
      }
    },
    cartTotalForGuest(guestNumber) {
      const payerKey = this.guestKey(guestNumber);
      return this.cart
        .filter((line) => line.payerKey === payerKey)
        .reduce((total, line) => total + line.unitPrice * line.quantity, 0);
    },
    applyPageMetadata() {
      document.title = this.settings.browserTitle;

      const descriptionTag = document.querySelector('meta[name="description"]');
      if (descriptionTag) {
        descriptionTag.setAttribute("content", this.settings.metaDescription);
      }
    },
    loadCart() {
      try {
        const raw = window.localStorage.getItem(CART_STORAGE_KEY);
        this.cart = raw ? JSON.parse(raw) : [];
      } catch {
        this.cart = [];
      }
    },
    saveCart() {
      window.localStorage.setItem(CART_STORAGE_KEY, JSON.stringify(this.cart));
    },
    cartToken() {
      if (this.cartTokenValue) {
        return this.cartTokenValue;
      }

      try {
        let token = window.localStorage.getItem(CART_TOKEN_STORAGE_KEY);
        if (!token) {
          token = this.generateCartToken();
          window.localStorage.setItem(CART_TOKEN_STORAGE_KEY, token);
        }
        this.cartTokenValue = token;
      } catch {
        // Private mode or storage disabled: keep one token for this page view,
        // which is enough for a single reminder.
        this.cartTokenValue = this.generateCartToken();
      }

      return this.cartTokenValue;
    },
    generateCartToken() {
      if (window.crypto?.randomUUID) {
        return window.crypto.randomUUID();
      }
      return `cart-${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 10)}`;
    },
    // ── کارت مهر / پروفایل مشتری ─────────────────────────────────────────
    restoreProfilePhone() {
      try {
        const phone = window.localStorage.getItem(PROFILE_STORAGE_KEY) || "";
        if (phone) {
          this.profilePhone = phone;
          this.profilePhoneInput = phone;
          if (!this.checkoutForm.customerPhone.trim()) {
            this.checkoutForm.customerPhone = phone;
          }
        }
      } catch {
        // Private mode: اعضا فقط در همین بازدید فعال هستند.
      }
    },
    openProfile() {
      this.profileError = "";
      this.profileOpen = true;
      if (this.profilePhone) {
        this.loadProfile();
      }
    },
    closeProfile() {
      this.profileOpen = false;
    },
    profileStatusChip() {
      if (this.profile?.activeReward) {
        return "جایزهٔ آماده دارد";
      }
      if (this.profile?.loyaltyPoints > 0) {
        return `${this.toPersianNumber(this.profile.loyaltyPoints)} امتیاز`;
      }
      return "";
    },
    profileStampSlots() {
      const total = this.profile?.ordersPerCard || 0;
      const filled = Math.min(this.profile?.currentCardStamps ?? 0, total);
      return Array.from({ length: total }, (_, index) => index < filled);
    },
    async submitProfilePhone() {
      const digits = (this.profilePhoneInput || "").replace(/\D/g, "");
      if (digits.length < 8) {
        this.profileError = "شماره موبایل معتبر وارد کنید.";
        return;
      }
      this.profilePhone = digits;
      try {
        window.localStorage.setItem(PROFILE_STORAGE_KEY, digits);
      } catch {
        // Private mode
      }
      if (!this.checkoutForm.customerPhone.trim()) {
        this.checkoutForm.customerPhone = digits;
      }
      await this.loadProfile();
    },
    // ثبت/تغییر تاریخ تولد (خود-سرویس، فقط شمارهٔ موبایل). ادمین می‌تواند
    // این حق را از تنظیمات قفل کند؛ آنگاه پیام خطای «قفل توسط مدیریت» می‌آید.
    async saveBirthday() {
      if (!this.profilePhone || !this.birthdayDraft) {
        return;
      }
      this.birthdaySaving = true;
      this.profileError = "";
      try {
        const response = await fetch(`${this.apiBaseUrl}/customers/profile/birthday`, {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({
            phoneNumber: this.profilePhone,
            dateOfBirth: this.birthdayDraft
          })
        });
        if (!response.ok) {
          const payload = await response.json().catch(() => ({}));
          throw new Error(payload.detail || payload.title || "ثبت تاریخ تولد انجام نشد.");
        }
        if (this.profile?.birthday) {
          this.profile = {
            ...this.profile,
            birthday: { ...this.profile.birthday, dateOfBirth: this.birthdayDraft }
          };
        }
      } catch (error) {
        this.profileError = error.message || "ثبت تاریخ تولد انجام نشد.";
      } finally {
        this.birthdaySaving = false;
      }
    },
    async loadProfile() {
      if (!this.profilePhone) {
        return;
      }
      this.profileLoading = true;
      this.profileError = "";
      try {
        const response = await fetch(`${this.apiBaseUrl}/customers/profile`, {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ phoneNumber: this.profilePhone })
        });
        if (!response.ok) {
          throw new Error("خطا در دریافت پروفایل.");
        }
        this.profile = await response.json();
      } catch (error) {
        this.profile = null;
        this.profileError = error.message || "خطا در دریافت پروفایل.";
      } finally {
        this.profileLoading = false;
      // وقتی پروفایل تازه می‌آید، تاریخ تولد ثبت‌شده را در ادیتور پیش‌فرض بگذار.
      this.birthdayDraft = this.profile?.birthday?.dateOfBirth || "";
      }
    },
    forgetProfile() {
      this.profileOpen = false;
      this.profile = null;
      this.profilePhone = null;
      this.profilePhoneInput = "";
      try {
        window.localStorage.removeItem(PROFILE_STORAGE_KEY);
      } catch {
        // Private mode
      }
    },
    scheduleCartTracking() {
      window.clearTimeout(this.cartTrackTimer);
      this.cartTrackTimer = window.setTimeout(() => {
        this.trackCart();
      }, CART_TRACK_DEBOUNCE_MS);
    },
    async trackCart() {
      if (this.salesClosed) {
        return;
      }

      window.clearTimeout(this.cartTrackTimer);

      try {
        await fetch(`${this.apiBaseUrl}/carts/track`, {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          keepalive: true,
          body: JSON.stringify({
            cartToken: this.cartToken(),
            customerName: this.checkoutForm.customerName.trim() || null,
            phoneNumber: this.checkoutForm.customerPhone.trim() || null,
            email: (this.checkoutForm.email || "").trim() || null,
            items: this.cart.map((line) => ({
              menuItemId: line.menuItemId,
              sizeKey: line.sizeKey,
              quantity: line.quantity,
              modifierIds: line.modifierIds || []
            }))
          })
        });
      } catch {
        // Tracking is best-effort; a failed heartbeat must never block the
        // customer's shopping or checkout flow.
      }
    },
    scheduleCartProgress() {
      window.clearTimeout(this.cartProgressTimer);
      this.cartProgressTimer = window.setTimeout(() => {
        this.refreshCartProgress();
      }, CART_TRACK_DEBOUNCE_MS);
    },
    async refreshCartProgress() {
      if (this.salesClosed) {
        this.cartProgress = null;
        return;
      }

      if (!this.cart.length) {
        this.cartProgress = null;
        return;
      }

      window.clearTimeout(this.cartProgressTimer);

      try {
        const response = await fetch(`${this.apiBaseUrl}/discounts/cart-progress`, {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          keepalive: true,
          body: JSON.stringify({
            lines: this.cart.map((line) => ({
              menuItemId: line.menuItemId,
              categoryId: null,
              persianName: line.persianName,
              sizeKey: line.sizeKey,
              quantity: line.quantity,
              unitPrice: line.unitPrice
            }))
          })
        });

        if (!response.ok) {
          this.cartProgress = null;
          return;
        }

        this.cartProgress = await response.json();
      } catch {
        this.cartProgress = null;
      }
    },
    /* ── pickup scheduling (TASK-012) ─────────────────────────────── */
    async loadPickupSchedule() {
      this.pickupLoading = true;
      try {
        const response = await fetch(`${this.apiBaseUrl}/orders/pickup-slots`);
        if (!response.ok) {
          this.pickupSchedule = null;
          return;
        }

        const schedule = await response.json();
        this.pickupSchedule = schedule;
        if (!schedule?.enabled) {
          this.pickupSelectionActive = false;
          this.selectedDeliverySlot = null;
          this.selectedPickupDate = null;
          return;
        }

        // Keep the current selection valid; otherwise default to the first
        // open day that still has at least one free slot.
        const currentDay = (schedule.days || []).find((day) => day.date === this.selectedPickupDate);
        const fallbackDay =
          (schedule.days || []).find((day) => day.isOpen && (day.slots || []).some((slot) => !slot.isFull)) ||
          (schedule.days || []).find((day) => day.isOpen) ||
          null;

        if (!currentDay) {
          this.selectedPickupDate = fallbackDay?.date || null;
        }

        if (this.selectedDeliverySlot) {
          const stillAvailable = (schedule.days || []).some((day) =>
            (day.slots || []).some(
              (slot) => slot.deliveryAt === this.selectedDeliverySlot && !slot.isFull
            )
          );
          if (!stillAvailable) {
            this.selectedDeliverySlot = null;
          }
        }
      } catch {
        this.pickupSchedule = null;
      } finally {
        this.pickupLoading = false;
      }
    },
    get selectedPickupDay() {
      if (!this.pickupSchedule?.days || !this.selectedPickupDate) {
        return null;
      }
      return this.pickupSchedule.days.find((day) => day.date === this.selectedPickupDate) || null;
    },
    get selectedPickupSlotLabel() {
      if (!this.selectedDeliverySlot) {
        return "";
      }
      for (const day of this.pickupSchedule?.days || []) {
        const slot = (day.slots || []).find((entry) => entry.deliveryAt === this.selectedDeliverySlot);
        if (slot) {
          return `${day.weekdayLabel} ${day.dateLabel} — ساعت ${slot.timeLabel}`;
        }
      }
      return this.formatPickupDateTime(this.selectedDeliverySlot);
    },
    choosePickupMode() {
      this.pickupSelectionActive = true;
      if (!this.selectedPickupDate) {
        const fallback = (this.pickupSchedule?.days || []).find(
          (day) => day.isOpen && (day.slots || []).some((slot) => !slot.isFull)
        );
        this.selectedPickupDate = fallback?.date || null;
      }
    },
    chooseAsSoonAsPossible() {
      this.pickupSelectionActive = false;
      this.selectedDeliverySlot = null;
    },
    selectPickupDay(day) {
      if (!day || !day.isOpen) {
        return;
      }
      this.selectedPickupDate = day.date;
      this.pickupSelectionActive = true;
      const currentStillThere = (day.slots || []).some(
        (slot) => slot.deliveryAt === this.selectedDeliverySlot && !slot.isFull
      );
      if (!currentStillThere) {
        this.selectedDeliverySlot = null;
      }
    },
    selectPickupSlot(slot) {
      if (!slot || slot.isFull) {
        return;
      }
      this.selectedDeliverySlot = slot.deliveryAt;
      this.pickupSelectionActive = true;
      this.pickupError = "";
    },
    formatPickupDateTime(value) {
      if (!value) {
        return "";
      }
      try {
        return new Intl.DateTimeFormat("fa-IR", {
          timeZone: this.pickupSchedule?.timeZoneId || this.settings.timeZoneId || "Asia/Tehran",
          dateStyle: "full",
          timeStyle: "short"
        }).format(new Date(value));
      } catch {
        return new Date(value).toLocaleString("fa-IR");
      }
    },
    generateIdempotencyKey() {
      if (window.crypto?.randomUUID) {
        return window.crypto.randomUUID();
      }
      return `ck-${Date.now()}-${Math.random().toString(36).slice(2, 10)}`;
    },
    startFlashSaleCountdown() {
      window.clearInterval(this.flashSaleTimer);
      const tick = () => {
        if (!this.flashSale?.endsAt) {
          window.clearInterval(this.flashSaleTimer);
          return;
        }
        const end = new Date(this.flashSale.endsAt).getTime();
        const now = Date.now();
        const diff = end - now;
        if (diff <= 0) {
          window.clearInterval(this.flashSaleTimer);
          this.flashSale = null;
          this.flashSaleRemaining = "";
          // Re-fetch menu so discounted prices revert automatically.
          this.refreshMenu();
          return;
        }
        const hours = Math.floor(diff / 3600000);
        const minutes = Math.floor((diff % 3600000) / 60000);
        const seconds = Math.floor((diff % 60000) / 1000);
        this.flashSaleRemaining = `${String(hours).padStart(2, "0")}:${String(minutes).padStart(2, "0")}:${String(seconds).padStart(2, "0")}`;
      };
      tick();
      this.flashSaleTimer = window.setInterval(tick, 1000);
    },
    async refreshMenu() {
      try {
        const response = await fetch(`${this.apiBaseUrl}/menu`);
        if (!response.ok) return;
        const menuData = await response.json();
        this.categories = (menuData.categories || []).map((category) => ({
          id: category.slug,
          label: category.label,
          order: category.displayOrder
        }));
        this.items = (menuData.items || []).map((item) => ({
          ...item,
          tags: item.tags || [],
          allergens: item.allergens || [],
          variants: item.variants || [],
          galleryImageUrls: item.galleryImageUrls || []
        }));
        this.addons = (menuData.addons || []).map((addon) => ({
          ...addon,
          tags: addon.tags || []
        }));
        this.modifiers = menuData.modifiers || [];
        this.flashSale = menuData.flashSale || null;
        if (this.flashSale) {
          this.startFlashSaleCountdown();
        }
        if (this.cart.length) {
          this.refreshCartProgress();
          this.loadPickupSchedule();
        }
      } catch {
        // ignore
      }
    },
    get cartCount() {
      return this.cart.reduce((total, line) => total + line.quantity, 0);
    },
    get salesClosed() {
      return (
        this.settings.isOnlineSalesEnabled === false ||
        this.settings.isOnlineOrderingAvailable === false
      );
    },
    get salesNoticeText() {
      if (this.settings.isOnlineOrderingAvailable === false) {
        // return "سفارش آنلاین هنوز فعال نشده است؛ لطفاً حضوری سفارش دهید.";
        return "";
      }

      const notice = (this.settings.onlineSalesNotice || "").trim();
      // return notice || "";
      return notice || "فروش آنلاین موقتاً غیرفعال است؛ می‌توانید حضوری سفارش دهید.";
    },
    // ── بنر ایونت و مناسبت (کنترل کامل از پنل ادمین) ────────────
    // eventBannerEnabled کل بنر را فعال/غیرفعال می‌کند؛ بدون تصویر هم بنر
    // رندر نمی‌شود و بازهٔ نمایش اختیاری آن را خودکار می‌بازد/می‌بندد.
    get eventBannerEnabled() {
      return this.settings.eventBannerEnabled === true;
    },
    get eventBannerImage() {
      return (this.settings.eventBannerImageUrl || "").trim();
    },
    get eventBannerTitle() {
      return (this.settings.eventBannerTitle || "").trim();
    },
    get eventBannerLink() {
      return (this.settings.eventBannerLinkUrl || "").trim();
    },
    get eventBannerVisible() {
      if (!this.eventBannerEnabled || !this.eventBannerImage) return false;
      const starts = this._parseEventDate(this.settings.eventBannerStartsAt);
      const ends = this._parseEventDate(this.settings.eventBannerEndsAt);
      const now = Date.now();
      if (starts && now < starts) return false;
      if (ends && now > ends) return false;
      return true;
    },
    _parseEventDate(value) {
      if (!value) return null;
      const time = new Date(value).getTime();
      return Number.isNaN(time) ? null : time;
    },
    get featuredItems() {
      return this.items.filter((item) => item.featured);
    },
    get hasFilters() {
      return Boolean(this.search) || this.selectedCategory !== "all";
    },
    get activeCategoryLabel() {
      if (this.selectedCategory === "all") {
        return "همه";
      }

      return this.categories.find((category) => category.id === this.selectedCategory)?.label || "همه";
    },
    get filteredItems() {
      const query = this.normalizeText(this.search);

      return this.items.filter((item) => {
        const matchesCategory =
          this.selectedCategory === "all" || item.category === this.selectedCategory;

        const haystack = this.normalizeText(
          [
            item.name,
            item.persianName,
            item.englishName,
            item.description,
            item.categoryLabel,
            item.volume,
            item.allergens.join(" "),
            item.tags.join(" ")
          ].join(" ")
        );
        const matchesSearch = !query || haystack.includes(query);

        return matchesCategory && matchesSearch;
      });
    },
    get filteredGroups() {
      return this.categories
        .map((category) => ({
          id: category.id,
          label: category.label,
          items: this.filteredItems.filter((item) => item.category === category.id)
        }))
        .filter((group) => group.items.length > 0);
    },
    countForCategory(categoryId) {
      return this.items.filter((item) => item.category === categoryId).length;
    },
    setCategory(categoryId) {
      this.selectedCategory = categoryId;

      window.requestAnimationFrame(() => {
        const targetId = categoryId === "all" ? "menu" : `group-${categoryId}`;
        const target = document.getElementById(targetId);
        if (!target) {
          return;
        }

        const stickyNav = document.querySelector(".menu-sticky-shell");
        const stickyOffset = stickyNav ? stickyNav.offsetHeight + 12 : 72;
        const top = target.getBoundingClientRect().top + window.scrollY - stickyOffset;
        window.scrollTo({ top: Math.max(top, 0), behavior: "smooth" });
      });
    },
    resetFilters() {
      this.search = "";
      this.selectedCategory = "all";
    },
    openCartDrawer(view = "cart") {
      if (this.salesClosed) {
        return;
      }
      this.cartView = view;
      this.cartDrawerOpen = true;
      this.checkoutError = "";
      if (view === "checkout") {
        this.trackCart();
        this.refreshCartProgress();
        this.loadPickupSchedule();
        this.loadCheckoutWallet();
      }
      if (view === "checkout" && this.checkoutForm.isSplitBill) {
        if (this.checkoutForm.splitBillMode === "ByItem") {
          this.ensureCartPayerAssignments();
        } else if (!this.checkoutForm.splitShares.length) {
          this.initSplitShares();
        }
      }
    },
    closeCartDrawer() {
      this.cartDrawerOpen = false;
      this.cartView = "cart";
      this.checkoutError = "";
    },
    // ── کیف پول در تسویه حساب ─────────────────────────────────────
    // شناسایی مشتری فقط با شماره موبایل است: موجودی کیف پول همان‌جا که شماره
    // تایپ/تغییر می‌کند بی‌صدا از پروفایل گرفته می‌شود (بدون رمز و کد تایید).
    async loadCheckoutWallet() {
      const digits = (this.checkoutForm.customerPhone || "").replace(/\D/g, "");
      if (digits.length < 8) {
        this.checkoutWallet = null;
        this.checkoutForm.useWallet = false;
        return;
      }

      this.checkoutWalletLoading = true;
      try {
        const response = await fetch(`${this.apiBaseUrl}/customers/profile`, {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ phoneNumber: digits })
        });
        const payload = response.ok ? await response.json() : null;
        // باکس به شماره‌ای که درخواست با آن ساخته شده قفل است تا اگر کاربر
        // شماره را عوض کرد، موجودی کسی دیگر نمایش داده نشود.
        this.checkoutWallet = payload
          ? {
              phone: digits,
              wallet: payload.wallet ?? null,
              group: payload.group ?? null,
              subscription: payload.subscription ?? null,
              personalCup: payload.personalCup ?? null
            }
          : null;
      } catch {
        this.checkoutWallet = null;
      } finally {
        this.checkoutWalletLoading = false;
        if (!this.walletPayable) {
          this.checkoutForm.useWallet = false;
        }
        if (!this.checkoutPersonalCup) {
          this.checkoutForm.personalCup = false;
        }
        if (!this.checkoutSubscription) {
          this.checkoutForm.useSubscription = false;
        }
      }
    },
    scrollToAddonSection() {
      this.$nextTick(() => {
        const target = document.querySelector(".addons-section");
        if (target) {
          target.scrollIntoView({ behavior: "smooth", block: "start" });
        }
      });
    },
    openAddToCartModal(item) {
      if (!item.isOrderable || this.salesClosed) {
        return;
      }

      const defaultVariant = this.defaultVariantForItem(item);
      const builderProfile = this.builderProfileForItem(item.id);
      const builderAmounts = {};
      const builderSelected = [];

      if (builderProfile) {
        builderProfile.groups.forEach((group) => {
          group.options.forEach((option) => {
            if (group.inputType === "Slider") {
              builderAmounts[option.id] = Number(option.defaultAmount) || 0;
            } else if (option.isDefault) {
              builderSelected.push(option.id);
            }
          });
        });
      }

      // Per-item option groups (TASK-013): pre-select every available
      // default option so the popup opens with a sane, ready-to-confirm state.
      const optionGroups = item.optionGroups || [];
      const selectedOptionIds = [];
      optionGroups.forEach((group) => {
        (group.options || []).forEach((option) => {
          if (option.isDefault && option.isAvailable !== false) {
            selectedOptionIds.push(option.id);
          }
        });
      });

      this.addToCartModal = {
        item,
        selectedSizeKey: defaultVariant?.key || null,
        selectedModifierIds: [],
        selectedOptionIds,
        quantity: 1,
        builderAmounts,
        builderSelected,
        builderError: "",
        optionError: ""
      };
    },
    closeAddToCartModal() {
      this.addToCartModal = null;
    },
    trapFocus(event) {
      const container = event.currentTarget;
      const selector =
        'a[href],button:not([disabled]),input:not([disabled]),select:not([disabled]),textarea:not([disabled]),[tabindex]:not([tabindex="-1"])';
      const focusables = [...container.querySelectorAll(selector)].filter(
        (element) => element.offsetParent !== null || element === document.activeElement
      );

      if (!focusables.length) {
        return;
      }

      const first = focusables[0];
      const last = focusables[focusables.length - 1];

      if (event.shiftKey && document.activeElement === first) {
        event.preventDefault();
        last.focus();
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault();
        first.focus();
      }
    },
    defaultVariantForItem(item) {
      if (!item.variants?.length) {
        return null;
      }

      return (
        item.variants.find((variant) => variant.key === "medium") ||
        item.variants[0]
      );
    },
    modalSelectedVariant() {
      if (!this.addToCartModal?.item?.variants?.length) {
        return null;
      }

      return (
        this.addToCartModal.item.variants.find(
          (variant) => variant.key === this.addToCartModal.selectedSizeKey
        ) || this.addToCartModal.item.variants[0]
      );
    },
    modalUnitPrice() {
      const componentsTotal = this.modalBuilderComponentsTotal();
      const optionsTotal = this.modalOptionsTotal();
      const variant = this.modalSelectedVariant();
      if (variant) {
        return (variant.specialOfferPrice || variant.price || 0) + componentsTotal + optionsTotal;
      }

      const item = this.addToCartModal?.item;
      if (!item) {
        return 0;
      }

      return (item.specialOfferPrice || item.price || 0) + componentsTotal + optionsTotal;
    },
    // ── Per-item option groups (TASK-013) ──────────────────────────────────
    // Required groups ("اجباری") always render first, optional ("دلخواه")
    // after them, each in its own section of the add-to-cart popup.
    modalOptionGroups() {
      const groups = this.addToCartModal?.item?.optionGroups || [];
      return [...groups].sort((left, right) => {
        if (left.isRequired !== right.isRequired) {
          return left.isRequired ? -1 : 1;
        }
        return (left.displayOrder ?? 0) - (right.displayOrder ?? 0);
      });
    },
    isOptionSelected(optionId) {
      return this.addToCartModal?.selectedOptionIds?.includes(optionId) ?? false;
    },
    optionGroupSelectionCount(group) {
      return (group.options || []).filter((option) => this.isOptionSelected(option.id)).length;
    },
    selectSingleOption(group, option) {
      if (!this.addToCartModal || option.isAvailable === false) {
        return;
      }

      // Radio semantics: one pick replaces every other option of this group.
      const groupOptionIds = (group.options || []).map((entry) => entry.id);
      this.addToCartModal.selectedOptionIds = [
        ...this.addToCartModal.selectedOptionIds.filter(
          (id) => !groupOptionIds.includes(id)
        ),
        option.id
      ];
      this.addToCartModal.optionError = "";
    },
    toggleMultiOption(group, option) {
      if (!this.addToCartModal || option.isAvailable === false) {
        return;
      }

      const groupOptionIds = (group.options || []).map((entry) => entry.id);
      if (this.isOptionSelected(option.id)) {
        this.addToCartModal.selectedOptionIds = this.addToCartModal.selectedOptionIds.filter(
          (id) => id !== option.id
        );
        this.addToCartModal.optionError = "";
        return;
      }

      if (this.optionGroupSelectionCount(group) >= (group.maxSelections || 1)) {
        this.addToCartModal.optionError =
          `حداکثر ${this.toPersianNumber(group.maxSelections || 1)} گزینه از «${group.persianName}» قابل انتخاب است.`;
        return;
      }

      this.addToCartModal.selectedOptionIds = [
        ...this.addToCartModal.selectedOptionIds,
        option.id
      ];
      this.addToCartModal.optionError = "";
    },
    modalOptionsTotal() {
      const groups = this.addToCartModal?.item?.optionGroups || [];
      const selected = this.addToCartModal?.selectedOptionIds || [];
      return groups
        .flatMap((group) => group.options || [])
        .filter((option) => selected.includes(option.id))
        .reduce((total, option) => total + (Number(option.priceDelta) || 0), 0);
    },
    modalOptionsLabels() {
      return this.modalOptionGroups()
        .flatMap((group) => group.options || [])
        .filter((option) => this.isOptionSelected(option.id))
        .map((option) => option.persianName);
    },
    optionsValidationError() {
      for (const group of this.modalOptionGroups()) {
        if (!group.isRequired) {
          continue;
        }
        const required = Number(group.minSelections) > 0 ? Number(group.minSelections) : 1;
        if (this.optionGroupSelectionCount(group) < required) {
          return required <= 1
            ? `انتخاب یک گزینه از «${group.persianName}» الزامی است.`
            : `حداقل ${this.toPersianNumber(required)} گزینه از «${group.persianName}» را انتخاب کنید.`;
        }
      }

      return "";
    },
    modalSizeLabel() {
      return this.modalSelectedVariant()?.label || null;
    },
    toggleModalModifier(modifierId) {
      if (!this.addToCartModal) {
        return;
      }

      const selected = this.addToCartModal.selectedModifierIds;
      if (selected.includes(modifierId)) {
        this.addToCartModal.selectedModifierIds = selected.filter((id) => id !== modifierId);
        return;
      }

      this.addToCartModal.selectedModifierIds = [...selected, modifierId];
    },
    isModalModifierSelected(modifierId) {
      return this.addToCartModal?.selectedModifierIds.includes(modifierId);
    },
    builderProfileForItem(menuItemId) {
      return (this.drinkBuilders || []).find(
        (profile) => profile.menuItemId === menuItemId
      ) || null;
    },
    modalBuilderProfile() {
      const itemId = this.addToCartModal?.item?.id;
      return itemId ? this.builderProfileForItem(itemId) : null;
    },
    unitLabel(unit) {
      const labels = {
        Gram: "گرم",
        Milliliter: "میلی‌لیتر",
        Piece: "عدد",
        Shot: "شات",
        Pump: "پمپ"
      };
      return labels[unit] || "";
    },
    builderAmount(optionId) {
      return Number(this.addToCartModal?.builderAmounts?.[optionId]) || 0;
    },
    setBuilderAmount(option, rawValue) {
      if (!this.addToCartModal) {
        return;
      }

      let next = Number(rawValue) || 0;
      const min = Number(option.minAmount) || 0;
      const max = Number(option.maxAmount) || 0;
      const step = Number(option.step) || 0;

      if (next > 0) {
        if (next < min) {
          next = min;
        }

        if (step > 0) {
          const steps = Math.round((next - min) / step);
          next = min + steps * step;
        }

        if (next > max) {
          next = max;
        }
      } else {
        next = 0;
      }

      this.addToCartModal.builderAmounts = {
        ...this.addToCartModal.builderAmounts,
        [option.id]: next
      };
      this.addToCartModal.builderError = "";
    },
    toggleBuilderAmount(option) {
      if (!this.addToCartModal) {
        return;
      }

      const current = this.builderAmount(option.id);
      const fallback = Number(option.defaultAmount) > 0
        ? Number(option.defaultAmount)
        : Number(option.minAmount) || Number(option.step) || 1;

      this.setBuilderAmount(option, current > 0 ? 0 : fallback);
    },
    isBuilderChoiceSelected(optionId) {
      return Boolean(this.addToCartModal?.builderSelected?.includes(optionId));
    },
    builderGroupSelectedCount(group) {
      const selected = this.addToCartModal?.builderSelected || [];
      return (group.options || []).filter((option) => selected.includes(option.id)).length;
    },
    canSelectBuilderChoice(group) {
      if (!this.addToCartModal) {
        return false;
      }

      if (group.selectionMode === "Single") {
        return true;
      }

      return this.builderGroupSelectedCount(group) < Math.max(1, Number(group.maxSelections) || 1);
    },
    toggleBuilderChoice(group, option) {
      if (!this.addToCartModal) {
        return;
      }

      const selected = this.addToCartModal.builderSelected || [];
      if (selected.includes(option.id)) {
        this.addToCartModal.builderSelected = selected.filter((id) => id !== option.id);
        this.addToCartModal.builderError = "";
        return;
      }

      if (group.selectionMode === "Single") {
        const groupOptionIds = (group.options || []).map((entry) => entry.id);
        this.addToCartModal.builderSelected = [
          ...selected.filter((id) => !groupOptionIds.includes(id)),
          option.id
        ];
        this.addToCartModal.builderError = "";
        return;
      }

      if (!this.canSelectBuilderChoice(group)) {
        this.addToCartModal.builderError =
          `حداکثر ${this.toPersianNumber(group.maxSelections)} گزینه از «${group.name}» قابل انتخاب است.`;
        return;
      }

      this.addToCartModal.builderSelected = [...selected, option.id];
      this.addToCartModal.builderError = "";
    },
    modalBuilderComponentsTotal() {
      const profile = this.modalBuilderProfile();
      if (!profile) {
        return 0;
      }

      let total = 0;
      profile.groups.forEach((group) => {
        group.options.forEach((option) => {
          if (group.inputType === "Slider") {
            total += this.builderAmount(option.id) * (Number(option.pricePerUnit) || 0);
            return;
          }

          if (this.isBuilderChoiceSelected(option.id)) {
            total += Number(option.pricePerUnit) || 0;
          }
        });
      });

      return Math.round(total * 100) / 100;
    },
    modalBuilderSummary() {
      const profile = this.modalBuilderProfile();
      if (!profile) {
        return [];
      }

      const labels = [];
      profile.groups.forEach((group) => {
        group.options.forEach((option) => {
          if (group.inputType === "Slider") {
            const amount = this.builderAmount(option.id);
            if (amount > 0) {
              labels.push(
                `${option.name} ${this.toPersianNumber(amount)} ${this.unitLabel(option.unit)}`
              );
            }
            return;
          }

          if (this.isBuilderChoiceSelected(option.id)) {
            labels.push(option.name);
          }
        });
      });

      return labels;
    },
    builderSelectionPayload() {
      const profile = this.modalBuilderProfile();
      if (!profile) {
        return null;
      }

      // Send every option explicitly, including zeros. This makes the submitted
      // composition authoritative so the server never re-adds a default the
      // customer deliberately removed.
      const selections = [];
      profile.groups.forEach((group) => {
        group.options.forEach((option) => {
          if (group.inputType === "Slider") {
            selections.push({ optionId: option.id, amount: this.builderAmount(option.id) });
            return;
          }

          selections.push({
            optionId: option.id,
            amount: this.isBuilderChoiceSelected(option.id) ? 1 : 0
          });
        });
      });

      return { profileId: profile.id, selections };
    },
    builderSignature() {
      const payload = this.builderSelectionPayload();
      if (!payload) {
        return "";
      }

      return payload.selections
        .map((selection) => `${selection.optionId}:${selection.amount}`)
        .sort()
        .join("_");
    },
    builderValidationError() {
      const profile = this.modalBuilderProfile();
      if (!profile) {
        return "";
      }

      for (const group of profile.groups) {
        const selectedCount = group.inputType === "Slider"
          ? group.options.filter((option) => this.builderAmount(option.id) > 0).length
          : this.builderGroupSelectedCount(group);
        const minimum = Math.max(group.isRequired ? 1 : 0, Number(group.minSelections) || 0);

        if (selectedCount < minimum) {
          return minimum <= 1
            ? `انتخاب حداقل یک گزینه از «${group.name}» الزامی است.`
            : `حداقل ${this.toPersianNumber(minimum)} گزینه از «${group.name}» را انتخاب کنید.`;
        }

        if (selectedCount > (Number(group.maxSelections) || 1)) {
          return `حداکثر ${this.toPersianNumber(group.maxSelections)} گزینه از «${group.name}» قابل انتخاب است.`;
        }

        if (group.inputType !== "Slider") {
          continue;
        }

        for (const option of group.options) {
          const amount = this.builderAmount(option.id);
          if (amount === 0) {
            continue;
          }

          if (amount < Number(option.minAmount) || amount > Number(option.maxAmount)) {
            return `مقدار «${option.name}» باید بین ${this.toPersianNumber(option.minAmount)} و ${this.toPersianNumber(option.maxAmount)} ${this.unitLabel(option.unit)} باشد.`;
          }
        }
      }

      return "";
    },
    buildCartKey(menuItemId, sizeKey, modifierIds, builderSignature = "", optionSignature = "") {
      const sortedModifiers = [...modifierIds].sort().join("-");
      const builderPart = builderSignature ? `-builder-${builderSignature}` : "";
      const optionPart = optionSignature ? `-opts-${optionSignature}` : "";
      return `${menuItemId}-${sizeKey || "default"}-${sortedModifiers}${builderPart}${optionPart}`;
    },
    confirmAddToCart() {
      if (!this.addToCartModal || this.salesClosed) {
        return;
      }

      const validationError = this.builderValidationError();
      if (validationError) {
        this.addToCartModal.builderError = validationError;
        return;
      }

      const optionValidationError = this.optionsValidationError();
      if (optionValidationError) {
        this.addToCartModal.optionError = optionValidationError;
        return;
      }

      const { item, selectedSizeKey, selectedModifierIds, quantity } = this.addToCartModal;
      const selectedOptionIds = [...this.addToCartModal.selectedOptionIds];
      const optionLabels = this.modalOptionsLabels();
      const variant = item.variants?.length
        ? item.variants.find((entry) => entry.key === selectedSizeKey) || item.variants[0]
        : null;
      const basePrice = variant
        ? variant.specialOfferPrice || variant.price
        : item.specialOfferPrice || item.price;
      const componentsTotal = this.modalBuilderComponentsTotal();
      const optionsTotal = this.modalOptionsTotal();
      const unitPrice = Math.round((basePrice + componentsTotal + optionsTotal) * 100) / 100;
      const builderSummary = this.modalBuilderSummary();
      const customDrink = this.builderSelectionPayload();
      const builderSignature = this.builderSignature();
      const optionSignature = selectedOptionIds.length
        ? [...selectedOptionIds].sort().join("-")
        : "";
      const modifierLabels = this.modifiers
        .filter((modifier) => selectedModifierIds.includes(modifier.id))
        .map((modifier) => modifier.persianName);
      const cartKey = this.buildCartKey(
        item.id,
        variant?.key || null,
        selectedModifierIds,
        builderSignature,
        optionSignature
      );
      const existingLine = this.cart.find((line) => line.cartKey === cartKey);

      if (existingLine) {
        existingLine.quantity += quantity;
      } else {
        this.cart.push({
          cartKey,
          menuItemId: item.id,
          persianName: item.persianName || item.name,
          englishName: item.englishName,
          sizeKey: variant?.key || null,
          sizeLabel: variant?.label || null,
          unitPrice,
          quantity,
          modifierIds: [...selectedModifierIds],
          modifierLabels,
          optionIds: selectedOptionIds,
          optionLabels,
          customDrink,
          customDrinkSummary: builderSummary,
          builderSignature,
          payerKey: null,
          payerLabel: null
        });
      }

      this.saveCart();
      this.scheduleCartTracking();
      this.appliedDiscount = null;
      this.scheduleCartProgress();
      this.loadPickupSchedule();
      this.closeAddToCartModal();
      this.openCartDrawer("cart");
    },
    updateCartQuantity(cartKey, delta) {
      const line = this.cart.find((entry) => entry.cartKey === cartKey);
      if (!line) {
        return;
      }

      line.quantity += delta;
      if (line.quantity <= 0) {
        this.removeFromCart(cartKey);
        return;
      }

      this.saveCart();
      this.scheduleCartTracking();
      this.appliedDiscount = null;
      this.scheduleCartProgress();
      this.loadPickupSchedule();
    },
    removeFromCart(cartKey) {
      this.cart = this.cart.filter((line) => line.cartKey !== cartKey);
      this.saveCart();
      this.scheduleCartTracking();
      this.appliedDiscount = null;
      this.scheduleCartProgress();
      this.loadPickupSchedule();
    },
    clearCart() {
      this.cart = [];
      this.saveCart();
    },
    async submitCheckout(options = {}) {
      if (!this.cart.length) {
        return;
      }

      if (this.salesClosed) {
        this.checkoutError = this.salesNoticeText;
        return;
      }

      const acceptPriceChanges = options.acceptPriceChanges === true;

      this.checkoutLoading = true;
      this.checkoutError = "";
      this.appliedDiscount = null;
      this.checkoutConflict = null;
      this.actionError = "";

      if (!this.checkoutIdempotencyKey) {
        this.checkoutIdempotencyKey = this.generateIdempotencyKey();
      }

      try {
        if (this.checkoutForm.isSplitBill && this.checkoutForm.splitBillMode === "ByItem") {
          this.ensureCartPayerAssignments();
        }

        // Persist the contact details + cart snapshot before the order is
        // created, so an abandoned checkout can still be reminded about.
        await this.trackCart();

        const discountCode = this.checkoutForm.discountCode.trim();
        let discountAmount = 0;

        if (discountCode) {
          const previewResponse = await fetch(`${this.apiBaseUrl}/discounts/preview`, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({
              code: discountCode,
              deliveryFee: 0,
              channel: "online",
              isOnlinePayment: true,
              phoneNumber: this.checkoutForm.customerPhone.trim() || null,
              lines: this.cart.map((line) => ({
                menuItemId: line.menuItemId,
                categoryId: null,
                persianName: line.persianName,
                sizeKey: line.sizeKey,
                quantity: line.quantity,
                unitPrice: line.unitPrice,
                payerKey: line.payerKey || null
              }))
            })
          });

          const previewPayload = await previewResponse.json().catch(() => null);

          if (!previewResponse.ok) {
            throw new Error(previewPayload?.detail || previewPayload?.title || "این کد تخفیف معتبر نیست.");
          }

          const rejections = previewPayload?.rejections || [];
          if (rejections.length) {
            throw new Error(rejections[0]?.message || "این کد تخفیف معتبر نیست.");
          }

          discountAmount = Number(previewPayload?.discountTotal) || 0;

          if (discountAmount > 0) {
            this.appliedDiscount = { code: discountCode, amount: discountAmount };
          }
        }

        const splitShares =
          this.checkoutForm.isSplitBill &&
          this.checkoutForm.splitBillMode !== "ByItem"
            ? this.checkoutForm.splitShares.map((share) => ({
                payerKey: share.payerKey,
                payerLabel: share.payerLabel,
                sharePercentage:
                  this.checkoutForm.splitBillMode === "ByPercentage"
                    ? Number(share.sharePercentage)
                    : null,
                shareAmount:
                  this.checkoutForm.splitBillMode === "ByCustomAmount"
                    ? Number(share.shareAmount)
                    : null
              }))
            : null;

        const response = await fetch(`${this.apiBaseUrl}/orders/checkout`, {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({
            customerName: this.checkoutForm.customerName.trim(),
            customerPhone: this.checkoutForm.customerPhone.trim(),
            note: this.checkoutForm.note.trim() || null,
            discountCode: this.checkoutForm.discountCode.trim() || null,
            redeemPoints: Number(this.checkoutForm.redeemPoints) || 0,
            useWallet: this.checkoutForm.useWallet && this.walletPayable,
            personalCup: this.checkoutForm.personalCup && !!this.checkoutPersonalCup,
            useSubscription: this.checkoutForm.useSubscription && !!this.checkoutSubscription,
            deliveryAt: this.selectedDeliverySlot || null,
            isSplitBill: this.checkoutForm.isSplitBill,
            splitBillMode: this.checkoutForm.isSplitBill ? this.checkoutForm.splitBillMode : null,
            guestCount: this.checkoutForm.isSplitBill ? Number(this.checkoutForm.guestCount) : null,
            splitShares,
            idempotencyKey: this.checkoutIdempotencyKey,
            acceptPriceChanges,
            expectedTotalAmount: Math.round(this.cartTotal * 100) / 100,
            expectedLinePrices: this.cart.map((line) => ({
              clientLineKey: line.cartKey,
              menuItemId: line.menuItemId,
              sizeKey: line.sizeKey,
              unitPrice: line.unitPrice
            })),
            items: this.cart.map((line) => ({
              menuItemId: line.menuItemId,
              sizeKey: line.sizeKey,
              quantity: line.quantity,
              modifierIds: line.modifierIds || [],
              clientLineKey: line.cartKey,
              optionIds: line.optionIds || null,
              payerKey:
                this.checkoutForm.isSplitBill && this.checkoutForm.splitBillMode === "ByItem"
                  ? line.payerKey
                  : null,
              payerLabel:
                this.checkoutForm.isSplitBill && this.checkoutForm.splitBillMode === "ByItem"
                  ? line.payerLabel
                  : null,
              customDrink: line.customDrink || null
            }))
          })
        });

        if (!response.ok) {
          const payload = await response.json().catch(() => ({}));
          if (response.status === 409) {
            this.handleCheckoutConflict(payload);
            return;
          }
          throw new Error(payload.detail || payload.title || "ثبت سفارش با خطا مواجه شد.");
        }

        const result = await response.json();
        this.checkoutIdempotencyKey = null;
        this.checkoutConflict = null;
        this.acceptPriceChanges = false;

        // Wallet covered the whole order: no gateway step — redirect to the
        // success screen (?order=success&code=...) directly.
        if (result.requiresPayment === false) {
          this.clearCart();
          this.closeCartDrawer();
          window.location.href = result.redirectUrl;
          return;
        }

        // Carry the server-computed final amount and the applied discount into
        // the payment page. The redirect reloads the app, so in-memory state is
        // gone by then; sessionStorage keeps the two screens in sync.
        try {
          window.sessionStorage.setItem("sugarista_pending_payment", JSON.stringify({
            amount: Number(result.totalAmount) || this.finalAmount,
            code: this.appliedDiscount?.code || null,
            discountAmount: this.appliedDiscount?.amount || 0
          }));
        } catch {
          // Storage may be unavailable (private mode) — the payment page then
          // falls back to the in-memory values.
        }

        this.clearCart();
        this.closeCartDrawer();
        window.location.href = result.redirectUrl;
      } catch (error) {
        this.checkoutError = error.message || "ثبت سفارش با خطا مواجه شد.";
      } finally {
        this.checkoutLoading = false;
      }
    },
    handleCheckoutConflict(payload) {
      const code = payload?.code || "conflict";
      const message =
        payload?.detail || payload?.title || "ثبت سفارش نیازمند بررسی دوباره است.";

      if (code === "duplicate_order" && payload?.trackingCode) {
        // The same idempotency key already produced an order — send the
        // customer to its tracking screen instead of creating a second one.
        this.checkoutIdempotencyKey = null;
        this.clearCart();
        this.closeCartDrawer();
        window.location.href = `/?order=success&code=${encodeURIComponent(payload.trackingCode)}`;
        return;
      }

      this.checkoutConflict = {
        code,
        message,
        items: Array.isArray(payload?.items) ? payload.items : [],
        changes: Array.isArray(payload?.changes) ? payload.changes : [],
        nearestSlot: payload?.nearestSlot || null,
        oldItemsTotal: payload?.oldItemsTotal ?? null,
        newItemsTotal: payload?.newItemsTotal ?? null,
        newTotal: payload?.newTotal ?? null,
        trackingCode: payload?.trackingCode || null
      };
      this.checkoutError = message;

      if (
        ["slot_full", "slot_unavailable", "too_soon", "outside_hours", "closed_day", "invalid_slot", "beyond_max_days", "past_date", "scheduling_disabled"].includes(
          code
        )
      ) {
        this.loadPickupSchedule();
      }
    },
    dismissCheckoutConflict() {
      this.checkoutConflict = null;
      this.checkoutError = "";
    },
    async confirmPriceChanges() {
      if (!this.checkoutConflict?.changes?.length && this.checkoutConflict?.newItemsTotal == null) {
        return;
      }

      // Apply the new prices to the visible cart, then re-submit with the
      // explicit confirmation flag. The customer always sees the new total.
      for (const change of this.checkoutConflict.changes || []) {
        const line = this.cart.find((entry) => entry.cartKey === change.clientLineKey);
        if (line && Number(change.newUnitPrice) >= 0) {
          line.unitPrice = Number(change.newUnitPrice);
        }
      }

      this.checkoutConflict = null;
      this.checkoutError = "";
      await this.submitCheckout({ acceptPriceChanges: true });
    },
    removeUnavailableConflictItems() {
      const unavailableIds = new Set(
        (this.checkoutConflict?.items || []).map((item) => item.menuItemId)
      );
      if (!unavailableIds.size) {
        return;
      }

      this.cart = this.cart.filter((line) => !unavailableIds.has(line.menuItemId));
      this.saveCart();
      this.checkoutConflict = null;
      this.checkoutError = "";
      if (!this.cart.length) {
        this.cartView = "cart";
      }
      this.loadPickupSchedule();
    },
    async applyNearestPickupSlot() {
      const nearest = this.checkoutConflict?.nearestSlot;
      if (!nearest) {
        return;
      }

      await this.loadPickupSchedule();
      const day = (this.pickupSchedule?.days || []).find((entry) =>
        (entry.slots || []).some((slot) => slot.deliveryAt === nearest)
      );
      const slot = day?.slots?.find((entry) => entry.deliveryAt === nearest);
      if (!day || !slot) {
        this.checkoutError = "بازهٔ جایگزین دیگر موجود نیست؛ لطفاً زمان دیگری انتخاب کنید.";
        return;
      }

      this.selectedPickupDate = day.date;
      this.selectedDeliverySlot = slot.deliveryAt;
      this.pickupSelectionActive = true;
      this.checkoutConflict = null;
      this.checkoutError = "";
      await this.submitCheckout();
    },

    /* ── order status / cancel / reschedule (TASK-012) ────────────── */
    startOrderStatusPolling(trackingCode) {
      this.stopOrderStatusPolling();
      this.loadOrderStatus(trackingCode);
      this.orderStatusTimer = window.setInterval(
        () => this.loadOrderStatus(trackingCode),
        30000
      );
    },
    stopOrderStatusPolling() {
      if (this.orderStatusTimer) {
        window.clearInterval(this.orderStatusTimer);
        this.orderStatusTimer = null;
      }
    },
    async loadOrderStatus(trackingCode) {
      if (!trackingCode) {
        return;
      }
      this.orderStatusLoading = true;
      try {
        const response = await fetch(
          `${this.apiBaseUrl}/orders/status/${encodeURIComponent(trackingCode)}`
        );
        if (response.ok) {
          this.orderStatus = await response.json();
        }
      } catch {
        // Polling is best-effort; the success screen keeps the tracking code.
      } finally {
        this.orderStatusLoading = false;
      }
    },
    openCancelConfirm() {
      if (!this.orderStatus?.canCancel) {
        return;
      }
      this.actionError = "";
      this.actionMessage = "";
      this.cancelConfirmOpen = true;
    },
    closeCancelConfirm() {
      this.cancelConfirmOpen = false;
    },
    async confirmCancelScheduledOrder() {
      if (!this.orderStatus?.canCancel) {
        return;
      }

      this.cancelConfirmOpen = false;
      this.actionError = "";
      this.actionMessage = "";
      try {
        const response = await fetch(
          `${this.apiBaseUrl}/orders/${this.orderStatus.id}/cancel`,
          {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ trackingCode: this.orderStatus.trackingCode })
          }
        );
        const payload = await response.json().catch(() => ({}));
        if (!response.ok) {
          throw new Error(payload.detail || payload.title || "لغو سفارش انجام نشد.");
        }

        this.actionMessage = payload.message || "سفارش لغو شد.";
        await this.loadOrderStatus(this.orderStatus.trackingCode);
        await this.loadPickupSchedule();
      } catch (error) {
        this.actionError = error.message || "لغو سفارش انجام نشد.";
      }
    },
    async openReschedule() {
      if (!this.orderStatus?.canReschedule) {
        return;
      }
      this.rescheduleOpen = true;
      this.cancelConfirmOpen = false;
      this.rescheduleError = "";
      this.actionError = "";
      this.rescheduleSlot = null;
      await this.loadPickupSchedule();
      if (!this.rescheduleDate) {
        const day = (this.pickupSchedule?.days || []).find(
          (entry) => entry.isOpen && (entry.slots || []).some((slot) => !slot.isFull)
        );
        this.rescheduleDate = day?.date || null;
      }
    },
    get rescheduleDay() {
      if (!this.pickupSchedule?.days || !this.rescheduleDate) {
        return null;
      }
      return this.pickupSchedule.days.find((day) => day.date === this.rescheduleDate) || null;
    },
    selectRescheduleDay(day) {
      if (!day || !day.isOpen) {
        return;
      }
      this.rescheduleDate = day.date;
      this.rescheduleSlot = null;
    },
    selectRescheduleSlot(slot) {
      if (!slot || slot.isFull) {
        return;
      }
      this.rescheduleSlot = slot.deliveryAt;
    },
    async submitReschedule() {
      if (!this.rescheduleSlot || !this.orderStatus?.id) {
        this.rescheduleError = "لطفاً یک بازهٔ جدید انتخاب کنید.";
        return;
      }

      this.rescheduleSaving = true;
      this.rescheduleError = "";
      this.actionError = "";
      try {
        const response = await fetch(
          `${this.apiBaseUrl}/orders/${this.orderStatus.id}/reschedule`,
          {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({
              trackingCode: this.orderStatus.trackingCode,
              deliveryAt: this.rescheduleSlot
            })
          }
        );
        const payload = await response.json().catch(() => ({}));
        if (!response.ok) {
          throw new Error(payload.detail || payload.title || "تغییر زمان انجام نشد.");
        }

        this.actionMessage = payload.message || "زمان تحویل تغییر کرد.";
        this.rescheduleOpen = false;
        this.rescheduleSlot = null;
        await this.loadOrderStatus(this.orderStatus.trackingCode);
        await this.loadPickupSchedule();
      } catch (error) {
        this.rescheduleError = error.message || "تغییر زمان انجام نشد.";
      } finally {
        this.rescheduleSaving = false;
      }
    },
    async confirmMockPayment() {
      if (this.splitMockPayment) {
        await this.confirmSplitMockPayment();
        return;
      }

      if (!this.mockPayment) {
        return;
      }

      this.paymentLoading = true;

      try {
        const response = await fetch(
          `${this.apiBaseUrl}/orders/${this.mockPayment.orderId}/confirm-mock`,
          {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ authority: this.mockPayment.authority })
          }
        );

        if (!response.ok) {
          const payload = await response.json().catch(() => ({}));
          throw new Error(payload.title || payload.detail || "تأیید پرداخت با خطا مواجه شد.");
        }

        const result = await response.json();
        const successDiscount = this.pendingPayment?.discountAmount > 0
          ? { code: this.pendingPayment.code, amount: this.pendingPayment.discountAmount }
          : this.appliedDiscount;
        const successDiscountQuery = successDiscount?.amount > 0
          ? `&discount=${encodeURIComponent(successDiscount.code)}&discountAmount=${successDiscount.amount}`
          : "";
        window.location.href = `/?order=success&code=${encodeURIComponent(result.trackingCode)}${successDiscountQuery}`;
      } catch (error) {
        this.checkoutError = error.message || "تأیید پرداخت با خطا مواجه شد.";
      } finally {
        this.paymentLoading = false;
      }
    },
    cancelMockPayment() {
      if (this.splitMockPayment) {
        window.location.href = `/?split=${this.splitMockPayment.orderId}`;
        return;
      }

      window.location.href = "/";
    },
    async initiateSplitPayment(payerKey) {
      if (!this.splitOrder) {
        return;
      }

      this.checkoutError = "";
      this.checkoutLoading = true;

      try {
        const response = await fetch(
          `${this.apiBaseUrl}/orders/${this.splitOrder.id}/split-payment`,
          {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ payerKey })
          }
        );

        if (!response.ok) {
          const payload = await response.json().catch(() => ({}));
          throw new Error(payload.detail || payload.title || "شروع پرداخت با خطا مواجه شد.");
        }

        const result = await response.json();
        window.location.href = result.redirectUrl;
      } catch (error) {
        this.checkoutError = error.message || "شروع پرداخت با خطا مواجه شد.";
      } finally {
        this.checkoutLoading = false;
      }
    },
    async confirmSplitMockPayment() {
      if (!this.splitMockPayment) {
        return;
      }

      this.paymentLoading = true;

      try {
        const response = await fetch(
          `${this.apiBaseUrl}/orders/${this.splitMockPayment.orderId}/split-payment/${this.splitMockPayment.paymentId}/confirm-mock`,
          {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ authority: this.splitMockPayment.authority })
          }
        );

        if (!response.ok) {
          const payload = await response.json().catch(() => ({}));
          throw new Error(payload.detail || payload.title || "تأیید پرداخت با خطا مواجه شد.");
        }

        const result = await response.json();
        if (result.isFullyPaid) {
          const successDiscount = this.pendingPayment?.discountAmount > 0
            ? { code: this.pendingPayment.code, amount: this.pendingPayment.discountAmount }
            : this.appliedDiscount;
          const successDiscountQuery = successDiscount?.amount > 0
            ? `&discount=${encodeURIComponent(successDiscount.code)}&discountAmount=${successDiscount.amount}`
            : "";
          window.location.href = `/?order=success&code=${encodeURIComponent(result.trackingCode)}${successDiscountQuery}`;
          return;
        }

        window.location.href = `/?split=${this.splitMockPayment.orderId}`;
      } catch (error) {
        this.checkoutError = error.message || "تأیید پرداخت با خطا مواجه شد.";
      } finally {
        this.paymentLoading = false;
      }
    },
    dismissOrderSuccess() {
      this.stopOrderStatusPolling();
      this.orderSuccess = null;
      this.orderStatus = null;
      this.actionMessage = "";
      this.actionError = "";
      this.cancelConfirmOpen = false;
      this.rescheduleOpen = false;
      this.rescheduleSlot = null;
      window.history.replaceState({}, "", "/");
    },
    normalizeText(value) {
      return value
        .toString()
        .toLowerCase()
        .replaceAll("ي", "ی")
        .replaceAll("ك", "ک")
        .trim();
    },
    formatPrice(value) {
      return `${new Intl.NumberFormat("fa-IR", {
        minimumFractionDigits: 0,
        maximumFractionDigits: 2
      }).format(Number(value) || 0)} تومان`;
    },
    formatPriceCompact(value) {
      return new Intl.NumberFormat("fa-IR", {
        minimumFractionDigits: 0,
        maximumFractionDigits: 0
      }).format(Number(value) || 0);
    },
    walletMoveLabel(type) {
      const labels = {
        TopUp: "شارژ",
        Cashback: "کش‌بک",
        Purchase: "پرداخت سفارش",
        Refund: "برگشت وجه",
        Adjustment: "اصلاح"
      };
      return labels[type] || type;
    },
    formatShamsi(value) {
      if (!value) {
        return "";
      }
      try {
        return new Date(value).toLocaleDateString("fa-IR", {
          year: "numeric", month: "short", day: "numeric"
        });
      } catch {
        return "";
      }
    },
    displayPrice(item) {
      const candidates = [item.specialOfferPrice, item.discountedPrice].filter((v) => v != null);
      return candidates.length ? Math.min(...candidates) : item.price;
    },
    hasOffer(item) {
      return (
        (item.specialOfferPrice && item.specialOfferPrice < item.price) ||
        (item.discountedPrice && item.discountedPrice < item.price)
      );
    },
    discountPercent(item) {
      return item.discountPercent || null;
    },
    variantDisplayPrice(variant) {
      const candidates = [variant.specialOfferPrice, variant.discountedPrice].filter((v) => v != null);
      return candidates.length ? Math.min(...candidates) : variant.price;
    },
    variantHasOffer(variant) {
      return (
        (variant.specialOfferPrice && variant.specialOfferPrice < variant.price) ||
        (variant.discountedPrice && variant.discountedPrice < variant.price)
      );
    },
    itemMeta(item) {
      return [
        item.preparingDurationMinutes ? `${this.toPersianNumber(item.preparingDurationMinutes)} دقیقه آماده‌سازی` : "",
        item.calories ? `${this.toPersianNumber(item.calories)} کالری` : "",
        item.volume || "",
        item.taxPercent ? `مالیات ${this.toPersianNumber(item.taxPercent)}٪` : ""
      ].filter(Boolean);
    },
    toPersianNumber(value) {
      return new Intl.NumberFormat("fa-IR").format(value);
    },
    callHref() {
      return `tel:${this.settings.contactPhone}`;
    },
    instagramHref() {
      return this.settings.instagramUrl || "#";
    }
  }));
});
