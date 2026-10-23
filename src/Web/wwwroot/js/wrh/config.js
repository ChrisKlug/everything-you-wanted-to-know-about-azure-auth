// Tab 1: where is everything? Ask the tenant's OpenID configuration.
(() => {
    const tenant = document.getElementById('config-tenant');
    const request = document.getElementById('config-request');
    const fetchButton = document.getElementById('config-fetch');
    const result = document.getElementById('config-result');
    const summary = document.getElementById('config-summary');
    const path = '/v2.0/.well-known/openid-configuration';
    const interesting = ['issuer', 'authorization_endpoint', 'token_endpoint', 'jwks_uri'];

    if (Wrh.context.tenant) tenant.value = Wrh.context.tenant;

    const url = () => `${Wrh.page.instance}${tenant.value.trim()}${path}`;

    const render = () => Wrh.renderRequest(request, {
        method: 'GET',
        url: [[Wrh.page.instance], [tenant.value.trim() || '<tenant>', true], [path]],
    });

    tenant.addEventListener('input', render);
    render();

    fetchButton.addEventListener('click', () => Wrh.busy(fetchButton, async () => {
        const response = await Wrh.send({ method: 'GET', url: url() });
        let config = null;
        if (response.status === 200) config = JSON.parse(response.body);
        Wrh.update({ tenant: tenant.value.trim(), configResponse: response, config });
    }));

    Wrh.onChange(context => {
        result.classList.toggle('d-none', !context.configResponse);
        if (!context.configResponse) return;

        Wrh.renderResponse(document.getElementById('config-response'), context.configResponse);

        summary.replaceChildren();
        for (const name of interesting) {
            if (!context.config?.[name]) continue;
            const row = summary.insertRow();
            row.insertCell().textContent = name;
            row.insertCell().textContent = context.config[name];
        }
    });
})();
