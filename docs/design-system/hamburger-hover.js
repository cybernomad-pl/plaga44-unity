// PROSTY HOVER - hamburger pokazuje tytuł
(function() {
  function init() {
    const hamburger = document.querySelector('.hamburger');
    const title = document.querySelector('.overlay-title');

    if (!hamburger || !title) {
      setTimeout(init, 100);
      return;
    }

    hamburger.addEventListener('mouseenter', () => {
      title.style.opacity = '1';
    });

    hamburger.addEventListener('mouseleave', () => {
      title.style.opacity = '0';
    });
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
  } else {
    init();
  }
})();
