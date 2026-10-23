// Tab 3, under the callback: swap the code for tokens. The secret is masked here and filled in by the server.
(() => {
    const section = document.getElementById('exchange');
    const received = document.getElementById('callback-received');
    const gotoExchange = document.getElementById('id-token-goto-exchange');
    const output = document.getElementById('exchange-request');
    const sendButton = document.getElementById('exchange-send');
    const result = document.getElementById('exchange-result');
    let shownResponse;

    const params = context => [
        { name: 'grant_type', value: 'authorization_code' },
        { name: 'client_id', value: Wrh.page.clientId },
        { name: 'code', value: context.tokens?.code ?? '', important: true, shorten: true },
        { name: 'redirect_uri', value: Wrh.page.redirectUri },
        ...(context.request?.code_verifier ? [{ name: 'code_verifier', value: context.request.code_verifier }] : []),
        { name: 'client_secret', value: Wrh.secretMask },
    ];

    const endpoint = context => context.config?.token_endpoint ?? '<token_endpoint>';

    Wrh.onChange(context => {
        // No code, nothing to exchange: an id_token-only sign-in ends at the callback.
        const hasCode = !!context.tokens?.code;
        const exchanged = context.exchangeResponse?.status === 200;
        section.classList.toggle('d-none', !hasCode);
        gotoExchange.classList.toggle('d-none', !hasCode || exchanged);

        // Fold the callback away once, when the exchange works. After that it's the presenter's to open.
        if (context.exchangeResponse !== shownResponse) {
            shownResponse = context.exchangeResponse;
            received.open = !exchanged;
        }

        Wrh.renderRequest(output, {
            method: 'POST',
            url: [[endpoint(context)]],
            headers: [{ name: 'Content-Type', value: 'application/x-www-form-urlencoded' }],
            mode: 'form',
            params: params(context),
        });

        result.classList.toggle('d-none', !context.exchangeResponse);
        if (!context.exchangeResponse) return;
        Wrh.renderResponse(document.getElementById('exchange-response'), context.exchangeResponse);
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
        // A new code starts a new chain of refresh tokens.
        Wrh.update({ exchangeResponse: response, tokens: { ...context.tokens, ...tokens }, refreshResponse: null, refreshTokens: null, oboResponse: null });
    }));
})();
