// Tab 8: client credentials. An app asks for a token as itself. Nobody signs in, so nobody can consent:
// it gets what an admin granted it, and the only scope is .default. The server fills in the app's secret.
(() => {
    const form = document.getElementById('client-credentials-form');
    const output = document.getElementById('client-credentials-request');
    const sendButton = document.getElementById('client-credentials-send');
    const result = document.getElementById('client-credentials-result');

    const params = () => [
        { name: 'grant_type', value: 'client_credentials', important: true },
        { name: 'client_id', value: form.elements.client_id.value, important: true },
        { name: 'client_secret', value: Wrh.secretMask },
        { name: 'scope', value: form.elements.scope.value, important: true },
    ];

    const endpoint = context => context.config?.token_endpoint ?? '<token_endpoint>';

    const render = () => Wrh.renderRequest(output, {
        method: 'POST',
        url: [[endpoint(Wrh.context)]],
        headers: [{ name: 'Content-Type', value: 'application/x-www-form-urlencoded' }],
        mode: 'form',
        params: params(),
    });

    form.addEventListener('change', render);
    form.addEventListener('submit', e => e.preventDefault());

    Wrh.onChange(context => {
        render();
        result.classList.toggle('d-none', !context.clientCredentialsResponse);
        if (context.clientCredentialsResponse) Wrh.renderResponse(document.getElementById('client-credentials-response'), context.clientCredentialsResponse);
    });

    sendButton.addEventListener('click', () => Wrh.busy(sendButton, async () => {
        const context = Wrh.context;
        const response = await Wrh.send({
            method: 'POST',
            url: endpoint(context),
            form: Object.fromEntries(params().map(p => [p.name, p.value])),
        });

        // A different caller, so the user's tokens go. No user, so no id_token, and nothing to refresh: the app just asks again.
        const accessToken = response.status === 200 ? JSON.parse(response.body).access_token : undefined;
        Wrh.update({
            clientCredentialsResponse: response,
            tokens: accessToken ? { access_token: accessToken } : context.tokens,
            refreshTokens: accessToken ? undefined : context.refreshTokens,
        });
    }));
})();
