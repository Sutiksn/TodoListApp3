/**
 * =============================================================================
 * TASKFLOW ENTERPRISE - UI UTILITIES & HELPERS
 * =============================================================================
 */

function initTheme() {
    const savedTheme = localStorage.getItem('taskflow_theme') || 'light';
    applyTheme(savedTheme);
}

function applyTheme(theme) {
    const activeTheme = (theme === 'dark') ? 'dark' : 'light';
    document.documentElement.setAttribute('data-bs-theme', activeTheme);
    if (document.body) {
        document.body.setAttribute('data-bs-theme', activeTheme);
    }
    localStorage.setItem('taskflow_theme', activeTheme);

    // Auth screen toggle button
    const authIcon = document.getElementById('authThemeIcon');
    const authLabel = document.getElementById('authThemeLabel');
    if (authIcon) {
        authIcon.className = activeTheme === 'dark' ? 'bi bi-sun-fill text-warning' : 'bi bi-moon-stars text-secondary';
    }
    if (authLabel) {
        authLabel.textContent = activeTheme === 'dark' ? 'Light' : 'Dark';
    }

    // Navbar toggle button
    const navIcon = document.getElementById('navThemeIcon');
    if (navIcon) {
        navIcon.className = activeTheme === 'dark' ? 'bi bi-sun-fill text-warning' : 'bi bi-moon-stars text-secondary';
    }
}

function toggleTheme() {
    const current = document.documentElement.getAttribute('data-bs-theme') || 'light';
    const next = current === 'dark' ? 'light' : 'dark';
    applyTheme(next);
}

function showToast(message, type = 'info', duration = 4000) {
    const container = document.getElementById('toastContainer');
    if (!container) return;

    const toastId = 'toast_' + Math.random().toString(36).substring(2, 9);
    const iconMap = {
        success: 'bi-check-circle-fill text-success',
        danger: 'bi-exclamation-octagon-fill text-danger',
        warning: 'bi-exclamation-triangle-fill text-warning',
        info: 'bi-info-circle-fill text-primary'
    };
    const icon = iconMap[type] || iconMap.info;

    const toastEl = document.createElement('div');
    toastEl.className = `toast align-items-center border-0 shadow-sm mb-2`;
    toastEl.id = toastId;
    toastEl.setAttribute('role', 'alert');
    toastEl.setAttribute('aria-live', 'assertive');
    toastEl.setAttribute('aria-atomic', 'true');
    toastEl.innerHTML = `
        <div class="d-flex p-2">
            <div class="toast-body d-flex align-items-center gap-2">
                <i class="bi ${icon} fs-5"></i>
                <div>${escapeHtml(message)}</div>
            </div>
            <button type="button" class="btn-close me-2 m-auto" data-bs-dismiss="toast" aria-label="Close"></button>
        </div>
    `;

    container.appendChild(toastEl);
    const bsToast = new bootstrap.Toast(toastEl, { delay: duration });
    bsToast.show();

    toastEl.addEventListener('hidden.bs.toast', () => {
        toastEl.remove();
    });
}

function escapeHtml(str) {
    if (!str) return '';
    return String(str)
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#039;');
}

function formatDate(dateStr) {
    if (!dateStr) return '—';
    try {
        const d = new Date(dateStr);
        return d.toLocaleDateString(undefined, { month: 'short', day: 'numeric', year: 'numeric' });
    } catch {
        return dateStr;
    }
}

function formatDateTime(dateStr) {
    if (!dateStr) return '—';
    try {
        const d = new Date(dateStr);
        return d.toLocaleDateString(undefined, { month: 'short', day: 'numeric' }) + ' ' +
               d.toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' });
    } catch {
        return dateStr;
    }
}

function getStatusBadgeClass(status) {
    const map = {
        'Plan': 'bg-secondary bg-opacity-10 text-secondary',
        'ToDo': 'bg-primary bg-opacity-10 text-primary',
        'InProgress': 'bg-warning bg-opacity-10 text-warning',
        'Review': 'bg-info bg-opacity-10 text-info',
        'Completed': 'bg-success bg-opacity-10 text-success'
    };
    return map[status] || 'bg-secondary bg-opacity-10 text-secondary';
}

function getPriorityBadgeClass(priority) {
    const map = {
        'Low': 'bg-secondary bg-opacity-10 text-secondary',
        'Medium': 'bg-info bg-opacity-10 text-info',
        'High': 'bg-warning bg-opacity-10 text-warning',
        'Urgent': 'bg-danger bg-opacity-10 text-danger'
    };
    return map[priority] || 'bg-secondary bg-opacity-10 text-secondary';
}

function getInitials(name) {
    if (!name) return 'U';
    const parts = name.trim().split(/\s+/);
    if (parts.length === 1) return parts[0].substring(0, 2).toUpperCase();
    return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
}

function getAvatarColor(name) {
    const colors = ['#4f46e5', '#06b6d4', '#10b981', '#f59e0b', '#ec4899', '#8b5cf6', '#3b82f6'];
    if (!name) return colors[0];
    let hash = 0;
    for (let i = 0; i < name.length; i++) {
        hash = name.charCodeAt(i) + ((hash << 5) - hash);
    }
    return colors[Math.abs(hash) % colors.length];
}

function renderAvatar(user, size = 32) {
    const name = (user && (user.fullName || user.username)) ? (user.fullName || user.username) : 'User';
    const color = getAvatarColor(name);
    const initials = getInitials(name);
    return `<div class="rounded-circle text-white d-inline-flex align-items-center justify-content-center fw-semibold user-avatar-badge" style="width: ${size}px; height: ${size}px; background-color: ${color}; font-size: ${Math.max(10, Math.round(size * 0.38))}px; flex-shrink: 0;" title="${escapeHtml(name)}">${initials}</div>`;
}

function togglePasswordVisibility(inputId, btnEl) {
    const input = document.getElementById(inputId);
    if (!input) return;
    const isPassword = input.getAttribute('type') === 'password';
    input.setAttribute('type', isPassword ? 'text' : 'password');
    const icon = btnEl.querySelector('i');
    if (icon) {
        icon.className = isPassword ? 'bi bi-eye-slash' : 'bi bi-eye';
    }
}
