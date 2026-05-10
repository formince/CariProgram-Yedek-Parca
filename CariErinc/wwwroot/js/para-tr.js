/**
 * Türk Lirası: giriş/çıkış 1.234,56 — sunucu model bağlama tr-TR ile uyumlu.
 * window.CariErincPara.parse | parseOrZero | fromApi | format | formatPercent | initInputs
 */
(function () {
    'use strict';

    function parse(str) {
        if (str == null || str === '') return NaN;
        var s = String(str).trim().replace(/\s/g, '');
        if (!s) return NaN;
        var lastComma = s.lastIndexOf(',');
        var lastDot = s.lastIndexOf('.');
        if (lastComma >= 0 && (lastDot < 0 || lastComma > lastDot)) {
            s = s.replace(/\./g, '').replace(',', '.');
        } else {
            s = s.replace(/,/g, '');
        }
        var n = parseFloat(s);
        return isNaN(n) ? NaN : n;
    }

    function parseOrZero(str) {
        var n = parse(str);
        return isNaN(n) ? 0 : n;
    }

    function fromApi(value) {
        if (value == null || value === '') return 0;
        if (typeof value === 'number') return isNaN(value) ? 0 : value;
        return parseOrZero(value);
    }

    function format(num) {
        if (typeof num !== 'number' || isNaN(num)) return '';
        return num.toLocaleString('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    }

    function formatPercent(num) {
        if (typeof num !== 'number' || isNaN(num)) return '';
        return num.toLocaleString('tr-TR', { minimumFractionDigits: 0, maximumFractionDigits: 4 });
    }

    function initInputs(root) {
        var scope = root || document;
        scope.querySelectorAll('.input-para-tr').forEach(function (el) {
            if (el.dataset.paraInit === '1') return;
            el.dataset.paraInit = '1';
            el.addEventListener('blur', function () {
                var n = parse(el.value);
                if (!isNaN(n)) el.value = format(n);
            });
            el.addEventListener('focus', function () {
                try { el.select(); } catch (e) { /* readonly */ }
            });
        });
    }

    function domReady(fn) {
        if (typeof document === 'undefined') return;
        if (document.readyState === 'loading')
            document.addEventListener('DOMContentLoaded', fn);
        else fn();
    }

    function patchJqueryValidatorNumber() {
        if (typeof jQuery === 'undefined' || !jQuery.validator || !jQuery.validator.methods || !jQuery.validator.methods.number) return;
        if (jQuery.validator.methods.number._paraTrPatched) return;
        var orig = jQuery.validator.methods.number;
        jQuery.validator.methods.number = function (value, element) {
            if (this.optional(element)) return true;
            if (jQuery(element).hasClass('input-para-tr')) {
                var n = parse(value);
                return !isNaN(n);
            }
            return orig.call(this, value, element);
        };
        jQuery.validator.methods.number._paraTrPatched = true;
    }

    function patchJqueryValidatorRanges() {
        if (typeof jQuery === 'undefined' || !jQuery.validator || !jQuery.validator.methods) return;
        if (jQuery.validator.methods.range && !jQuery.validator.methods.range._paraTrPatched) {
            var origRange = jQuery.validator.methods.range;
            jQuery.validator.methods.range = function (value, element, param) {
                if (this.optional(element)) return true;
                if (!jQuery(element).hasClass('input-para-tr'))
                    return origRange.call(this, value, element, param);
                var n = parse(value);
                if (isNaN(n)) return false;
                return n >= Number(param[0]) && n <= Number(param[1]);
            };
            jQuery.validator.methods.range._paraTrPatched = true;
        }

        if (jQuery.validator.methods.min && !jQuery.validator.methods.min._paraTrPatched) {
            var origMin = jQuery.validator.methods.min;
            jQuery.validator.methods.min = function (value, element, param) {
                if (this.optional(element)) return true;
                if (!jQuery(element).hasClass('input-para-tr'))
                    return origMin.call(this, value, element, param);
                var n = parse(value);
                if (isNaN(n)) return false;
                return n >= Number(param);
            };
            jQuery.validator.methods.min._paraTrPatched = true;
        }

        if (jQuery.validator.methods.max && !jQuery.validator.methods.max._paraTrPatched) {
            var origMax = jQuery.validator.methods.max;
            jQuery.validator.methods.max = function (value, element, param) {
                if (this.optional(element)) return true;
                if (!jQuery(element).hasClass('input-para-tr'))
                    return origMax.call(this, value, element, param);
                var n = parse(value);
                if (isNaN(n)) return false;
                return n <= Number(param);
            };
            jQuery.validator.methods.max._paraTrPatched = true;
        }
    }

    function formatMoney(num) {
        if (typeof num !== 'number' || isNaN(num)) return '';
        return format(num) + ' \u20BA';
    }

    domReady(function () {
        initInputs();
        patchJqueryValidatorNumber();
        patchJqueryValidatorRanges();
    });

    window.addEventListener('load', function () {
        initInputs();
        patchJqueryValidatorNumber();
        patchJqueryValidatorRanges();
    });

    window.CariErincPara = {
        parse: parse,
        parseOrZero: parseOrZero,
        fromApi: fromApi,
        format: format,
        formatMoney: formatMoney,
        formatPercent: formatPercent,
        initInputs: initInputs
    };
})();
