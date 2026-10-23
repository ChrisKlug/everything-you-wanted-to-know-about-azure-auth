// Tab 2: send the user to Entra. The URL rebuilds on every change.
(() => {
    const form = document.getElementById('challenge-form');
    const output = document.getElementById('challenge-request');
    const sendButton = document.getElementById('challenge-send');
    const pkce = document.getElementById('challenge-pkce');
    const pkceOutput = document.getElementById('challenge-pkce-output');
    const storageKey = 'what-really-happens:challenge';

    const randomValue = () => {
        const bytes = crypto.getRandomValues(new Uint8Array(12));
        return Array.from(bytes, b => b.toString(16).padStart(2, '0')).join('');
    };

    // Entra requires a nonce whenever an ID token is requested. State and nonce are new for every request.
    const state = randomValue();
    const nonce = randomValue();

    // PKCE: the verifier stays in the browser, only its hash goes to Entra. The exchange sends the verifier itself,
    // so only whoever started the sign-in can finish it.
    const base64Url = bytes => btoa(String.fromCharCode(...bytes)).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
    const codeVerifier = base64Url(crypto.getRandomValues(new Uint8Array(32)));
    let codeChallenge = '';

    // Drop-downs read and write through form.elements[name].value. Scopes and PKCE are checkboxes.
    const selects = ['response_type', 'response_mode', 'prompt'];
    const scopeBoxes = () => [...form.querySelectorAll('input[name="scope"]')];
    const pkceBox = form.elements.pkce;

    // Scopes go in the order they're ticked, not the order on the page, so the order can be played with.
    // openid can't be unticked and always goes first.
    let scopeOrder = scopeBoxes().filter(box => box.checked).map(box => box.value);

    const choices = () => ({
        scope: scopeOrder.join(' '),
        ...Object.fromEntries(selects.map(name => [name, form.elements[name].value])),
        pkce: pkceBox.checked,
    });

    try {
        const saved = JSON.parse(localStorage.getItem(storageKey) ?? '{}');
        if (typeof saved.scope === 'string') {
            const scopes = saved.scope.split(' ');
            scopeBoxes().forEach(box => box.checked = box.disabled || scopes.includes(box.value));
            const known = scopeBoxes().filter(box => box.checked).map(box => box.value);
            scopeOrder = [...new Set(['openid', ...scopes])].filter(scope => known.includes(scope));
        }
        for (const name of selects) {
            if (saved[name] !== undefined) form.elements[name].value = saved[name];
        }
        if (typeof saved.pkce === 'boolean') pkceBox.checked = saved.pkce;
    } catch { /* nothing saved */ }

    const save = () => {
        try {
            localStorage.setItem(storageKey, JSON.stringify(choices()));
        } catch { /* storage unavailable */ }
    };

    scopeBoxes().forEach(box => box.addEventListener('input', () => {
        scopeOrder = scopeOrder.filter(scope => scope !== box.value);
        if (box.checked) scopeOrder.push(box.value);
    }));

    const value = name => choices()[name];

    // PKCE protects the code, so it only applies when a code is asked for.
    const asksForCode = () => value('response_type').split(' ').includes('code');
    const usesPkce = () => asksForCode() && pkceBox.checked;

    const params = () => [
        { name: 'client_id', value: Wrh.page.clientId || '<missing>' },
        { name: 'response_type', value: value('response_type'), important: true },
        { name: 'redirect_uri', value: Wrh.page.redirectUri },
        { name: 'response_mode', value: value('response_mode'), important: true },
        { name: 'scope', value: value('scope'), important: true },
        { name: 'state', value: state },
        { name: 'nonce', value: nonce },
        ...(value('prompt') ? [{ name: 'prompt', value: value('prompt'), important: true }] : []),
        ...(usesPkce() ? [
            { name: 'code_challenge', value: codeChallenge, important: true },
            { name: 'code_challenge_method', value: 'S256', important: true },
        ] : []),
    ];

    const endpoint = () => Wrh.context.config?.authorization_endpoint ?? '<authorization_endpoint>';

    const buildUrl = () => `${endpoint()}?${params().map(p => `${p.name}=${encodeURIComponent(p.value)}`).join('&')}`;

    const renderPkce = () => {
        pkceBox.disabled = !asksForCode();
        pkce.classList.toggle('d-none', !usesPkce());
        const line = (name, value) => {
            const element = document.createElement('span');
            element.className = 'url-line';
            element.innerHTML = `<span class="url-name">${name}</span> = <span class="url-value">${value}</span>`;
            return element;
        };
        pkceOutput.replaceChildren(
            line('code_verifier ', codeVerifier),
            line('code_challenge', 'BASE64URL(SHA256(code_verifier))'),
            line('              ', codeChallenge));
    };

    const render = () => {
        Wrh.renderRequest(output, { method: 'GET', url: [[endpoint()]], params: params() });
        renderPkce();
    };

    form.addEventListener('input', () => { save(); render(); });
    form.addEventListener('change', () => { save(); render(); });
    form.addEventListener('submit', e => e.preventDefault());
    Wrh.onChange(render);

    crypto.subtle.digest('SHA-256', new TextEncoder().encode(codeVerifier)).then(hash => {
        codeChallenge = base64Url(new Uint8Array(hash));
        render();
    });

    sendButton.addEventListener('click', () => {
        const url = buildUrl();
        // A new sign-in starts a new story. Forget what came back last time.
        Wrh.update({
            request: {
                state, nonce, ...choices(),
                ...(usesPkce() ? { code_verifier: codeVerifier } : {}),
            },
            callback: null, tokens: {}, exchangeResponse: null, apiResponse: null, refreshResponse: null, refreshTokens: null, oboResponse: null,
        });
        window.location.assign(url);
    });
})();
