// Tab 3: whatever Entra sent back, however it was sent.
(() => {
    const via = document.getElementById('callback-via');
    const stateBadge = document.getElementById('callback-state');
    const output = document.getElementById('callback-request');
    const error = document.getElementById('callback-error');
    const important = ['code', 'id_token', 'access_token', 'error', 'error_description'];
    const shortened = ['code', 'id_token', 'access_token'];
    const modeNames = { form: 'form_post', fragment: 'fragment', query: 'query' };

    Wrh.onChange(context => {
        const callback = context.callback;
        if (!callback) return;

        via.textContent = modeNames[callback.mode];
        stateBadge.textContent = callback.stateMatches ? 'state matches' : "state doesn't match";
        stateBadge.className = `badge mono fs-6 ${callback.stateMatches ? 'text-bg-success' : 'text-bg-danger'}`;

        Wrh.renderRequest(output, {
            method: callback.method,
            url: [[callback.url]],
            mode: callback.mode,
            params: callback.params.map(([name, value]) => ({
                name, value, important: important.includes(name), shorten: shortened.includes(name),
            })),
        });

        const values = Object.fromEntries(callback.params);
        error.classList.toggle('d-none', !values.error);
        document.getElementById('callback-error-code').textContent = values.error ?? '';
        document.getElementById('callback-error-description').textContent = values.error_description ?? '';

    });
})();
