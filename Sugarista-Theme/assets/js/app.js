document.addEventListener("alpine:init", () => {
  const CART_STORAGE_KEY = "sugarista_cart";

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
    cart: [],
    cartDrawerOpen: false,
    cartView: "cart",
    checkoutForm: {
      customerName: "",
      customerPhone: "",
      note: ""
    },
    checkoutLoading: false,
    checkoutError: "",
    addToCartModal: null,
    mockPayment: null,
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
      heroHours: "08:00 - 23:00",
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
      contactPhone: "+980000000000",
      address: "",
      breakfastHours: "",
      takeawayStatus: "",
      instagramUrl: "https://instagram.com/",
      footerText: "Sugarista Cafe Digital Menu Theme",
      footerPopularLabel: "پرفروش‌ها",
      footerMenuLabel: "منو",
      footerCallLabel: "تماس"
    },
    async init() {
      this.loadCart();
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
        this.settings = { ...this.settings, ...settingsData };
        this.applyPageMetadata();
      } catch (error) {
        this.error = error.message || "دریافت اطلاعات با خطا مواجه شد.";
      } finally {
        this.loading = false;
      }
    },
    handleRouteState() {
      const params = new URLSearchParams(window.location.search);

      const successCode = params.get("code");
      if (params.get("order") === "success" && successCode) {
        this.orderSuccess = { trackingCode: successCode };
        return;
      }

      const payOrderId = params.get("pay");
      const authority = params.get("authority");
      if (payOrderId && authority) {
        this.mockPayment = {
          orderId: payOrderId,
          authority
        };
      }
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
    get cartCount() {
      return this.cart.reduce((total, line) => total + line.quantity, 0);
    },
    get cartTotal() {
      return this.cart.reduce((total, line) => total + line.unitPrice * line.quantity, 0);
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
    },
    resetFilters() {
      this.search = "";
      this.selectedCategory = "all";
    },
    openCartDrawer(view = "cart") {
      this.cartView = view;
      this.cartDrawerOpen = true;
      this.checkoutError = "";
    },
    closeCartDrawer() {
      this.cartDrawerOpen = false;
      this.cartView = "cart";
      this.checkoutError = "";
    },
    openAddToCartModal(item) {
      if (!item.isOrderable) {
        return;
      }

      const defaultVariant = this.defaultVariantForItem(item);
      this.addToCartModal = {
        item,
        selectedSizeKey: defaultVariant?.key || null,
        selectedModifierIds: [],
        quantity: 1
      };
    },
    closeAddToCartModal() {
      this.addToCartModal = null;
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
      const variant = this.modalSelectedVariant();
      if (variant) {
        return variant.specialOfferPrice || variant.price || 0;
      }

      const item = this.addToCartModal?.item;
      if (!item) {
        return 0;
      }

      return item.specialOfferPrice || item.price || 0;
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
    buildCartKey(menuItemId, sizeKey, modifierIds) {
      const sortedModifiers = [...modifierIds].sort().join("-");
      return `${menuItemId}-${sizeKey || "default"}-${sortedModifiers}`;
    },
    confirmAddToCart() {
      if (!this.addToCartModal) {
        return;
      }

      const { item, selectedSizeKey, selectedModifierIds, quantity } = this.addToCartModal;
      const variant = item.variants?.length
        ? item.variants.find((entry) => entry.key === selectedSizeKey) || item.variants[0]
        : null;
      const unitPrice = variant
        ? variant.specialOfferPrice || variant.price
        : item.specialOfferPrice || item.price;
      const modifierLabels = this.modifiers
        .filter((modifier) => selectedModifierIds.includes(modifier.id))
        .map((modifier) => modifier.persianName);
      const cartKey = this.buildCartKey(item.id, variant?.key || null, selectedModifierIds);
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
          modifierLabels
        });
      }

      this.saveCart();
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
    },
    removeFromCart(cartKey) {
      this.cart = this.cart.filter((line) => line.cartKey !== cartKey);
      this.saveCart();
    },
    clearCart() {
      this.cart = [];
      this.saveCart();
    },
    async submitCheckout() {
      if (!this.cart.length) {
        return;
      }

      this.checkoutLoading = true;
      this.checkoutError = "";

      try {
        const response = await fetch(`${this.apiBaseUrl}/orders/checkout`, {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({
            customerName: this.checkoutForm.customerName.trim(),
            customerPhone: this.checkoutForm.customerPhone.trim(),
            note: this.checkoutForm.note.trim() || null,
            items: this.cart.map((line) => ({
              menuItemId: line.menuItemId,
              sizeKey: line.sizeKey,
              quantity: line.quantity,
              modifierIds: line.modifierIds || []
            }))
          })
        });

        if (!response.ok) {
          const payload = await response.json().catch(() => ({}));
          throw new Error(payload.title || payload.detail || "ثبت سفارش با خطا مواجه شد.");
        }

        const result = await response.json();
        this.clearCart();
        this.closeCartDrawer();
        window.location.href = result.redirectUrl;
      } catch (error) {
        this.checkoutError = error.message || "ثبت سفارش با خطا مواجه شد.";
      } finally {
        this.checkoutLoading = false;
      }
    },
    async confirmMockPayment() {
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
        window.location.href = `/?order=success&code=${encodeURIComponent(result.trackingCode)}`;
      } catch (error) {
        this.checkoutError = error.message || "تأیید پرداخت با خطا مواجه شد.";
      } finally {
        this.paymentLoading = false;
      }
    },
    cancelMockPayment() {
      window.location.href = "/";
    },
    dismissOrderSuccess() {
      this.orderSuccess = null;
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
      return `${new Intl.NumberFormat("fa-IR").format(value || 0)} تومان`;
    },
    displayPrice(item) {
      return item.specialOfferPrice || item.price;
    },
    hasOffer(item) {
      return Boolean(item.specialOfferPrice && item.specialOfferPrice < item.price);
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
