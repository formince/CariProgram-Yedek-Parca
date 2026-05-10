const faturaWizard = {
    modal: null,
    dropZone: null,
    fileInput: null,
    currentData: null,

    open: function () {
        this.modal = new bootstrap.Modal(document.getElementById('modalFaturaWizard'));
        this.dropZone = document.getElementById('dropZone');
        this.fileInput = document.getElementById('faturaFile');
        
        // Reset state
        document.getElementById('wizardContent').innerHTML = `
            <div class="text-center py-5" id="dropZone">
                <div class="display-3 text-primary opacity-25 mb-3">upload_file</div>
                <h5 class="fw-bold">Fatura dosyasını buraya sürükleyin</h5>
                <p class="text-muted">veya dosya seçmek için tıklayın (.xml, .pdf, .jpg, .png)</p>
                <input type="file" id="faturaFile" class="d-none" accept=".xml,.pdf,.png,.jpg,.jpeg" />
                <button type="button" class="btn btn-primary-container rounded-pill px-4 mt-2" onclick="document.getElementById('faturaFile').click()">
                    Dosya Seç
                </button>
            </div>
        `;
        document.getElementById('btnSihirbazTamamla').classList.add('d-none');
        
        this.initEvents();
        this.modal.show();
    },

    initEvents: function () {
        const fileInput = document.getElementById('faturaFile');
        fileInput.addEventListener('change', (e) => this.handleFile(e.target.files[0]));

        const drp = document.getElementById('dropZone');
        drp.addEventListener('dragover', (e) => { e.preventDefault(); drp.classList.add('bg-light'); });
        drp.addEventListener('dragleave', () => drp.classList.remove('bg-light'));
        drp.addEventListener('drop', (e) => {
            e.preventDefault();
            drp.classList.remove('bg-light');
            this.handleFile(e.dataTransfer.files[0]);
        });
    },

    handleFile: function (file) {
        if (!file) return;
        
        const formData = new FormData();
        formData.append('file', file);

        document.getElementById('wizardContent').innerHTML = `
            <div class="text-center py-5">
                <div class="spinner-border text-primary mb-3" role="status"></div>
                <h5 class="fw-bold">Fatura analiz ediliyor...</h5>
                <p class="text-muted">GİB standartları ve yapay zeka entegrasyonu devrede.</p>
            </div>
        `;

        fetch('/FaturaAnaliz/AnalyzeXml', {
            method: 'POST',
            body: formData
        })
        .then(response => response.json())
        .then(res => {
            if (res.success) {
                this.currentData = res.data;
                this.renderResults();
            } else {
                alert(res.message);
                this.open();
            }
        })
        .catch(err => {
            console.error(err);
            alert("İşlem sırasında bir hata oluştu.");
            this.open();
        });
    },

    renderResults: function () {
        const data = this.currentData;
        let html = `
            <div class="alert alert-info py-2 d-flex align-items-center gap-2 rounded-3 border-0">
                <span class="material-symbols-outlined">info</span>
                <span><strong>${data.faturaNo}</strong> nolu fatura bulundu. (${data.tedarikciUnvan})</span>
            </div>
            <div class="table-responsive">
                <table class="table table-sm align-middle mt-3">
                    <thead class="table-light">
                        <tr>
                            <th>Fatura Ürün Adı</th>
                            <th>Miktar</th>
                            <th class="text-end">Birim Fiyat</th>
                            <th>Eşleşen Ürün (Sistem)</th>
                            <th class="text-center">Durum</th>
                        </tr>
                    </thead>
                    <tbody>
        `;

        data.satirlar.forEach((satir, index) => {
            const rowClass = satir.durum === 0 ? 'table-success' : (satir.durum === 1 ? 'table-warning' : 'table-danger');
            const statusIcon = satir.durum === 0 ? 'check_circle' : (satir.durum === 1 ? 'help' : 'error');
            
            html += `
                <tr id="row-${index}">
                    <td class="small fw-medium">${satir.faturaUrunAdi}</td>
                    <td>${satir.miktar}</td>
                    <td class="text-end fw-bold">${satir.birimFiyat.toLocaleString('tr-TR', { minimumFractionDigits: 2 })} TL</td>
                    <td>
                        <div class="d-flex align-items-center gap-2">
                            <span id="match-name-${index}" class="small">${satir.sistemUrunAdi || '<span class="text-danger">Bulunamadı</span>'}</span>
                            ${satir.durum === 2 ? `<button onclick="faturaWizard.openQuickAdd(${index})" class="btn btn-sm btn-primary-container p-1 rounded-circle" title="Hızlı Ekle"><span class="material-symbols-outlined fs-6">add</span></button>` : ''}
                        </div>
                    </td>
                    <td class="text-center">
                        <span class="material-symbols-outlined text-${satir.durum === 0 ? 'success' : (satir.durum === 1 ? 'warning' : 'danger')}">${statusIcon}</span>
                    </td>
                </tr>
            `;
        });

        html += `</tbody></table></div>`;
        document.getElementById('wizardContent').innerHTML = html;
        document.getElementById('btnSihirbazTamamla').classList.remove('d-none');
    },

    openQuickAdd: function(index) {
        const satir = this.currentData.satirlar[index];
        
        const quickAddHtml = `
            <div class="card bg-surface-container-low border-0 shadow-sm mb-4 mt-2 mx-2 rounded-4 overflow-hidden">
                <div class="card-body p-4">
                    <div class="d-flex align-items-center gap-2 mb-3">
                        <span class="material-symbols-outlined text-primary">add_circle</span>
                        <h6 class="fw-bold mb-0">Hızlı Ürün Tanımlama</h6>
                    </div>
                    
                    <div class="row g-3">
                        <div class="col-md-6">
                            <label class="form-label-caps extra-small">Ürün Adı</label>
                            <input type="text" id="qa-ad" class="form-control-ghost w-100" value="${satir.faturaUrunAdi}">
                        </div>
                        <div class="col-md-3">
                            <label class="form-label-caps extra-small">Barkod / SKU</label>
                            <input type="text" id="qa-barkod" class="form-control-ghost w-100" value="${satir.barkod || ''}">
                        </div>
                        <div class="col-md-3">
                            <label class="form-label-caps extra-small">Marka / Kategori</label>
                            <input type="text" id="qa-category" class="form-control-ghost w-100" value="Genel">
                        </div>
                        
                        <div class="col-md-4">
                            <div class="p-3 bg-white rounded-4 border border-surface-container shadow-sm">
                                <label class="form-label-caps text-primary extra-small">Satış Fiyatı (KDV Dahil)</label>
                                <div class="input-group">
                                    <input type="number" id="qa-satis" class="form-control border-0 bg-transparent fw-bold" value="${(satir.birimFiyat * 1.5).toFixed(2)}">
                                    <span class="input-group-text bg-transparent border-0 fw-bold">₺</span>
                                </div>
                            </div>
                        </div>
                        <div class="col-md-4">
                            <div class="p-3 bg-white rounded-4 border border-surface-container shadow-sm">
                                <label class="form-label-caps text-muted extra-small">Alış Fiyatı (Maliyet)</label>
                                <div class="input-group">
                                    <input type="number" id="qa-alis" class="form-control border-0 bg-transparent fw-bold" value="${satir.birimFiyat}">
                                    <span class="input-group-text bg-transparent border-0 fw-bold">₺</span>
                                </div>
                            </div>
                        </div>
                        <div class="col-md-4">
                            <div class="p-3 bg-white rounded-4 border border-surface-container shadow-sm">
                                <label class="form-label-caps text-success extra-small">Mevcut Stok</label>
                                <div class="input-group">
                                    <input type="number" id="qa-stok" class="form-control border-0 bg-transparent fw-bold" value="0">
                                    <span class="input-group-text bg-transparent border-0 fw-bold material-symbols-outlined fs-6">inventory_2</span>
                                </div>
                            </div>
                        </div>

                        <div class="col-md-6">
                            <label class="form-label-caps extra-small">KDV (%)</label>
                            <select id="qa-kdv" class="form-select-ghost w-100">
                                <option value="0" ${satir.kdvOrani == 0 ? 'selected' : ''}>%0</option>
                                <option value="1" ${satir.kdvOrani == 1 ? 'selected' : ''}>%1</option>
                                <option value="10" ${satir.kdvOrani == 10 ? 'selected' : ''}>%10</option>
                                <option value="20" ${satir.kdvOrani == 20 ? 'selected' : ''}>%20</option>
                            </select>
                        </div>

                        <div class="col-md-6 d-flex align-items-end justify-content-end gap-2">
                             <button type="button" class="btn btn-outline-secondary rounded-pill px-3 extra-small" onclick="this.closest('tr').remove()">İptal</button>
                             <button type="button" class="btn btn-primary rounded-pill px-4 fw-bold shadow-sm d-flex align-items-center gap-2" onclick="faturaWizard.saveQuickAdd(${index})">
                                <span class="material-symbols-outlined fs-5">save</span> Ürünü Kaydet
                            </button>
                        </div>
                    </div>
                </div>
            </div>
        `;
        
        const row = document.getElementById(`row-${index}`);
        const newRow = document.createElement('tr');
        newRow.innerHTML = `<td colspan="5">${quickAddHtml}</td>`;
        row.parentNode.insertBefore(newRow, row.nextSibling);
    },

    saveQuickAdd: function(index) {
        const payload = {
            ad: document.getElementById('qa-ad').value,
            barkod: document.getElementById('qa-barkod').value,
            kategori: document.getElementById('qa-category').value,
            kdvOrani: parseInt(document.getElementById('qa-kdv').value),
            alisFiyati: parseFloat(document.getElementById('qa-alis').value),
            birimFiyat: parseFloat(document.getElementById('qa-satis').value),
            stokAdedi: parseInt(document.getElementById('qa-stok').value) || 0
        };

        fetch('/FaturaAnaliz/QuickCreateProduct', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload)
        })
        .then(r => r.json())
        .then(res => {
            if (res.success) {
                // Update local data
                this.currentData.satirlar[index].sistemUrunId = res.urunId;
                this.currentData.satirlar[index].sistemUrunAdi = res.urunAdi;
                this.currentData.satirlar[index].durum = 0; // Success
                this.renderResults();
            } else {
                alert(res.message);
            }
        });
    },

    applyToForm: function () {
        // We will store this in localStorage and redirect to Alis/Form
        localStorage.setItem('faturaWizardDraft', JSON.stringify(this.currentData));
        window.location.href = '/Alis/Form';
    }
};
