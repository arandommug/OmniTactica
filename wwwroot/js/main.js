// Scroll to top functionality
window.initScrollToTop = (dotNetRef) => {
    window.addEventListener('scroll', () => {
        const scrollTop = window.pageYOffset || document.documentElement.scrollTop;
        dotNetRef.invokeMethodAsync('UpdateVisibility', scrollTop > 300);
    });
};

window.scrollToTop = () => {
    window.scrollTo({
        top: 0,
        behavior: 'smooth'
    });
};

// Scroll position preservation
window.saveScrollPosition = (key) => {
    const scrollTop = window.pageYOffset || document.documentElement.scrollTop;
    sessionStorage.setItem(key, scrollTop.toString());
};

window.restoreScrollPosition = (key) => {
    const scrollTop = sessionStorage.getItem(key);
    if (scrollTop) {
        window.scrollTo(0, parseInt(scrollTop));
        sessionStorage.removeItem(key);
    }
};

// Initialize Bootstrap tooltips
window.initTooltips = () => {
    // Dispose existing tooltips first to avoid duplicates
    const existingTooltips = document.querySelectorAll('[data-bs-toggle="tooltip"]');
    existingTooltips.forEach(el => {
        const existing = bootstrap.Tooltip.getInstance(el);
        if (existing) {
            existing.dispose();
        }
    });
    // Initialize all tooltips on the page
    const tooltipTriggerList = document.querySelectorAll('[data-bs-toggle="tooltip"]');
    const tooltipList = [...tooltipTriggerList].map(tooltipTriggerEl => new bootstrap.Tooltip(tooltipTriggerEl, {
        html: true,
        trigger: 'hover',
        sanitize: false,
        customClass: 'ability-tooltip'
    }));
};

// Auto-initialize tooltips when DOM changes (for Blazor dynamic content)
document.addEventListener('DOMContentLoaded', () => {
    window.initTooltips();

    if (typeof MutationObserver !== 'undefined') {
        const observer = new MutationObserver((mutations) => {
            // Debounce tooltip initialization
            clearTimeout(window.tooltipInitTimeout);
            window.tooltipInitTimeout = setTimeout(() => {
                window.initTooltips();
            }, 200);
        });

        // Observe the entire app for changes
        const appElement = document.getElementById('app');
        if (appElement) {
            observer.observe(appElement, {
                childList: true,
                subtree: true
            });
        }
    }
});
