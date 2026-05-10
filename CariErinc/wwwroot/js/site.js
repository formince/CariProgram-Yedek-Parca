var siteJs = (function() {
  'use strict';

  function initSidebarToggle() {
    var sidebar = document.getElementById('sidebar');
    var overlay = document.getElementById('sidebarOverlay');
    var toggleBtn = document.getElementById('sidebarToggle');
    var collapseBtn = document.getElementById('sidebarCollapse');

    // Durumu yükle
    var isCollapsed = localStorage.getItem('sidebar-collapsed') === 'true';
    if (isCollapsed && sidebar) {
      sidebar.classList.add('collapsed');
    }

    if (toggleBtn) {
      toggleBtn.addEventListener('click', function() {
        sidebar.classList.toggle('show');
        overlay.classList.toggle('show');
      });
    }

    if (collapseBtn) {
      collapseBtn.addEventListener('click', function() {
        sidebar.classList.toggle('collapsed');
        localStorage.setItem('sidebar-collapsed', sidebar.classList.contains('collapsed'));
      });
    }

    if (overlay) {
      overlay.addEventListener('click', function() {
        sidebar.classList.remove('show');
        overlay.classList.remove('show');
      });
    }
  }

  function showToast(message, type) {
    type = type || 'info';
    var container = document.getElementById('toastContainer');
    if (!container) return;

    var toastEl = document.createElement('div');
    toastEl.className = 'toast align-items-center text-bg-' + type + ' border-0';
    toastEl.setAttribute('role', 'alert');
    toastEl.innerHTML = '<div class="d-flex"><div class="toast-body">' + escapeHtml(message) + '</div><button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast"></button></div>';

    container.appendChild(toastEl);
    var toast = new bootstrap.Toast(toastEl, { delay: 4000 });
    toast.show();
    toastEl.addEventListener('hidden.bs.toast', function() {
      toastEl.remove();
    });
  }

  function escapeHtml(text) {
    var div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
  }

  function initSidebarScroll() {
    var sidebarNav = document.querySelector('.sidebar-nav');
    if (!sidebarNav) return;

    // 1. Restore scroll position from session storage
    var scrollPos = sessionStorage.getItem('sidebar-scroll');
    if (scrollPos) {
      sidebarNav.scrollTop = scrollPos;
    }

    // 2. Scroll active link into view (if it's not visible)
    var activeLink = sidebarNav.querySelector('.sidebar-link.active');
    if (activeLink) {
        // block: 'nearest' ensures it only scrolls if needed
        activeLink.scrollIntoView({ behavior: 'auto', block: 'nearest' });
    }

    // 3. Save scroll position on scroll
    sidebarNav.addEventListener('scroll', function() {
      sessionStorage.setItem('sidebar-scroll', sidebarNav.scrollTop);
    }, { passive: true });

    // 4. Save scroll position on link click (extra safety)
    $(document).on('click', '.sidebar-link', function() {
        sessionStorage.setItem('sidebar-scroll', sidebarNav.scrollTop);
    });
  }

  function initDeleteButtons() {
    const swalPremium = Swal.mixin({
      customClass: {
        popup: 'swal2-premium-popup',
        title: 'swal2-premium-title',
        htmlContainer: 'swal2-premium-html',
        confirmButton: 'swal2-premium-confirm',
        cancelButton: 'swal2-premium-cancel',
        icon: 'swal2-premium-icon'
      },
      buttonsStyling: false,
      reverseButtons: true,
      backdrop: `rgba(25, 28, 30, 0.4)` // Matching site.css sidebar overlay
    });

    $(document).on('click', '.btn-delete', function(e) {
      e.preventDefault();
      var btn = $(this);
      var url = btn.data('url');
      var title = btn.data('title') || 'Silme Onayı';
      var text = btn.data('text') || 'Bu öğeyi silmek istediğinize emin misiniz?';
      var confirmBtnText = btn.data('confirm-text') || 'Evet, Sil!';
      var cancelBtnText = btn.data('cancel-text') || 'Vazgeç';

      swalPremium.fire({
        title: title,
        text: text,
        icon: 'warning',
        showCancelButton: true,
        confirmButtonText: confirmBtnText,
        cancelButtonText: cancelBtnText
      }).then((result) => {
        if (result.isConfirmed) {
          $.ajax({
            url: url,
            type: 'POST',
            data: {
              __RequestVerificationToken: $('input[name="__RequestVerificationToken"]').val()
            },
            success: function(response) {
              if (response.success) {
                showToast(response.message || 'Başarıyla silindi.', 'success');
                setTimeout(function() {
                    location.reload();
                }, 1000);
              } else {
                swalPremium.fire('Hata!', response.message || 'Silme işlemi başarısız oldu.', 'error');
              }
            },
            error: function() {
              swalPremium.fire('Hata!', 'Sunucuyla iletişim kurulurken bir hata oluştu.', 'error');
            }
          });
        }
      });
    });
  }

  function getModalHtmlContent(htmlText) {
    var parser = new DOMParser();
    var doc = parser.parseFromString(htmlText, 'text/html');
    var modalPage = doc.querySelector('[data-modal-page="1"]');
    if (modalPage) return modalPage.outerHTML;

    var mainContent = doc.querySelector('#main-content');
    if (mainContent) return mainContent.innerHTML;

    if (doc.body) return doc.body.innerHTML;
    return htmlText;
  }

  function withModalQuery(url) {
    var resolvedUrl = new URL(url, window.location.origin);
    resolvedUrl.searchParams.set('modal', '1');
    return resolvedUrl.toString();
  }

  function setModalDialogSize(dialogEl, size) {
    if (!dialogEl) return;

    var className = 'modal-dialog modal-dialog-scrollable';
    if (size === 'sm') className += ' modal-sm';
    else if (size === 'xl') className += ' modal-xl';
    else className += ' modal-lg';

    dialogEl.className = className;
  }

  function parseUnobtrusive(container) {
    if (!container || !window.jQuery) return;
    if (!$.validator || !$.validator.unobtrusive) return;

    var forms = container.querySelectorAll('form');
    forms.forEach(function (formEl) {
      try {
        $(formEl).removeData('validator');
        $(formEl).removeData('unobtrusiveValidation');
        $.validator.unobtrusive.parse(formEl);
      } catch (err) {
        console.warn('Validator parse hatası:', err);
      }
    });
  }

  function closeAjaxModal(modalEl, modalInstance, options) {
    if (modalInstance) modalInstance.hide();
    window.dispatchEvent(new CustomEvent('ajax-modal:success'));

    if (options && options.reloadOnSuccess) window.location.reload();
  }

  function initAjaxModalForms() {
    var modalEl = document.getElementById('globalAjaxModal');
    var dialogEl = document.getElementById('globalAjaxModalDialog');
    var titleEl = document.getElementById('globalAjaxModalTitle');
    var bodyEl = document.getElementById('globalAjaxModalBody');
    if (!modalEl || !dialogEl || !titleEl || !bodyEl || !window.bootstrap) return;

    var modalInstance = new bootstrap.Modal(modalEl);
    var currentOptions = { reloadOnSuccess: false };

    modalEl.addEventListener('hidden.bs.modal', function () {
      bodyEl.innerHTML = '';
      titleEl.textContent = 'Form';
      setModalDialogSize(dialogEl, 'lg');
      currentOptions = { reloadOnSuccess: false };
    });

    document.addEventListener('click', async function (event) {
      var sourceEl = event.target instanceof Element ? event.target : null;
      if (!sourceEl) return;

      var trigger = sourceEl.closest('[data-open-modal="1"], .modalin_ex, .modalin-ex');
      if (!trigger) return;

      event.preventDefault();

      var rawUrl = trigger.getAttribute('href') || trigger.getAttribute('data-url');
      if (!rawUrl) return;

      var modalTitle = trigger.getAttribute('data-modal-title') || trigger.getAttribute('title') || 'Form';
      var modalSize = trigger.getAttribute('data-modal-size') || 'lg';
      var reloadOnSuccess = trigger.getAttribute('data-modal-reload') === '1';
      var parentModalEl = trigger.closest('.modal');

      if (parentModalEl && parentModalEl.id !== 'globalAjaxModal') {
        var parentModal = bootstrap.Modal.getInstance(parentModalEl);
        if (parentModal) parentModal.hide();
      }

      currentOptions = { reloadOnSuccess: reloadOnSuccess };
      titleEl.textContent = modalTitle;
      setModalDialogSize(dialogEl, modalSize);
      bodyEl.innerHTML = '<div class="text-center py-4"><div class="spinner-border text-primary" role="status"></div></div>';
      modalInstance.show();

      try {
        var response = await fetch(withModalQuery(rawUrl), {
          headers: { 'X-Requested-With': 'XMLHttpRequest' }
        });
        if (!response.ok) throw new Error('Form yüklenemedi');

        var htmlText = await response.text();
        bodyEl.innerHTML = getModalHtmlContent(htmlText);
        parseUnobtrusive(bodyEl);
      } catch (error) {
        bodyEl.innerHTML = '<div class="alert alert-danger mb-0">Form açılırken hata oluştu.</div>';
      }
    });

    bodyEl.addEventListener('click', function (event) {
      var closeBtn = event.target.closest('[data-modal-close="1"]');
      if (!closeBtn) return;
      event.preventDefault();
      modalInstance.hide();
    });

    bodyEl.addEventListener('submit', async function (event) {
      var form = event.target;
      if (!(form instanceof HTMLFormElement)) return;

      event.preventDefault();

      var action = form.getAttribute('action') || window.location.href;
      var method = (form.getAttribute('method') || 'POST').toUpperCase();
      var submitButton = form.querySelector('[type="submit"]');
      if (submitButton) submitButton.disabled = true;

      try {
        var response = await fetch(withModalQuery(action), {
          method: method,
          body: new FormData(form),
          headers: { 'X-Requested-With': 'XMLHttpRequest' }
        });

        var contentType = response.headers.get('content-type') || '';
        if (response.redirected) {
          closeAjaxModal(modalEl, modalInstance, currentOptions);
          return;
        }

        if (contentType.indexOf('application/json') >= 0) {
          var json = await response.json();
          if (json && json.success) {
            closeAjaxModal(modalEl, modalInstance, currentOptions);
          } else {
            showToast((json && (json.message || json.error)) || 'İşlem başarısız.', 'danger');
          }
          return;
        }

        var htmlText = await response.text();
        bodyEl.innerHTML = getModalHtmlContent(htmlText);
        parseUnobtrusive(bodyEl);
      } catch (error) {
        showToast('İşlem sırasında hata oluştu.', 'danger');
      } finally {
        if (submitButton) submitButton.disabled = false;
      }
    });
  }

  document.addEventListener('DOMContentLoaded', function() {
    initSidebarToggle();
    initSidebarScroll();
    initDeleteButtons();
    initAjaxModalForms();
  });

  return {
    showToast: showToast,
    initDeleteButtons: initDeleteButtons
  };
})();
