/**
 * BitFlow Application Utilities
 * Unified JavaScript module for culture, theme, and app utilities
 * @version 1.0.0
 */

(function (window, document) {
    'use strict';

    // ===========================================
    // TAILWIND CONFIGURATION
    // ===========================================
    
    if (window.tailwind && window.tailwind.config) {
        window.tailwind.config = {
            theme: {
                extend: {
                    colors: {
                        purple: {
                            50: '#EBF7FF',
                            100: '#D7F0FF',
                            200: '#B0E2FF',
                            300: '#88D3FF',
                            400: '#61C5FF',
                            500: '#38B6FF',
                            600: '#38B6FF', // Primary brand
                            700: '#2D92CC',
                            800: '#1A8FD6',
                            900: '#1070AC',
                            950: '#0A4F7A',
                        }
                    }
                }
            }
        };
    }

    // ===========================================
    // COOKIE MANAGEMENT
    // ===========================================
    
    const Cookie = {
        get(name) {
            const value = `; ${document.cookie}`;
            const parts = value.split(`; ${name}=`);
            return parts.length === 2 ? parts.pop().split(';').shift() : null;
        },

        set(name, value, days = 365) {
            const expires = new Date();
            expires.setTime(expires.getTime() + (days * 24 * 60 * 60 * 1000));
            document.cookie = `${name}=${value}; expires=${expires.toUTCString()}; path=/; SameSite=Lax`;
        },

        remove(name) {
            document.cookie = `${name}=; expires=Thu, 01 Jan 1970 00:00:00 UTC; path=/;`;
        }
    };

    // ===========================================
    // URL & QUERY PARAMETERS
    // ===========================================
    
    const URL = {
        getQueryParam(name) {
            const params = new URLSearchParams(window.location.search);
            return params.get(name);
        },

        getAllQueryParams() {
            const params = new URLSearchParams(window.location.search);
            const result = {};
            for (const [key, value] of params) {
                result[key] = value;
            }
            return result;
        },

        updateQueryParam(name, value) {
            const url = new window.URL(window.location);
            url.searchParams.set(name, value);
            window.history.pushState({}, '', url);
        }
    };

    // ===========================================
    // CULTURE & LOCALIZATION
    // ===========================================
    
    const Culture = {
        current: null,

        initialize() {
            let culture = URL.getQueryParam('culture') || URL.getQueryParam('ui-culture');

            if (!culture) {
                const cookie = Cookie.get('.AspNetCore.Culture');
                if (cookie) {
                    const match = cookie.match(/c=([^|;]+)/);
                    if (match) culture = match[1];
                }
            }

            if (!culture) {
                culture = navigator.language || navigator.userLanguage || 'en';
            }

            culture = culture.split('-')[0].toLowerCase();
            this.current = culture;

            // Apply to DOM
            document.documentElement.lang = culture;
            const dir = (culture === 'ar') ? 'rtl' : 'ltr';
            document.documentElement.dir = dir;
            if (document.body) document.body.dir = dir;

            return culture;
        },

        setCookie(culture) {
            try {
                const cookieValue = `c=${culture}|uic=${culture}`;
                const expires = new Date();
                expires.setFullYear(expires.getFullYear() + 1);
                document.cookie = `.AspNetCore.Culture=${cookieValue}; expires=${expires.toUTCString()}; path=/; SameSite=Lax`;
                // Page reload is handled by Blazor NavigationManager on the C# side
            } catch (e) {
                console.error('Failed to set culture cookie:', e);
            }
        },

        isRTL() {
            return this.current === 'ar';
        }
    };

    // ===========================================
    // THEME MANAGEMENT
    // ===========================================
    
    const Theme = {
        // Color conversion utilities
        hexToRgb(hex) {
            hex = hex.trim().replace('#', '');
            if (hex.length === 3) {
                hex = hex.split('').map(c => c + c).join('');
            }
            if (hex.length !== 6) return null;

            return {
                r: parseInt(hex.substr(0, 2), 16),
                g: parseInt(hex.substr(2, 2), 16),
                b: parseInt(hex.substr(4, 2), 16)
            };
        },

        rgbToHex(r, g, b) {
            return `#${r.toString(16).padStart(2, '0')}${g.toString(16).padStart(2, '0')}${b.toString(16).padStart(2, '0')}`;
        },

        // Calculate relative luminance
        getLuminance(r, g, b) {
            return (0.299 * r + 0.587 * g + 0.114 * b) / 255;
        },

        // Get contrast color (black or white)
        getContrastColor(r, g, b) {
            const lum = this.getLuminance(r, g, b);
            return lum > 0.6 ? '#000000' : '#ffffff';
        },

        // Darken color
        darken(value, factor = 0.85) {
            return Math.max(0, Math.min(255, Math.round(value * factor)));
        },

        // Create tint (lighter version)
        createTint(r, g, b, percent) {
            const tintR = Math.round(r + (255 - r) * (1 - percent));
            const tintG = Math.round(g + (255 - g) * (1 - percent));
            const tintB = Math.round(b + (255 - b) * (1 - percent));
            return this.rgbToHex(tintR, tintG, tintB);
        },

        // Set theme colors
        setColors(primaryColor) {
            try {
                if (!primaryColor) return false;

                const rgb = this.hexToRgb(primaryColor);
                if (!rgb) {
                    console.error('Invalid color format:', primaryColor);
                    return false;
                }

                const { r, g, b } = rgb;
                const primaryHex = this.rgbToHex(r, g, b);
                const textColor = this.getContrastColor(r, g, b);
                const darkHex = this.rgbToHex(
                    this.darken(r),
                    this.darken(g),
                    this.darken(b)
                );

                // Set CSS custom properties
                const root = document.documentElement.style;
                const props = {
                    '--bf-primary': primaryHex,
                    '--bf-primary-text': textColor,
                    '--bf-primary-contrast': textColor,
                    '--bf-primary-dark': darkHex,
                    '--bf-primary-50': this.createTint(r, g, b, 0.06),
                    '--bf-primary-100': this.createTint(r, g, b, 0.12),
                    '--bf-primary-200': this.createTint(r, g, b, 0.24),
                    '--bf-primary-300': this.createTint(r, g, b, 0.36),
                    '--bf-primary-400': this.createTint(r, g, b, 0.48),
                    '--bf-primary-500': this.createTint(r, g, b, 0.64),
                    '--bf-primary-600': primaryHex,
                    '--bf-primary-700': darkHex,
                    '--bf-primary-800': this.rgbToHex(
                        this.darken(r, 0.85),
                        this.darken(g, 0.85),
                        this.darken(b, 0.85)
                    ),
                    '--bf-primary-900': this.rgbToHex(
                        this.darken(r, 0.92),
                        this.darken(g, 0.92),
                        this.darken(b, 0.92)
                    )
                };

                Object.entries(props).forEach(([key, value]) => {
                    root.setProperty(key, value);
                });

                console.log('? Theme colors updated:', primaryHex);
                return true;
            } catch (error) {
                console.error('Theme.setColors error:', error);
                return false;
            }
        },

        // Get current primary color
        getCurrentColor() {
            return getComputedStyle(document.documentElement)
                .getPropertyValue('--bf-primary')
                .trim();
        }
    };

    // ===========================================
    // DOM UTILITIES
    // ===========================================
    
    const DOM = {
        ready(callback) {
            if (document.readyState !== 'loading') {
                callback();
            } else {
                document.addEventListener('DOMContentLoaded', callback);
            }
        },

        on(element, event, callback) {
            if (typeof element === 'string') {
                element = document.querySelector(element);
            }
            if (element) {
                element.addEventListener(event, callback);
            }
        },

        toggle(element, className) {
            if (typeof element === 'string') {
                element = document.querySelector(element);
            }
            if (element) {
                element.classList.toggle(className);
            }
        },

        hide(elementId) {
            const element = typeof elementId === 'string' 
                ? document.getElementById(elementId) 
                : elementId;
            if (element) {
                element.style.display = 'none';
            }
        },

        show(elementId) {
            const element = typeof elementId === 'string' 
                ? document.getElementById(elementId) 
                : elementId;
            if (element) {
                element.style.display = '';
            }
        }
    };

    // ===========================================
    // STORAGE (LocalStorage/SessionStorage)
    // ===========================================
    
    const Storage = {
        local: {
            get(key) {
                try {
                    const item = localStorage.getItem(key);
                    return item ? JSON.parse(item) : null;
                } catch (e) {
                    console.error('Storage.local.get error:', e);
                    return null;
                }
            },

            set(key, value) {
                try {
                    localStorage.setItem(key, JSON.stringify(value));
                    return true;
                } catch (e) {
                    console.error('Storage.local.set error:', e);
                    return false;
                }
            },

            remove(key) {
                try {
                    localStorage.removeItem(key);
                    return true;
                } catch (e) {
                    console.error('Storage.local.remove error:', e);
                    return false;
                }
            },

            clear() {
                try {
                    localStorage.clear();
                    return true;
                } catch (e) {
                    console.error('Storage.local.clear error:', e);
                    return false;
                }
            }
        },

        session: {
            get(key) {
                try {
                    const item = sessionStorage.getItem(key);
                    return item ? JSON.parse(item) : null;
                } catch (e) {
                    console.error('Storage.session.get error:', e);
                    return null;
                }
            },

            set(key, value) {
                try {
                    sessionStorage.setItem(key, JSON.stringify(value));
                    return true;
                } catch (e) {
                    console.error('Storage.session.set error:', e);
                    return false;
                }
            },

            remove(key) {
                try {
                    sessionStorage.removeItem(key);
                    return true;
                } catch (e) {
                    console.error('Storage.session.remove error:', e);
                    return false;
                }
            },

            clear() {
                try {
                    sessionStorage.clear();
                    return true;
                } catch (e) {
                    console.error('Storage.session.clear error:', e);
                    return false;
                }
            }
        }
    };

    // ===========================================
    // ADMIN DARK MODE
    // ===========================================

    const AdminDarkMode = {
        get() {
            try { return localStorage.getItem('admin-dark-mode') === 'true'; }
            catch (e) { return false; }
        },
        set(val) {
            try { localStorage.setItem('admin-dark-mode', val ? 'true' : 'false'); }
            catch (e) {}
        }
    };

    // ===========================================
    // BLAZOR ERROR UI HANDLER
    // ===========================================
    
    const ErrorUI = {
        hide() {
            DOM.hide('blazor-error-ui');
        },

        show() {
            DOM.show('blazor-error-ui');
        }
    };

    // ===========================================
    // INITIALIZATION
    // ===========================================
    
    // Auto-initialize culture on load
    Culture.initialize();

    // Setup error UI close handler
    DOM.ready(() => {
        const errorUI = document.getElementById('blazor-error-ui');
        if (errorUI) {
            const closeButton = errorUI.querySelector('button');
            if (closeButton) {
                closeButton.onclick = () => ErrorUI.hide();
            }
        }
    });

    // ===========================================
    // PUBLIC API
    // ===========================================
    
    const App = {
        Cookie,
        URL,
        Culture,
        Theme,
        DOM,
        Storage,
        ErrorUI,
        version: '1.0.0'
    };

    // Export to window
    const CashierDarkMode = {
        get() {
            try { return localStorage.getItem('cashier-dark-mode') === 'true'; }
            catch (e) { return false; }
        },
        set(val) {
            try { localStorage.setItem('cashier-dark-mode', val ? 'true' : 'false'); }
            catch (e) {}
        }
    };

    // ===========================================
    // FILE DOWNLOAD (used by export-to-Excel etc.)
    // ===========================================

    App.getRect = function (el) {
        if (!el || typeof el.getBoundingClientRect !== 'function') return null;
        const r = el.getBoundingClientRect();
        return {
            top: r.top,
            left: r.left,
            bottom: r.bottom,
            right: r.right,
            width: r.width,
            height: r.height,
            viewportWidth: window.innerWidth,
            viewportHeight: window.innerHeight
        };
    };

    App.downloadFile = function (filename, contentType, base64Data) {
        try {
            const binary = atob(base64Data);
            const len = binary.length;
            const bytes = new Uint8Array(len);
            for (let i = 0; i < len; i++) {
                bytes[i] = binary.charCodeAt(i);
            }
            const blob = new Blob([bytes], { type: contentType || 'application/octet-stream' });
            const url = window.URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url;
            a.download = filename || 'download';
            document.body.appendChild(a);
            a.click();
            document.body.removeChild(a);
            // Defer revoke so the browser has a moment to start the download.
            setTimeout(() => window.URL.revokeObjectURL(url), 1000);
            return true;
        } catch (e) {
            console.error('App.downloadFile error:', e);
            return false;
        }
    };

    // ===========================================
    // AI CHAT WIDGET RESIZER (drag inner edge to set width)
    // ===========================================

    const AiChatResizer = {
        start(startX, isMaximized, isRtl) {
            const panel = document.querySelector('.ai-chat-panel');
            if (!panel) return;

            const startWidth = panel.getBoundingClientRect().width;
            const MIN = 288;                                   // 18rem
            const MAX = Math.min(window.innerWidth * 0.6, 760);
            const root = document.documentElement.style;
            const prop = isMaximized ? '--ai-dock-w' : '--ai-chat-float-w';

            const onMove = (e) => {
                // Dragging the inner edge toward the page center widens the panel.
                const delta = isRtl ? (e.clientX - startX) : (startX - e.clientX);
                const w = Math.max(MIN, Math.min(MAX, startWidth + delta));
                root.setProperty(prop, w + 'px');
            };
            const onUp = () => {
                document.removeEventListener('pointermove', onMove);
                document.removeEventListener('pointerup', onUp);
                document.body.style.userSelect = '';
                document.body.style.cursor = '';
            };

            document.body.style.userSelect = 'none';
            document.body.style.cursor = 'ew-resize';
            document.addEventListener('pointermove', onMove);
            document.addEventListener('pointerup', onUp);
        }
    };

    window.App = App;
    window.AdminDarkMode = AdminDarkMode;
    window.CashierDarkMode = CashierDarkMode;
    window.AiChatResizer = AiChatResizer;

    // Backward compatibility aliases
    window.setCultureCookie = Culture.setCookie.bind(Culture);
    // Disable runtime theme overrides so styles come from static CSS only
    window.setThemeColors = function() { console.log('setThemeColors disabled - using static CSS only'); return false; };
    window.AppUtils = {
        getCookie: Cookie.get,
        setCookie: Cookie.set,
        getQueryParam: URL.getQueryParam,
        setCultureCookie: Culture.setCookie.bind(Culture),
        // provide a disabled placeholder for theme setter
        setThemeColors: function() { console.log('AppUtils.setThemeColors disabled - using static CSS only'); return false; },
                initializeCulture: Culture.initialize.bind(Culture),
                printBarcodeNow: function(barcodeValue, barcodeSvg) {
                        if (!barcodeValue || !barcodeSvg) return;

                        const printWindow = window.open('', '_blank', 'width=420,height=620');
                        if (!printWindow) return;

                        const safeValue = String(barcodeValue)
                                .replace(/&/g, '&amp;')
                                .replace(/</g, '&lt;')
                                .replace(/>/g, '&gt;')
                                .replace(/"/g, '&quot;')
                                .replace(/'/g, '&#39;');

                        const html = `<!DOCTYPE html>
<html>
<head>
    <meta charset="utf-8" />
    <title>Barcode</title>
    <style>
        @page { margin: 8mm; }
        html, body { margin: 0; padding: 0; background: #fff; }
        body {
            font-family: Arial, sans-serif;
            display: flex;
            align-items: center;
            justify-content: center;
            min-height: 100vh;
        }
        .label {
            width: 100%;
            max-width: 340px;
            text-align: center;
            border: 1px solid #000;
            padding: 8px;
            box-sizing: border-box;
        }
        .barcode svg {
            width: 100%;
            height: auto;
            display: block;
        }
        .value {
            margin-top: 8px;
            font: 600 14px/1.2 Consolas, 'Courier New', monospace;
            letter-spacing: 0.06em;
            word-break: break-all;
        }
    </style>
</head>
<body>
    <div class="label">
        <div class="barcode">${barcodeSvg}</div>
        <div class="value">${safeValue}</div>
    </div>
    <script>
        window.onload = function () {
            window.focus();
            window.print();
        };
    <\/script>
</body>
</html>`;

                        printWindow.document.open();
                        printWindow.document.write(html);
                        printWindow.document.close();
                }
    };

    // Log initialization
    console.log(`%c? App v${App.version} loaded`, 'color: #38B6FF; font-weight: bold;');
    console.log(`  Culture: ${Culture.current} | RTL: ${Culture.isRTL()}`);

})(window, document);
