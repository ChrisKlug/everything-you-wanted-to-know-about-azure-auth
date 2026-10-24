// Step 3: a token for my API, and calling it.
(() => {
    const exchanged = s => s.exchangeResponse?.status === 200;
    Parts.step('step-3', {
        challenge: (section, store) => Parts.challenge(section, store, 'step-3'),
        callback: Parts.callbackView,
        exchange: Parts.exchange,
        tokens: Parts.tokenPair,
        api: Parts.apiCall,
    }, {
        callback: s => !!s.callback,
        exchange: s => !!s.tokens?.code,
        tokens: exchanged,
        api: exchanged,
    }, { fold: 'challenge' });
})();
