window.scrollToTopListeners = window.scrollToTopListeners || new Map();

// Scroll to top functionality
window.initScrollToTop = (listenerId, dotNetRef) => {
    window.disposeScrollToTop(listenerId);

    let ticking = false;
    let lastVisible = false;

    const updateVisibility = () => {
        ticking = false;

        const scrollTop = window.pageYOffset || document.documentElement.scrollTop;
        const isVisible = scrollTop > 300;
        if (isVisible === lastVisible) {
            return;
        }

        lastVisible = isVisible;
        dotNetRef.invokeMethodAsync('UpdateVisibility', isVisible);
    };

    const onScroll = () => {
        if (ticking) {
            return;
        }

        ticking = true;
        window.requestAnimationFrame(updateVisibility);
    };

    window.scrollToTopListeners.set(listenerId, onScroll);
    window.addEventListener('scroll', onScroll, { passive: true });
};

window.disposeScrollToTop = (listenerId) => {
    const onScroll = window.scrollToTopListeners.get(listenerId);
    if (!onScroll) {
        return;
    }

    window.removeEventListener('scroll', onScroll);
    window.scrollToTopListeners.delete(listenerId);
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

// Body scroll lock — prevent background page scroll when an overlay is open
window.lockBodyScroll = () => {
    document.body.style.overflow = 'hidden';
};

window.unlockBodyScroll = () => {
    document.body.style.overflow = '';
};

window.bootstrapInterop = {
    showModal: (id) => {
        const element = document.getElementById(id);
        if (!element) {
            return;
        }

        const modal = bootstrap.Modal.getOrCreateInstance(element);
        modal.show();
    },

    hideModal: (id) => {
        const element = document.getElementById(id);
        if (!element) {
            return;
        }

        const modal = bootstrap.Modal.getInstance(element);
        modal?.hide();
    },

    onModalHidden: (id, dotNetRef) => {
        const element = document.getElementById(id);
        if (!element) {
            return;
        }

        const handler = () => {
            element.removeEventListener('hidden.bs.modal', handler);
            dotNetRef.invokeMethodAsync('OnModalHidden');
        };
        element.addEventListener('hidden.bs.modal', handler);
    }
};

// Initialize Bootstrap tooltips
window.initTooltips = (root) => {
    const container = root instanceof Element ? root : document;
    const tooltipElements = [];

    if (container.matches?.('[data-bs-toggle="tooltip"]')) {
        tooltipElements.push(container);
    }

    tooltipElements.push(...container.querySelectorAll?.('[data-bs-toggle="tooltip"]') ?? []);

    tooltipElements.forEach((element) => {
        if (bootstrap.Tooltip.getInstance(element)) {
            return;
        }

        new bootstrap.Tooltip(element, {
            html: true,
            trigger: 'hover',
            sanitize: false,
            customClass: 'ability-tooltip'
        });
    });
};

// Auto-initialize tooltips when DOM changes (for Blazor dynamic content)
document.addEventListener('DOMContentLoaded', () => {
    window.initTooltips();

    if (typeof MutationObserver !== 'undefined') {
        let pendingRoots = [];

        const observer = new MutationObserver((mutations) => {
            clearTimeout(window.tooltipInitTimeout);

            pendingRoots = mutations
                .flatMap(mutation => [...mutation.addedNodes])
                .filter(node => node.nodeType === Node.ELEMENT_NODE);

            window.tooltipInitTimeout = setTimeout(() => {
                if (!pendingRoots.length) {
                    return;
                }

                pendingRoots.forEach(root => window.initTooltips(root));
                pendingRoots = [];
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
