// Step 2: a token for my API. Sign in, see what came back, trade the code, look at the tokens, call the API.
// With response_type=id_token there's no code, so no exchange: the tokens come straight from the callback.
(() => {
    const exchanged = s => s.exchangeResponse?.status === 200;
    const signedIn = s => !!(s.tokens?.id_token || s.tokens?.access_token) && (!s.tokens?.code || exchanged(s));
    Parts.step('step-2', {
        challenge: (section, store) => Parts.challenge(section, store, 'step-2'),
        callback: Parts.callbackView,
        exchange: Parts.exchange,
        tokens: Parts.tokenPair,
        api: Parts.apiCall,
    }, {
        callback: Parts.callbackError,
        exchange: s => !!s.tokens?.code,
        tokens: signedIn,
        api: signedIn,
    });
})();
