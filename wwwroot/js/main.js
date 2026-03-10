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
