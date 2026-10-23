// Tab 7: On-Behalf-Of. The Calendar API trades the token it was sent for a Graph token, as the same user.
// It's the API's request, so it's the API's client_id and secret. The web never sees a secret: the server fills it in.
(() => {
    const output = document.getElementById('obo-request');
    const sendButton = document.getElementById('obo-send');
    const result = document.getElementById('obo-result');
    const wrongAudience = document.getElementById('obo-wrong-audience');

    // The assertion has to be a token for the API. Graph's app ID reads better as its name.
    const audience = token => {
        try { return Tokens.decodeJson(token.split('.')[1]).aud; } catch { return undefined; }
    };
    const graphAudiences = ['00000003-0000-0000-c000-000000000000', 'https://graph.microsoft.com'];
    // Not a data-goto: those hide while their tab is locked, and this is part of a sentence.
    document.getElementById('obo-goto-refresh').addEventListener('click', () => Wrh.showTab('refresh'));

    const params = context => [
        { name: 'grant_type', value: 'urn:ietf:params:oauth:grant-type:jwt-bearer', important: true },
        { name: 'client_id', value: Wrh.page.calendarApiClientId, important: true },
        { name: 'client_secret', value: Wrh.secretMask },
        { name: 'assertion', value: context.tokens?.access_token ?? '', important: true, shorten: true },
        { name: 'requested_token_use', value: 'on_behalf_of', important: true },
        { name: 'scope', value: 'https://graph.microsoft.com/Calendars.Read', important: true },
    ];

    const endpoint = context => context.config?.token_endpoint ?? '<token_endpoint>';

    Wrh.onChange(context => {
        Wrh.renderRequest(output, {
            method: 'POST',
            url: [[endpoint(context)]],
            headers: [{ name: 'Content-Type', value: 'application/x-www-form-urlencoded' }],
            mode: 'form',
            params: params(context),
        });

        const aud = context.tokens?.access_token ? audience(context.tokens.access_token) : undefined;
        const isWrong = aud !== undefined && aud !== Wrh.page.calendarApiClientId;
        wrongAudience.classList.toggle('d-none', !isWrong);
        if (isWrong) document.getElementById('obo-audience').textContent = graphAudiences.includes(aud) ? `Microsoft Graph (${aud})` : aud;

        result.classList.toggle('d-none', !context.oboResponse);
        if (context.oboResponse) Wrh.renderResponse(document.getElementById('obo-response'), context.oboResponse);
    });

    sendButton.addEventListener('click', () => Wrh.busy(sendButton, async () => {
        const context = Wrh.context;
        const response = await Wrh.send({
            method: 'POST',
            url: endpoint(context),
            form: Object.fromEntries(params(context).map(p => [p.name, p.value])),
        });

        // Only the access token. A refresh token in here would be the API's, not part of the web's chain.
        const accessToken = response.status === 200 ? JSON.parse(response.body).access_token : undefined;
        Wrh.update({
            oboResponse: response,
            tokens: accessToken ? { ...context.tokens, access_token: accessToken } : context.tokens,
        });
    }));
})();
