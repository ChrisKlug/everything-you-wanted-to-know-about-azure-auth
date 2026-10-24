// Step 6: the web's token for my API, and my API trading it for a Graph token, On-Behalf-Of Damon.
(() => {
    const exchanged = s => s.exchangeResponse?.status === 200;
    Parts.step('step-6', {
        challenge: (section, store) => Parts.challenge(section, store, 'step-6'),
        callback: Parts.callbackView,
        exchange: Parts.exchange,
        tokens: Parts.tokenPair,
        obo: Parts.onBehalfOf,
        'obo-tokens': Parts.tokenPair,
        api: Parts.apiCall,
    }, {
        callback: s => !!s.callback,
        exchange: s => !!s.tokens?.code,
        tokens: exchanged,
        obo: exchanged,
        'obo-tokens': s => !!s.tokens?.obo_token,
        api: s => !!s.tokens?.obo_token,
    }, { fold: 'challenge' });
})();
