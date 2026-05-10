/**
 * Hızlı Satış — sepet, barkod / ürün arama, ödeme formu.
 * Genel indirim: satis-hizli-genel-indirim.js (modal).
 */
(function () {
    'use strict';

    var cfg = window.CariErincHizliSatis;
    if (!cfg || !cfg.urls) return;

    var urls = cfg.urls;
    var urunler = cfg.urunler || [];
    var varsayilanKdvGlobal = typeof cfg.varsayilanKdv === 'number' ? cfg.varsayilanKdv : 20;
    var urunAraMetinLimitBarkodSonrasi = cfg.urunAraMetinLimitBarkodSonrasi || 50;

    var satirlar = [];

    if (Array.isArray(cfg.initialSatirlar) && cfg.initialSatirlar.length > 0) {
        satirlar = cfg.initialSatirlar.map(function (s) {
            if (!s || !s.urunId || s.urunId <= 0) return null;
            var u = urunler.find(function (x) { return x.id === s.urunId; });
            var nh = s.satirNetTutarHedef;
            return {
                urunId: s.urunId,
                ad: u ? u.ad : ('Ürün #' + s.urunId),
                barkod: u ? (u.barkod || '') : '',
                miktar: s.miktar,
                birimFiyat: s.birimFiyat,
                indirimOrani: s.indirimOrani,
                kdvOrani: s.kdvOrani,
                satirNetHedef: nh != null && nh !== '' && !isNaN(Number(nh)) ? Math.round(Number(nh) * 100) / 100 : null
            };
        }).filter(function (s) { return !!s; });
    }

    var barkodInput         = document.getElementById('barkodInput');
    var sepetBody           = document.getElementById('sepetBody');
    var sepetBos            = document.getElementById('sepetBos');
    var araToplamDisp       = document.getElementById('araToplam');
    var tamamlaBtn          = document.getElementById('tamamlaBtn');
    var bekletBtn           = document.getElementById('bekletBtn');
    var satirlarContainer   = document.getElementById('satirlarContainer');
    var taslakIdInput       = document.getElementById('taslakIdInput');
    var urunAraInput        = document.getElementById('urunAraInput');
    var urunAraSonuc        = document.getElementById('urunAraSonuc');
    var urunAraModal        = document.getElementById('urunAraModal');
    var urunAraYardim       = document.getElementById('urunAraYardim');

    var musteriAraInput  = document.getElementById('musteriAraInput');
    var musteriAraSonuc  = document.getElementById('musteriAraSonuc');
    var musteriIdHidden  = document.getElementById('musteriId');
    var musteriSecildi   = document.getElementById('musteriSecildi');
    var musteriAraTimer  = null;

    function round2(x) {
        if (typeof x !== 'number' || isNaN(x)) return 0;
        return Math.round(x * 100) / 100;
    }

    function paraFmt(n) {
        if (typeof n !== 'number' || isNaN(n)) n = 0;
        if (window.CariErincPara && typeof window.CariErincPara.format === 'function')
            return window.CariErincPara.format(n);
        return n.toLocaleString('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    }

    function paraParse(s) {
        if (window.CariErincPara && typeof window.CariErincPara.parse === 'function') {
            var v = window.CariErincPara.parse(s);
            return isNaN(v) ? NaN : v;
        }
        var t = String(s == null ? '' : s).replace(/\./g, "").replace(",", ".");
        var v2 = parseFloat(t);
        return isNaN(v2) ? NaN : v2;
    }

    function parseMiktarInt(s) {
        if (window.CariErincPara && typeof window.CariErincPara.parseOrZero === 'function') {
            var n = window.CariErincPara.parseOrZero(s);
            var v = Math.floor(Math.abs(n));
            return v < 1 ? 0 : v;
        }
        var x = parseInt(String(s), 10);
        return isNaN(x) || x < 1 ? 0 : x;
    }

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

    if (cfg.ui) {
        if (cfg.ui.musteriId > 0) {
            musteriIdHidden.value = String(cfg.ui.musteriId);
            var tipVeresiye = document.getElementById('tipVeresiye');
            if (tipVeresiye) tipVeresiye.checked = true;
            var musteriGroup = document.getElementById('musteriGroup');
            if (musteriGroup) musteriGroup.style.display = 'block';
            var ad = cfg.ui.seciliMusteriAd || '';
            if (ad && musteriAraInput) {
                musteriAraInput.value = ad;
                if (musteriSecildi) {
                    musteriSecildi.textContent = '✓ ' + ad;
                    musteriSecildi.style.display = 'inline';
                }
            }
        }
    }

    function musteriSec(id, ad) {
        musteriIdHidden.value = id;
        musteriAraInput.value = ad;
        if (musteriSecildi) {
            musteriSecildi.textContent = ad;
            var container = document.getElementById('musteriSecildiContainer');
            if (container) container.style.setProperty('display', 'flex', 'important');
        }
        musteriAraSonuc.style.display = 'none';
        musteriAraSonuc.innerHTML = '';
    }

    musteriAraInput.addEventListener('input', function () {
        clearTimeout(musteriAraTimer);
        musteriIdHidden.value = '';
        if (musteriSecildi) musteriSecildi.textContent = '';
        var container = document.getElementById('musteriSecildiContainer');
        if (container) container.style.setProperty('display', 'none', 'important');

        var q = musteriAraInput.value.trim();
        if (q.length < 1) { musteriAraSonuc.style.display = 'none'; musteriAraSonuc.innerHTML = ''; return; }
        musteriAraTimer = setTimeout(async function () {
            try {
                var res = await fetch(urls.musteriAra + '?q=' + encodeURIComponent(q));
                if (!res.ok) { musteriAraSonuc.style.display = 'none'; return; }
                var data = await res.json();
                musteriAraSonuc.innerHTML = '';
                if (!data || !data.length) {
                    musteriAraSonuc.innerHTML = '<div class="list-group-item text-muted p-3">Müşteri bulunamadı</div>';
                    musteriAraSonuc.style.display = 'block';
                    return;
                }
                data.forEach(function (m) {
                    var btn = document.createElement('button');
                    btn.type = 'button';
                    btn.className = 'list-group-item list-group-item-action border-0 p-3 hover:bg-primary-container transition-colors';
                    btn.innerHTML = '<div class="fw-bold">' + escapeHtml(m.ad) + '</div><small class="text-muted">' + escapeHtml(m.telefon || 'Telefon belirtilmemiş') + '</small>';
                    btn.addEventListener('click', function () { musteriSec(m.id, m.ad); });
                    musteriAraSonuc.appendChild(btn);
                });
                musteriAraSonuc.style.display = 'block';
            } catch (e) { musteriAraSonuc.style.display = 'none'; }
        }, 250);
    });

    document.addEventListener('click', function (e) {
        if (!musteriAraSonuc.contains(e.target) && e.target !== musteriAraInput)
            musteriAraSonuc.style.display = 'none';
    });

    function normalizeApiUrun(urun) {
        if (!urun) return null;
        var id = urun.id;
        if (id == null) return null;
        var ad = urun.ad || "";
        var barkod = urun.barkod || "";
        var bfRaw = urun.birimFiyat;
        var bf = window.CariErincPara && typeof window.CariErincPara.fromApi === 'function'
            ? window.CariErincPara.fromApi(bfRaw)
            : (function (r) { var x = parseFloat(r); return isNaN(x) ? 0 : x; })(bfRaw);
        var kdv = parseInt(urun.kdvOrani, 10);
        if (isNaN(kdv)) kdv = 0;
        return { id: id, ad: ad, barkod: barkod, birimFiyat: bf, kdvOrani: kdv };
    }

    function sepeteUrunEkle(urun) {
        var u = normalizeApiUrun(urun);
        if (!u) return;
        var mevcut = satirlar.find(function (s) { return s.urunId === u.id; });
        if (mevcut) mevcut.miktar++;
        else {
            satirlar.unshift({
                urunId: u.id,
                ad: u.ad,
                barkod: u.barkod,
                miktar: 1,
                birimFiyat: u.birimFiyat,
                indirimOrani: 0,
                kdvOrani: u.kdvOrani > 0 ? u.kdvOrani : varsayilanKdvGlobal
            });
        }
        renderSepet();
    }

    if (window.CariErincSatisUrunArama && typeof window.CariErincSatisUrunArama.init === 'function') {
        window.CariErincSatisUrunArama.init({
            urls: { barkodAra: urls.barkodAra, urunAraMetin: urls.urunAraMetin },
            urunAraMetinLimitBarkodSonrasi: urunAraMetinLimitBarkodSonrasi,
            elements: {
                barkodInput: barkodInput,
                urunAraModal: urunAraModal,
                urunAraInput: urunAraInput,
                urunAraSonuc: urunAraSonuc,
                urunAraYardim: urunAraYardim
            },
            onProductPicked: function (u) { sepeteUrunEkle(u); }
        });
    }

    document.querySelectorAll('input[name="odemeTipi"]').forEach(function (r) {
        r.addEventListener('change', function () {
            document.getElementById('musteriGroup').style.display = this.value === 'Veresiye' ? 'block' : 'none';
        });
    });

    /** Satır neti sabitlendiğinde % gösterimi (POST'ta % kısaltılmaz diye net hedef ayrı gider). */
    function indirimPctEkran(brut, net) {
        if (!(brut > 0)) return 0;
        return Math.round((1 - net / brut) * 100 * 1e5) / 1e5;
    }

    function renderSepet() {
        sepetBody.innerHTML = '';
        var araToplam = 0;

        satirlar.forEach(function (s, i) {
            var tr = document.createElement('tr');
            var brut = s.miktar * s.birimFiyat;
            var satirToplam;
            var netBirim;
            var indirimYuzdeGoster;
            if (s.satirNetHedef != null && typeof s.satirNetHedef === 'number' && !isNaN(s.satirNetHedef)) {
                satirToplam = round2(Math.min(Math.max(0, s.satirNetHedef), brut));
                netBirim = s.miktar > 0 ? round2(satirToplam / s.miktar) : s.birimFiyat;
                indirimYuzdeGoster = indirimPctEkran(brut, satirToplam);
            } else {
                var indirimTutari = round2(brut * (s.indirimOrani || 0) / 100);
                satirToplam = round2(brut - indirimTutari);
                netBirim = s.miktar > 0 ? round2(satirToplam / s.miktar) : s.birimFiyat;
                indirimYuzdeGoster = s.indirimOrani || 0;
            }

            tr.className = 'group hover:bg-white/50 transition-colors';
            tr.innerHTML =
                '<td class="ps-4 py-4">' +
                    '<div class="fw-bold text-on-surface small">' + escapeHtml(s.ad || '') + '</div>' +
                '</td>' +
                '<td class="py-4 text-muted small font-medium">' + escapeHtml(s.barkod || '-') + '</td>' +
                '<td class="py-4"><input type="number" class="form-control form-control-sm border-0 bg-surface-container rounded-3 miktar-input text-center" value="' + s.miktar + '" min="1" step="1" data-i="' + i + '" /></td>' +
                '<td class="py-4"><input type="text" class="form-control form-control-sm border-0 bg-surface-container rounded-3 fiyat-input input-para-tr text-end font-mono" inputmode="decimal" autocomplete="off" value="' + paraFmt(s.birimFiyat) + '" data-i="' + i + '" /></td>' +
                '<td class="py-4"><input type="text" inputmode="decimal" autocomplete="off" class="form-control form-control-sm border-0 bg-surface-container rounded-3 indirim-input text-center font-mono" value="' + indirimYuzdeGoster + '" data-i="' + i + '" /></td>' +
                '<td class="py-4"><input type="text" class="form-control form-control-sm border-0 bg-surface-container rounded-3 net-birim-input input-para-tr text-end font-mono" inputmode="decimal" autocomplete="off" value="' + paraFmt(netBirim) + '" data-i="' + i + '" /></td>' +
                '<td class="py-4 text-end font-bold text-on-surface"><input type="text" class="form-control form-control-sm border-0 bg-surface-container rounded-3 toplam-input input-para-tr fw-bold text-end font-mono" inputmode="decimal" autocomplete="off" value="' + paraFmt(satirToplam) + '" data-i="' + i + '" /></td>' +
                '<td class="pe-4 py-4 text-end">' +
                    '<button type="button" class="btn btn-link text-danger p-2 hover:bg-danger-subtle rounded-3 satir-cikar" data-i="' + i + '">' +
                        '<span class="material-symbols-outlined fs-5">delete</span>' +
                    '</button>' +
                '</td>';
            sepetBody.appendChild(tr);
            araToplam += satirToplam;
        });
        araToplam = round2(araToplam);

        if (window.CariErincPara && window.CariErincPara.initInputs) window.CariErincPara.initInputs(sepetBody);

        if (araToplamDisp) araToplamDisp.textContent = paraFmt(araToplam) + ' ₺';

        if (window.CariErincHizliGenelIndirim && typeof window.CariErincHizliGenelIndirim.onSepetAraToplam === 'function')
            window.CariErincHizliGenelIndirim.onSepetAraToplam(araToplam);

        sepetBos.style.display = satirlar.length ? 'none' : 'block';
        tamamlaBtn.disabled = satirlar.length === 0;
        bekletBtn.disabled = satirlar.length === 0;
    }

    renderSepet();

    sepetBody.addEventListener('click', function (e) {
        var btn = e.target.closest('.satir-cikar');
        if (btn) {
            var i = parseInt(btn.getAttribute('data-i'), 10);
            if (!isNaN(i)) {
                satirlar.splice(i, 1);
                renderSepet();
            }
        }
    });

    sepetBody.addEventListener('change', function (e) {
        var i = parseInt(e.target.getAttribute('data-i'), 10);
        if (isNaN(i)) return;

        if (e.target.classList.contains('miktar-input')) {
            var val = parseMiktarInt(e.target.value);
            if (val > 0) { satirlar[i].miktar = val; satirlar[i].satirNetHedef = null; renderSepet(); }

        } else if (e.target.classList.contains('fiyat-input')) {
            var val = paraParse(e.target.value);
            if (!isNaN(val) && val >= 0) { satirlar[i].birimFiyat = val; satirlar[i].satirNetHedef = null; renderSepet(); }

        } else if (e.target.classList.contains('indirim-input')) {
            var val = paraParse(e.target.value);
            if (!isNaN(val) && val >= 0 && val <= 100) {
                satirlar[i].satirNetHedef = null;
                satirlar[i].indirimOrani = val;
                renderSepet();
            }

        } else if (e.target.classList.contains('net-birim-input')) {
            var val = paraParse(e.target.value);
            if (!isNaN(val) && val >= 0) {
                satirlar[i].birimFiyat = val;
                satirlar[i].indirimOrani = 0;
                satirlar[i].satirNetHedef = null;
                renderSepet();
            }

        } else if (e.target.classList.contains('toplam-input')) {
            var val = paraParse(e.target.value);
            if (!isNaN(val) && val >= 0) {
                var miktar = satirlar[i].miktar || 0;
                var brut = miktar * (satirlar[i].birimFiyat || 0);
                var capped = val;
                if (capped > brut) capped = brut;
                satirlar[i].satirNetHedef = round2(capped);
                if (brut > 0)
                    satirlar[i].indirimOrani = ((brut - satirlar[i].satirNetHedef) / brut) * 100;
                renderSepet();
            }
        }
    });

    function trDecimalInput(n) {
        return paraFmt(typeof n === 'number' && !isNaN(n) ? n : 0);
    }

    document.getElementById('odemeForm').addEventListener('submit', function (e) {
        if (satirlar.length === 0) {
            e.preventDefault();
            showToast('Sepette ürün bulunmuyor', 'danger');
            return;
        }
        var odemeTipi = document.querySelector('input[name="odemeTipi"]:checked').value;
        if (odemeTipi === 'Veresiye') {
            var musteriId = document.getElementById('musteriId').value;
            if (!musteriId) {
                e.preventDefault();
                showToast('Veresiye için müşteri seçiniz', 'danger');
                return;
            }
        }
        satirlarContainer.innerHTML = '';
        satirlar.forEach(function (s, i) {
            function addInput(name, value) {
                var inp = document.createElement('input');
                inp.type = 'hidden';
                inp.name = 'satirlar[' + i + '].' + name;
                inp.value = value;
                satirlarContainer.appendChild(inp);
            }
            addInput('UrunId', s.urunId);
            addInput('Miktar', s.miktar);
            addInput('BirimFiyat', trDecimalInput(s.birimFiyat));
            addInput('KdvOrani', s.kdvOrani || 0);
            addInput('IndirimOrani', trDecimalInput(s.indirimOrani || 0));
            if (s.satirNetHedef != null && typeof s.satirNetHedef === 'number' && !isNaN(s.satirNetHedef))
                addInput('SatirNetTutarHedef', trDecimalInput(s.satirNetHedef));
        });
    });

    bekletBtn.addEventListener('click', async function () {
        if (satirlar.length === 0) return;

        var hidMod = document.getElementById('hidGenelIndirimModu');
        var hidOran = document.getElementById('hidGenelIndirimOrani');
        var hidTutar = document.getElementById('hidGenelIndirimTutari');
        var hidHedef = document.getElementById('hidHedefToplam');

        var data = {
            musteriId: parseInt(document.getElementById('musteriId').value, 10) || null,
            odemeTipi: document.querySelector('input[name="odemeTipi"]:checked').value === 'Pesin' ? 0 : 1,
            genelIndirimModu: hidMod ? parseInt(hidMod.value, 10) || 0 : 0,
            genelIndirimOrani: hidOran ? paraParse(hidOran.value) : 0,
            genelIndirimTutari: hidTutar ? paraParse(hidTutar.value) : 0,
            hedefToplam: hidHedef ? paraParse(hidHedef.value) : 0,
            taslakId: parseInt(taslakIdInput.value, 10) || null,
            aciklama: (function () {
                var ta = document.getElementById('hizliSatisAciklama');
                var t = ta ? String(ta.value || '').trim() : '';
                return t || null;
            })(),
            satirlar: satirlar.map(function (s) {
                return {
                    urunId: s.urunId,
                    miktar: s.miktar,
                    birimFiyat: s.birimFiyat,
                    indirimOrani: s.indirimOrani,
                    kdvOrani: s.kdvOrani,
                    satirNetTutarHedef: s.satirNetHedef != null ? s.satirNetHedef : null
                };
            })
        };

        try {
            bekletBtn.disabled = true;
            var res = await fetch(urls.taslakKaydet, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(data)
            });
            var result = await res.json();
            if (result.basarili) {
                showToast(result.mesaj, 'success');
                satirlar = [];
                taslakIdInput.value = '';
                renderSepet();
                setTimeout(function () { location.reload(); }, 1500);
            } else {
                showToast(result.mesaj, 'danger');
                bekletBtn.disabled = false;
            }
        } catch (err) {
            showToast('Hata oluştu', 'danger');
            bekletBtn.disabled = false;
        }
    });
})();
