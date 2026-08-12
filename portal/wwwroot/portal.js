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
})();
