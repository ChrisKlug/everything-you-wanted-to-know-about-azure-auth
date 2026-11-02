// Step 7: signing in from another organization. The authority is organizations, so the tokens come from InGen.
(() => {
    const exchanged = s => s.exchangeResponse?.status === 200;
    Parts.step('step-7', {
        challenge: (section, store) => Parts.challenge(section, store, 'step-7', { tenant: 'organizations' }),
        callback: Parts.callbackView,
        exchange: (section, store) => Parts.exchange(section, store, { tenant: 'organizations' }),
        tokens: Parts.tokenPair,
        api: Parts.apiCall,
    }, {
        callback: Parts.callbackError,
        exchange: s => !!s.tokens?.code,
        tokens: exchanged,
        api: exchanged,
    });
})();
