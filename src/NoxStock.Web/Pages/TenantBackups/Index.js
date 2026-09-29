(function () {
    const rows = document.getElementById('backupRows');
    const cloneForm = document.getElementById('cloneForm');
    if (cloneForm) {
        cloneForm.addEventListener('submit', function () {
            const status = document.getElementById('cloneStatus');
            if (status) status.textContent = 'Clone işi kuyruğa alınıyor…';
            cloneForm.querySelectorAll('button').forEach(button => button.disabled = true);
        });
        const operation = document.getElementById('cloneOperation');
        if (operation && operation.dataset.operationId) {
            const names = ['—', 'Sırada', 'Doğrulanıyor', 'Tenant oluşturuluyor', 'Ana veriler aktarılıyor', 'Ticari veriler aktarılıyor', 'Doğrulanıyor', 'Tamamlandı', 'Başarısız', 'İptal edildi'];
            const status = document.getElementById('cloneOperationStatus');
            const failure = document.getElementById('cloneOperationFailure');
            const poll = async () => {
                const response = await fetch(abp.appPath + `api/app/tenant-restore/clone-operation/${operation.dataset.operationId}`, { credentials: 'same-origin' });
                if (!response.ok) return;
                const item = await response.json();
                if (status) status.textContent = `Durum: ${names[item.status] || item.status}; işlenen kayıt: ${item.processedRecordCount}.`;
                if (failure) failure.textContent = item.failureCode || '';
                if (item.status < 7) window.setTimeout(poll, 2000);
            };
            window.setTimeout(poll, 1000);
        }
        return;
    }
    if (!rows) return;
    const statusNames = ['—', 'Sırada', 'Hazırlanıyor', 'Hazır', 'Başarısız'];

    async function request(url, options) {
        const response = await fetch(abp.appPath + url.replace(/^\//, ''), Object.assign({
            credentials: 'same-origin',
            headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': abp.security.antiForgery.getToken() }
        }, options || {}));
        if (!response.ok) throw new Error((await response.text()) || 'İşlem tamamlanamadı.');
        return response.status === 204 ? null : response.json();
    }

    async function load() {
        const result = await request('/api/app/tenant-backup?skipCount=0&maxResultCount=50');
        rows.innerHTML = '';
        for (const item of result.items) {
            const tr = document.createElement('tr');
            const ready = item.status === 3;
            tr.innerHTML = `<td>${new Date(item.requestedAtUtc).toLocaleString()}</td><td>${statusNames[item.status] || item.status}</td>` +
                `<td>${item.fileSize ? Math.ceil(item.fileSize / 1024) + ' KB' : '—'}</td><td>${new Date(item.expiresAtUtc).toLocaleString()}</td>` +
                `<td class="text-end"><button class="btn btn-sm btn-outline-primary download" ${ready ? '' : 'disabled'}>İndir</button> ` +
                `<button class="btn btn-sm btn-outline-danger delete">Sil</button></td>`;
            tr.querySelector('.download').onclick = async () => {
                const token = await request(`/api/app/tenant-backup/${item.id}/download-token`);
                window.location.href = `${abp.appPath}api/app/tenant-backup/${item.id}/download?token=${encodeURIComponent(token.token)}`;
            };
            tr.querySelector('.delete').onclick = async () => { await request(`/api/app/tenant-backup/${item.id}`, { method: 'DELETE' }); await load(); };
            rows.appendChild(tr);
        }
        if (!result.items.length) rows.innerHTML = '<tr><td colspan="5" class="text-muted">Henüz yedek yok.</td></tr>';
    }

    document.getElementById('createBackup').onclick = async () => {
        const password = window.prompt('Yedek parolası (en az 12 karakter):');
        if (!password) return;
        try { await request('/api/app/tenant-backup', { method: 'POST', body: JSON.stringify({ password }) }); await load(); }
        catch (error) { abp.message.error(error.message); }
    };
    load().catch(error => { rows.innerHTML = `<tr><td colspan="5" class="text-danger">${error.message}</td></tr>`; });
    window.setInterval(() => load().catch(() => {}), 10000);
})();
