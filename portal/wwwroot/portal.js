(() => {
  const tokenForm = document.querySelector('[data-token-form]');
  if (tokenForm && window.location.hash.startsWith('#')) {
    const values = new URLSearchParams(window.location.hash.slice(1));
    const token = values.get('token');
    if (token) {
      tokenForm.querySelector('input[name="Token"]').value = token;
      history.replaceState(null, '', window.location.pathname);
      tokenForm.requestSubmit();
    }
  }

  const lensResult = document.querySelector('[data-lens-result]');
  if (lensResult && window.location.hash === '#lens-conclusion') {
    window.addEventListener('load', () => lensResult.focus({ preventScroll: true }), { once: true });
  }
})();
