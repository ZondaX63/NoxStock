(function () {
    const rows = document.getElementById('backupRows');
    const cloneForm = document.getElementById('cloneForm');
    const asText = (value) => String(value ?? '');

    if (cloneForm) {
        cloneForm.addEventListener('submit', function () {
            const status = document.getElementById('cloneStatus');
            if (status) status.textContent = 'Clone işi güvenli biçimde kuyruğa alınıyor…';
            cloneForm.querySelectorAll('button').forEach(button => button.disabled = true);
        });

        const operation = document.getElementById('cloneOperation');
        if (operation?.dataset.operationId) {
            const names = ['—', 'Sırada', 'Doğrulanıyor', 'Tenant oluşturuluyor', 'Ana veriler aktarılıyor', 'Ticari veriler aktarılıyor', 'Doğrulanıyor', 'Tamamlandı', 'Başarısız', 'İptal edildi'];
            const status = document.getElementById('cloneOperationStatus');
            const failure = document.getElementById('cloneOperationFailure');
            const poll = async () => {
                const response = await fetch(abp.appPath + 'api/app/tenant-restore/clone-operation/' + operation.dataset.operationId, { credentials: 'same-origin' });
                if (!response.ok) return;
                const item = await response.json();
                if (status) status.textContent = 'Durum: ' + (names[item.status] || item.status) + '; işlenen kayıt: ' + item.processedRecordCount + '.';
                if (failure) failure.textContent = item.failureCode || '';
                if (item.status < 7) window.setTimeout(poll, 2000);
            };
            window.setTimeout(poll, 1000);
        }
        return;
    }

    if (!rows) return;
    const statusNames = ['—', 'Sırada', 'Hazırlanıyor', 'Hazır', 'Başarısız'];
    const statusStyles = ['bg-slate-100 text-slate-600', 'bg-amber-100 text-amber-800', 'bg-indigo-100 text-indigo-800', 'bg-emerald-100 text-emerald-800', 'bg-rose-100 text-rose-800'];
    const refreshState = document.getElementById('backupRefreshState');
    const createButton = document.getElementById('createBackup');
    const confirmCreateButton = document.getElementById('confirmCreateBackup');
    const passwordInput = document.getElementById('BackupPassword');
    const passwordError = document.getElementById('backupPasswordError');
    const modalElement = document.getElementById('CreateBackupModal');
    const backupModal = modalElement && window.bootstrap ? bootstrap.Modal.getOrCreateInstance(modalElement) : null;

    async function request(url, options) {
        const response = await fetch(abp.appPath + url.replace(/^\//, ''), Object.assign({
            credentials: 'same-origin',
            headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': abp.security.antiForgery.getToken() }
        }, options || {}));
        if (response.ok) return response.status === 204 ? null : response.json();
        const body = await response.text();
        try {
            const parsed = JSON.parse(body);
            const error = parsed.error || parsed;
            throw new Error(error.validationErrors?.[0]?.message || error.message || 'İşlem tamamlanamadı.');
        } catch (error) {
            if (error instanceof Error && error.message !== 'İşlem tamamlanamadı.') throw error;
            throw new Error('İşlem tamamlanamadı. Lütfen tekrar deneyin.');
        }
    }

    function appendCell(row, value, className) {
        const cell = document.createElement('td');
        cell.className = 'px-5 py-4 ' + (className || '');
        cell.textContent = value;
        row.appendChild(cell);
        return cell;
    }
    function formatDate(value) {
        const date = new Date(value);
        return Number.isNaN(date.valueOf()) ? '—' : date.toLocaleString();
    }
    function badge(status) {
        const element = document.createElement('span');
        element.className = 'inline-flex rounded-full px-2.5 py-1 text-xs font-bold ' + (statusStyles[status] || statusStyles[0]);
        element.textContent = statusNames[status] || asText(status);
        return element;
    }

    async function load() {
        const result = await request('/api/app/tenant-backup?skipCount=0&maxResultCount=50');
        rows.replaceChildren();
        for (const item of result.items) {
            const row = document.createElement('tr');
            row.className = 'bg-white dark:bg-slate-800';
            appendCell(row, formatDate(item.requestedAtUtc), 'whitespace-nowrap text-slate-700 dark:text-slate-200');
            const statusCell = appendCell(row, '');
            statusCell.appendChild(badge(item.status));
            appendCell(row, item.fileSize ? Math.ceil(item.fileSize / 1024) + ' KB' : 'Hazırlanıyor…', 'text-slate-500 dark:text-slate-400');
            appendCell(row, formatDate(item.expiresAtUtc), 'whitespace-nowrap text-slate-500 dark:text-slate-400');
            const actions = appendCell(row, '', 'whitespace-nowrap text-right');
            const download = document.createElement('button');
            download.type = 'button';
            download.className = 'mr-2 inline-flex h-8 items-center rounded-md border border-indigo-200 px-3 text-xs font-semibold text-indigo-700 hover:bg-indigo-50 disabled:cursor-not-allowed disabled:opacity-40 dark:border-indigo-900 dark:text-indigo-300';
            download.textContent = 'İndir';
            download.disabled = item.status !== 3;
            download.addEventListener('click', async () => {
                try {
                    download.disabled = true;
                    const token = await request('/api/app/tenant-backup/' + item.id + '/download-token');
                    window.location.assign(abp.appPath + 'api/app/tenant-backup/' + item.id + '/download?token=' + encodeURIComponent(token.token));
                } catch (error) {
                    abp.message.error(error.message);
                    download.disabled = false;
                }
            });
            const remove = document.createElement('button');
            remove.type = 'button';
            remove.className = 'inline-flex h-8 items-center rounded-md border border-rose-200 px-3 text-xs font-semibold text-rose-700 hover:bg-rose-50 dark:border-rose-900 dark:text-rose-300';
            remove.textContent = 'Sil';
            remove.addEventListener('click', async () => {
                const confirmed = await abp.message.confirm('Bu yedek paketi kalıcı olarak silinecek. Devam edilsin mi?', 'Yedeği sil');
                if (!confirmed) return;
                try {
                    remove.disabled = true;
                    await request('/api/app/tenant-backup/' + item.id, { method: 'DELETE' });
                    await load();
                } catch (error) {
                    abp.message.error(error.message);
                    remove.disabled = false;
                }
            });
            actions.append(download, remove);
            rows.appendChild(row);
        }
        if (!result.items.length) {
            const row = document.createElement('tr');
            const cell = appendCell(row, 'Henüz yedek yok. İlk şifreli paketinizi oluşturun.', 'py-10 text-center text-slate-500 dark:text-slate-400');
            cell.colSpan = 5;
            rows.appendChild(row);
        }
        if (refreshState) refreshState.textContent = 'Son güncelleme: ' + new Date().toLocaleTimeString();
    }

    function showPasswordError(message) {
        if (!passwordError) return;
        passwordError.textContent = message || '';
        passwordError.classList.toggle('hidden', !message);
    }
    createButton?.addEventListener('click', () => {
        if (passwordInput) passwordInput.value = '';
        showPasswordError('');
        if (backupModal) backupModal.show();
        else passwordInput?.focus();
    });
    confirmCreateButton?.addEventListener('click', async () => {
        const password = asText(passwordInput?.value).trim();
        if (password.length < 12) {
            showPasswordError('Paket parolası en az 12 karakter olmalıdır.');
            passwordInput?.focus();
            return;
        }
        showPasswordError('');
        confirmCreateButton.disabled = true;
        confirmCreateButton.textContent = 'Sıraya alınıyor…';
        try {
            await request('/api/app/tenant-backup', { method: 'POST', body: JSON.stringify({ password: password }) });
            backupModal?.hide();
            await load();
            abp.notify.success('Yedekleme isteği sıraya alındı. Durum bu sayfada otomatik güncellenecek.');
        } catch (error) {
            showPasswordError(error.message);
        } finally {
            confirmCreateButton.disabled = false;
            confirmCreateButton.textContent = 'Yedeği sıraya al';
        }
    });

    const resetConfirmation = document.getElementById('TenantDataResetConfirmation');
    const resetAcknowledge = document.getElementById('TenantDataResetAcknowledge');
    const resetSubmit = document.getElementById('TenantDataResetSubmit');
    const refreshResetButton = () => {
        if (!resetConfirmation || !resetAcknowledge || !resetSubmit) return;
        const confirmed = asText(resetConfirmation.value).trim().toLocaleUpperCase('tr-TR') === 'SIFIRLA';
        resetSubmit.disabled = !confirmed || !resetAcknowledge.checked;
    };
    resetConfirmation?.addEventListener('input', refreshResetButton);
    resetAcknowledge?.addEventListener('change', refreshResetButton);
    document.getElementById('TenantDataResetForm')?.addEventListener('submit', () => {
        if (resetSubmit) { resetSubmit.disabled = true; resetSubmit.textContent = 'Sıfırlanıyor…'; }
    });

    load().catch(error => {
        rows.replaceChildren();
        const row = document.createElement('tr');
        const cell = appendCell(row, error.message, 'py-8 text-center text-rose-700');
        cell.colSpan = 5;
        rows.appendChild(row);
        if (refreshState) refreshState.textContent = 'Yenileme başarısız';
    });
    window.setInterval(() => load().catch(() => {}), 10000);
})();
