document.addEventListener("alpine:init", () => {
  Alpine.data("cafeMenu", () => ({
    apiBaseUrl: window.SUGARISTA_API_BASE_URL || "/api",
    loading: true,
    error: "",
    search: "",
    selectedCategory: "all",
    categories: [],
    items: [],
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
        this.settings = { ...this.settings, ...settingsData };
        this.applyPageMetadata();
      } catch (error) {
        this.error = error.message || "دریافت اطلاعات با خطا مواجه شد.";
      } finally {
        this.loading = false;
      }
    },
    applyPageMetadata() {
      document.title = this.settings.browserTitle;

      const descriptionTag = document.querySelector('meta[name="description"]');
      if (descriptionTag) {
        descriptionTag.setAttribute("content", this.settings.metaDescription);
      }
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
