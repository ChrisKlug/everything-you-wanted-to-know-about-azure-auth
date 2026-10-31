// Step 5: an app with no user. Its own credentials, a token, and what my API says to it.
(() => {
    Parts.step('step-5', {
        'client-credentials': Parts.clientCredentials,
        tokens: Parts.tokenPair,
        api: Parts.apiCall,
    }, {
        tokens: s => !!s.tokens?.access_token,
        api: s => !!s.tokens?.access_token,
    });
})();
