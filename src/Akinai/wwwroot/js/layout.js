// Layout JavaScript module for Blazor interop

export function toggleMobileSidebar() {
    const sidebar = document.getElementById('sidebar');
    const overlay = document.getElementById('sidebar-overlay');
    if (sidebar && overlay) {
        sidebar.classList.toggle('-translate-x-full');
        overlay.classList.toggle('hidden');
    }
}

export function toggleSidebarCollapse() {
    const sidebar = document.getElementById('sidebar');
    const mainContent = document.getElementById('main-content');
    const collapseIcon = document.getElementById('collapse-icon');

    if (!sidebar || !mainContent) return false;

    const isExpanded = sidebar.classList.contains('sidebar-expanded');

    if (isExpanded) {
        sidebar.classList.remove('sidebar-expanded');
        sidebar.classList.add('sidebar-collapsed');
        mainContent.classList.remove('sidebar-expanded-margin');
        mainContent.classList.add('sidebar-collapsed-margin');
        if (collapseIcon) collapseIcon.style.transform = 'rotate(180deg)';
        localStorage.setItem('sidebarCollapsed', 'true');
        return true; // collapsed
    } else {
        sidebar.classList.remove('sidebar-collapsed');
        sidebar.classList.add('sidebar-expanded');
        mainContent.classList.remove('sidebar-collapsed-margin');
        mainContent.classList.add('sidebar-expanded-margin');
        if (collapseIcon) collapseIcon.style.transform = 'rotate(0deg)';
        localStorage.setItem('sidebarCollapsed', 'false');
        return false; // expanded
    }
}

export function toggleDarkMode() {
    const html = document.documentElement;
    const isDark = html.classList.toggle('dark');
    localStorage.setItem('darkMode', isDark ? 'true' : 'false');
    return isDark;
}

export function getDarkMode() {
    const stored = localStorage.getItem('darkMode');
    if (stored !== null) {
        return stored === 'true';
    }
    // Fall back to system preference
    return window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches;
}

export function setDarkMode(isDark) {
    const html = document.documentElement;
    if (isDark) {
        html.classList.add('dark');
    } else {
        html.classList.remove('dark');
    }
    localStorage.setItem('darkMode', isDark ? 'true' : 'false');
}

export function getSidebarCollapsed() {
    return localStorage.getItem('sidebarCollapsed') === 'true';
}

export function restoreSidebarState() {
    const isCollapsed = localStorage.getItem('sidebarCollapsed') === 'true';
    if (isCollapsed) {
        const sidebar = document.getElementById('sidebar');
        const mainContent = document.getElementById('main-content');
        const collapseIcon = document.getElementById('collapse-icon');
        if (sidebar && mainContent) {
            sidebar.classList.remove('sidebar-expanded');
            sidebar.classList.add('sidebar-collapsed');
            mainContent.classList.remove('sidebar-expanded-margin');
            mainContent.classList.add('sidebar-collapsed-margin');
            if (collapseIcon) collapseIcon.style.transform = 'rotate(180deg)';
        }
    }
    return isCollapsed;
}
