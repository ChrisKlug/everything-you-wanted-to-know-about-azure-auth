// Back from Entra, with the response in the form body, the #fragment or the ?query. Note it, and go back to the step.
(() => {
    const posted = document.getElementById('posted-parameters');
    const fragment = new URLSearchParams(window.location.hash.substring(1));
    const query = new URLSearchParams(window.location.search);

    const [mode, params] = posted ? ['form', Object.entries(JSON.parse(posted.textContent))]
        : fragment.size > 0 ? ['fragment', [...fragment]]
        : ['query', [...query]];

    Wrh.finishCallback({
        mode,
        method: posted ? 'POST' : 'GET',
        url: `${window.location.origin}${window.location.pathname}`,
        params,
    });
})();
