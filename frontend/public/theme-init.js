// Apply the stored theme before first paint to avoid a flash of the wrong
// theme. Light is the default; ThemeService is the runtime source of truth.
// External file (not inline) so the CSP can stay at script-src 'self'.
(function () {
  try {
    var stored = localStorage.getItem('judo.theme');
    document.documentElement.setAttribute('data-theme', stored === 'dark' ? 'dark' : 'light');
  } catch (e) {
    document.documentElement.setAttribute('data-theme', 'light');
  }
})();
