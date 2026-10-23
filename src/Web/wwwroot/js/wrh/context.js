// Shared state for /what-really-happens. It fills up tab by tab and lives in sessionStorage,
// so it survives the trip to Entra and back.
const Wrh = (() => {
    const storageKey = 'what-really-happens:context';
    const tabKey = 'what-really-happens:tab';
    const root = document.getElementById('what-really-happens');
    const listeners = [];

    const read = key => { try { return sessionStorage.getItem(key); } catch { return null; } };
    const write = (key, value) => { try { sessionStorage.setItem(key, value); } catch { /* storage unavailable */ } };

    let context;
    try { context = JSON.parse(read(storageKey)) ?? {}; } catch { context = {}; }

    // What each tab needs before it makes sense.
    const tabRules = {
        'config': () => true,
        'challenge': c => !!c.config,
        'callback': c => !!c.callback,
        'tokens': c => !!(c.tokens?.id_token || c.tokens?.access_token),
        'api': () => true,
        'refresh': c => !!c.tokens?.refresh_token,
        'obo': c => !!c.tokens?.access_token,
        'client-credentials': c => !!c.config,
    };

    const tabButton = name => root.querySelector(`[data-tab="${name}"]`);

    const refreshTabs = () => {
        for (const [name, rule] of Object.entries(tabRules)) {
            tabButton(name).disabled = !rule(context);
        }
        // "Next" buttons only show when there's somewhere to go, and something to look at when they name a token.
        root.querySelectorAll('[data-goto]').forEach(button =>
            button.classList.toggle('d-none', tabButton(button.dataset.goto).disabled
                || (!!button.dataset.token && !context.tokens?.[button.dataset.token])));
    };

    const notify = () => {
        refreshTabs();
        listeners.forEach(listener => listener(context));
    };

    const update = patch => {
        context = { ...context, ...patch };
        write(storageKey, JSON.stringify(context));
        notify();
    };

    const onChange = listener => {
        listeners.push(listener);
        listener(context);
    };

    const showTab = name => {
        const button = tabButton(name);
        if (button && !button.disabled) bootstrap.Tab.getOrCreateInstance(button).show();
    };

    const restoreTab = () => {
        const name = read(tabKey);
        showTab(tabRules[name]?.(context) ? name : 'config');
    };

    root.addEventListener('shown.bs.tab', e => write(tabKey, e.target.dataset.tab));
    root.addEventListener('click', e => {
        const goto = e.target.closest('[data-goto]');
        if (goto) showTab(goto.dataset.goto);
    });

    // The server sends it, the browser only shows it. See ProxyEndpoints.cs.
    const send = async request => {
        const response = await fetch('/what-really-happens/send', {
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

        if (mode === 'form' && params.length > 0) element.append(span('url-line', ' '));

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

    // Tokens are long, and each one has its own tab. Where they're only passing through, they're clamped to two
    // lines. In a request a click shows the rest, in a response there's no need. One line per row, so a single
    // token can be clamped.
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

    refreshTabs();

    return {
        get context() { return context; },
        page: root.dataset,
        secretMask: '••••••',
        update, onChange, showTab, restoreTab, send, busy,
        renderRequest, renderJson, renderResponse, makeExpandable,
    };
})();
