function affiliatePortalApp() {
  const apiBase = (window.SUGARISTA_API_BASE_URL || '/api').replace(/\/+$/, '');

  return {
    loading: true,
    dashboard: null,
    activeTab: 'overview',
    chartRange: 7,
    mediaFilter: 'all',

    // OTP Login state
    otpStep: 'phone', // 'phone' | 'code'
    otpPhone: '',
    otpCode: '',
    otpName: '',
    otpBusy: false,

    // Apply form state
    applySheba: '',
    applyCard: '',
    applySlug: '',
    applyBusy: false,

    // Link builder state
    newSubId: '',
    newTargetUrl: '/',
    linkBusy: false,

    // Finance state
    convertAmount: '',
    convertBusy: false,
    payoutAmount: '',
    payoutBusy: false,

    // Toast feedback
    toastMessage: '',
    toastIsError: false,
    toastTimer: null,

    async init() {
      await this.loadDashboard();
    },

    notify(message, isError = false) {
      this.toastMessage = message;
      this.toastIsError = isError;
      if (this.toastTimer) clearTimeout(this.toastTimer);
      this.toastTimer = setTimeout(() => {
        this.toastMessage = '';
      }, 3600);
    },

    formatNum(val) {
      const num = Number(val) || 0;
      return new Intl.NumberFormat('fa-IR').format(Math.round(num));
    },

    async api(path, options = {}) {
      const res = await fetch(`${apiBase}${path}`, {
        credentials: 'include',
        headers: {
          'Content-Type': 'application/json',
          ...(options.headers || {}),
        },
        ...options,
      });

      const text = await res.text();
      let json = null;
      try {
        json = text ? JSON.parse(text) : null;
      } catch {
        json = null;
      }

      if (!res.ok) {
        const msg = json?.detail || json?.title || json?.error || json?.message || text || 'خطای ارتباط با سرور';
        throw new Error(msg);
      }

      return json;
    },

    async loadDashboard() {
      this.loading = true;
      try {
        this.dashboard = await this.api('/affiliate-portal/me');
      } catch (err) {
        this.notify(err.message, true);
      } finally {
        this.loading = false;
      }
    },

    async requestOtp() {
      const phone = this.otpPhone.trim();
      if (!phone) {
        this.notify('لطفاً شماره موبایل خود را وارد کنید.', true);
        return;
      }
      this.otpBusy = true;
      try {
        await this.api('/customers/auth/otp/request', {
          method: 'POST',
          body: JSON.stringify({
            phoneNumber: phone,
            persianName: this.otpName.trim() || undefined,
          }),
        });
        this.otpStep = 'code';
        this.notify('کد تایید پیامکی ارسال شد.');
      } catch (err) {
        this.notify(err.message, true);
      } finally {
        this.otpBusy = false;
      }
    },

    async verifyOtp() {
      if (!this.otpCode.trim()) {
        this.notify('کد تایید را وارد کنید.', true);
        return;
      }
      this.otpBusy = true;
      try {
        await this.api('/customers/auth/otp/verify', {
          method: 'POST',
          body: JSON.stringify({
            phoneNumber: this.otpPhone.trim(),
            code: this.otpCode.trim(),
            persianName: this.otpName.trim() || undefined,
          }),
        });
        this.notify('احراز هویت با موفقیت انجام شد.');
        await this.loadDashboard();
      } catch (err) {
        this.notify(err.message, true);
      } finally {
        this.otpBusy = false;
      }
    },

    async submitApplication() {
      if (!this.applySheba.trim()) {
        this.notify('شماره شبا به نام خودتان الزامی است.', true);
        return;
      }
      this.applyBusy = true;
      try {
        this.dashboard = await this.api('/affiliate-portal/apply', {
          method: 'POST',
          body: JSON.stringify({
            shebaNumber: this.applySheba.trim(),
            cardNumber: this.applyCard.trim() || null,
            preferredSlug: this.applySlug.trim() || null,
          }),
        });
        this.notify('درخواست همکاری در فروش با موفقیت ثبت شد.');
      } catch (err) {
        this.notify(err.message, true);
      } finally {
        this.applyBusy = false;
      }
    },

    async createCampaignLink() {
      if (!this.newSubId.trim()) {
        this.notify('شناسه کمپین (Sub-ID) را وارد کنید.', true);
        return;
      }
      this.linkBusy = true;
      try {
        await this.api('/affiliate-portal/links', {
          method: 'POST',
          body: JSON.stringify({
            subId: this.newSubId.trim(),
            targetUrl: this.newTargetUrl.trim() || '/',
          }),
        });
        this.newSubId = '';
        this.notify('لینک کمپین جدید ساخته شد.');
        await this.loadDashboard();
      } catch (err) {
        this.notify(err.message, true);
      } finally {
        this.linkBusy = false;
      }
    },

    async copyText(text, label = 'کپی شد') {
      try {
        await navigator.clipboard.writeText(text);
        this.notify(label);
      } catch {
        this.notify('امکان کپی خودکار نبود؛ لطفاً دستی کپی کنید.', true);
      }
    },

    get visibleChartStats() {
      const all = this.dashboard?.dailyStats30d || [];
      return this.chartRange === 7 ? all.slice(-7) : all;
    },

    get maxChartSales() {
      const stats = this.visibleChartStats;
      return Math.max(1, ...stats.map((s) => Number(s.salesAmount) || 0));
    },

    get filteredMediaKit() {
      const list = this.dashboard?.mediaKit || [];
      if (this.mediaFilter === 'all') return list;
      return list.filter((item) => String(item.assetType) === this.mediaFilter);
    },

    get convertPreview() {
      const amt = Number(this.convertAmount) || 0;
      const bonusPct = Number(this.dashboard?.creditBonusPercentage) || 20;
      const bonus = Math.floor((amt * bonusPct) / 100);
      return {
        amount: amt,
        bonusPct,
        bonus,
        totalCredit: amt + bonus,
      };
    },

    get canRequestCashPayout() {
      const cash = Number(this.dashboard?.cashBalance) || 0;
      const min = Number(this.dashboard?.minPayoutAmount) || 500000;
      return cash >= min;
    },

    async submitConvertToCredit() {
      const amt = Number(this.convertAmount) || 0;
      if (amt <= 0) {
        this.notify('مبلغ معتبر وارد کنید.', true);
        return;
      }
      this.convertBusy = true;
      try {
        const res = await this.api('/affiliate-portal/convert-to-credit', {
          method: 'POST',
          body: JSON.stringify({ amount: amt }),
        });
        this.convertAmount = '';
        this.notify(`${this.formatNum(res.totalCreditGranted)} تومان اعتبار خرید به کیف پول کافه شما اضافه شد!`);
        await this.loadDashboard();
      } catch (err) {
        this.notify(err.message, true);
      } finally {
        this.convertBusy = false;
      }
    },

    async submitPayoutRequest() {
      const amt = Number(this.payoutAmount) || Number(this.dashboard?.cashBalance) || 0;
      if (!this.canRequestCashPayout || amt < (this.dashboard?.minPayoutAmount || 500000)) {
        this.notify('موجودی نقدی به حداقل سقف برداشت نرسیده است.', true);
        return;
      }
      this.payoutBusy = true;
      try {
        await this.api('/affiliate-portal/payout-request', {
          method: 'POST',
          body: JSON.stringify({ amount: amt }),
        });
        this.payoutAmount = '';
        this.notify('درخواست واریز وجه با موفقیت ثبت شد.');
        await this.loadDashboard();
      } catch (err) {
        this.notify(err.message, true);
      } finally {
        this.payoutBusy = false;
      }
    },

    payoutStatusLabel(status) {
      const map = {
        Requested: 'ثبت‌شده',
        Processing: 'در حال واریز',
        Completed: 'واریز شده',
        Rejected: 'رد شده',
      };
      return map[status] || status;
    },

    mediaTypeLabel(type) {
      const map = {
        Image: 'تصویر',
        Video: 'ویدیو ریلز',
        Logo: 'لوگو',
        CopyText: 'متن تبلیغاتی',
      };
      return map[type] || type;
    },
  };
}
