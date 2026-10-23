// Coming back from Entra lands here with the response in the form body, the #fragment or the ?query.
(() => {
    const posted = document.getElementById('posted-parameters');
    const fragment = new URLSearchParams(window.location.hash.substring(1));
    const query = new URLSearchParams(window.location.search);

    let mode, params;
    if (posted) {
        mode = 'form';
        params = Object.entries(JSON.parse(posted.textContent));
    } else if (window.location.pathname.endsWith('/callback') && fragment.size > 0) {
        mode = 'fragment';
        params = [...fragment];
    } else if (window.location.pathname.endsWith('/callback') && query.size > 0) {
        mode = 'query';
        params = [...query];
    }

    if (!mode) {
        Wrh.restoreTab();
        return;
    }

    const values = Object.fromEntries(params);
    Wrh.update({
        callback: {
            mode,
            method: posted ? 'POST' : 'GET',
            url: `${window.location.origin}${window.location.pathname}`,
            params,
            stateMatches: values.state !== undefined && values.state === Wrh.context.request?.state,
        },
        tokens: { id_token: values.id_token, access_token: values.access_token, code: values.code },
    });

    // Get the tokens out of the address bar, and make a reload not post the response again.
    window.history.replaceState(null, '', window.location.pathname);
    Wrh.showTab('callback');
})();
