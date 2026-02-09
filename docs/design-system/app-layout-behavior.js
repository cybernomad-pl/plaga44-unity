// PROSTY KOD - klik w content chowa tytuł i drawer
(function() {
  let appLayout;
  let lastScrollTop = 0;

  function init() {
    appLayout = document.querySelector('vaadin-app-layout');
    if (!appLayout) {
      setTimeout(init, 100);
      return;
    }

    // Klik w document - chowaj wszystko
    document.addEventListener('click', (e) => {
      // Sprawdź czy kliknięto w hamburger lub jego obszar
      const toggle = e.target.closest('vaadin-drawer-toggle');
      if (toggle) return;

      // Sprawdź czy kliknięto w drawer
      const drawer = e.target.closest('[slot="drawer"]');
      if (drawer) return;

      // Sprawdź czy kliknięto w navbar (ale nie w content)
      const navbar = e.target.closest('[slot="navbar"]');
      if (navbar) return;

      // W przeciwnym razie - schowaj
      console.log('HIDING - clicked on:', e.target);
      appLayout.classList.add('hide-title');
      if (appLayout.hasAttribute('drawer-opened')) {
        appLayout.removeAttribute('drawer-opened');
      }
    }, true); // true = capture phase

    // Hover na hamburger - pokaż, mouse out - chowaj
    setTimeout(() => {
      const toggle = appLayout.querySelector('vaadin-drawer-toggle');
      if (toggle) {
        toggle.addEventListener('mouseenter', () => {
          appLayout.classList.remove('hide-title');
        });
        toggle.addEventListener('mouseleave', () => {
          appLayout.classList.add('hide-title');
        });
      }
    }, 500);

    // Scroll do góry - pokaż
    window.addEventListener('scroll', () => {
      const scrollTop = window.pageYOffset || document.documentElement.scrollTop;
      if (scrollTop < lastScrollTop || scrollTop < 50) {
        appLayout.classList.remove('hide-title');
      }
      lastScrollTop = scrollTop <= 0 ? 0 : scrollTop;
    });
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
  } else {
    init();
  }
})();
