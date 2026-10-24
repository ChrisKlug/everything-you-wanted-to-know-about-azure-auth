// Is this token real? The checks AddJwtBearer() does, one at a time: find the key by kid, verify the
// signature, then iss, aud and the lifetime. The signature check runs here in the browser, with WebCrypto.
const Validate = (() => {
    const modalElement = document.getElementById('validate-modal');
    const $ = id => document.getElementById(id);
    const headerOutput = $('validate-header');
    const jwksRequest = $('validate-jwks-request');
    const fetchButton = $('validate-fetch');
    const keysPanel = $('validate-keys');
    const codeOutput = $('validate-code');
    const runButton = $('validate-run');
    const verdict = $('validate-verdict');
    const skewSeconds = 300;

    // Who's checking decides what the audience has to be. An ID token is for my web. Any other token, for the
    // Calendar API, because that's the one I wrote. A Graph token is Graph's business.
    const expectedAudience = name => name === 'id_token'
        ? { value: Wrh.page.clientId, label: "my web's client ID" }
        : { value: Wrh.page.calendarApiClientId, label: "the Calendar API's client ID" };

    let state;

    const parts = () => state.token.split('.');
    const short = (value = '', length = 12) => value.length > length ? `${value.slice(0, length)}…` : value;
    const findKey = () => state.jwks?.keys?.find(key => key.kid === state.header.kid);

    const verifySignature = async () => {
        const key = findKey();
        if (!key) return null;
        const [header, payload, signature] = parts();
        const cryptoKey = await crypto.subtle.importKey('jwk', { kty: key.kty, n: key.n, e: key.e, ext: true },
            { name: 'RSASSA-PKCS1-v1_5', hash: 'SHA-256' }, false, ['verify']);
        return crypto.subtle.verify('RSASSA-PKCS1-v1_5', cryptoKey, Tokens.base64UrlToBytes(signature),
            new TextEncoder().encode(`${header}.${payload}`));
    };

    // One entry per line of pseudo code. run() says true, false, or null for "couldn't check".
    const checks = () => {
        const [header, payload] = parts();
        const audience = expectedAudience(state.name);
        const issuer = state.config?.issuer ?? '?';
        const now = Date.now() / 1000;
        return [
            { name: 'key', comment: 'the key that signed it', code: `key = jwks.keys.find(k => k.kid === "${state.header.kid}")`,
                run: () => !!findKey() },
            { name: 'alg', comment: 'RSA with SHA-256', code: `header.alg === "RS256"`,
                run: () => state.header.alg === 'RS256' },
            { name: 'signature', comment: 'over the first two parts, exactly as they are', code: `verify(key, "${short(header)}.${short(payload)}", signature)`,
                run: verifySignature },
            { name: 'issuer', comment: 'issuer from the openid-configuration', code: `payload.iss === "${issuer}"`,
                run: () => state.payload.iss === issuer },
            { name: 'audience', comment: audience.label, code: `payload.aud === "${audience.value || '?'}"`,
                run: () => !!audience.value && state.payload.aud === audience.value },
            { name: 'lifetime', comment: 'give or take five minutes', code: 'payload.nbf <= now && now < payload.exp',
                run: () => (state.payload.nbf ?? 0) - skewSeconds <= now && now < state.payload.exp + skewSeconds },
        ];
    };

    const line = (className, html = '') => {
        const element = document.createElement('span');
        element.className = className;
        element.innerHTML = html;
        return element;
    };

    const highlight = text => hljs.highlight(text, { language: 'javascript', ignoreIllegals: true }).value;

    const renderCode = (results = []) => {
        codeOutput.replaceChildren();
        codeOutput.classList.add('hljs');
        checks().forEach((check, i) => {
            const result = results[i];
            const label = result === true ? ' <span class="check-label pass">Valid</span>'
                : result === false ? ' <span class="check-label fail">Invalid</span>' : '';
            codeOutput.append(line('code-line code-comment', `<span class="code-gutter"></span>${highlight(`// ${check.comment}`)}${label}`));
            const mark = result === true ? '✓' : result === false ? '✗' : result === null ? '–' : '';
            const row = line(`code-line ${result === true ? 'pass' : result === false ? 'fail' : ''}`,
                `<span class="code-gutter">${mark}</span>${highlight(check.code)}`);
            codeOutput.append(row);
        });
    };

    const renderHeader = () => {
        Wrh.renderJson(headerOutput, state.header);
        headerOutput.querySelectorAll('.json-line').forEach(element => {
            if (/^\s*"(kid|alg)":/.test(element.textContent)) element.classList.add('json-important');
        });
    };

    // The keys, one line each: kty, use and kid. A click shows the whole key. The one the header's kid points at is
    // highlighted. The long values in it (the modulus, the certificates) are clamped too.
    const preview = key => `{ ${['kty', 'use', 'kid'].filter(name => key[name] !== undefined)
        .map(name => `"${name}": ${JSON.stringify(key[name])}`).join(', ')} … }`;

    const jsonLine = (text, className = '') => {
        const element = document.createElement('span');
        element.className = `json-line ${className}`;
        element.innerHTML = hljs.highlight(text, { language: 'json', ignoreIllegals: true }).value;
        return element;
    };

    const renderKeys = () => {
        keysPanel.classList.toggle('d-none', !state.jwksResponse);
        $('validate-checks').classList.toggle('d-none', !state.jwks);
        if (!state.jwksResponse) return;
        const container = $('validate-jwks-response');
        if (!state.jwks) {
            Wrh.renderResponse(container, state.jwksResponse);
            return;
        }

        const status = document.createElement('span');
        status.className = 'response-status ok';
        status.textContent = `${state.jwksResponse.status} ${state.jwksResponse.statusText ?? ''}`.trim();
        const panel = document.createElement('pre');
        panel.className = 'mono-panel json-panel mt-2';
        const code = document.createElement('code');
        code.className = 'hljs';
        panel.append(code);
        container.replaceChildren(status, panel);

        code.append(jsonLine('{'), jsonLine('  "keys": ['));
        state.jwks.keys.forEach((key, i) => {
            const comma = i < state.jwks.keys.length - 1 ? ',' : '';
            const match = key.kid === state.header.kid;
            const item = document.createElement('span');
            item.className = `jwks-key${match ? ' json-important' : ''}`;
            item.title = 'Click to show the whole key';

            const collapsed = jsonLine(`    ${preview(key)}${comma}`);
            const expanded = document.createElement('span');
            expanded.className = 'd-none';
            (JSON.stringify(key, null, 2) + comma).split('\n').forEach(text => {
                const element = jsonLine(`    ${text}`, /^\s*"kid":/.test(text) && match ? 'json-key-kid' : '');
                if (text.length > 100) Wrh.makeExpandable(element);
                expanded.append(element);
            });
            if (match) collapsed.classList.add('json-key-kid');
            item.append(collapsed, expanded);

            // Clicking a clamped value opens that value, not the whole key.
            item.addEventListener('click', e => {
                if (!collapsed.contains(e.target) && e.target.closest('.expandable')) return;
                collapsed.classList.toggle('d-none');
                expanded.classList.toggle('d-none');
            });
            code.append(item);
        });
        code.append(jsonLine('  ]'), jsonLine('}'));
    };

    // The issuer and where the keys are come from the tenant's openid-configuration, fetched once.
    let config;
    const loadConfig = async () => {
        if (config) return config;
        const response = await Wrh.send({ method: 'GET', url: Wrh.openidConfiguration() });
        if (response.status === 200) config = JSON.parse(response.body);
        return config;
    };

    const open = async (name, token) => {
        if (!token) return;
        try {
            state = { name, token, header: Tokens.decodeJson(token.split('.')[0]), payload: Tokens.decodeJson(token.split('.')[1]) };
        } catch (e) {
            alert(`That doesn't decode as a JWT: ${e.message}`);
            return;
        }
        $('validate-name').textContent = name;
        state.config = await loadConfig().catch(() => null);

        const jwksUri = state.config?.jwks_uri;
        jwksRequest.classList.toggle('d-none', !jwksUri);
        fetchButton.classList.toggle('d-none', !jwksUri);
        if (jwksUri) {
            const url = new URL(jwksUri);
            Wrh.renderRequest(jwksRequest, { method: 'GET', url: [[url.origin], [url.pathname, true]] });
        }

        verdict.classList.add('d-none');
        renderHeader();
        renderKeys();
        renderCode();
        // Dialogs don't stack. Coming from Decode a token, that one closes.
        document.querySelectorAll('.modal.show').forEach(element => bootstrap.Modal.getInstance(element)?.hide());
        bootstrap.Modal.getOrCreateInstance(modalElement).show();
    };

    fetchButton.addEventListener('click', () => Wrh.busy(fetchButton, async () => {
        const response = await Wrh.send({ method: 'GET', url: state.config.jwks_uri });
        state.jwksResponse = response;
        state.jwks = response.status === 200 ? JSON.parse(response.body) : null;
        renderKeys();
    }));

    runButton.addEventListener('click', () => Wrh.busy(runButton, async () => {
        const results = [];
        for (const check of checks()) {
            try { results.push(await check.run()); } catch { results.push(false); }
        }
        renderCode(results);

        const failed = checks().filter((_, i) => results[i] !== true).map(check => check.name);
        verdict.className = `validate-verdict ${failed.length === 0 ? 'ok' : 'error'}`;
        verdict.textContent = failed.length === 0
            ? 'Valid. Entra signed it, my tenant issued it, it\'s for me, and it\'s still good.'
            : `Not valid: ${failed.join(', ')}.`;
    }));

    return { open };
})();
