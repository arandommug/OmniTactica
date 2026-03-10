// Bookmark navigation utilities

window.bookmarkNavigation = {
    // Expand a Bootstrap accordion item
    expandAccordion: function(accordionId) {
        if (!accordionId) return;
        
        const element = document.getElementById(accordionId);
        if (element) {
            // If it's a collapse element, show it
            const bsCollapse = new bootstrap.Collapse(element, {
                toggle: false
            });
            bsCollapse.show();
            
            // Scroll to the element after a short delay to ensure it's expanded
            setTimeout(() => {
                element.scrollIntoView({ behavior: 'smooth', block: 'start' });
            }, 300);
        }
    },
    
    // Set a select dropdown value
    setDropdownValue: function(dropdownId, value) {
        if (!dropdownId) return;
        
        const dropdown = document.getElementById(dropdownId);
        if (dropdown) {
            dropdown.value = value;
            // Trigger change event to notify Blazor
            dropdown.dispatchEvent(new Event('change', { bubbles: true }));
        }
    }
};
