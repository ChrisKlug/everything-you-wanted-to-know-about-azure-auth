// Tab 6: trade the refresh token for new tokens, for any API. Entra sends a new refresh token back as well.
(() => {
    const form = document.getElementById('refresh-form');
    const output = document.getElementById('refresh-request');
    const sendButton = document.getElementById('refresh-send');
    const chain = document.getElementById('refresh-chain');
    const result = document.getElementById('refresh-result');

    const scope = () => form.elements.resource.value;

    // Every refresh token so far, oldest first. The last one is the one to send next.
    const refreshTokens = context => context.refreshTokens ?? (context.tokens?.refresh_token ? [context.tokens.refresh_token] : []);

    const params = context => [
        { name: 'grant_type', value: 'refresh_token' },
        { name: 'client_id', value: Wrh.page.clientId },
        { name: 'refresh_token', value: context.tokens?.refresh_token ?? '', important: true, shorten: true },
        { name: 'scope', value: scope(), important: true },
        { name: 'client_secret', value: Wrh.secretMask },
    ];

    const endpoint = context => context.config?.token_endpoint ?? '<token_endpoint>';

    const render = () => Wrh.renderRequest(output, {
        method: 'POST',
        url: [[endpoint(Wrh.context)]],
        headers: [{ name: 'Content-Type', value: 'application/x-www-form-urlencoded' }],
        mode: 'form',
        params: params(Wrh.context),
    });

    const span = (className, text) => {
        const element = document.createElement('span');
        element.className = className;
        element.textContent = text;
        return element;
    };

    // Refresh tokens are opaque and start much the same, so the end is what tells them apart.
    const shorten = token => `${token.slice(0, 12)} … ${token.slice(-32)}`;

    let shownCount = 0;

    const renderChain = context => {
        const tokens = refreshTokens(context);
        chain.replaceChildren(...tokens.map((token, i) => {
            const current = i === tokens.length - 1;
            const line = span(`refresh-token ${current ? 'current' : 'used'}`, '');
            line.append(span('refresh-label', `#${i + 1} ${current ? 'current' : 'used'}`), span('refresh-value', shorten(token)));
            return line;
        }));
        if (tokens.length === 0) chain.textContent = 'No refresh token. Ask for offline_access.';

        // Flash the new one.
        if (shownCount > 0 && tokens.length > shownCount) {
            const line = chain.lastElementChild;
            line.classList.add('changed');
            requestAnimationFrame(() => requestAnimationFrame(() => line.classList.remove('changed')));
        }
        shownCount = tokens.length;
    };

    form.addEventListener('change', render);
    form.addEventListener('submit', e => e.preventDefault());

    Wrh.onChange(context => {
        render();
        renderChain(context);

        result.classList.toggle('d-none', !context.refreshResponse);
        if (context.refreshResponse) Wrh.renderResponse(document.getElementById('refresh-response'), context.refreshResponse);
    });

    sendButton.addEventListener('click', () => Wrh.busy(sendButton, async () => {
        const context = Wrh.context;
        const response = await Wrh.send({
            method: 'POST',
            url: endpoint(context),
            form: Object.fromEntries(params(context).map(p => [p.name, p.value])),
        });

        let tokens = {};
        if (response.status === 200) {
            const body = JSON.parse(response.body);
            tokens = Object.fromEntries(['id_token', 'access_token', 'refresh_token']
                .filter(name => body[name]).map(name => [name, body[name]]));
        }
        const sent = refreshTokens(context);
        Wrh.update({
            refreshResponse: response,
            tokens: { ...context.tokens, ...tokens },
            refreshTokens: tokens.refresh_token && tokens.refresh_token !== sent.at(-1) ? [...sent, tokens.refresh_token] : sent,
        });
    }));
})();
