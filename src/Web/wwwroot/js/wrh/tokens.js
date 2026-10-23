// Tab 4: split and decode a JWT.
// JWTs are base64url, not base64: '-' and '_' instead of '+' and '/', and no '=' padding.
const Tokens = (() => {
    const base64UrlToBytes = value => {
        const base64 = value.replace(/-/g, '+').replace(/_/g, '/').padEnd(Math.ceil(value.length / 4) * 4, '=');
        return Uint8Array.from(atob(base64), c => c.charCodeAt(0));
    };

    const decodeJson = segment => JSON.parse(new TextDecoder().decode(base64UrlToBytes(segment)));

    // The signature is binary. Decoding it as text shows what it is: not meant to be read.
    const decodeGarbage = segment => Array.from(base64UrlToBytes(segment), b => String.fromCharCode(b)).join('');

    return { base64UrlToBytes, decodeJson, decodeGarbage };
})();

// Raw -> Split -> Decode. Both tokens side by side, one set of buttons, so they change together and
// can be compared claim by claim.
(() => {
    const controls = document.querySelector('[data-token-controls]');
    const button = role => controls.querySelector(`[data-role="${role}"]`);
    const splitButton = button('split');
    const decodeButton = button('decode');
    const rawButton = button('raw');
    const classes = ['jwt-header', 'jwt-payload', 'jwt-signature'];

    // token is null until the first render, so an empty column still gets its placeholder.
    const views = [...document.querySelectorAll('[data-token-view]')].map(root => ({
        name: root.dataset.tokenView,
        element: root.querySelector('[data-role="view"]'),
        token: null,
    }));
    const present = () => views.filter(view => view.token);

    const show = state => {
        if (state === 'raw') {
            for (const view of views) {
                view.element.classList.remove('decoded');
                view.element.classList.toggle('text-secondary', !view.token);
                view.element.textContent = view.token || `No ${view.name} this time.`;
            }
        }
        rawButton.classList.toggle('d-none', state === 'raw');
        splitButton.disabled = state !== 'raw' || present().length === 0;
        decodeButton.disabled = state !== 'split';
    };

    // Timestamps are seconds since 1970. Say what they mean, right next to them.
    const withTimes = (json, payload) => json.replace(/^(\s*"(iat|nbf|exp)": (\d+),?)$/gm,
        (line, _, claim) => `${line} // ${new Date(payload[claim] * 1000).toLocaleString()}`);

    // App IDs don't say which app they are. Same idea: the name right next to the GUID. With the name there, the
    // first group of the GUID is enough. The names are the registrations' display names.
    // Graph's ID is the same in every tenant.
    const appNames = {
        [Wrh.page.clientId]: 'Kick-Ass Calendar',
        [Wrh.page.calendarApiClientId]: 'Kick-Ass Calendar API',
        [Wrh.page.digestClientId]: 'Kick-Ass Calendar Digest',
        'https://kickassdev.se/api': 'Kick-Ass Calendar API',
        '00000003-0000-0000-c000-000000000000': 'Microsoft Graph',
        'https://graph.microsoft.com': 'Microsoft Graph',
    };
    const shortGuid = value => value.replace(/^([0-9a-f]{8})(-[0-9a-f]{4}){3}-[0-9a-f]{12}$/i, '$1-…');
    const withNames = json => json.replace(/^(\s*"(?:aud|azp|appid)": )"([^"]+)"(,?)$/gm,
        (line, start, value, comma) => appNames[value] ? `${start}"${shortGuid(value)}"${comma} // ${appNames[value]}` : line);

    const showJson = (element, value, annotate = json => json) => {
        const code = document.createElement('code');
        code.className = 'language-json';
        code.textContent = annotate(JSON.stringify(value, null, 2));
        element.replaceChildren(code);
        hljs.highlightElement(code);
    };

    Wrh.onChange(context => {
        let changed = false;
        for (const view of views) {
            const current = context.tokens?.[view.name] ?? '';
            if (current !== view.token) {
                view.token = current;
                changed = true;
            }
        }
        if (changed) show('raw');
    });

    const split = view => {
        view.element.replaceChildren();
        view.token.split('.').forEach((segment, i) => {
            if (i > 0) {
                const dot = document.createElement('div');
                dot.className = 'jwt-dot';
                dot.textContent = '.';
                view.element.append(dot);
            }
            const element = document.createElement('div');
            element.className = `jwt-segment ${classes[i] ?? ''}`;
            element.textContent = segment;
            view.element.append(element);
        });
    };

    const decode = view => {
        const [header, payload, signature] = view.token.split('.');
        const [headerElement, payloadElement, signatureElement] = view.element.querySelectorAll('.jwt-segment');
        const decodedHeader = Tokens.decodeJson(header);
        const decodedPayload = Tokens.decodeJson(payload);
        showJson(headerElement, decodedHeader);
        showJson(payloadElement, decodedPayload, json => withNames(withTimes(json, decodedPayload)));
        if (signatureElement) signatureElement.textContent = Tokens.decodeGarbage(signature);
        view.element.classList.add('decoded');
    };

    splitButton.addEventListener('click', () => {
        present().forEach(split);
        show('split');
    });

    decodeButton.addEventListener('click', () => {
        for (const view of present()) {
            try {
                decode(view);
            } catch (e) {
                alert(`The ${view.name} doesn't decode as a JWT: ${e.message}`);
            }
        }
        show('decoded');
    });

    rawButton.addEventListener('click', () => show('raw'));
})();
