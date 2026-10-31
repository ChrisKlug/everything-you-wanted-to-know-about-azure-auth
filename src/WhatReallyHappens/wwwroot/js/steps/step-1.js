// Step 1: an ID token straight back through the browser.
(() => {
    Parts.step('step-1', {
        challenge: (section, store) => Parts.challenge(section, store, 'step-1'),
        callback: Parts.callbackView,
        tokens: Parts.tokenPair,
    }, {
        callback: s => !!s.callback,
        tokens: s => !!s.tokens?.id_token,
    }, { fold: 'challenge' });
})();
