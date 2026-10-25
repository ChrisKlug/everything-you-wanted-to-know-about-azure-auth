// Step 8: signing in from another organization. The authority is organizations, so the tokens come from InGen.
(() => {
    const exchanged = s => s.exchangeResponse?.status === 200;
    Parts.step('step-8', {
        challenge: (section, store) => Parts.challenge(section, store, 'step-8', { tenant: 'organizations' }),
        callback: Parts.callbackView,
        exchange: (section, store) => Parts.exchange(section, store, { tenant: 'organizations' }),
        tokens: Parts.tokenPair,
        api: Parts.apiCall,
    }, {
        callback: s => !!s.callback,
        exchange: s => !!s.tokens?.code,
        tokens: exchanged,
        api: exchanged,
    }, { fold: 'challenge' });
})();
