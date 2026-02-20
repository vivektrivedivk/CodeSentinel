window.scrollToBottom = (element) => {
    if (element && element.scrollIntoView) {
        element.scrollIntoView({ behavior: 'smooth', block: 'end' });
    }
};
