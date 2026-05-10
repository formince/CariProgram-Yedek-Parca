/**
 * Hızlı satış — genel indirim modalı ve ödeme formu gizli alanları.
 * Sepet: satis-hizli.js → CariErincHizliGenelIndirim.onSepetAraToplam(araToplam)
 */
(function () {
    'use strict';

    var cfg = window.CariErincHizliSatis;
    if (!cfg) return;

    var modalEl = document.getElementById('genelIndirimModal');
    var btnAc = document.getElementById('btnGenelIndirimAc');
    var ozetEl = document.getElementById('genelIndirimOzet');
    var hidMod = document.getElementById('hidGenelIndirimModu');
    var hidOran = document.getElementById('hidGenelIndirimOrani');
    var hidTutar = document.getElementById('hidGenelIndirimTutari');
    var hidHedef = document.getElementById('hidHedefToplam');
    var indirimSatiri = document.getElementById('indirimSatiri');
    var indirimSatiriLabel = document.getElementById('indirimSatiriLabel');
    var genelIndirimTutarDisp = document.getElementById('genelIndirimTutarDisp');
    var genelToplamDisp = document.getElementById('genelToplam');
    var modalModYuzde = document.getElementById('modalIndirimModYuzde');
    var modalModManuel = document.getElementById('modalIndirimModManuel');
    var modalOran = document.getElementById('modalGenelIndirimOrani');
    var modalHedef = document.getElementById('modalGenelToplamHedef');
    var btnUygula = document.getElementById('btnGenelIndirimUygula');
    var btnTemizle = document.getElementById('btnGenelIndirimTemizle');

    if (!hidMod || !hidOran || !hidTutar || !hidHedef) return;

    var sonAraToplam = 0;

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
        var t = String(s == null ? '' : s).replace(/\./g, '').replace(',', '.');
        var v2 = parseFloat(t);
        return isNaN(v2) ? NaN : v2;
    }

    function pctFmt(n) {
        if (window.CariErincPara && typeof window.CariErincPara.formatPercent === 'function')
            return window.CariErincPara.formatPercent(n);
        return paraFmt(n);
    }

    /** GenelIndirimModu: 0=Yuzde, 1=ManuelHedef, 2=Sabit — formda saklı */
    function yazHid(mod, oran, tutar, hedef) {
        hidMod.value = String(mod);
        hidOran.value = paraFmt(oran);
        hidTutar.value = paraFmt(tutar);
        hidHedef.value = paraFmt(hedef);
    }

    function okuHid() {
        return {
            mod: parseInt(hidMod.value, 10) || 0,
            oran: paraParse(hidOran.value),
            tutar: paraParse(hidTutar.value),
            hedef: paraParse(hidHedef.value)
        };
    }

    function hesaplaGosterim(araToplam) {
        araToplam = round2(araToplam);
        var h = okuHid();
        var mod = h.mod;
        var genelTutar = 0;
        var genelOran = 0;
        var toplam = araToplam;

        if (mod === 1) {
            var hedef = round2(h.hedef);
            if (hedef < 0) hedef = 0;
            if (hedef > araToplam) hedef = araToplam;
            genelTutar = round2(araToplam - hedef);
            genelOran = araToplam > 0 ? (genelTutar / araToplam) * 100 : 0;
            toplam = hedef;
        } else if (mod === 2) {
            genelTutar = round2(Math.min(h.tutar, araToplam));
            genelOran = araToplam > 0 ? (genelTutar / araToplam) * 100 : 0;
            toplam = round2(araToplam - genelTutar);
        } else {
            genelOran = isNaN(h.oran) || h.oran < 0 ? 0 : h.oran;
            if (genelOran > 100) genelOran = 100;
            genelTutar = round2(araToplam * genelOran / 100);
            toplam = round2(araToplam - genelTutar);
        }

        return { araToplam: araToplam, genelTutar: genelTutar, genelOran: genelOran, toplam: toplam, mod: mod };
    }

    function guncelleOzetVeTfoot(araToplam) {
        sonAraToplam = round2(araToplam);
        var x = hesaplaGosterim(sonAraToplam);

        if (ozetEl) {
            if (x.genelTutar > 0.005) {
                if (x.mod === 1)
                    ozetEl.textContent = 'Hedef toplam ' + paraFmt(x.toplam) + ' ₺ (−' + paraFmt(x.genelTutar) + ' ₺)';
                else if (x.mod === 2)
                    ozetEl.textContent = 'Sabit indirim −' + paraFmt(x.genelTutar) + ' ₺';
                else
                    ozetEl.textContent = '%' + pctFmt(x.genelOran) + ' (−' + paraFmt(x.genelTutar) + ' ₺)';
            } else {
                ozetEl.textContent = 'Genel indirim uygulanmadı.';
            }
        }

        if (indirimSatiri && genelIndirimTutarDisp && indirimSatiriLabel) {
            if (x.genelTutar > 0.005) {
                indirimSatiriLabel.textContent = 'Genel İndirim (%' + pctFmt(x.genelOran) + ')';
                genelIndirimTutarDisp.textContent = '-' + paraFmt(x.genelTutar) + ' ₺';
                indirimSatiri.style.display = '';
            } else {
                indirimSatiri.style.display = 'none';
            }
        }

        if (genelToplamDisp)
            genelToplamDisp.textContent = paraFmt(x.toplam) + ' ₺';
    }

    function modalModGoster() {
        var y = modalModYuzde && modalModYuzde.checked;
        var secYuzde = document.getElementById('modalIndirimYuzdeSec');
        var secManuel = document.getElementById('modalIndirimManuelSec');
        if (secYuzde) secYuzde.style.display = y ? '' : 'none';
        if (secManuel) secManuel.style.display = y ? 'none' : '';
    }

    function modalAcDoldur() {
        var h = okuHid();
        if (h.mod === 1) {
            if (modalModManuel) modalModManuel.checked = true;
            if (modalOran) modalOran.value = paraFmt(0);
            if (modalHedef) modalHedef.value = paraFmt(h.hedef > 0 ? h.hedef : sonAraToplam);
        } else if (h.mod === 2) {
            if (modalModManuel) modalModManuel.checked = true;
            if (modalOran) modalOran.value = paraFmt(0);
            var hedefSabit = round2(Math.max(0, sonAraToplam - h.tutar));
            if (modalHedef) modalHedef.value = paraFmt(hedefSabit);
        } else {
            if (modalModYuzde) modalModYuzde.checked = true;
            if (modalOran) modalOran.value = paraFmt(h.oran);
            var t = hesaplaGosterim(sonAraToplam);
            if (modalHedef) modalHedef.value = paraFmt(t.toplam);
        }
        modalModGoster();
    }

    function uygulaModal() {
        var ara = sonAraToplam;
        var yuzdeMi = modalModYuzde && modalModYuzde.checked;
        if (yuzdeMi) {
            var oran = paraParse(modalOran ? modalOran.value : '0');
            if (isNaN(oran) || oran < 0) oran = 0;
            if (oran > 100) oran = 100;
            var tut = round2(ara * oran / 100);
            yazHid(0, oran, 0, 0);
        } else {
            var hedef = paraParse(modalHedef ? modalHedef.value : '');
            if (isNaN(hedef) || hedef < 0) hedef = 0;
            if (hedef > ara) hedef = ara;
            yazHid(1, 0, 0, hedef);
        }
        guncelleOzetVeTfoot(ara);
        if (modalEl && window.bootstrap) {
            var inst = bootstrap.Modal.getInstance(modalEl);
            if (inst) inst.hide();
        }
    }

    function temizle() {
        yazHid(0, 0, 0, 0);
        guncelleOzetVeTfoot(sonAraToplam);
        if (modalEl && window.bootstrap) {
            var inst = bootstrap.Modal.getInstance(modalEl);
            if (inst) inst.hide();
        }
    }

    if (modalModYuzde) modalModYuzde.addEventListener('change', modalModGoster);
    if (modalModManuel) modalModManuel.addEventListener('change', modalModGoster);
    if (btnAc && modalEl && window.bootstrap) {
        btnAc.addEventListener('click', function () {
            modalAcDoldur();
            var m = bootstrap.Modal.getInstance(modalEl) || new bootstrap.Modal(modalEl);
            m.show();
            if (window.CariErincPara && window.CariErincPara.initInputs)
                window.CariErincPara.initInputs(modalEl);
        });
    }
    if (btnUygula) btnUygula.addEventListener('click', uygulaModal);
    if (btnTemizle) btnTemizle.addEventListener('click', temizle);

    if (cfg.ui) {
        var ui = cfg.ui;
        var mod = typeof ui.genelIndirimModu === 'number' ? ui.genelIndirimModu : parseInt(ui.genelIndirimModu, 10) || 0;
        var oran = Number(ui.genelIndirimOrani) || 0;
        var tut = Number(ui.genelIndirimTutari) || 0;
        var hedef = Number(ui.hedefToplam) || 0;
        if (mod === 1 && hedef > 0)
            yazHid(1, 0, 0, hedef);
        else if (tut > 0.005)
            yazHid(2, oran, tut, 0);
        else
            yazHid(0, oran, 0, 0);
    }

    window.CariErincHizliGenelIndirim = {
        onSepetAraToplam: function (araToplam) {
            guncelleOzetVeTfoot(araToplam);
        }
    };
})();
