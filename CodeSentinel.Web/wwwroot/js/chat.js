// CodeSentinel - JavaScript Interop Functions

/**
 * Initialize chat functionality
 * @param {Element} messageListRef - Reference to the messages container
 */
function initChat(messageListRef) {
    window.messageListRef = messageListRef;
}

/**
 * Scroll to the bottom of the messages container
 * @param {Element} element - The container element
 */
function scrollToBottom(element) {
    if (element) {
        element.scrollTop = element.scrollHeight;
    }
}

/**
 * Auto-resize textarea based on content
 * @param {HTMLTextAreaElement} textarea - The textarea element
 */
function autoResizeTextarea(textarea) {
    if (!textarea) return;

    // Reset height to auto to get the correct scrollHeight
    textarea.style.height = 'auto';

    // Calculate new height (min 44px, max 200px)
    const newHeight = Math.min(Math.max(textarea.scrollHeight, 44), 200);
    textarea.style.height = newHeight + 'px';
}

/**
 * Copy code to clipboard
 * @param {HTMLElement} button - The copy button element
 */
async function copyCode(button) {
    const codeBlock = button.closest('.code-block');
    if (!codeBlock) return;

    const code = codeBlock.querySelector('code');
    if (!code) return;

    try {
        await navigator.clipboard.writeText(code.textContent || '');

        // Update button text temporarily
        const originalHTML = button.innerHTML;
        button.classList.add('copied');
        button.innerHTML = `
            <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <polyline points="20 6 9 17 4 12"></polyline>
            </svg>
            <span>Copied!</span>
        `;

        setTimeout(() => {
            button.classList.remove('copied');
            button.innerHTML = originalHTML;
        }, 2000);
    } catch (err) {
        console.error('Failed to copy code:', err);
    }
}

/**
 * Highlight code blocks with syntax highlighting
 * This is a placeholder for future Prism.js or highlight.js integration
 */
function highlightCode() {
    // Placeholder for syntax highlighting initialization
    // In the future, integrate with Prism.js or highlight.js
    console.log('Code highlighting initialized');
}

/**
 * Apply theme to document
 * @param {string} theme - The theme name ('dark' or 'light')
 */
function applyTheme(theme) {
    document.documentElement.setAttribute('data-theme', theme);
    localStorage.setItem('codesentinel-theme', theme);
}

/**
 * Get saved theme from localStorage
 * @returns {string} The saved theme or 'dark' as default
 */
function getSavedTheme() {
    return localStorage.getItem('codesentinel-theme') || 'dark';
}

/**
 * Save settings to localStorage
 * @param {string} key - The settings key
 * @param {string} value - The settings value (JSON string)
 */
function saveSettings(key, value) {
    localStorage.setItem(key, value);
}

/**
 * Load settings from localStorage
 * @param {string} key - The settings key
 * @returns {string|null} The stored value or null
 */
function loadSettings(key) {
    return localStorage.getItem(key);
}

/**
 * Focus an element
 * @param {HTMLElement} element - The element to focus
 */
function focusElement(element) {
    if (element) {
        element.focus();
    }
}

/**
 * Select text in an input element
 * @param {HTMLInputElement|HTMLTextAreaElement} element - The input element
 */
function selectText(element) {
    if (element) {
        element.select();
    }
}

/**
 * Get element's bounding rectangle
 * @param {HTMLElement} element - The element
 * @returns {DOMRect} The bounding rectangle
 */
function getBoundingRect(element) {
    return element ? element.getBoundingClientRect() : null;
}

/**
 * Check if element is in viewport
 * @param {HTMLElement} element - The element to check
 * @returns {boolean} True if element is in viewport
 */
function isInViewport(element) {
    if (!element) return false;

    const rect = element.getBoundingClientRect();
    return (
        rect.top >= 0 &&
        rect.left >= 0 &&
        rect.bottom <= (window.innerHeight || document.documentElement.clientHeight) &&
        rect.right <= (window.innerWidth || document.documentElement.clientWidth)
    );
}

/**
 * Observe element visibility changes
 * @param {HTMLElement} element - The element to observe
 * @param {string} dotNetHelper - The .NET helper reference
 */
function observeVisibility(element, dotNetHelper) {
    if (!element || !window.IntersectionObserver) return;

    const observer = new IntersectionObserver((entries) => {
        entries.forEach(entry => {
            if (entry.isIntersecting) {
                dotNetHelper.invokeMethodAsync('OnElementVisible');
            }
        });
    });

    observer.observe(element);

    // Store observer for cleanup
    element._visibilityObserver = observer;
}

/**
 * Disconnect visibility observer
 * @param {HTMLElement} element - The element being observed
 */
function disconnectVisibilityObserver(element) {
    if (element && element._visibilityObserver) {
        element._visibilityObserver.disconnect();
        delete element._visibilityObserver;
    }
}

/**
 * Register keyboard shortcut
 * @param {string} key - The key combination (e.g., 'Ctrl+B')
 * @param {Function} callback - The callback function
 */
function registerShortcut(key, callback) {
    document.addEventListener('keydown', (e) => {
        const keys = key.toLowerCase().split('+');
        const keyPressed = keys.every(k => {
            if (k === 'ctrl') return e.ctrlKey || e.metaKey;
            if (k === 'shift') return e.shiftKey;
            if (k === 'alt') return e.altKey;
            return e.key.toLowerCase() === k;
        });

        if (keyPressed) {
            e.preventDefault();
            callback();
        }
    });
}

/**
 * Initialize all CodeSentinel functionality
 */
function initCodeSentinel() {
    // Apply saved theme
    const savedTheme = getSavedTheme();
    applyTheme(savedTheme);

    // Register global shortcuts
    registerShortcut('Ctrl+B', () => {
        // Toggle sidebar - this will be handled by Blazor
        console.log('Toggle sidebar shortcut');
    });

    console.log('CodeSentinel initialized');
}

// Initialize on DOM ready
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initCodeSentinel);
} else {
    initCodeSentinel();
}

// Export functions for Blazor interop
window.CodeSentinel = {
    initChat,
    scrollToBottom,
    autoResizeTextarea,
    copyCode,
    highlightCode,
    applyTheme,
    getSavedTheme,
    saveSettings,
    loadSettings,
    focusElement,
    selectText,
    getBoundingRect,
    isInViewport,
    observeVisibility,
    disconnectVisibilityObserver,
    registerShortcut
};