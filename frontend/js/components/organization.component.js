/**
 * =============================================================================
 * TASKFLOW ENTERPRISE - ORGANIZATION GOVERNANCE & ACCESS REQUESTS COMPONENT
 * =============================================================================
 */

let cachedIncomingRequests = [];
let cachedApprovedRequests = [];
let cachedMyRequests = [];

async function loadBusinessMode() {
    try {
        const badge = document.getElementById('modeBadge');
        const title = document.getElementById('modeTitle');
        const subtext = document.getElementById('modeSubtext');
        const toolbar = document.getElementById('modeToolbar');

        if (badge) badge.textContent = 'ORGANIZATION GOVERNANCE';

        const orgs = await apiCall('/api/organizations');
        if (!orgs || orgs.length === 0) {
            activeOrgId = null;
            if (title) title.textContent = 'Organization Governance';
            if (subtext) subtext.textContent = 'Enterprise governance, departments, project tracking, and employee directories';
            if (toolbar) toolbar.innerHTML = '<span class="badge bg-white text-secondary px-3 py-2 fw-semibold shadow-sm">No Organization Assigned</span>';
            const statsRow = document.getElementById('businessStatsRow');
            if (statsRow) {
                statsRow.innerHTML = `
                    <div class="col-12 col-sm-6 col-xl-3">
                        <div class="stats-card">
                            <div class="text-muted small fw-semibold">Total Employees</div>
                            <h3 class="fw-bold mb-0 mt-1">0</h3>
                        </div>
                    </div>
                    <div class="col-12 col-sm-6 col-xl-3">
                        <div class="stats-card">
                            <div class="text-muted small fw-semibold">Departments</div>
                            <h3 class="fw-bold mb-0 mt-1">0</h3>
                        </div>
                    </div>
                    <div class="col-12 col-sm-6 col-xl-3">
                        <div class="stats-card">
                            <div class="text-muted small fw-semibold">Pending Approvals</div>
                            <h3 class="fw-bold mb-0 mt-1 text-warning">0</h3>
                        </div>
                    </div>
                    <div class="col-12 col-sm-6 col-xl-3">
                        <div class="stats-card">
                            <div class="text-muted small fw-semibold">Org Total Tasks</div>
                            <h3 class="fw-bold mb-0 mt-1 text-success">0/0</h3>
                        </div>
                    </div>
                `;
            }
            const pendingBadge = document.getElementById('pendingApprovalsBadge');
            if (pendingBadge) pendingBadge.textContent = '0 Pending';
            const appList = document.getElementById('approvalsQueueList');
            if (appList) appList.innerHTML = `<div class="text-muted small text-center py-3">No organization assigned.</div>`;
            const deptList = document.getElementById('departmentStatsList');
            if (deptList) deptList.innerHTML = `<div class="text-muted small text-center py-3">No organization assigned.</div>`;
            const empList = document.getElementById('orgEmployeeList');
            if (empList) empList.innerHTML = `<div class="text-center text-muted small py-4"><i class="bi bi-building-slash fs-3 d-block mb-1 opacity-50"></i>You are not currently assigned to any organization.</div>`;
            return;
        }

        const org = orgs[0];
        activeOrgId = org.Id || org.id;

        if (title) title.textContent = org.Name || org.name;
        if (subtext) subtext.textContent = org.Description || org.description || 'Enterprise governance, departments, project tracking, and employee directories';

        const isRegistered = cachedWorkspaces && cachedWorkspaces.some(w => (w.organizationId === activeOrgId || w.organizationId === org.id) && (w.mode === 'Business' || w.mode === 'Team'));
        const canManageOrg = currentUser && (currentUser.role === 'PlatformAdmin' || currentUser.role === 'OrgAdmin');

        if (toolbar) {
            if (!isRegistered && canManageOrg) {
                toolbar.innerHTML = `
                    <button class="btn btn-light fw-bold px-3 py-2 shadow-sm text-success" id="btnRegisterOrgWorkspace" onclick="handleRegisterOrgWorkspace(${activeOrgId})">
                        <i class="bi bi-rocket-takeoff-fill me-1"></i> Register Organization & Launch Workspace
                    </button>
                `;
            } else if (isRegistered) {
                toolbar.innerHTML = `
                    <span class="badge bg-white text-success px-3 py-2 fw-semibold fs-6 shadow-sm"><i class="bi bi-check-circle-fill me-1"></i> Active & Registered</span>
                    ${canManageOrg ? `
                        <button class="btn btn-light btn-sm fw-semibold shadow-sm" onclick="openAddOrgMemberModal()"><i class="bi bi-person-plus me-1"></i> Add Employee</button>
                    ` : ''}
                `;
            } else {
                toolbar.innerHTML = `
                    <span class="badge bg-white text-warning px-3 py-2 fw-semibold fs-6 shadow-sm"><i class="bi bi-hourglass-split me-1"></i> Pending Registration</span>
                `;
            }
        }

        const analytics = await apiCall(`/api/analytics/business?orgId=${activeOrgId}`);
        const statsRow = document.getElementById('businessStatsRow');
        if (statsRow) {
            statsRow.innerHTML = `
                <div class="col-12 col-sm-6 col-xl-3">
                    <div class="stats-card">
                        <div class="text-muted small fw-semibold">Total Employees</div>
                        <h3 class="fw-bold mb-0 mt-1">${analytics.totalEmployees}</h3>
                    </div>
                </div>
                <div class="col-12 col-sm-6 col-xl-3">
                    <div class="stats-card">
                        <div class="text-muted small fw-semibold">Departments</div>
                        <h3 class="fw-bold mb-0 mt-1">${analytics.totalDepartments}</h3>
                    </div>
                </div>
                <div class="col-12 col-sm-6 col-xl-3">
                    <div class="stats-card">
                        <div class="text-muted small fw-semibold">Pending Approvals</div>
                        <h3 class="fw-bold mb-0 mt-1 text-warning">${analytics.pendingApprovals}</h3>
                    </div>
                </div>
                <div class="col-12 col-sm-6 col-xl-3">
                    <div class="stats-card">
                        <div class="text-muted small fw-semibold">Org Total Tasks</div>
                        <h3 class="fw-bold mb-0 mt-1 text-success">${analytics.completedTasks}/${analytics.totalTasks}</h3>
                    </div>
                </div>
            `;
        }

        const pendingBadge = document.getElementById('pendingApprovalsBadge');
        if (pendingBadge) pendingBadge.textContent = `${analytics.pendingApprovals} Pending`;

        const approvals = await apiCall(`/api/workflow/approvals?orgId=${activeOrgId}`);
        const appList = document.getElementById('approvalsQueueList');
        if (appList) {
            if (approvals.length === 0) {
                appList.innerHTML = `<div class="text-muted small text-center py-3">No pending approval requests. Everything is running smoothly!</div>`;
            } else {
                appList.innerHTML = approvals.map(a => `
                    <div class="p-3 mb-2 rounded shadow-sm d-flex flex-column flex-sm-row justify-content-between align-items-sm-center gap-2" style="background: var(--bg-card); border: 1px solid var(--border-color);">
                        <div>
                            <div class="fw-bold small">${escapeHtml(a.taskTitle)}</div>
                            <div class="text-muted" style="font-size:0.75rem;">Requested by <strong>${escapeHtml(a.requesterFullName)}</strong> • ${formatDate(a.createdAt)}</div>
                            ${a.comments ? `<div class="small text-secondary mt-1">"${escapeHtml(a.comments)}"</div>` : ''}
                        </div>
                        <div class="d-flex align-items-center gap-2">
                            ${a.status === 'Pending' ? `
                                <button class="btn btn-success btn-sm py-1 px-3" onclick="decideApproval(${a.id}, 'Approved')"><i class="bi bi-check-lg me-1"></i> Approve</button>
                                <button class="btn btn-outline-danger btn-sm py-1 px-3" onclick="decideApproval(${a.id}, 'Rejected')"><i class="bi bi-x-lg me-1"></i> Reject</button>
                            ` : `
                                <span class="badge bg-${a.status === 'Approved' ? 'success' : 'danger'} rounded-pill">${a.status}</span>
                            `}
                        </div>
                    </div>
                `).join('');
            }
        }

        const deptList = document.getElementById('departmentStatsList');
        if (deptList) {
            deptList.innerHTML = analytics.departmentBreakdown.map(d => `
                <div class="d-flex justify-content-between align-items-center p-2 mb-2 rounded" style="background: rgba(0,0,0,0.02); border: 1px solid var(--border-color);">
                    <div>
                        <strong class="small">${escapeHtml(d.department)}</strong>
                        <div class="text-muted" style="font-size:0.72rem;">${d.employeeCount} Members</div>
                    </div>
                    <div class="text-end">
                        <span class="badge bg-primary rounded-pill">${d.completedTasks}/${d.totalTasks} Done</span>
                    </div>
                </div>
            `).join('');
        }

        const members = await apiCall(`/api/organizations/${activeOrgId}/members`);
        const empList = document.getElementById('orgEmployeeList');
        if (empList) {
            if (members.length === 0) {
                empList.innerHTML = `<div class="text-center text-muted small py-4"><i class="bi bi-people fs-3 d-block mb-1 opacity-50"></i>No employees in this organization yet. Click "Add Employee" to enroll team members.</div>`;
            } else {
                const getOrgBadgeStyle = (role) => {
                    switch (role) {
                        case 'OrgAdmin': return 'bg-primary-subtle text-primary border border-primary-subtle';
                        case 'Manager': return 'bg-warning-subtle text-dark border border-warning-subtle';
                        case 'Employee': return 'bg-info-subtle text-info border border-info-subtle';
                        default: return 'bg-secondary-subtle text-secondary border border-secondary-subtle';
                    }
                };

                empList.innerHTML = members.map(m => {
                    const isSelf = currentUser && m.userId === currentUser.id;
                    const displayRole = m.role === 'OrgAdmin' ? 'Organization Admin' : m.role;

                    return `
                    <div class="d-flex justify-content-between align-items-center p-2 mb-2 rounded shadow-sm" style="background: var(--bg-card); border: 1px solid var(--border-color);">
                        <div class="d-flex align-items-center gap-2">
                            <div class="avatar-circle" style="width:32px; height:32px; font-size:0.75rem;">${escapeHtml(m.fullName.charAt(0).toUpperCase())}</div>
                            <div>
                                <div class="d-flex align-items-center gap-1 flex-wrap">
                                    <span class="fw-semibold small">${escapeHtml(m.fullName)}</span>
                                    <span class="badge ${getOrgBadgeStyle(m.role)} rounded-pill px-2 py-0" style="font-size:0.65rem;">${escapeHtml(displayRole)}</span>
                                    ${m.status && m.status !== 'Active' ? `<span class="badge bg-secondary rounded-pill px-1 py-0" style="font-size:0.6rem;">${escapeHtml(m.status)}</span>` : ''}
                                </div>
                                <div class="text-muted" style="font-size:0.72rem;">${escapeHtml(m.department || 'General')} • @${escapeHtml(m.username)}</div>
                            </div>
                        </div>
                        <div class="d-flex align-items-center gap-2">
                            <span class="badge bg-light text-dark rounded-pill">${m.activeTasks} Active</span>
                            ${canManageOrg ? `
                                <div class="btn-group btn-group-sm">
                                    <button type="button" class="btn btn-outline-primary btn-sm py-0 px-2" style="font-size:0.72rem;" title="Edit Employee" onclick="openEditOrgMemberModal(${m.id}, '${escapeHtml(m.fullName)}', '${escapeHtml(m.role)}', '${escapeHtml(m.department || '')}', '${escapeHtml(m.status || 'Active')}')">
                                        <i class="bi bi-pencil"></i>
                                    </button>
                                    ${!isSelf ? `
                                        <button type="button" class="btn btn-outline-danger btn-sm py-0 px-2" style="font-size:0.72rem;" title="Remove Employee" onclick="handleRemoveOrgMember(${m.id}, '${escapeHtml(m.fullName)}')">
                                            <i class="bi bi-trash"></i>
                                        </button>
                                    ` : ''}
                                </div>
                            ` : ''}
                        </div>
                    </div>
                    `;
                }).join('');
            }
        }
    } catch {}
}

async function handleRegisterOrgWorkspace(orgId) {
    if (!orgId) return;
    const btn = document.getElementById('btnRegisterOrgWorkspace');
    if (btn) {
        btn.disabled = true;
        btn.innerHTML = '<span class="spinner-border spinner-border-sm me-1"></span> Launching Workspace...';
    }

    try {
        const res = await apiCall(`/api/organizations/${orgId}/register-workspace`, 'POST');
        showToast(res.message || 'Organization workspace registered and collaborative modes unlocked!', 'success');
        await loadWorkspaces();
        updateModeLockStates();
        await loadBusinessMode();
    } catch {
        if (btn) {
            btn.disabled = false;
            btn.innerHTML = '<i class="bi bi-rocket-takeoff-fill me-1"></i> Register Organization & Launch Workspace';
        }
    }
}

function openAddOrgMemberModal() {
    const isManagerOrAdmin = currentUser && (currentUser.role === 'PlatformAdmin' || currentUser.role === 'OrgAdmin' || currentUser.role === 'Manager');
    if (!isManagerOrAdmin) {
        showToast('Only Managers and Administrators can add employees to the organization.', 'warning');
        return;
    }
    const sel = document.getElementById('orgMemberSelectUser');
    if (sel && cachedUsers) {
        sel.innerHTML = '<option value="">-- Choose registered user (optional) --</option>' +
            cachedUsers.filter(u => (!currentUser || u.id !== currentUser.id)).map(u => `<option value="${escapeHtml(u.username)}">${escapeHtml(u.fullName)} (@${escapeHtml(u.username)})</option>`).join('');
    }
    new bootstrap.Modal(document.getElementById('modalAddOrgMember')).show();
}

async function handleAddOrgMember(e) {
    e.preventDefault();
    if (!activeOrgId) {
        const orgs = await apiCall('/api/organizations');
        if (orgs && orgs.length > 0) {
            activeOrgId = orgs[0].Id || orgs[0].id;
        }
    }
    if (!activeOrgId) {
        showToast('Unable to find active organization.', 'danger');
        return;
    }

    const usernameOrEmail = document.getElementById('orgMemberUserInput').value.trim();
    const role = document.getElementById('orgMemberRoleInput').value;
    const department = document.getElementById('orgMemberDeptInput').value;

    try {
        await apiCall(`/api/organizations/${activeOrgId}/members`, 'POST', { usernameOrEmail, role, department });
        bootstrap.Modal.getInstance(document.getElementById('modalAddOrgMember')).hide();
        document.getElementById('formAddOrgMember').reset();
        showToast('Employee added to organization!', 'success');
        if (activeMode === 'Business') await loadBusinessMode();
        if (activeMode === 'Team') await loadTeamMode();
    } catch {}
}

function openEditOrgMemberModal(memberId, fullName, role, department, status) {
    const idInput = document.getElementById('editOrgMemberId');
    const nameInput = document.getElementById('editOrgMemberName');
    const roleSelect = document.getElementById('editOrgMemberRole');
    const deptSelect = document.getElementById('editOrgMemberDept');
    const statusSelect = document.getElementById('editOrgMemberStatus');

    if (idInput) idInput.value = memberId;
    if (nameInput) nameInput.value = fullName;
    if (roleSelect) roleSelect.value = role || 'Employee';
    if (deptSelect) deptSelect.value = department || 'General';
    if (statusSelect) statusSelect.value = status || 'Active';

    new bootstrap.Modal(document.getElementById('modalEditOrgMember')).show();
}

async function handleUpdateOrgMember(e) {
    e.preventDefault();
    if (!activeOrgId) return;

    const memberId = document.getElementById('editOrgMemberId').value;
    const role = document.getElementById('editOrgMemberRole').value;
    const department = document.getElementById('editOrgMemberDept').value;
    const status = document.getElementById('editOrgMemberStatus').value;

    try {
        const res = await apiCall(`/api/organizations/${activeOrgId}/members/${memberId}`, 'PUT', {
            role,
            department,
            status
        });
        bootstrap.Modal.getInstance(document.getElementById('modalEditOrgMember')).hide();
        showToast(res.message || 'Employee updated successfully!', 'success');
        if (activeMode === 'Business') await loadBusinessMode();
        if (activeMode === 'Team') await loadTeamMode();
        await fetchCurrentUser();
        renderUserInfo();
    } catch {}
}

async function handleRemoveOrgMember(memberId, fullName) {
    if (!activeOrgId) return;
    if (!confirm(`Are you sure you want to remove "${fullName}" from this organization?`)) return;

    try {
        const res = await apiCall(`/api/organizations/${activeOrgId}/members/${memberId}`, 'DELETE');
        showToast(res.message || 'Employee removed from organization.', 'success');
        if (activeMode === 'Business') await loadBusinessMode();
        if (activeMode === 'Team') await loadTeamMode();
        await fetchCurrentUser();
        renderUserInfo();
    } catch {}
}

function openRequestsDrawer(defaultTab = 'incoming') {
    const isPlatformAdmin = currentUser && currentUser.role === 'PlatformAdmin';
    const isDecider = currentUser && (currentUser.role === 'OrgAdmin' || currentUser.role === 'Manager');

    const colIncoming = document.getElementById('colTabIncoming');
    const colApproved = document.getElementById('colTabApproved');
    const colMyRequests = document.getElementById('colTabMyRequests');
    const colNewRequest = document.getElementById('colTabNewRequest');

    if (isPlatformAdmin) {
        if (colIncoming) colIncoming.classList.remove('d-none');
        if (colApproved) colApproved.classList.remove('d-none');
        if (colMyRequests) colMyRequests.classList.add('d-none');
        if (colNewRequest) colNewRequest.classList.add('d-none');
    } else if (isDecider) {
        if (colIncoming) colIncoming.classList.remove('d-none');
        if (colApproved) colApproved.classList.remove('d-none');
        if (colMyRequests) colMyRequests.classList.remove('d-none');
        if (colNewRequest) colNewRequest.classList.remove('d-none');
    } else {
        if (colIncoming) colIncoming.classList.add('d-none');
        if (colApproved) colApproved.classList.add('d-none');
        if (colMyRequests) colMyRequests.classList.remove('d-none');
        if (colNewRequest) colNewRequest.classList.remove('d-none');
    }

    let tabToOpen = defaultTab;
    if (!tabToOpen) {
        if (isPlatformAdmin || isDecider) {
            tabToOpen = 'incoming';
        } else {
            tabToOpen = 'my';
        }
    }
    
    switchRequestTab(tabToOpen);

    const offcanvasEl = document.getElementById('offcanvasRequestsPanel');
    const offcanvas = bootstrap.Offcanvas.getOrCreateInstance(offcanvasEl);
    offcanvas.show();
}

function closeAccessRestrictedModalAndOpenRequests() {
    const modal = bootstrap.Modal.getInstance(document.getElementById('modalAccessRestricted'));
    if (modal) modal.hide();
    openRequestsDrawer('new');
}

function switchRequestTab(tab) {
    const tabIncomingBtn = document.getElementById('tabIncomingBtn');
    const tabApprovedBtn = document.getElementById('tabApprovedBtn');
    const tabMyRequestsBtn = document.getElementById('tabMyRequestsBtn');
    const tabNewRequestBtn = document.getElementById('tabNewRequestBtn');

    if (tabIncomingBtn) tabIncomingBtn.classList.toggle('active', tab === 'incoming');
    if (tabApprovedBtn) tabApprovedBtn.classList.toggle('active', tab === 'approved');
    if (tabMyRequestsBtn) tabMyRequestsBtn.classList.toggle('active', tab === 'my');
    if (tabNewRequestBtn) tabNewRequestBtn.classList.toggle('active', tab === 'new');

    const panelIncoming = document.getElementById('panelIncomingRequests');
    const panelApproved = document.getElementById('panelApprovedRequests');
    const panelMy = document.getElementById('panelMyRequests');
    const panelNew = document.getElementById('panelNewRequest');

    if (panelIncoming) panelIncoming.classList.toggle('d-none', tab !== 'incoming');
    if (panelApproved) panelApproved.classList.toggle('d-none', tab !== 'approved');
    if (panelMy) panelMy.classList.toggle('d-none', tab !== 'my');
    if (panelNew) panelNew.classList.toggle('d-none', tab !== 'new');

    if (tab === 'incoming') loadIncomingRequests();
    else if (tab === 'approved') loadApprovedRequests();
    else if (tab === 'my') loadMyRequests();
    else if (tab === 'new') populateInAppOrgManagerDropdowns();
}

async function loadIncomingRequests() {
    if (!currentUser) return;
    const isDecider = currentUser.role === 'PlatformAdmin' || currentUser.role === 'OrgAdmin' || currentUser.role === 'Manager';
    const list = document.getElementById('incomingRequestsList');
    const badge = document.getElementById('incomingBadgeCount');
    const navBadge = document.getElementById('navRequestsBadge');

    if (!isDecider) {
        if (list) list.innerHTML = `<div class="text-center text-muted small py-4">You have no decider responsibilities. Switch to "My Requests" or "Request Role".</div>`;
        if (badge) badge.classList.add('d-none');
        if (navBadge) navBadge.classList.add('d-none');
        return;
    }

    try {
        cachedIncomingRequests = await apiCall('/api/access-requests/incoming');
        const count = cachedIncomingRequests.length;

        if (count > 0) {
            if (badge) { badge.textContent = count; badge.classList.remove('d-none'); }
            if (navBadge) { navBadge.textContent = count; navBadge.classList.remove('d-none'); }
        } else {
            if (badge) badge.classList.add('d-none');
            if (navBadge) navBadge.classList.add('d-none');
        }

        if (!list) return;

        if (count === 0) {
            list.innerHTML = `<div class="text-center text-muted small py-4"><i class="bi bi-check2-all fs-3 d-block mb-1 text-success"></i>All incoming access requests are caught up!</div>`;
            return;
        }

        list.innerHTML = cachedIncomingRequests.map(r => {
            const displayName = r.fullName || r.username || 'User';
            const email = r.email || '';
            const mgrName = r.targetManagerName || r.targetManagerFullName || '';
            return `
            <div class="request-item-card p-3 mb-3">
                <div class="d-flex justify-content-between align-items-start mb-2">
                    <div class="d-flex align-items-center gap-2">
                        <div class="avatar-circle" style="width:32px; height:32px; font-size:0.8rem;">${displayName.charAt(0).toUpperCase()}</div>
                        <div>
                            <div class="fw-bold small">${escapeHtml(displayName)}</div>
                            <div class="text-muted" style="font-size:0.75rem;">@${escapeHtml(r.username)}${email ? ` • ${escapeHtml(email)}` : ''}</div>
                        </div>
                    </div>
                    <span class="badge bg-primary rounded-pill px-2 py-1" style="font-size:0.7rem;">${r.requestedRole}</span>
                </div>

                <div class="bg-light p-2 rounded mb-2 small text-muted" style="font-size:0.75rem;">
                    ${r.organizationName ? `<div><strong>Organization:</strong> ${escapeHtml(r.organizationName)}</div>` : ''}
                    ${mgrName ? `<div><strong>Target Manager:</strong> ${escapeHtml(mgrName)}</div>` : ''}
                    ${r.comments ? `<div class="mt-1 fst-italic">"${escapeHtml(r.comments)}"</div>` : ''}
                    <div class="mt-1 text-secondary" style="font-size:0.7rem;">Requested: ${formatDate(r.createdAt)}</div>
                </div>

                <div class="d-flex gap-2">
                    <button type="button" class="btn btn-success btn-sm flex-grow-1 py-1 fw-semibold" onclick="handleDecideAccessRequest(${r.id}, true)">
                        <i class="bi bi-check-lg me-1"></i> Approve
                    </button>
                    <button type="button" class="btn btn-outline-danger btn-sm flex-grow-1 py-1 fw-semibold" onclick="handleDecideAccessRequest(${r.id}, false)">
                        <i class="bi bi-x-lg me-1"></i> Reject
                    </button>
                </div>
            </div>
        `;}).join('');
    } catch {
        if (list) list.innerHTML = `<div class="text-center text-danger small py-3">Failed to load incoming requests.</div>`;
    }
}

async function loadApprovedRequests() {
    if (!currentUser) return;
    const list = document.getElementById('approvedRequestsList');
    if (!list) return;

    try {
        cachedApprovedRequests = await apiCall('/api/access-requests/approved');
        if (cachedApprovedRequests.length === 0) {
            list.innerHTML = `<div class="text-center text-muted small py-4"><i class="bi bi-clock-history fs-3 d-block mb-1 text-muted"></i>No approved role requests recorded yet.</div>`;
            return;
        }

        list.innerHTML = cachedApprovedRequests.map(r => {
            const displayName = r.fullName || r.username || 'User';
            const email = r.email || '';
            const mgrName = r.targetManagerName || r.targetManagerFullName || '';
            return `
            <div class="request-item-card p-3 mb-3 border-success-subtle">
                <div class="d-flex justify-content-between align-items-start mb-2">
                    <div class="d-flex align-items-center gap-2">
                        <div class="avatar-circle" style="width:32px; height:32px; font-size:0.8rem; background: rgba(16, 185, 129, 0.1); color: #059669;">${displayName.charAt(0).toUpperCase()}</div>
                        <div>
                            <div class="fw-bold small">${escapeHtml(displayName)}</div>
                            <div class="text-muted" style="font-size:0.75rem;">@${escapeHtml(r.username)}${email ? ` • ${escapeHtml(email)}` : ''}</div>
                        </div>
                    </div>
                    <span class="badge bg-success rounded-pill px-2 py-1" style="font-size:0.7rem;"><i class="bi bi-check-circle me-1"></i>${r.requestedRole}</span>
                </div>

                <div class="bg-light p-2 rounded small text-muted" style="font-size:0.75rem;">
                    ${r.organizationName ? `<div><strong>Organization:</strong> ${escapeHtml(r.organizationName)}</div>` : ''}
                    ${mgrName ? `<div><strong>Target Manager:</strong> ${escapeHtml(mgrName)}</div>` : ''}
                    <div><strong>Approved by:</strong> @${escapeHtml(r.decidedByUsername || 'Admin')} on ${formatDate(r.decidedAt)}</div>
                    ${r.comments ? `<div class="mt-1 fst-italic">"${escapeHtml(r.comments)}"</div>` : ''}
                </div>
            </div>
        `;}).join('');
    } catch {
        list.innerHTML = `<div class="text-center text-danger small py-3">Failed to load approved requests.</div>`;
    }
}

async function loadMyRequests() {
    if (!currentUser) return;
    const list = document.getElementById('myRequestsList');
    if (!list) return;

    try {
        cachedMyRequests = await apiCall('/api/access-requests/my');
        if (cachedMyRequests.length === 0) {
            list.innerHTML = `<div class="text-center text-muted small py-4">You have not submitted any access requests yet.</div>`;
            return;
        }

        list.innerHTML = cachedMyRequests.map(r => {
            let statusBadge = '';
            if (r.status === 'Approved') statusBadge = '<span class="badge bg-success rounded-pill">Approved</span>';
            else if (r.status === 'Rejected') statusBadge = '<span class="badge bg-danger rounded-pill">Rejected</span>';
            else statusBadge = '<span class="badge bg-warning text-dark rounded-pill">Pending Review</span>';

            return `
                <div class="request-item-card p-3 mb-3">
                    <div class="d-flex justify-content-between align-items-center mb-1">
                        <span class="fw-bold small">Role: ${r.requestedRole}</span>
                        ${statusBadge}
                    </div>
                    <div class="text-muted small" style="font-size:0.75rem;">
                        ${r.organizationName ? `<div>Org: ${escapeHtml(r.organizationName)}</div>` : ''}
                        ${r.targetManagerFullName ? `<div>Manager: ${escapeHtml(r.targetManagerFullName)}</div>` : ''}
                        <div>Submitted: ${formatDate(r.createdAt)}</div>
                        ${r.decidedAt ? `<div>Decided: ${formatDate(r.decidedAt)} by @${escapeHtml(r.decidedByUsername || 'Admin')}</div>` : ''}
                        ${r.comments ? `<div class="mt-1 text-secondary">Note: ${escapeHtml(r.comments)}</div>` : ''}
                    </div>
                </div>
            `;
        }).join('');
    } catch {
        list.innerHTML = `<div class="text-center text-danger small py-3">Failed to load your requests.</div>`;
    }
}

async function handleDecideAccessRequest(requestId, approved) {
    const actionName = approved ? 'approve' : 'reject';
    const comments = prompt(`Optional feedback/comment for ${actionName} decision:`, '') || '';

    try {
        const res = await apiCall(`/api/access-requests/${requestId}/decide`, 'POST', {
            approved,
            comments: comments.trim()
        });
        showToast(res.message || `Request ${actionName}d successfully!`, 'success');
        await loadIncomingRequests();
        await loadApprovedRequests();
        if (typeof loadAdminIncomingRequests === 'function') await loadAdminIncomingRequests();
        await fetchCurrentUser();
        renderUserInfo();
        await loadUsers();
        await loadWorkspaces();
        updateModeLockStates();
        if (activeMode === 'Admin') await loadAdminMode();
    } catch {}
}

async function populateInAppOrgManagerDropdowns() {
    try {
        cachedOrgsWithManagers = await apiCall('/api/access-requests/organizations-with-managers');
        const orgSelect = document.getElementById('reqOrgInput');
        if (orgSelect) {
            orgSelect.innerHTML = '<option value="">-- Choose Organization --</option>' +
                cachedOrgsWithManagers.map(o => `<option value="${o.id}">${escapeHtml(o.name)}</option>`).join('');
        }
        const lockedInput = document.getElementById('reqOrgLockedInput');
        if (lockedInput && currentUser && currentUser.organizationName) {
            lockedInput.value = currentUser.organizationName;
        }
        handleInAppRoleChange();
    } catch {}
}

function handleInAppRoleChange() {
    const role = document.getElementById('reqRoleInput').value;
    const orgContainer = document.getElementById('inAppOrgContainer');
    const orgLockedContainer = document.getElementById('inAppOrgLockedContainer');
    const mgrContainer = document.getElementById('inAppManagerContainer');
    const notice = document.getElementById('inAppRoleNotice');

    const hasOrg = !!(currentUser && currentUser.organizationId);

    if (hasOrg) {
        if (orgLockedContainer) orgLockedContainer.classList.remove('d-none');
        if (orgContainer) orgContainer.classList.add('d-none');
    }

    if (role === 'OrgAdmin') {
        if (!hasOrg && orgContainer) orgContainer.classList.add('d-none');
        if (mgrContainer) mgrContainer.classList.add('d-none');
        if (notice) {
            notice.classList.remove('d-none');
            notice.innerHTML = hasOrg
                ? `<i class="bi bi-info-circle text-primary me-1"></i> Request for Organization Admin will be routed to the Platform Administrator for enterprise approval in <strong>${escapeHtml(currentUser.organizationName)}</strong>.`
                : `<i class="bi bi-info-circle text-primary me-1"></i> Request for Organization Admin will be routed directly to the Platform Administrator for enterprise approval.`;
        }
    } else if (role === 'Manager') {
        if (!hasOrg && orgContainer) orgContainer.classList.remove('d-none');
        if (mgrContainer) mgrContainer.classList.add('d-none');
        if (notice) {
            notice.classList.remove('d-none');
            notice.innerHTML = hasOrg
                ? `<i class="bi bi-info-circle text-primary me-1"></i> Request will be routed to the Organization Administrator of <strong>${escapeHtml(currentUser.organizationName)}</strong>.`
                : '<i class="bi bi-info-circle text-primary me-1"></i> Request will be routed to the chosen Organization Administrator.';
        }
    } else if (role === 'Employee') {
        if (!hasOrg && orgContainer) orgContainer.classList.remove('d-none');
        if (mgrContainer) mgrContainer.classList.remove('d-none');
        if (notice) {
            notice.classList.remove('d-none');
            notice.innerHTML = hasOrg
                ? `<i class="bi bi-info-circle text-primary me-1"></i> Request will be routed to the chosen Manager in <strong>${escapeHtml(currentUser.organizationName)}</strong>.`
                : '<i class="bi bi-info-circle text-primary me-1"></i> Request will be routed to the chosen Manager.';
        }
        handleInAppOrgChange();
    }
}

function handleInAppOrgChange() {
    const orgSelect = document.getElementById('reqOrgInput');
    const mgrSelect = document.getElementById('reqManagerInput');
    if (!mgrSelect) return;

    let targetOrgId = null;
    if (currentUser && currentUser.organizationId) {
        targetOrgId = currentUser.organizationId;
    } else if (orgSelect && orgSelect.value) {
        targetOrgId = parseInt(orgSelect.value);
    }

    if (!targetOrgId) {
        mgrSelect.innerHTML = '<option value="">-- Choose Manager --</option>';
        return;
    }

    const org = (cachedOrgsWithManagers || []).find(o => o.id === targetOrgId);

    if (org && org.managers && org.managers.length > 0) {
        mgrSelect.innerHTML = '<option value="">-- Choose Manager --</option>' +
            org.managers.map(m => `<option value="${m.id}">${escapeHtml(m.fullName)} (@${escapeHtml(m.username)})</option>`).join('');
    } else {
        mgrSelect.innerHTML = '<option value="">(No managers found in this org)</option>';
    }
}

async function handleSubmitAccessRequest(e) {
    e.preventDefault();
    const btn = document.getElementById('btnSubmitRoleRequest');
    if (btn) btn.disabled = true;

    const requestedRole = document.getElementById('reqRoleInput').value;
    const orgVal = document.getElementById('reqOrgInput') ? document.getElementById('reqOrgInput').value : '';
    const organizationId = (currentUser && currentUser.organizationId) ? currentUser.organizationId : (orgVal ? parseInt(orgVal) : null);

    const mgrVal = document.getElementById('reqManagerInput') ? document.getElementById('reqManagerInput').value : '';
    const targetManagerUserId = mgrVal ? parseInt(mgrVal) : null;

    const comments = document.getElementById('reqCommentsInput').value.trim();

    try {
        const res = await apiCall('/api/access-requests', 'POST', {
            requestedRole,
            organizationId,
            targetManagerUserId,
            comments
        });
        showToast(res.message || 'Access request submitted successfully!', 'success');
        document.getElementById('formSubmitAccessRequest').reset();
        switchRequestTab('my');
        await loadMyRequests();
    } catch {}
    finally {
        if (btn) btn.disabled = false;
    }
}
