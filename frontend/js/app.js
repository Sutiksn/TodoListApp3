/**
 * =============================================================================
 * TASKFLOW ENTERPRISE - APPLICATION BOOTSTRAPPER & EVENT ORCHESTRATOR
 * =============================================================================
 */

document.addEventListener('DOMContentLoaded', async () => {
    initTheme();

    authToken = sessionStorage.getItem('taskflow_token') || null;
    activeMode = sessionStorage.getItem('taskflow_mode') || 'Personal';

    if (authToken) {
        try {
            const user = await fetchCurrentUser();
            if (user) {
                await initApp();
            } else {
                showAuthScreen();
            }
        } catch {
            handleLogout();
        }
    } else {
        showAuthScreen();
    }
});

async function initApp() {
    const authSec = document.getElementById('authSection');
    const appShell = document.getElementById('appShell');
    if (authSec) authSec.classList.add('d-none');
    if (appShell) appShell.classList.remove('d-none');

    renderUserInfo();
    if (activeMode === 'Admin' && currentUser.role !== 'PlatformAdmin') {
        activeMode = 'Personal';
        sessionStorage.setItem('taskflow_mode', 'Personal');
    }
    await loadUsers();
    await loadWorkspaces();
    updateModeLockStates();
    await resolveRoute();
    loadIncomingRequests();
}
