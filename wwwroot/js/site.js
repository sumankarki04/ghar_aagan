// Ghar Aagan site scripts

// Clickable table rows: <tr class="ga-row-link" data-href="/url">.
// Clicks on inner links/buttons/forms keep their own behavior.
document.addEventListener('click', (event) => {
    const row = event.target.closest('.ga-row-link[data-href]');
    if (!row) return;
    if (event.target.closest('a, button, form, input, select, label')) return;
    window.location.href = row.dataset.href;
});
