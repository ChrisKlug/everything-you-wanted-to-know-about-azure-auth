// The parts a step is built from. Each one gets its own section and the step's store, and draws itself from it.
const Parts = (() => {
    const formHeaders = [{ name: 'Content-Type', value: 'application/x-www-form-urlencoded' }];

    // How an app proves who it is at the token endpoint. A secret, or (step 11) its managed identity's token.
    // Either way it's masked here and filled in by the server.
    const credential = (kind = 'secret') => kind === 'assertion'
        ? [
            { name: 'client_assertion_type', value: 'urn:ietf:params:oauth:client-assertion-type:jwt-bearer', important: true },
            { name: 'client_assertion', value: Wrh.secretMask, important: true },
        ]
        : [{ name: 'client_secret', value: Wrh.secretMask, important: true }];

    const tokensFrom = (response, names = ['id_token', 'access_token', 'refresh_token']) => {
        if (response.status !== 200) return {};
        const body = JSON.parse(response.body);
        return Object.fromEntries(names.filter(name => body[name]).map(name => [name, body[name]]));
    };

    // A POST to the token endpoint, drawn and sent the same way everywhere.
    const tokenRequest = ({ root, store, params, tenant, onResponse }) => {
        const output = Wrh.role(root, 'request');
        const sendButton = Wrh.role(root, 'send');
        const responseKey = `${root.dataset.section}Response`;

        store.onChange(state => {
            Wrh.renderRequest(output, {
                method: 'POST', url: [[Wrh.endpoint('token', tenant)]], headers: formHeaders, mode: 'form', params: params(state),
            });
            Wrh.role(root, 'result').classList.toggle('d-none', !state[responseKey]);
            if (state[responseKey]) Wrh.renderResponse(Wrh.role(root, 'response'), state[responseKey]);
        });

        sendButton.addEventListener('click', () => Wrh.busy(sendButton, async () => {
            const state = store.state;
            const response = await Wrh.send({
                method: 'POST',
                url: Wrh.endpoint('token', tenant),
                form: Object.fromEntries(params(state).map(p => [p.name, p.value])),
            });
            store.update({ [responseKey]: response, ...onResponse(response, state) });
            // Straight to the answer, past any section the tokens just unfolded below it.
            Wrh.role(root, 'result').scrollIntoView({ behavior: 'smooth', block: 'start' });
        }));
    };

    // Where is everything? The tenant's OpenID configuration. Every library starts here.
    const config = (root, store) => {
        const tenant = Wrh.role(root, 'tenant');
        const output = Wrh.role(root, 'request');
        const sendButton = Wrh.role(root, 'send');
        const path = '/v2.0/.well-known/openid-configuration';
        const interesting = ['issuer', 'authorization_endpoint', 'token_endpoint', 'jwks_uri'];

        if (store.state.tenant) tenant.value = store.state.tenant;
        const render = () => Wrh.renderRequest(output, {
            method: 'GET',
            url: [[Wrh.page.instance], [tenant.value.trim() || '<tenant>', true], [path]],
        });
        tenant.addEventListener('input', render);
        render();

        sendButton.addEventListener('click', () => Wrh.busy(sendButton, async () => {
            const response = await Wrh.send({ method: 'GET', url: `${Wrh.page.instance}${tenant.value.trim()}${path}` });
            store.update({ tenant: tenant.value.trim(), configResponse: response, config: response.status === 200 ? JSON.parse(response.body) : null });
            // Straight to the answer, so the presenter doesn't have to scroll.
            Wrh.role(root, 'result').scrollIntoView({ behavior: 'smooth', block: 'start' });
        }));

        store.onChange(state => {
            Wrh.role(root, 'result').classList.toggle('d-none', !state.configResponse);
            if (!state.configResponse) return;
            Wrh.renderResponse(Wrh.role(root, 'response'), state.configResponse);
            // The lines the table below pulls out, so they can be found in the whole document first. Top level only:
            // mtls_endpoint_aliases has a token_endpoint of its own.
            for (const line of Wrh.role(root, 'response').querySelectorAll('.json-line')) {
                if (interesting.includes(line.textContent.match(/^ {2}"([^"]+)":/)?.[1])) line.classList.add('json-important');
            }
            const summary = Wrh.role(root, 'summary');
            summary.replaceChildren();
            for (const name of interesting) {
                if (!state.config?.[name]) continue;
                const row = summary.insertRow();
                row.insertCell().textContent = name;
                row.insertCell().textContent = state.config[name];
            }
        });
    };

    // Send the user to Entra. The request rebuilds on every change. What's chosen is remembered per step.
    const challenge = (root, store, stepName, { tenant } = {}) => {
        const form = Wrh.role(root, 'form');
        const output = Wrh.role(root, 'request');
        const pkceOption = Wrh.role(root, 'pkce-option');
        const pkcePanel = Wrh.role(root, 'pkce');
        const pkceOutput = Wrh.role(root, 'pkce-output');
        const sendButton = Wrh.role(root, 'send');
        const storageKey = `wrh:challenge:${stepName}`;

        // Entra requires a nonce whenever an ID token is requested. State and nonce are new for every request.
        const state = Wrh.randomValue();
        const nonce = Wrh.randomValue();

        // PKCE: the verifier stays here, only its hash goes to Entra. The exchange sends the verifier itself,
        // so only whoever started the sign-in can finish it.
        const codeVerifier = Wrh.base64Url(crypto.getRandomValues(new Uint8Array(32)));
        let codeChallenge = '';

        const selects = ['response_type', 'response_mode', 'prompt'].filter(name => form.elements[name]);
        const scopeBoxes = () => [...form.querySelectorAll('input[name="scope"]')];
        const pkceBox = form.elements.pkce;

        // Scopes go in the order they're ticked, so the order can be played with. openid always goes first.
        let scopeOrder = scopeBoxes().filter(box => box.checked).map(box => box.value);

        const choices = () => ({
            scope: scopeOrder.join(' '),
            ...Object.fromEntries(selects.map(name => [name, form.elements[name].value])),
            pkce: !!pkceBox?.checked,
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
                const options = form.elements[name].options;
                if (saved[name] !== undefined && options && [...options].some(o => o.value === saved[name])) {
                    form.elements[name].value = saved[name];
                }
            }
            if (typeof saved.pkce === 'boolean' && pkceBox) pkceBox.checked = saved.pkce;
        } catch { /* nothing saved */ }

        const save = () => {
            try { localStorage.setItem(storageKey, JSON.stringify(choices())); } catch { /* storage unavailable */ }
        };

        scopeBoxes().forEach(box => box.addEventListener('input', () => {
            scopeOrder = scopeOrder.filter(scope => scope !== box.value);
            if (box.checked) scopeOrder.push(box.value);
        }));

        const value = name => choices()[name];
        const responseType = () => value('response_type') ?? 'code';

        // PKCE protects the code, so it only applies when a code is asked for.
        const asksForCode = () => responseType().split(' ').includes('code');
        const usesPkce = () => asksForCode() && !!pkceBox?.checked;

        const params = () => [
            { name: 'client_id', value: Wrh.page.clientId || '<missing>' },
            { name: 'response_type', value: responseType(), important: true },
            { name: 'redirect_uri', value: Wrh.redirectUri },
            ...(value('response_mode') ? [{ name: 'response_mode', value: value('response_mode'), important: true }] : []),
            { name: 'scope', value: value('scope'), important: true },
            { name: 'state', value: state },
            { name: 'nonce', value: nonce },
            ...(value('prompt') ? [{ name: 'prompt', value: value('prompt'), important: true }] : []),
            ...(usesPkce() ? [
                { name: 'code_challenge', value: codeChallenge, important: true },
                { name: 'code_challenge_method', value: 'S256', important: true },
            ] : []),
        ];

        const authorize = Wrh.endpoint('authorize', tenant);
        const buildUrl = () => `${authorize}?${params().map(p => `${p.name}=${encodeURIComponent(p.value)}`).join('&')}`;

        const renderPkce = () => {
            // PKCE only protects a code, so the option is only there when one is asked for.
            pkceOption?.classList.toggle('d-none', !asksForCode());
            pkcePanel.classList.toggle('d-none', !usesPkce());
            const line = (name, text) => {
                const element = document.createElement('span');
                element.className = 'url-line';
                element.innerHTML = `<span class="url-name">${name}</span> = <span class="url-value">${text}</span>`;
                return element;
            };
            pkceOutput.replaceChildren(
                line('code_verifier ', codeVerifier),
                line('code_challenge', 'BASE64URL(SHA256(code_verifier))'),
                line('              ', codeChallenge));
        };

        // The tenant in the URL is bold when it isn't mine: that's the point of step 9.
        const urlParts = () => tenant
            ? [[Wrh.page.instance], [tenant, true], ['/oauth2/v2.0/authorize']]
            : [[authorize]];

        // Tokens never go in the query string: they'd end up in server logs and browser history.
        const renderResponseModes = () => {
            const select = form.elements.response_mode;
            if (!select?.options) return;
            const query = [...select.options].find(o => o.value === 'query');
            if (!query) return;
            query.disabled = responseType().split(' ').some(type => type.endsWith('token'));
            if (query.disabled && query.selected) select.value = [...select.options].find(o => !o.disabled).value;
        };

        const render = () => {
            renderResponseModes();
            Wrh.renderRequest(output, { method: 'GET', url: urlParts(), params: params() });
            renderPkce();
        };

        form.addEventListener('input', () => { save(); render(); });
        form.addEventListener('change', () => { save(); render(); });
        form.addEventListener('submit', e => e.preventDefault());
        sendButton.disabled = !Wrh.page.clientId;

        crypto.subtle.digest('SHA-256', new TextEncoder().encode(codeVerifier)).then(hash => {
            codeChallenge = Wrh.base64Url(new Uint8Array(hash));
            render();
        });
        render();

        sendButton.addEventListener('click', () => {
            const url = buildUrl();
            // A new sign-in starts the step's story over, from the sign-in on. Where everything is stays known.
            const { tenant: configTenant, config, configResponse } = store.state;
            store.reset();
            store.update({ tenant: configTenant, config, configResponse, request:{ state, nonce, ...choices(), ...(usesPkce() ? { code_verifier: codeVerifier } : {}) } });
            Wrh.signIn(url);
        });
    };

    // Whatever Entra sent back. The step page takes it from the callback page when it loads.
    const callbackView = (root, store) => {
        const important = ['code', 'id_token', 'access_token', 'error', 'error_description'];
        const shortened = ['code', 'id_token', 'access_token'];
        const modeNames = { form: 'form_post', fragment: 'fragment', query: 'query' };

        store.onChange(({ callback, request }) => {
            if (!callback) return;
            const values = Object.fromEntries(callback.params);
            const stateMatches = values.state !== undefined && values.state === request?.state;

            Wrh.role(root, 'via').textContent = modeNames[callback.mode];
            const stateBadge = Wrh.role(root, 'state');
            stateBadge.textContent = stateMatches ? 'state matches' : "state doesn't match";
            stateBadge.className = `badge mono fs-6 ${stateMatches ? 'text-bg-success' : 'text-bg-danger'}`;

            Wrh.renderRequest(Wrh.role(root, 'response'), {
                method: callback.method,
                url: [[callback.url]],
                mode: callback.mode,
                params: callback.params.map(([name, value]) => ({
                    name, value, important: important.includes(name), shorten: shortened.includes(name),
                })),
            });

            Wrh.role(root, 'error').classList.toggle('d-none', !values.error);
            Wrh.role(root, 'error-code').textContent = values.error ?? '';
            Wrh.role(root, 'error-description').textContent = values.error_description ?? '';
        });
    };

    // Take what came back from the callback page, if anything, into the step's store.
    const receiveCallback = store => {
        const callback = Wrh.takeCallback();
        if (!callback) return false;
        const values = Object.fromEntries(callback.params);
        store.update({
            callback,
            tokens: Object.fromEntries(['code', 'id_token', 'access_token'].filter(name => values[name]).map(name => [name, values[name]])),
        });
        return true;
    };

    // Trade the code for tokens.
    const exchange = (root, store, { tenant, credential: kind } = {}) => tokenRequest({
        root, store, tenant,
        params: state => [
            { name: 'grant_type', value: 'authorization_code' },
            { name: 'client_id', value: Wrh.page.clientId },
            { name: 'code', value: state.tokens?.code ?? '', important: true, shorten: true },
            { name: 'redirect_uri', value: Wrh.redirectUri },
            ...(state.request?.code_verifier ? [{ name: 'code_verifier', value: state.request.code_verifier, important: true }] : []),
            ...credential(kind),
        ],
        onResponse: (response, state) => ({ tokens: { ...state.tokens, ...tokensFrom(response) } }),
    });

    // Refresh tokens are opaque and start much the same, so the end is what tells them apart.
    const shortenOpaque = token => `${token.slice(0, 12)} … ${token.slice(-32)}`;

    // Trade the refresh token for an access token, for any API the user has consented to. Entra sends a new
    // refresh token back as well, and the one sent is used up. The new access token is refreshed_token.
    const refresh = (root, store) => {
        const form = Wrh.role(root, 'form');
        const chain = Wrh.role(root, 'chain');
        const chainOf = state => state.refreshTokens ?? (state.tokens?.refresh_token ? [state.tokens.refresh_token] : []);

        tokenRequest({
            root, store,
            params: state => [
                { name: 'grant_type', value: 'refresh_token', important: true },
                { name: 'client_id', value: Wrh.page.clientId },
                { name: 'refresh_token', value: chainOf(state).at(-1) ?? '', important: true, shorten: true },
                { name: 'scope', value: form.elements.scope.value, important: true },
                ...credential(),
            ],
            onResponse: (response, state) => {
                const tokens = tokensFrom(response);
                const sent = chainOf(state);
                return {
                    tokens: { ...state.tokens, ...(tokens.access_token ? { refreshed_token: tokens.access_token } : {}) },
                    refreshTokens: tokens.refresh_token && tokens.refresh_token !== sent.at(-1) ? [...sent, tokens.refresh_token] : sent,
                };
            },
        });

        form.addEventListener('change', () => store.update({}));
        form.addEventListener('submit', e => e.preventDefault());

        let shownCount = 0;
        store.onChange(state => {
            const tokens = chainOf(state);
            const line = (token, i) => {
                const current = i === tokens.length - 1;
                const element = document.createElement('span');
                element.className = `refresh-token ${current ? 'current' : 'used'}`;
                const label = document.createElement('span');
                label.className = 'refresh-label';
                label.textContent = `#${i + 1} ${current ? 'current' : 'used'}`;
                const value = document.createElement('span');
                value.className = 'refresh-value';
                value.textContent = shortenOpaque(token);
                element.append(label, value);
                return element;
            };
            chain.replaceChildren(...tokens.map(line));
            if (tokens.length === 0) chain.textContent = 'No refresh token. Ask for offline_access.';
            if (shownCount > 0 && tokens.length > shownCount) {
                chain.lastElementChild.classList.add('changed');
                requestAnimationFrame(() => requestAnimationFrame(() => chain.lastElementChild?.classList.remove('changed')));
            }
            shownCount = tokens.length;
        });
    };

    // On-Behalf-Of: the Calendar API trades the token it was sent for a Graph token, as the same user. It's the API's
    // request, so the API's client_id and secret. The Graph token is obo_token.
    const onBehalfOf = (root, store) => {
        const warning = Wrh.role(root, 'wrong-audience');
        const audience = token => { try { return Tokens.decodeJson(token.split('.')[1]).aud; } catch { return undefined; } };

        tokenRequest({
            root, store,
            params: state => [
                { name: 'grant_type', value: 'urn:ietf:params:oauth:grant-type:jwt-bearer', important: true },
                { name: 'client_id', value: Wrh.page.calendarApiClientId, important: true },
                ...credential(),
                { name: 'assertion', value: state.tokens?.access_token ?? '', important: true, shorten: true },
                { name: 'requested_token_use', value: 'on_behalf_of', important: true },
                { name: 'scope', value: 'https://graph.microsoft.com/Calendars.Read', important: true },
            ],
            onResponse: (response, state) => {
                const { access_token } = tokensFrom(response, ['access_token']);
                return { tokens: { ...state.tokens, ...(access_token ? { obo_token: access_token } : {}) } };
            },
        });

        // The assertion has to be a token for the API. Entra refuses anything else (AADSTS50013).
        store.onChange(state => {
            const aud = state.tokens?.access_token ? audience(state.tokens.access_token) : undefined;
            warning.classList.toggle('d-none', aud === undefined || aud === Wrh.page.calendarApiClientId);
        });
    };

    // An app asks for a token as itself. Nobody signs in, so nobody can consent: it gets what an admin granted it,
    // and the only scope is .default.
    const clientCredentials = (root, store) => {
        const form = Wrh.role(root, 'form');
        tokenRequest({
            root, store,
            params: () => [
                { name: 'grant_type', value: 'client_credentials', important: true },
                { name: 'client_id', value: form.elements.client_id.value, important: true },
                ...credential(),
                { name: 'scope', value: form.elements.scope.value, important: true },
            ],
            onResponse: response => ({ tokens: tokensFrom(response, ['access_token']) }),
        });
        form.addEventListener('change', () => store.update({}));
        form.addEventListener('submit', e => e.preventDefault());
    };

    // The tokens side by side, raw, split or decoded.
    const tokenPair = (root, store) => {
        const refreshViews = Tokens.mountViews(root, () => store.state.tokens ?? {});
        store.onChange(refreshViews);
    };

    // Tokens from somewhere else (a log), pasted in, side by side.
    const pastedTokens = (root, store) => {
        const inputs = [...root.querySelectorAll('[data-paste]')];
        for (const input of inputs) {
            input.value = store.state.tokens?.[input.dataset.paste] ?? '';
            input.addEventListener('input', () => store.update({
                tokens: { ...store.state.tokens, [input.dataset.paste]: input.value.trim() },
            }));
        }
        tokenPair(root, store);
    };

    // Call something with a bearer token.
    const apiCall = (root, store) => {
        const url = Wrh.role(root, 'url');
        const tokenSelect = Wrh.role(root, 'token');
        const output = Wrh.role(root, 'request');
        const sendButton = Wrh.role(root, 'send');

        const selectedToken = () => tokenSelect.value ? store.state.tokens?.[tokenSelect.value] : null;

        const urlParts = () => {
            try {
                const parsed = new URL(url.value.trim());
                return [[parsed.origin], [parsed.pathname + parsed.search, true]];
            } catch {
                return [[url.value.trim(), true]];
            }
        };

        const render = () => {
            const token = selectedToken();
            Wrh.renderRequest(output, {
                method: 'GET',
                url: urlParts(),
                headers: token ? [{ name: 'Authorization', value: `Bearer ${token}`, shorten: true }] : [],
            });
        };

        // A drop-down when there's a choice, a box when there's only one: change for the first, input for typing.
        url.addEventListener('change', render);
        url.addEventListener('input', render);
        tokenSelect.addEventListener('change', render);

        // The newest token is the one to try. The options are in the order they come in a step.
        let available = '';
        store.onChange(state => {
            for (const option of tokenSelect.options) {
                option.disabled = option.value !== '' && !state.tokens?.[option.value];
            }
            const now = [...tokenSelect.options].filter(o => !o.disabled && o.value).map(o => o.value).join(' ');
            if (now !== available) {
                const newest = [...tokenSelect.options].filter(o => !o.disabled && o.value).at(-1);
                if (newest) tokenSelect.value = newest.value;
                available = now;
            }
            if (tokenSelect.selectedOptions[0]?.disabled) {
                tokenSelect.value = [...tokenSelect.options].find(o => !o.disabled).value;
            }
            render();
            Wrh.role(root, 'result').classList.toggle('d-none', !state.apiResponse);
            if (state.apiResponse) Wrh.renderResponse(Wrh.role(root, 'response'), state.apiResponse);
        });

        sendButton.addEventListener('click', () => Wrh.busy(sendButton, async () => {
            const token = selectedToken();
            const response = await Wrh.send({
                method: 'GET',
                url: url.value.trim(),
                headers: token ? { Authorization: `Bearer ${token}` } : {},
            });
            store.update({ apiResponse: response });
            // Straight to the answer, so the presenter doesn't have to scroll.
            Wrh.role(root, 'result').scrollIntoView({ behavior: 'smooth', block: 'start' });
        }));
    };

    // Wires up a step page: every part it has, the order its sections unfold in, and what comes back from Entra.
    //   parts: { sectionName: (section, store) => void }
    //   rules: { sectionName: state => bool }
    //   fold:  the sign-in section's name, folded away once Entra has answered
    const step = (name, parts, rules, { fold } = {}) => {
        const root = document.getElementById('step');
        const section = sectionName => root.querySelector(`[data-section="${sectionName}"]`);
        const store = Wrh.store(name);
        const returned = receiveCallback(store);

        for (const [sectionName, mount] of Object.entries(parts)) mount(section(sectionName), store);
        store.onChange(state => Wrh.unfold(root, rules, state));

        if (fold) {
            const setFolded = Wrh.collapsible(section(fold));
            let folded;
            store.onChange(state => {
                if (!!state.callback !== folded) setFolded(!!state.callback);
                folded = !!state.callback;
            });
        }
        if (returned) section('callback')?.scrollIntoView({ block: 'start' });
        return store;
    };

    return {
        config, challenge, callbackView, exchange, refresh, onBehalfOf, clientCredentials, tokenPair, pastedTokens, apiCall, step,
    };
})();
