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
