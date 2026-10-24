// The Decode a token dialog: paste, split, decode. Instead of jwt.ms.
(() => {
    const modal = document.getElementById('decode-modal');
    const input = document.getElementById('decode-input');
    const refresh = Tokens.mountViews(document.getElementById('decode-views'), () => ({ token: input.value.trim() }));

    input.addEventListener('input', refresh);
    refresh();

    document.querySelectorAll('[data-decode]').forEach(button => button.addEventListener('click', () => {
        bootstrap.Modal.getOrCreateInstance(modal).show();
        input.focus();
    }));
})();
