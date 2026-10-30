// The Decode a token dialog: paste, and it's decoded. Instead of jwt.ms.
(() => {
    const modal = document.getElementById('decode-modal');
    const input = document.getElementById('decode-input');
    const view = document.getElementById('decode-view');
    const validate = document.getElementById('decode-validate');

    // Anything that isn't a whole token yet stays as it is: this runs on every keystroke, so no alerts.
    const refresh = () => {
        const token = input.value.trim();
        validate.disabled = !token;
        try {
            if (token) Tokens.decode(view, token);
            else Tokens.raw(view, token, 'token');
        } catch {
            Tokens.raw(view, token, 'token');
        }
    };

    input.addEventListener('input', refresh);
    validate.addEventListener('click', () => Validate.open('token', input.value.trim()));
    refresh();

    // The old token is selected, so the next paste replaces it.
    document.querySelectorAll('[data-decode]').forEach(button => button.addEventListener('click', () => {
        bootstrap.Modal.getOrCreateInstance(modal).show();
    }));
    modal.addEventListener('shown.bs.modal', () => input.select());
})();
