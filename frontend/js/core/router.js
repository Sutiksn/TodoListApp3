/**
 * =============================================================================
 * TASKFLOW ENTERPRISE - CLIENT ROUTER & MODE NAVIGATION ENGINE
 * =============================================================================
 */

function getNormalizedPath() {
    const p = window.location.pathname.toLowerCase();
    if (p.endsWith('/') && p.length > 1) {
        return p.slice(0, -1);
    }
    return p;
}

function navigateTo(path, pushState = true) {
    if (pushState && window.location.pathname.toLowerCase() !== path.toLowerCase()) {
        window.history.pushState(null, '', path);
    }
    resolveRoute();
}

window.addEventListener('popstate', () => {
    resolveRoute();
});

async function resolveRoute() {
    const path = getNormalizedPath();

    if (!authToken || !currentUser) {
        showAuthScreen();
        return;
    }

    // User is authenticated: hide auth screen and show appShell
    const authSec = document.getElementById('authSection');
    const appShell = document.getElementById('appShell');
    if (authSec) authSec.classList.add('d-none');
    if (appShell) appShell.classList.remove('d-none');

    if (path === '/profile') {
        await showProfilePage(false);
        return;
    } else if (path === '/admin') {
        if (currentUser.role === 'PlatformAdmin') {
            await switchMode('Admin', false);
        } else {
            showToast('Access denied. Administrator privileges required.', 'warning');
            navigateTo('/personal', true);
        }
    } else if (path === '/team') {
        await switchMode('Team', false);
    } else if (path === '/project') {
        await switchMode('Project', false);
    } else if (path === '/business') {
        await switchMode('Business', false);
    } else if (path === '/personal') {
        await switchMode('Personal', false);
    } else if (path === '/login' || path === '' || path === '/') {
        const targetMode = currentUser.role === 'PlatformAdmin' ? 'Admin' : (activeMode && activeMode !== 'Admin' ? activeMode : 'Personal');
        navigateTo(`/${targetMode.toLowerCase()}`, true);
    } else {
        const fallbackMode = currentUser.role === 'PlatformAdmin' ? 'Admin' : 'Personal';
        navigateTo(`/${fallbackMode.toLowerCase()}`, true);
    }
}

function isModeLocked(mode) {
    if (!currentUser || mode === 'Personal') return false;

    // PlatformAdmin: Only Personal, Business (Org Governance), and Admin are accessible
    if (currentUser.role === 'PlatformAdmin') {
        if (mode === 'Admin' || mode === 'Business') return false;
        return true;
    }

    // OrgAdmin: Personal and Business are unlocked. Team and Project are locked until organization workspace is registered!
    if (currentUser.role === 'OrgAdmin') {
        if (mode === 'Business') return false;
        if (mode === 'Admin') return true;
        const hasRegisteredWorkspace = cachedWorkspaces && cachedWorkspaces.some(w => w.mode !== 'Personal');
        return !hasRegisteredWorkspace;
    }

    // Manager / Employee: Locked out of Team, Project, and Business until approved/enrolled into an organization workspace
    if (currentUser.role === 'Manager' || currentUser.role === 'Employee') {
        if (mode === 'Admin') return true;
        const hasCollabWorkspace = cachedWorkspaces && cachedWorkspaces.some(w => w.mode !== 'Personal');
        return !hasCollabWorkspace;
    }

    // Standard 'User' role is locked out of collaborative modes
    return true;
}

function updateModeLockStates() {
    const isPlatformAdmin = currentUser && currentUser.role === 'PlatformAdmin';

    // Navbar buttons visibility for Platform Admin (desktop & mobile)
    const btnTeam = document.querySelector('.mode-btn[data-mode="Team"]');
    const btnProject = document.querySelector('.mode-btn[data-mode="Project"]');
    const mobileBtnTeam = document.querySelector('#mobileNavDrawer .mode-btn[data-mode="Team"]');
    const mobileBtnProject = document.querySelector('#mobileNavDrawer .mode-btn[data-mode="Project"]');
    const btnNavRequests = document.getElementById('btnNavRequests');
    const navAdminBtn = document.getElementById('navAdminBtn');
    const mobileNavAdminBtn = document.getElementById('mobileNavAdminBtn');

    if (btnTeam) btnTeam.classList.toggle('d-none', !!isPlatformAdmin);
    if (btnProject) btnProject.classList.toggle('d-none', !!isPlatformAdmin);
    if (mobileBtnTeam) mobileBtnTeam.classList.toggle('d-none', !!isPlatformAdmin);
    if (mobileBtnProject) mobileBtnProject.classList.toggle('d-none', !!isPlatformAdmin);
    if (btnNavRequests) btnNavRequests.classList.toggle('d-none', !!isPlatformAdmin);
    if (navAdminBtn) navAdminBtn.classList.toggle('d-none', !isPlatformAdmin);
    if (mobileNavAdminBtn) mobileNavAdminBtn.classList.toggle('d-none', !isPlatformAdmin);

    const modes = ['Personal', 'Team', 'Project', 'Business', 'Admin'];
    modes.forEach(m => {
        const btns = document.querySelectorAll(`.mode-btn[data-mode="${m}"]`);
        const locked = isModeLocked(m);

        btns.forEach(btn => {
            btn.classList.toggle('locked-mode', locked);

            let iconHtml = '';
            if (m === 'Personal') iconHtml = '<i class="bi bi-person"></i> Personal';
            else if (m === 'Team') iconHtml = '<i class="bi bi-people"></i> Team';
            else if (m === 'Project') iconHtml = '<i class="bi bi-rocket-takeoff"></i> Project';
            else if (m === 'Business') iconHtml = '<i class="bi bi-building"></i> Organization Governance';
            else if (m === 'Admin') iconHtml = '<i class="bi bi-shield-lock"></i> Admin';

            if (locked) {
                btn.innerHTML = `${iconHtml} <i class="bi bi-lock-fill lock-badge text-warning" title="Access Restricted - Requires Role/Registration Approval"></i>`;
                btn.title = 'Access Restricted - Request Role/Workspace Registration to Unlock';
            } else {
                btn.innerHTML = iconHtml;
                btn.title = '';
            }
        });
    });
}

async function switchMode(mode, updateUrl = true) {
    if (currentUser && currentUser.role === 'PlatformAdmin' && (mode === 'Team' || mode === 'Project')) {
        showToast('Platform Administrators manage platform and organization governance, and do not participate in internal team/project workspaces.', 'warning');
        return;
    }

    if (isModeLocked(mode)) {
        const modalMsg = document.getElementById('modalAccessRestrictedMessage');
        if (modalMsg) {
            modalMsg.textContent = `You do not currently have access to ${mode} Mode. Standard users must request an Employee or Manager role via the Access Requests panel to participate in organizations, teams, and collaborative projects.`;
        }
        new bootstrap.Modal(document.getElementById('modalAccessRestricted')).show();
        return;
    }

    if (mode === 'Admin' && currentUser && currentUser.role !== 'PlatformAdmin') {
        showToast('Admin mode is restricted to Platform Administrators.', 'warning');
        mode = 'Personal';
    }
    activeMode = mode;
    sessionStorage.setItem('taskflow_mode', mode);

    if (updateUrl) {
        const targetPath = `/${mode.toLowerCase()}`;
        if (window.location.pathname.toLowerCase() !== targetPath) {
            window.history.pushState(null, '', targetPath);
        }
    }

    // Align activeWorkspaceId with the selected mode if available
    if (cachedWorkspaces && cachedWorkspaces.length > 0 && mode !== 'Admin') {
        const currentWs = cachedWorkspaces.find(w => w.id === activeWorkspaceId);
        if (!currentWs || currentWs.mode.toLowerCase() !== mode.toLowerCase()) {
            const matchingWs = cachedWorkspaces.find(w => w.mode.toLowerCase() === mode.toLowerCase());
            if (matchingWs) {
                activeWorkspaceId = matchingWs.id;
                const nameEl = document.getElementById('activeWorkspaceName');
                if (nameEl) nameEl.textContent = matchingWs.name;
                renderWorkspaceDropdown();
            }
        }
    }

    // Update active navbar pill
    document.querySelectorAll('.mode-btn').forEach(btn => {
        btn.classList.toggle('active', btn.getAttribute('data-mode') === mode);
    });

    // Hide all mode views
    document.querySelectorAll('.mode-view').forEach(v => {
        v.classList.add('d-none');
        v.classList.remove('fade-in-motion');
    });

    // Configure Mode Banner & Header with motion
    const banner = document.getElementById('modeBanner');
    if (banner) {
        banner.className = `mode-banner mode-${mode.toLowerCase()} fade-in-motion`;
    }

    const badge = document.getElementById('modeBadge');
    const title = document.getElementById('modeTitle');
    const subtext = document.getElementById('modeSubtext');
    const toolbar = document.getElementById('modeToolbar');

    if (badge) {
        if (mode === 'Business') {
            badge.textContent = 'ORGANIZATION GOVERNANCE';
        } else {
            badge.textContent = `${mode.toUpperCase()} MODE`;
        }
    }

    const activeView = document.getElementById(`view${mode}`);
    if (activeView) {
        activeView.classList.remove('d-none');
        void activeView.offsetWidth; // Force DOM reflow for CSS animation
        activeView.classList.add('fade-in-motion');
    }

    const canManageCollab = currentUser && (currentUser.role === 'OrgAdmin' || currentUser.role === 'Manager');

    if (mode === 'Personal') {
        if (title) title.textContent = "My Focus Board & Daily Tasks";
        if (subtext) subtext.textContent = "Personal Productivity & Individual Execution";
        if (toolbar) {
            toolbar.innerHTML = `
                <button class="btn btn-light btn-sm fw-semibold shadow-sm" onclick="openCreateTaskModal()"><i class="bi bi-plus-lg me-1"></i> Add Task</button>
            `;
        }
        await loadPersonalMode();
    } else if (mode === 'Team') {
        if (title) title.textContent = "Team Collaboration & Workload Distribution";
        if (subtext) subtext.textContent = "Cross-functional task assignments, sprints, and feeds";
        if (toolbar) {
            if (canManageCollab) {
                toolbar.innerHTML = `
                    <button class="btn btn-light btn-sm fw-semibold shadow-sm" onclick="openCreateTaskModal()"><i class="bi bi-plus-lg me-1"></i> Delegate Task</button>
                    <button class="btn btn-outline-light btn-sm fw-semibold" onclick="openAddWorkspaceMemberModal()"><i class="bi bi-person-plus me-1"></i> Add Member</button>
                `;
            } else {
                toolbar.innerHTML = `
                    <button class="btn btn-light btn-sm fw-semibold shadow-sm" onclick="loadTeamMode()"><i class="bi bi-arrow-clockwise me-1"></i> Refresh Feed</button>
                `;
            }
        }
        await loadTeamMode();
    } else if (mode === 'Project') {
        if (title) title.textContent = "Projects, Milestones & Release Tracks";
        if (subtext) subtext.textContent = "Strategic goals, delivery deadlines, and milestone velocity";
        if (toolbar) {
            if (canManageCollab) {
                toolbar.innerHTML = `
                    <button class="btn btn-light btn-sm fw-semibold shadow-sm" onclick="openCreateProjectModal()"><i class="bi bi-plus-lg me-1"></i> New Project</button>
                    <button class="btn btn-outline-light btn-sm fw-semibold" onclick="openCreateMilestoneModal()"><i class="bi bi-flag me-1"></i> Add Milestone</button>
                `;
            } else {
                toolbar.innerHTML = `
                    <button class="btn btn-outline-light btn-sm fw-semibold" onclick="openCreateMilestoneModal()"><i class="bi bi-flag me-1"></i> Add Milestone</button>
                `;
            }
        }
        await loadProjectMode();
    } else if (mode === 'Business') {
        if (title) title.textContent = "Organization Governance & Workflow Approvals";
        if (subtext) subtext.textContent = "Enterprise governance, departments, project tracking, and employee directories";
        await loadBusinessMode();
    } else if (mode === 'Admin') {
        if (title) title.textContent = "Platform Administration & Security Governance";
        if (subtext) subtext.textContent = "Global user accounts, role permissions, and audit logs";
        if (toolbar) {
            toolbar.innerHTML = `
                <button class="btn btn-light btn-sm fw-semibold shadow-sm" onclick="loadAdminMode()"><i class="bi bi-arrow-clockwise me-1"></i> Refresh Logs</button>
            `;
        }
        await loadAdminMode();
    }
}
