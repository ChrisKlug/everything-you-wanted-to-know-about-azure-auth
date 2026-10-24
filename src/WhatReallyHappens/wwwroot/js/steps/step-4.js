// Step 4: a token for my API, and the checks that say it's real.
(() => {
    const exchanged = s => s.exchangeResponse?.status === 200;
    Parts.step('step-4', {
        challenge: (section, store) => Parts.challenge(section, store, 'step-4'),
        callback: Parts.callbackView,
        exchange: Parts.exchange,
        tokens: Parts.tokenPair,
    }, {
        callback: s => !!s.callback,
        exchange: s => !!s.tokens?.code,
        tokens: exchanged,
    }, { fold: 'challenge' });
})();
