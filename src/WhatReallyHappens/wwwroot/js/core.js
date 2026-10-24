// What every page shares: the app's settings, a store per step, the proxy, and how requests and responses look.
const Wrh = (() => {
    const read = (storage, key) => { try { return storage.getItem(key); } catch { return null; } };
    const write = (storage, key, value) => { try { storage.setItem(key, value); } catch { /* storage unavailable */ } };
    const remove = (storage, key) => { try { storage.removeItem(key); } catch { /* storage unavailable */ } };

    // Each step keeps its own state, in sessionStorage so it survives the trip to Entra and back. Steps never share.
    const store = name => {
        const key = `wrh:${name}`;
        const listeners = [];
        let state;
        try { state = JSON.parse(read(sessionStorage, key)) ?? {}; } catch { state = {}; }

        const notify = () => listeners.forEach(listener => listener(state));
        return {
            get state() { return state; },
            update(patch) {
                state = { ...state, ...patch };
                write(sessionStorage, key, JSON.stringify(state));
                notify();
            },
            reset() {
                state = {};
                remove(sessionStorage, key);
                notify();
            },
            onChange(listener) {
                listeners.push(listener);
                listener(state);
            },
        };
    };

    // Sign-in leaves the page. The callback page brings back what Entra sent, to the page that asked.
    const returnKey = 'wrh:return-to';
    const callbackKey = 'wrh:callback';

    const signIn = url => {
        write(sessionStorage, returnKey, window.location.pathname);
        window.location.assign(url);
    };

    const takeCallback = () => {
        const value = read(sessionStorage, callbackKey);
        remove(sessionStorage, callbackKey);
        try { return value ? JSON.parse(value) : null; } catch { return null; }
    };

    const finishCallback = callback => {
        write(sessionStorage, callbackKey, JSON.stringify(callback));
        window.location.replace(read(sessionStorage, returnKey) ?? '/');
    };

    // The server sends it, the browser only shows it. See ProxyEndpoints.cs.
    const send = async request => {
        const response = await fetch('/send', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(request),
        });
        if (!response.ok) throw new Error(await response.text());
        return response.json();
    };

    const busy = async (button, action) => {
        button.disabled = true;
        try { await action(); } catch (e) { alert(e.message); } finally { button.disabled = false; }
    };

    const randomValue = () => Array.from(crypto.getRandomValues(new Uint8Array(12)), b => b.toString(16).padStart(2, '0')).join('');
    const base64Url = bytes => btoa(String.fromCharCode(...bytes)).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');

    const span = (className, text = '') => {
        const element = document.createElement('span');
        element.className = className;
        element.textContent = text;
        return element;
    };

    const flash = element => {
        element.classList.add('changed');
        requestAnimationFrame(() => requestAnimationFrame(() => element.classList.remove('changed')));
    };

    const makeExpandable = line => {
        line.classList.add('expandable', 'clamp');
        line.title = 'Click to expand';
        line.addEventListener('click', () => line.classList.toggle('clamp'));
    };

    // Renders an HTTP request, or a redirect, one parameter per line. The parts you choose are bold. Values are shown
    // as they read, not URL-encoded: the encoding is the browser's job, and nobody can read it from the back row.
    //   url:     [[text, important], ...]
    //   params:  [{ name, value, important, shorten }]
    //   headers: [{ name, value, important, shorten }]
    //   mode:    'query' | 'fragment' | 'form'
    const renderRequest = (element, { method, url, params = [], headers = [], mode = 'query' }) => {
        const previous = element.wrhPrevious ?? {};
        const current = {};
        element.replaceChildren();

        const first = span('url-line url-base');
        if (method) first.append(span('url-method', `${method} `));
        for (const [text, important] of url) first.append(span(important ? 'url-part important' : 'url-part', text));
        element.append(first);

        if (headers.length > 0) element.append(span('url-line', ' '));
        for (const { name, value, important, shorten } of headers) {
            const line = span('url-line url-header');
            line.append(span('url-name', name), ': ', span(important ? 'url-value important' : 'url-value', value));
            if (shorten) makeExpandable(line);
            element.append(line);
        }

        if (mode === 'form' && params.length > 0) element.append(span('url-line', ' '));

        const [firstPrefix, nextPrefix] = { query: ['?', '&'], fragment: ['#', '&'], form: ['', '&'] }[mode];
        params.forEach(({ name, value, important, shorten }, i) => {
            current[name] = value;

            const line = span(mode === 'form' ? 'url-line url-param url-form' : 'url-line url-param');
            line.append(i === 0 ? firstPrefix : nextPrefix, span('url-name', name), '=',
                span(important ? 'url-value important' : 'url-value', value));
            if (shorten) makeExpandable(line);
            element.append(line);

            // Flash what changed, and what's new since the last render.
            if (Object.keys(previous).length > 0 && previous[name] !== value) flash(line);
        });

        element.wrhPrevious = current;
    };

    // Tokens are long. Where they're only passing through, they're clamped to two lines.
    const tokenKeys = ['access_token', 'id_token', 'refresh_token', 'code'];

    const renderJson = (element, value) => {
        element.replaceChildren();
        element.classList.add('hljs');
        for (const text of JSON.stringify(value, null, 2).split('\n')) {
            const line = span('json-line');
            line.innerHTML = hljs.highlight(text, { language: 'json', ignoreIllegals: true }).value;
            if (tokenKeys.includes(text.match(/^\s*"([^"]+)":/)?.[1])) {
                line.classList.add('json-token', 'clamp');
            }
            element.append(line);
        }
    };

    const renderResponse = (container, response) => {
        container.replaceChildren();

        // The status first, big enough to read from the back of the room.
        const ok = response.status >= 200 && response.status < 300;
        container.append(span(`response-status ${ok ? 'ok' : 'error'}`, `${response.status} ${response.statusText ?? ''}`.trim()));

        const challenge = Object.entries(response.headers).find(([name]) => name.toLowerCase() === 'www-authenticate')?.[1];
        if (challenge) {
            const line = document.createElement('pre');
            line.className = 'mono-panel mt-2';
            line.append(span('url-name', 'WWW-Authenticate'), ': ', span('url-value important', challenge));
            container.append(line);
        }

        if (response.image) {
            const image = document.createElement('img');
            image.className = 'response-image mt-2';
            image.src = response.image;
            image.alt = response.body;
            container.append(image);
            return;
        }

        const body = document.createElement('pre');
        body.className = 'mono-panel json-panel mt-2';
        let json;
        try { json = JSON.parse(response.body); } catch { /* not JSON */ }
        if (json !== undefined) {
            const code = document.createElement('code');
            code.className = 'language-json';
            body.append(code);
            renderJson(code, json);
        } else {
            body.textContent = response.body || '(empty body)';
        }
        container.append(body);
    };

    // A step's sections unfold in order: each shows once the one above it has something to show. A section that
    // appears is scrolled into view, so the presenter doesn't have to.
    // The numbers count the sections that show, so a skipped one leaves no gap.
    const unfold = (root, rules, state) => {
        let number = 0;
        for (const section of root.querySelectorAll('[data-section]')) {
            const show = rules[section.dataset.section]?.(state) ?? true;
            const wasHidden = section.hidden;
            section.hidden = !show;
            if (show) {
                const badge = section.querySelector('.step-number');
                if (badge) badge.textContent = ++number;
            }
            if (show && wasHidden && section.dataset.seen) {
                section.scrollIntoView({ behavior: 'smooth', block: 'start' });
            }
            section.dataset.seen = 'true';
        }
    };

    const role = (root, name) => root.querySelector(`[data-role="${name}"]`);

    // A section whose heading folds it away. The step folds it when the story has moved on, a click opens it again.
    const collapsible = section => {
        role(section, 'toggle').addEventListener('click', () => section.classList.toggle('collapsed'));
        return collapsed => section.classList.toggle('collapsed', collapsed);
    };

    return {
        page: document.body.dataset,
        secretMask: '••••••',
        store, signIn, takeCallback, finishCallback, send, busy, randomValue, base64Url,
        renderRequest, renderJson, renderResponse, makeExpandable, unfold, role, collapsible,
        // My tenant, unless a step says otherwise (organizations, in step 9).
        endpoint: (name, tenant) => `${document.body.dataset.instance}${tenant ?? document.body.dataset.tenantId}/oauth2/v2.0/${name}`,
        openidConfiguration: tenant => `${document.body.dataset.instance}${tenant ?? document.body.dataset.tenantId}/v2.0/.well-known/openid-configuration`,
        redirectUri: `${window.location.origin}/callback`,
    };
})();
