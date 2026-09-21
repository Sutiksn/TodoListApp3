/**
 * =============================================================================
 * TASKFLOW ENTERPRISE - WORKSPACE & MEMBERS COMPONENT
 * =============================================================================
 */

async function loadUsers() {
    try {
        const users = await apiCall('/api/auth/users');
        cachedUsers = users || [];
        populateUserDropdowns();
    } catch {}
}

function populateUserDropdowns() {
    const taskAssignee = document.getElementById('taskInputAssignee');
    if (taskAssignee) {
        taskAssignee.innerHTML = '<option value="">(Unassigned)</option>' +
            cachedUsers.map(u => `<option value="${u.id}">${escapeHtml(u.fullName)} (${u.department || 'General'})</option>`).join('');
    }
}

async function loadWorkspaces() {
    try {
        cachedWorkspaces = await apiCall('/api/workspaces');
        renderWorkspaceDropdown();
        updateModeLockStates();

        if (cachedWorkspaces.length > 0) {
            const match = cachedWorkspaces.find(w => w.mode.toLowerCase() === activeMode.toLowerCase()) || cachedWorkspaces[0];
            activeWorkspaceId = match.id;
            const activeWsNameEl = document.getElementById('activeWorkspaceName');
            if (activeWsNameEl) activeWsNameEl.textContent = match.name;
        }
    } catch {}
}

function renderWorkspaceDropdown() {
    const list = document.getElementById('workspaceDropdownList');
    if (!list) return;

    let availableWorkspaces = cachedWorkspaces || [];
    if (currentUser && currentUser.role === 'PlatformAdmin') {
        availableWorkspaces = availableWorkspaces.filter(w => w.mode === 'Personal' || w.mode === 'Business');
    }

    list.innerHTML = `
        <li class="px-3 py-1 text-muted small fw-bold text-uppercase d-flex justify-content-between align-items-center">
            <span>Workspaces</span>
            <span class="badge bg-secondary-subtle text-secondary rounded-pill" style="font-size:0.7rem;">${availableWorkspaces.length}</span>
        </li>
        ${availableWorkspaces.map(w => {
            const isCreator = currentUser && w.createdByUserId === currentUser.id;
            const isAdmin = currentUser && (currentUser.role === 'PlatformAdmin' || currentUser.role === 'OrgAdmin');
            const canDelete = isAdmin || isCreator;
            const showDelete = canDelete && availableWorkspaces.length > 1;
            return `
            <li>
                <div class="dropdown-item small py-2 d-flex align-items-center justify-content-between ${w.id === activeWorkspaceId ? 'active fw-bold' : ''}" style="cursor: pointer;" onclick="selectWorkspace(${w.id})">
                    <div class="d-flex align-items-center gap-2 overflow-hidden text-truncate me-2">
                        <i class="bi bi-circle-fill flex-shrink-0" style="color:${w.color}; font-size: 0.6rem;"></i>
                        <span class="text-truncate">${escapeHtml(w.name)}</span>
                    </div>
                    <div class="d-flex align-items-center gap-2 flex-shrink-0">
                        <span class="badge bg-secondary-subtle text-secondary border border-secondary-subtle rounded-pill" style="font-size: 0.68rem;">${w.mode === 'Business' ? 'Organization' : w.mode}</span>
                        ${showDelete ? `
                            <button type="button" class="btn btn-link btn-sm text-danger text-opacity-75 p-0" title="Delete Workspace" onclick="event.stopPropagation(); handleDeleteWorkspace(${w.id}, '${escapeHtml(w.name)}')">
                                <i class="bi bi-trash"></i>
                            </button>
                        ` : ''}
                    </div>
                </div>
            </li>
            `;
        }).join('')}
        <li><hr class="dropdown-divider my-1"></li>
        <li><a class="dropdown-item small py-2 text-primary" href="javascript:void(0)" onclick="openCreateWorkspaceModal()"><i class="bi bi-plus-circle me-2"></i> Create Workspace</a></li>
    `;
}

async function selectWorkspace(id) {
    activeWorkspaceId = id;
    const ws = cachedWorkspaces.find(w => w.id === id);
    if (ws) {
        const nameEl = document.getElementById('activeWorkspaceName');
        if (nameEl) nameEl.textContent = ws.name;
        const mobWs = document.getElementById('mobileActiveWorkspaceName');
        if (mobWs) mobWs.textContent = ws.name;
        renderWorkspaceDropdown();
        let targetMode = ws.mode || 'Personal';
        if (currentUser && currentUser.role === 'PlatformAdmin' && (targetMode === 'Team' || targetMode === 'Project')) {
            targetMode = 'Business';
        }
        await switchMode(targetMode);
    } else {
        renderWorkspaceDropdown();
        await switchMode(activeMode);
    }
}

function openCreateWorkspaceModal() {
    const role = currentUser ? currentUser.role : 'User';
    const modeSelect = document.getElementById('wsInputMode');
    if (modeSelect) {
        if (role === 'OrgAdmin' || role === 'PlatformAdmin') {
            modeSelect.innerHTML = `
                <option value="Personal">Personal Mode</option>
                <option value="Team" selected>Team Mode</option>
                <option value="Project">Project Mode</option>
                <option value="Business">Organization Governance</option>
            `;
        } else if (role === 'Manager') {
            modeSelect.innerHTML = `
                <option value="Personal">Personal Mode</option>
                <option value="Team" selected>Team Mode</option>
                <option value="Project">Project Mode</option>
            `;
        } else {
            modeSelect.innerHTML = `
                <option value="Personal" selected>Personal Mode</option>
            `;
        }
    }
    new bootstrap.Modal(document.getElementById('modalCreateWorkspace')).show();
}

async function handleCreateWorkspace(e) {
    e.preventDefault();
    const name = document.getElementById('wsInputName').value.trim();
    const description = document.getElementById('wsInputDesc').value.trim();
    const mode = document.getElementById('wsInputMode').value;

    try {
        const ws = await apiCall('/api/workspaces', 'POST', { name, description, mode });
        bootstrap.Modal.getInstance(document.getElementById('modalCreateWorkspace')).hide();
        document.getElementById('formCreateWorkspace').reset();
        showToast(`Workspace '${name}' created in ${mode} Mode!`, 'success');
        await loadWorkspaces();
        await selectWorkspace(ws.id);
    } catch {}
}

async function handleDeleteWorkspace(workspaceId, workspaceName) {
    if (cachedWorkspaces.length <= 1) {
        showToast('Cannot delete your only remaining workspace.', 'warning');
        return;
    }

    if (!confirm(`Are you sure you want to permanently delete workspace "${workspaceName}"?\nAll tasks, projects, milestones, and data within it will be removed.`)) {
        return;
    }

    try {
        const res = await apiCall(`/api/workspaces/${workspaceId}`, 'DELETE');
        showToast(res.message || 'Workspace deleted successfully.', 'success');
        
        await loadWorkspaces();

        if (activeWorkspaceId === workspaceId) {
            const fallbackWs = cachedWorkspaces.find(w => w.mode === activeMode) || cachedWorkspaces[0];
            if (fallbackWs) {
                await selectWorkspace(fallbackWs.id);
            }
        } else {
            renderWorkspaceDropdown();
        }
    } catch {}
}

async function openAddWorkspaceMemberModal() {
    const isManagerOrAdmin = currentUser && (currentUser.role === 'PlatformAdmin' || currentUser.role === 'OrgAdmin' || currentUser.role === 'Manager');
    if (!isManagerOrAdmin) {
        showToast('Only Managers and Administrators can add workspace members.', 'warning');
        return;
    }
    if (!activeWorkspaceId) return;

    try {
        if (!cachedProjects || cachedProjects.length === 0) {
            cachedProjects = await apiCall(`/api/projects?workspaceId=${activeWorkspaceId}`);
        }
    } catch {
        cachedProjects = [];
    }

    if (!cachedProjects || cachedProjects.length === 0) {
        showToast('Please create at least one project in the workspace first', 'warning');
        return;
    }

    const sel = document.getElementById('wsMemberSelectUser');
    if (sel && cachedUsers) {
        sel.innerHTML = '<option value="">-- Choose registered user (optional) --</option>' +
            cachedUsers.filter(u => (!currentUser || u.id !== currentUser.id)).map(u => `<option value="${escapeHtml(u.username)}">${escapeHtml(u.fullName)} (@${escapeHtml(u.username)})</option>`).join('');
    }

    const projSel = document.getElementById('wsMemberProjectInput');
    if (projSel) {
        projSel.innerHTML = '<option value="">-- Choose Project to Assign (Mandatory) --</option>' +
            cachedProjects.map(p => `<option value="${p.id}" ${p.id === selectedProjectId ? 'selected' : ''}>${escapeHtml(p.name)}</option>`).join('');
    }

    new bootstrap.Modal(document.getElementById('modalAddWorkspaceMember')).show();
}

async function handleAddWorkspaceMember(e) {
    e.preventDefault();
    if (!activeWorkspaceId) return;

    const usernameOrEmail = document.getElementById('wsMemberUserInput').value.trim();
    const role = document.getElementById('wsMemberRoleInput').value;
    const projectInput = document.getElementById('wsMemberProjectInput');
    const projectId = projectInput ? projectInput.value : '';

    if (!projectId) {
        showToast('Please select a project to assign this member to. Project assignment is mandatory.', 'warning');
        if (projectInput) projectInput.focus();
        return;
    }

    try {
        await apiCall(`/api/workspaces/${activeWorkspaceId}/members`, 'POST', {
            usernameOrEmail,
            role,
            projectId: parseInt(projectId)
        });
        bootstrap.Modal.getInstance(document.getElementById('modalAddWorkspaceMember')).hide();
        document.getElementById('formAddWorkspaceMember').reset();
        showToast('Member assigned to project and added to workspace!', 'success');
        if (activeMode === 'Team') await loadTeamMode();
        if (activeMode === 'Project') await loadProjectMode();
    } catch {}
}

async function handleRemoveTeamMember(userId, fullName) {
    if (!activeWorkspaceId) return;
    if (!confirm(`Are you sure you want to remove "${fullName}" from this team workspace?`)) return;

    try {
        const res = await apiCall(`/api/workspaces/${activeWorkspaceId}/members/${userId}`, 'DELETE');
        showToast(res.message || 'Member removed from team.', 'success');
        if (activeMode === 'Team') await loadTeamMode();
    } catch {}
}
