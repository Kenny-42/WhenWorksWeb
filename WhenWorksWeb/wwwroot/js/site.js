// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

(() => {
  const footer = document.querySelector(".site-footer");
  const main = document.querySelector("main[role='main']");
  if (!footer || !main) {
    return;
  }

  const updateFooterState = () => {
    const mainBottom = main.getBoundingClientRect().bottom;
    const footerTop = footer.getBoundingClientRect().top;
    footer.classList.toggle("footer-overlap", mainBottom > footerTop);
  };

  const scheduleUpdate = () => requestAnimationFrame(updateFooterState);

  window.addEventListener("load", scheduleUpdate);
  window.addEventListener("resize", scheduleUpdate);
  window.addEventListener("scroll", scheduleUpdate, { passive: true });

  if ("ResizeObserver" in window) {
    const observer = new ResizeObserver(scheduleUpdate);
    observer.observe(main);
    observer.observe(footer);
    observer.observe(document.body);
  }

  scheduleUpdate();
})();
