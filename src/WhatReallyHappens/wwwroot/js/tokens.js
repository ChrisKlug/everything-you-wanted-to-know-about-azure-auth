// JWTs are base64url, not base64: '-' and '_' instead of '+' and '/', and no '=' padding.
const Tokens = (() => {
    const base64UrlToBytes = value => {
        const base64 = value.replace(/-/g, '+').replace(/_/g, '/').padEnd(Math.ceil(value.length / 4) * 4, '=');
        return Uint8Array.from(atob(base64), c => c.charCodeAt(0));
    };

    const decodeJson = segment => JSON.parse(new TextDecoder().decode(base64UrlToBytes(segment)));

    // The signature is binary. Decoding it as text shows what it is: not meant to be read.
    const decodeGarbage = segment => Array.from(base64UrlToBytes(segment), b => String.fromCharCode(b)).join('');

    // Timestamps are seconds since 1970. Say what they mean, right next to them.
    const withTimes = (json, payload) => json.replace(/^(\s*"(iat|nbf|exp)": (\d+),?)$/gm,
        (line, _, claim) => `${line} // ${new Date(payload[claim] * 1000).toLocaleString()}`);

    // App IDs don't say which app they are. Same idea: the name right next to the GUID. With the name there, the
    // first group of the GUID is enough. Graph's ID is the same in every tenant.
    const appNames = () => ({
        [Wrh.page.clientId]: 'Kick-Ass Calendar',
        [Wrh.page.calendarApiClientId]: 'Kick-Ass Calendar API',
        [Wrh.page.digestClientId]: 'Kick-Ass Calendar Digest',
        'https://kickassdev.se/api': 'Kick-Ass Calendar API',
        '00000003-0000-0000-c000-000000000000': 'Microsoft Graph',
        'https://graph.microsoft.com': 'Microsoft Graph',
    });
    const shortGuid = value => value.replace(/^([0-9a-f]{8})(-[0-9a-f]{4}){3}-[0-9a-f]{12}$/i, '$1-…');
    const withNames = json => {
        const names = appNames();
        return json.replace(/^(\s*"(?:aud|azp|appid)": )"([^"]+)"(,?)$/gm,
            (line, start, value, comma) => names[value] ? `${start}"${shortGuid(value)}"${comma} // ${names[value]}` : line);
    };

    const showJson = (element, value, annotate = json => json) => {
        const code = document.createElement('code');
        code.className = 'language-json';
        code.textContent = annotate(JSON.stringify(value, null, 2));
        element.replaceChildren(code);
        hljs.highlightElement(code);
    };

    const classes = ['jwt-header', 'jwt-payload', 'jwt-signature'];

    const split = (element, token) => {
        element.replaceChildren();
        element.classList.remove('decoded');
        token.split('.').forEach((segment, i) => {
            if (i > 0) {
                const dot = document.createElement('div');
                dot.className = 'jwt-dot';
                dot.textContent = '.';
                element.append(dot);
            }
            const part = document.createElement('div');
            part.className = `jwt-segment ${classes[i] ?? ''}`;
            part.textContent = segment;
            element.append(part);
        });
    };

    const decode = (element, token) => {
        split(element, token);
        const [header, payload, signature] = token.split('.');
        const [headerElement, payloadElement, signatureElement] = element.querySelectorAll('.jwt-segment');
        const decodedPayload = decodeJson(payload);
        showJson(headerElement, decodeJson(header));
        showJson(payloadElement, decodedPayload, json => withNames(withTimes(json, decodedPayload)));
        if (signatureElement) signatureElement.textContent = decodeGarbage(signature);
        element.classList.add('decoded');
    };

    const raw = (element, token, name) => {
        element.classList.remove('decoded');
        element.classList.toggle('text-secondary', !token);
        element.textContent = token || element.dataset.empty || `No ${name} this time.`;
    };

    // Raw -> Split -> Decode, for one or more tokens side by side. One set of buttons, so they change together and
    // can be compared claim by claim. Each view: <div data-token-view="id_token"> with a [data-role="view"] inside.
    const mountViews = (root, getTokens) => {
        const splitButton = Wrh.role(root, 'split');
        const decodeButton = Wrh.role(root, 'decode');
        const rawButton = Wrh.role(root, 'raw');
        const views = [...root.querySelectorAll('[data-token-view]')].map(view => ({
            name: view.dataset.tokenView,
            element: Wrh.role(view, 'view'),
            validate: Wrh.role(view, 'validate'),
        }));
        let shown = 'raw';

        const render = state => {
            shown = state;
            const tokens = getTokens();
            for (const view of views) {
                const token = tokens[view.name];
                if (view.validate) view.validate.disabled = !token;
                try {
                    if (!token || state === 'raw') raw(view.element, token, view.name);
                    else if (state === 'split') split(view.element, token);
                    else decode(view.element, token);
                } catch (e) {
                    raw(view.element, token, view.name);
                    alert(`The ${view.name} doesn't decode as a JWT: ${e.message}`);
                }
            }
            const any = views.some(view => tokens[view.name]);
            rawButton.classList.toggle('d-none', state === 'raw');
            splitButton.disabled = state !== 'raw' || !any;
            decodeButton.disabled = state !== 'split';
        };

        splitButton.addEventListener('click', () => render('split'));
        decodeButton.addEventListener('click', () => render('decoded'));
        rawButton.addEventListener('click', () => render('raw'));
        for (const view of views) {
            view.validate?.addEventListener('click', () => Validate.open(view.name, getTokens()[view.name]));
        }

        // New tokens start raw again.
        let last;
        return () => {
            const current = JSON.stringify(views.map(view => getTokens()[view.name] ?? ''));
            if (current !== last) {
                last = current;
                render('raw');
            } else {
                render(shown);
            }
        };
    };

    return { base64UrlToBytes, decodeJson, decodeGarbage, split, decode, raw, mountViews };
})();
