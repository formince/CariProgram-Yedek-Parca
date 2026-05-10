/**
 * Alış fişi satırları — barkod okuma üstteki tek input üzerinden yapılır.
 * window.CariErincAlisCreate: { urls: { barkodAra, urunAraMetin }, urunAraMetinLimitBarkodSonrasi? }
 */
(function () {
    'use strict';

    document.addEventListener('DOMContentLoaded', function () {
        var cfg = window.CariErincAlisCreate;
        if (!cfg || !cfg.urls) return;

        var urls = cfg.urls;
        var limitBarkod = cfg.urunAraMetinLimitBarkodSonrasi || 50;

        var urunAraModal = document.getElementById('alisUrunAraModal');
        var urunAraInput = document.getElementById('alisUrunAraInput');
        var urunAraSonuc = document.getElementById('alisUrunAraSonuc');
        var urunAraYardim = document.getElementById('alisUrunAraYardim');
        var urunAraTimer = null;
        var varsayilanUrunAraYardim = urunAraYardim ? urunAraYardim.textContent : '';

        var urunModalPrefillQuery = null;
        var urunModalPrefillItems = null;
        var urunModalPrefillYardim = null;

        /** Barkod / modal seçiminin uygulanacağı satır */
        var activeRow = null;

        var satirlarBody = document.getElementById('satirlarBody');
        if (!satirlarBody) return;

        // ── Yardımcılar ──────────────────────────────────────────────────────────

        function showToast(msg, type) {
            if (typeof siteJs !== 'undefined' && siteJs.showToast) {
                siteJs.showToast(msg, type || 'info');
            } else {
                alert(msg);
            }
        }

        function escapeHtml(text) {
            if (text == null) return '';
            var div = document.createElement('div');
            div.textContent = text;
            return div.innerHTML;
        }

        function apiNum(v) {
            if (window.CariErincPara && typeof window.CariErincPara.fromApi === 'function')
                return window.CariErincPara.fromApi(v);
            var x = parseFloat(v);
            return isNaN(x) ? 0 : x;
        }

        function parseAlisFiyat(u) {
            return apiNum(u.alisFiyati);
        }

        function parseBirimFiyat(u) {
            return apiNum(u.birimFiyat);
        }

        function parseKdvOrani(u) {
            var v = parseInt(u.kdvOrani, 10);
            return isNaN(v) ? 0 : v;
        }

        function varsayilanAlisBirimFiyat(u) {
            var alis = parseAlisFiyat(u);
            if (alis > 0) return alis;
            return parseBirimFiyat(u);
        }

        function sonAlisObj(u) {
            return u.sonAlis || null;
        }

        function paraFmtLocal(n) {
            if (typeof n !== 'number' || isNaN(n)) n = 0;
            if (window.CariErincPara && typeof window.CariErincPara.format === 'function')
                return window.CariErincPara.format(n);
            return n.toLocaleString('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
        }

        function urunListeFiyatMetni(u) {
            var sa = sonAlisObj(u);
            if (sa) {
                var liste = apiNum(sa.birimFiyat);
                var i1 = apiNum(sa.iskonto1);
                var i2 = apiNum(sa.iskonto2);
                var txt = paraFmtLocal(liste) + ' ₺';
                if (i1 > 0 || i2 > 0)
                    txt += ' <span class="text-muted">(isk. %' + i1 + (i2 > 0 ? ' / %' + i2 : '') + ')</span>';
                return txt;
            }
            return paraFmtLocal(varsayilanAlisBirimFiyat(u)) + ' ₺';
        }

        function normalizeApiUrun(urun) {
            if (!urun) return null;
            var id = urun.id;
            if (id == null) return null;
            var ad = urun.ad || "";
            var barkod = urun.barkod || "";
            return {
                id: id,
                ad: ad,
                barkod: barkod,
                varsayilanAlisBirim: varsayilanAlisBirimFiyat(urun)
            };
        }

        function notifySatirGuncellendi() {
            document.dispatchEvent(new Event('alis-urun-satir-guncellendi'));
        }

        // ── Satıra ürün uygula ───────────────────────────────────────────────────

        function applyUrunToRow(row, rawUrun, autoFocusMiktar) {
            var n = normalizeApiUrun(rawUrun);
            if (!n || !row) return;

            var idInp = row.querySelector('.alis-urun-id');
            var adEl = row.querySelector('.alis-urun-ad');
            var barkodInp = row.querySelector('.barkod-input');
            var fiyatInp = row.querySelector('.fiyat-input');
            var isk1Inp = row.querySelector('.isk1-input');
            var isk2Inp = row.querySelector('.isk2-input');
            var miktarInp = row.querySelector('.miktar-input');
            var kdvInp = row.querySelector('.alis-kdv');

            if (idInp) idInp.value = String(n.id);
            if (adEl) adEl.textContent = n.ad;
            if (barkodInp) barkodInp.value = n.barkod;

            var sa = sonAlisObj(rawUrun);
            if (sa && sa.birimFiyat != null) {
                var listeBf = apiNum(sa.birimFiyat);
                var i1 = apiNum(sa.iskonto1);
                var i2 = apiNum(sa.iskonto2);
                if (fiyatInp) fiyatInp.value = (window.CariErincPara && window.CariErincPara.format) ? window.CariErincPara.format(listeBf) : listeBf.toFixed(2);
                if (isk1Inp) isk1Inp.value = String(i1);
                if (isk2Inp) isk2Inp.value = String(i2);
            } else {
                if (fiyatInp) fiyatInp.value = (window.CariErincPara && window.CariErincPara.format) ? window.CariErincPara.format(n.varsayilanAlisBirim) : n.varsayilanAlisBirim.toFixed(2);
                if (isk1Inp) isk1Inp.value = '0';
                if (isk2Inp) isk2Inp.value = '0';
            }

            if (kdvInp) kdvInp.value = String(parseKdvOrani(rawUrun));

            notifySatirGuncellendi();

            if (autoFocusMiktar && miktarInp) {
                setTimeout(function () {
                    miktarInp.focus();
                    miktarInp.select();
                }, 100);
            }
        }

        // ── Yeni satır oluştur ───────────────────────────────────────────────────

        function createNewRow() {
            var existingRows = satirlarBody.querySelectorAll('tr.satir-row');
            var rowCount = existingRows.length;
            var templateRow = existingRows[0];
            if (!templateRow) return null;

            var newRow = templateRow.cloneNode(true);

            newRow.querySelector('.alis-urun-id').value = '0';
            newRow.querySelector('.alis-urun-ad').textContent = '';
            newRow.querySelector('.miktar-input').value = '1';
            newRow.querySelector('.barkod-input').value = '';

            var fiyatInp = newRow.querySelector('.fiyat-input');
            if (fiyatInp) fiyatInp.value = (window.CariErincPara && window.CariErincPara.format) ? window.CariErincPara.format(0) : '0,00';

            newRow.querySelector('.isk1-input').value = '0';
            newRow.querySelector('.isk2-input').value = '0';
            newRow.querySelector('.satir-toplam').innerHTML = '<span class="text-muted">—</span>';

            var kdvEl = newRow.querySelector('.alis-kdv');
            if (kdvEl) {
                kdvEl.value = String((window.CariErincAlisCreate && window.CariErincAlisCreate.varsayilanKdv) || 20);
                kdvEl.name = 'Satirlar[' + rowCount + '].KdvOrani';
            }

            newRow.querySelector('.alis-urun-id').name = 'Satirlar[' + rowCount + '].UrunId';
            newRow.querySelector('.barkod-input').name = 'Satirlar[' + rowCount + '].Barkod';
            newRow.querySelector('.miktar-input').name = 'Satirlar[' + rowCount + '].Miktar';
            newRow.querySelector('.fiyat-input').name = 'Satirlar[' + rowCount + '].BirimFiyat';
            newRow.querySelector('.isk1-input').name = 'Satirlar[' + rowCount + '].Iskonto1';
            newRow.querySelector('.isk2-input').name = 'Satirlar[' + rowCount + '].Iskonto2';

            satirlarBody.insertBefore(newRow, satirlarBody.firstChild);

            if (window.CariErincPara && window.CariErincPara.initInputs) {
                window.CariErincPara.initInputs(newRow);
            }

            notifySatirGuncellendi();
            return newRow;
        }

        // ── Hedef satırı belirle (son boş satır veya yeni satır) ────────────────

        function hedefSatirBul() {
            var rows = satirlarBody.querySelectorAll('tr.satir-row');
            var lastRow = rows[rows.length - 1];
            if (!lastRow) return createNewRow();
            var lastId = lastRow.querySelector('.alis-urun-id').value;
            if (!lastId || lastId === '0') return lastRow;  // Boş satır var, onu kullan
            return createNewRow();                           // Hepsi dolu, yeni ekle
        }

        // ── Modal ürün listesi ───────────────────────────────────────────────────

        function urunListedeGosterAd(u) { return u.ad || ""; }
        function urunListedeGosterBarkod(u) { return u.barkod || "-"; }
        function urunListedeGosterStok(u) { return u.stokAdedi; }

        function urunAraSonuclariGoster(items) {
            if (!urunAraSonuc) return;
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
                a.innerHTML =
                    '<div><strong>' + escapeHtml(urunListedeGosterAd(u)) + '</strong><br/>' +
                    '<small class="text-muted">' + escapeHtml(urunListedeGosterBarkod(u)) + '</small>' + stokUyari + '</div>' +
                    '<span class="ms-2 text-nowrap small text-end">Son fiş: ' + urunListeFiyatMetni(u) + '</span>';

                a.addEventListener('click', function () {
                    var targetRow = activeRow || hedefSatirBul();
                    applyUrunToRow(targetRow, u, true);
                    activeRow = null;

                    var inst = bootstrap.Modal.getInstance(urunAraModal);
                    if (inst) inst.hide();
                    if (urunAraInput) urunAraInput.value = '';
                    if (urunAraSonuc) urunAraSonuc.innerHTML = '';
                    if (urunAraYardim) urunAraYardim.textContent = varsayilanUrunAraYardim;

                    // Sonraki barkod için üst inputa focus
                    var tekBarkod = document.getElementById('tekBarkodInput');
                    if (tekBarkod) setTimeout(function () { tekBarkod.focus(); }, 200);
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
            urunModalPrefillYardim = 'Aramaya uyan ürünler aşağıda. Satıra eklemek için birini seçin.';
            getUrunModal().show();
        }

        // ── Modal text arama ─────────────────────────────────────────────────────

        if (urunAraInput) {
            urunAraInput.addEventListener('input', function () {
                clearTimeout(urunAraTimer);
                var q = urunAraInput.value.trim();
                if (q.length < 2) {
                    if (urunAraSonuc) urunAraSonuc.innerHTML = '';
                    return;
                }
                urunAraTimer = setTimeout(async function () {
                    try {
                        var res = await fetch(urls.urunAraMetin + '?q=' + encodeURIComponent(q));
                        if (!res.ok) { urunAraSonuclariGoster([]); return; }
                        var data = await res.json();
                        urunAraSonuclariGoster(Array.isArray(data) ? data : []);
                    } catch (e) {
                        urunAraSonuclariGoster([]);
                    }
                }, 300);
            });
        }

        // ── Modal aç/kapat olayları ──────────────────────────────────────────────

        if (urunAraModal) {
            urunAraModal.addEventListener('shown.bs.modal', function () {
                if (urunModalPrefillQuery != null) {
                    if (urunAraInput) urunAraInput.value = urunModalPrefillQuery;
                    urunAraSonuclariGoster(urunModalPrefillItems || []);
                    if (urunAraYardim && urunModalPrefillYardim) urunAraYardim.textContent = urunModalPrefillYardim;
                    urunModalPrefillQuery = null;
                    urunModalPrefillItems = null;
                    urunModalPrefillYardim = null;
                    if (urunAraInput) urunAraInput.focus();
                    return;
                }
                if (urunAraYardim) urunAraYardim.textContent = varsayilanUrunAraYardim;
                if (urunAraInput) { urunAraInput.focus(); urunAraInput.value = ''; }
                if (urunAraSonuc) urunAraSonuc.innerHTML = '';
            });

            urunAraModal.addEventListener('hidden.bs.modal', function () {
                activeRow = null;
                // Modal kapanınca barkod inputuna geri dön
                var tekBarkod = document.getElementById('tekBarkodInput');
                if (tekBarkod) setTimeout(function () { tekBarkod.focus(); }, 100);
            });
        }

        // ── Üstteki 🔍 butonu → modal aç ────────────────────────────────────────

        var tekBarkodModalAc = document.getElementById('tekBarkodModalAc');
        if (tekBarkodModalAc) {
            tekBarkodModalAc.addEventListener('click', function () {
                activeRow = hedefSatirBul();
                urunModalPrefillQuery = null;
                urunModalPrefillItems = null;
                urunModalPrefillYardim = null;
                getUrunModal().show();
            });
        }

        // ── Üstteki barkod input → Enter ile ürün ara ───────────────────────────

        var tekBarkodInput = document.getElementById('tekBarkodInput');
        if (tekBarkodInput) {
            tekBarkodInput.addEventListener('keydown', async function (e) {
                if (e.key !== 'Enter') return;
                e.preventDefault();

                var barkod = tekBarkodInput.value.trim();
                if (!barkod) return;

                var targetRow = hedefSatirBul();
                activeRow = targetRow;

                try {
                    var res = await fetch(urls.barkodAra + '?barkod=' + encodeURIComponent(barkod));
                    var urun = res.ok ? await res.json() : null;

                    if (normalizeApiUrun(urun)) {
                        applyUrunToRow(targetRow, urun, false); // focus barkod inputta kalsın
                        activeRow = null;
                    } else {
                        var resAra = await fetch(urls.urunAraMetin + '?q=' + encodeURIComponent(barkod) + '&limit=' + limitBarkod);
                        var dataAra = resAra.ok ? await resAra.json() : [];

                        if (!Array.isArray(dataAra) || dataAra.length === 0) {
                            showToast('Ürün bulunamadı', 'danger');
                            activeRow = null;
                        } else if (dataAra.length === 1 && normalizeApiUrun(dataAra[0])) {
                            applyUrunToRow(targetRow, dataAra[0], false);
                            activeRow = null;
                        } else {
                            acUrunSecimModalindanBarkod(barkod, dataAra);
                        }
                    }
                } catch (err) {
                    showToast('Bir hata oluştu', 'danger');
                    activeRow = null;
                }

                tekBarkodInput.value = '';
                tekBarkodInput.focus(); // Sonraki barkod için hazır
            });
        }

    });
})();