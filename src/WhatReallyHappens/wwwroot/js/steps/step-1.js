// Step 1: where everything is, then an ID token straight back through the browser.
(() => {
    Parts.step('step-1', {
        config: Parts.config,
        challenge: (section, store) => Parts.challenge(section, store, 'step-1'),
        callback: Parts.callbackView,
        tokens: Parts.tokenPair,
    }, {
        challenge: s => !!s.config,
        callback: s => !!s.callback,
        tokens: s => !!s.tokens?.id_token,
    }, { fold: 'challenge' });
})();
