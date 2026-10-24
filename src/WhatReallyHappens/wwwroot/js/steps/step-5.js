// Step 5: one refresh token, a token for another API, no sign-in.
(() => {
    const exchanged = s => s.exchangeResponse?.status === 200;
    Parts.step('step-5', {
        challenge: (section, store) => Parts.challenge(section, store, 'step-5'),
        callback: Parts.callbackView,
        exchange: Parts.exchange,
        tokens: Parts.tokenPair,
        refresh: Parts.refresh,
        refreshed: Parts.tokenPair,
        api: Parts.apiCall,
    }, {
        callback: s => !!s.callback,
        exchange: s => !!s.tokens?.code,
        tokens: exchanged,
        refresh: exchanged,
        refreshed: s => !!s.tokens?.refreshed_token,
        api: s => !!s.tokens?.refreshed_token,
    }, { fold: 'challenge' });
})();
