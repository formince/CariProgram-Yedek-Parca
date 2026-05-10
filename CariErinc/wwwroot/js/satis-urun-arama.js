/**
 * Satış ekranları — barkod / metin arama ve ürün seçim modalı (Hızlı Satış + Satış Formu).
 */
(function (global) {
    'use strict';

    var inited = false;

    function escapeHtml(text) {
        if (text == null) return '';
        var div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    function showToast(msg, type) {
        if (typeof siteJs !== 'undefined' && siteJs.showToast) {
            siteJs.showToast(msg, type || 'info');
        } else {
            alert(msg);
        }
    }

    function paraFmt(n) {
        if (typeof n !== 'number' || isNaN(n)) n = 0;
        if (window.CariErincPara && typeof window.CariErincPara.format === 'function') {
            return window.CariErincPara.format(n);
        }
        return n.toLocaleString('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    }

    function urunListedeGosterFiyat(u) {
        var raw = u.birimFiyat;
        return window.CariErincPara && typeof window.CariErincPara.fromApi === 'function'
            ? window.CariErincPara.fromApi(raw)
            : (parseFloat(raw) || 0);
    }

    function urunListedeGosterStok(u) {
        var s = u.stokAdedi != null ? u.stokAdedi : u.StokAdedi;
        return s;
    }

    function normalizeApiUrun(urun) {
        if (!urun) return null;
        var id = urun.id;
        if (id == null) return null;
        return urun;
    }

    /**
     * @param {{ barkodAra: string, urunAraMetin: string }} opts.urls
     * @param {number} [opts.urunAraMetinLimitBarkodSonrasi]
     * @param {{ barkodInput: HTMLElement, urunAraModal: HTMLElement, urunAraInput: HTMLElement, urunAraSonuc: HTMLElement, urunAraYardim?: HTMLElement|null }} opts.elements
     * @param {(urun: object) => void} opts.onProductPicked
     */
    function init(opts) {
        if (inited) return;
        inited = true;

        if (!opts || !opts.urls || !opts.onProductPicked) return;

        var urls = opts.urls;
        if (!urls.barkodAra || !urls.urunAraMetin) return;

        var limit = typeof opts.urunAraMetinLimitBarkodSonrasi === 'number'
            ? opts.urunAraMetinLimitBarkodSonrasi
            : 50;

        var els = opts.elements || {};
        var barkodInput = els.barkodInput;
        var urunAraModal = els.urunAraModal;
        var urunAraInput = els.urunAraInput;
        var urunAraSonuc = els.urunAraSonuc;
        var urunAraYardim = els.urunAraYardim || null;
        var onProductPicked = opts.onProductPicked;

        if (!barkodInput || !urunAraModal || !urunAraInput || !urunAraSonuc) return;

        var urunAraTimer = null;
        var urunModalPrefillQuery = null;
        var urunModalPrefillItems = null;
        var urunModalPrefillYardim = null;
        var varsayilanUrunAraYardim = urunAraYardim ? urunAraYardim.textContent : '';

        function urunAraSonuclariGoster(items) {
            urunAraSonuc.innerHTML = '';
            if (!items || !items.length) {
                urunAraSonuc.innerHTML = '<div class="list-group-item text-muted">Sonuç yok</div>';
                return;
            }
            items.forEach(function (u) {
                var a = document.createElement('button');
                a.type = 'button';
                a.className = 'list-group-item list-group-item-action d-flex justify-content-between align-items-start text-start';
                var stok = urunListedeGosterStok(u);
                var stokUyari = (stok != null && stok <= 0) ? ' <span class="badge bg-warning text-dark">Stok: 0</span>' : '';
                a.innerHTML = '<div><strong>' + escapeHtml(u.ad || '') + '</strong><br/><small class="text-muted">' + escapeHtml(u.barkod || '-') + '</small>' + stokUyari + '</div>' +
                    '<span class="ms-2">' + paraFmt(urunListedeGosterFiyat(u)) + ' ₺</span>';
                a.addEventListener('click', function () {
                    onProductPicked(u);
                    var inst = bootstrap.Modal.getInstance(urunAraModal);
                    if (inst) inst.hide();
                    urunAraInput.value = '';
                    urunAraSonuc.innerHTML = '';
                    if (urunAraYardim) urunAraYardim.textContent = varsayilanUrunAraYardim;
                    barkodInput.focus();
                });
                urunAraSonuc.appendChild(a);
            });
        }

        function getUrunModal() {
            return bootstrap.Modal.getInstance(urunAraModal) || new bootstrap.Modal(urunAraModal);
        }

        function acUrunSecimModalindanBarkod(aramaMetni, sonuclar) {
            urunModalPrefillQuery = aramaMetni;
            urunModalPrefillItems = sonuclar;
            urunModalPrefillYardim = 'Aramaya uyan ürünler aşağıda. Sepete eklemek için birini seçin.';
            getUrunModal().show();
        }

        urunAraInput.addEventListener('input', function () {
            clearTimeout(urunAraTimer);
            var q = urunAraInput.value.trim();
            if (q.length < 2) { urunAraSonuc.innerHTML = ''; return; }
            urunAraTimer = setTimeout(async function () {
                try {
                    var res = await fetch(urls.urunAraMetin + '?q=' + encodeURIComponent(q));
                    if (!res.ok) { urunAraSonuclariGoster([]); return; }
                    var data = await res.json();
                    urunAraSonuclariGoster(Array.isArray(data) ? data : []);
                } catch (e) { urunAraSonuclariGoster([]); }
            }, 300);
        });

        urunAraModal.addEventListener('shown.bs.modal', function () {
            if (urunModalPrefillQuery != null) {
                urunAraInput.value = urunModalPrefillQuery;
                urunAraSonuclariGoster(urunModalPrefillItems || []);
                if (urunAraYardim && urunModalPrefillYardim) urunAraYardim.textContent = urunModalPrefillYardim;
                urunModalPrefillQuery = null;
                urunModalPrefillItems = null;
                urunModalPrefillYardim = null;
                urunAraInput.focus();
                return;
            }
            if (urunAraYardim) urunAraYardim.textContent = varsayilanUrunAraYardim;
            urunAraInput.focus();
            urunAraInput.value = '';
            urunAraSonuc.innerHTML = '';
        });

        barkodInput.addEventListener('keydown', async function (e) {
            if (e.key !== 'Enter') return;
            e.preventDefault();
            var barkod = barkodInput.value.trim();
            if (!barkod) return;
            try {
                var res = await fetch(urls.barkodAra + '?barkod=' + encodeURIComponent(barkod));
                var urun = (res.ok) ? await res.json() : null;
                if (normalizeApiUrun(urun)) {
                    onProductPicked(urun);
                } else {
                    var resAra = await fetch(urls.urunAraMetin + '?q=' + encodeURIComponent(barkod) + '&limit=' + limit);
                    var dataAra = (resAra.ok) ? await resAra.json() : [];
                    if (!Array.isArray(dataAra) || dataAra.length === 0) {
                        showToast('Ürün bulunamadı', 'danger');
                    } else if (dataAra.length === 1 && normalizeApiUrun(dataAra[0])) {
                        onProductPicked(dataAra[0]);
                    } else {
                        acUrunSecimModalindanBarkod(barkod, dataAra);
                    }
                }
            } catch (err) {
                showToast('Bir hata oluştu', 'danger');
            }
            barkodInput.value = '';
            barkodInput.focus();
        });
    }

    global.CariErincSatisUrunArama = { init: init };
})(window);
