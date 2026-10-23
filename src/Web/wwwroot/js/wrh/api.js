// Tab 5: call something with a bearer token. Any token, so we can see what happens with the wrong one.
(() => {
    const url = document.getElementById('api-url');
    const tokenSelect = document.getElementById('api-token');
    const output = document.getElementById('api-request');
    const sendButton = document.getElementById('api-send');
    const result = document.getElementById('api-result');

    const selectedToken = () => tokenSelect.value ? Wrh.context.tokens?.[tokenSelect.value] : null;

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

    // The preset list shows which preset the URL is, or none once it's been typed over.
    const preset = document.getElementById('api-preset');
    const showPreset = () => {
        preset.value = [...preset.options].some(o => o.value === url.value.trim()) ? url.value.trim() : '';
    };
    preset.addEventListener('change', () => {
        if (preset.value) url.value = preset.value;
        render();
    });
    url.addEventListener('input', () => {
        showPreset();
        render();
    });
    showPreset();
    tokenSelect.addEventListener('change', render);

    // The access token is the one meant for APIs. Whenever a new one arrives, it's selected.
    let lastAccessToken;

    Wrh.onChange(context => {
        for (const option of tokenSelect.options) {
            option.disabled = option.value !== '' && !context.tokens?.[option.value];
        }
        const accessToken = context.tokens?.access_token;
        if (accessToken && accessToken !== lastAccessToken) {
            tokenSelect.value = 'access_token';
        } else if (tokenSelect.selectedOptions[0]?.disabled) {
            tokenSelect.value = [...tokenSelect.options].find(o => !o.disabled).value;
        }
        lastAccessToken = accessToken;
        render();

        result.classList.toggle('d-none', !context.apiResponse);
        if (context.apiResponse) Wrh.renderResponse(document.getElementById('api-response'), context.apiResponse);
    });

    sendButton.addEventListener('click', () => Wrh.busy(sendButton, async () => {
        const token = selectedToken();
        const response = await Wrh.send({
            method: 'GET',
            url: url.value.trim(),
            headers: token ? { Authorization: `Bearer ${token}` } : {},
        });
        Wrh.update({ apiResponse: response });
    }));
})();
