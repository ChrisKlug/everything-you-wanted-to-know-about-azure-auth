// Step 11: the code flow with no secret. The managed identity's token is the credential.
(() => {
    const exchanged = s => s.exchangeResponse?.status === 200;
    Parts.step('step-11', {
        challenge: (section, store) => Parts.challenge(section, store, 'step-11'),
        callback: Parts.callbackView,
        exchange: (section, store) => Parts.exchange(section, store, { credential: 'assertion' }),
        tokens: Parts.tokenPair,
        api: Parts.apiCall,
    }, {
        callback: s => !!s.callback,
        exchange: s => !!s.tokens?.code,
        tokens: exchanged,
        api: exchanged,
    }, { fold: 'challenge' });
})();
