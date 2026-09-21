/**
 * =============================================================================
 * TASKFLOW ENTERPRISE - PLATFORM ADMIN COMPONENT
 * =============================================================================
 */

async function loadAdminMode() {
    try {
        const analytics = await apiCall('/api/analytics/admin');
        const statsRow = document.getElementById('adminStatsRow');
        if (statsRow) {
            statsRow.innerHTML = `
                <div class="col-12 col-sm-6 col-xl-3">
                    <div class="stats-card">
                        <div class="text-muted small fw-semibold">Registered Users</div>
                        <h3 class="fw-bold mb-0 mt-1 text-danger">${analytics.totalUsers}</h3>
                    </div>
                </div>
                <div class="col-12 col-sm-6 col-xl-3">
                    <div class="stats-card">
                        <div class="text-muted small fw-semibold">Organizations</div>
                        <h3 class="fw-bold mb-0 mt-1">${analytics.totalOrganizations}</h3>
                    </div>
                </div>
                <div class="col-12 col-sm-6 col-xl-3">
                    <div class="stats-card">
                        <div class="text-muted small fw-semibold">Platform Tasks</div>
                        <h3 class="fw-bold mb-0 mt-1">${analytics.totalTasks}</h3>
                    </div>
                </div>
                <div class="col-12 col-sm-6 col-xl-3">
                    <div class="stats-card">
                        <div class="text-muted small fw-semibold">Security Audit Logs</div>
                        <h3 class="fw-bold mb-0 mt-1">${analytics.totalAuditLogs}</h3>
                    </div>
                </div>
            `;
        }

        const healthBox = document.getElementById('adminHealthBox');
        if (healthBox) {
            healthBox.innerHTML = `
                <div class="p-2 mb-2 rounded bg-success bg-opacity-10 text-success small fw-semibold"><i class="bi bi-check-circle me-1"></i> ${analytics.systemStatus}</div>
                <div class="small text-muted mb-1"><strong>Database:</strong> ${analytics.dbProvider}</div>
                <div class="small text-muted"><strong>Active Users (24h):</strong> ${analytics.activeUsersToday}</div>
            `;
        }

        const users = await apiCall('/api/admin/users');
        const tbody = document.getElementById('adminUserTableBody');
        if (tbody) {
            tbody.innerHTML = users.map(u => `
                <tr>
                    <td>
                        <div class="d-flex align-items-center gap-2">
                            <div class="avatar-circle" style="width:26px; height:26px; font-size:0.7rem;">${u.fullName.charAt(0)}</div>
                            <strong>${escapeHtml(u.fullName)}</strong>
                        </div>
                    </td>
                    <td>${escapeHtml(u.email)}</td>
                    <td>${escapeHtml(u.department || '-')}</td>
                    <td>
                        <select class="form-select form-select-sm py-0" style="font-size:0.75rem; width:140px;" onchange="changeUserRole(${u.id}, this.value)" ${u.role === 'PlatformAdmin' ? 'disabled' : ''}>
                            <option value="PlatformAdmin" ${u.role === 'PlatformAdmin' ? 'selected' : ''}>Platform Admin</option>
                            <option value="OrgAdmin" ${u.role === 'OrgAdmin' ? 'selected' : ''}>Organization Admin</option>
                            <option value="Manager" ${u.role === 'Manager' ? 'selected' : ''}>Manager</option>
                            <option value="Employee" ${u.role === 'Employee' ? 'selected' : ''}>Employee</option>
                            <option value="User" ${u.role === 'User' ? 'selected' : ''}>User</option>
                        </select>
                    </td>
                    <td>
                        <span class="badge bg-${u.isActive ? 'success' : 'secondary'} rounded-pill">${u.isActive ? 'Active' : 'Inactive'}</span>
                    </td>
                    <td>
                        ${u.role !== 'PlatformAdmin' ? `
                            <div class="btn-group btn-group-sm">
                                <button class="btn btn-outline-${u.isActive ? 'warning' : 'success'} btn-sm py-0 px-2" style="font-size:0.72rem;" onclick="toggleUserStatus(${u.id})">
                                    ${u.isActive ? 'Deactivate' : 'Activate'}
                                </button>
                                <button class="btn btn-outline-danger btn-sm py-0 px-2" style="font-size:0.72rem;" onclick="handleAdminDeleteUser(${u.id}, '${escapeHtml(u.username)}')">
                                    <i class="bi bi-trash"></i>
                                </button>
                            </div>
                        ` : '<span class="text-muted small fw-semibold">Platform Admin</span>'}
                    </td>
                </tr>
            `).join('');
        }

        const logs = await apiCall('/api/admin/activity-logs?limit=50');
        const logList = document.getElementById('adminAuditLogsList');
        if (logList) {
            if (!logs || logs.length === 0) {
                logList.innerHTML = `<div class="text-muted small text-center py-4"><i class="bi bi-shield-check fs-3 d-block mb-1 text-muted opacity-50"></i>No security audit events recorded yet.</div>`;
            } else {
                logList.innerHTML = logs.map(l => `
                    <div class="p-2 mb-2 rounded small" style="background: rgba(0,0,0,0.02); border: 1px solid var(--border-color);">
                        <div class="d-flex justify-content-between align-items-center mb-1">
                            <div class="d-flex align-items-center gap-1">
                                <span class="badge bg-secondary-subtle text-secondary" style="font-size: 0.65rem;">${escapeHtml(l.mode || 'Security')}</span>
                                <strong>${escapeHtml(l.username || 'System')}</strong>
                            </div>
                            <span class="text-muted" style="font-size:0.7rem;">${formatDate(l.timestamp)}</span>
                        </div>
                        <div class="text-secondary" style="word-break: break-word;"><strong class="text-dark">${escapeHtml(l.action)}</strong>: ${escapeHtml(l.details)}</div>
                    </div>
                `).join('');
            }
        }

        await loadAdminIncomingRequests();
        await loadAdminWorkspaces();
        await loadAdminOrganizations();
    } catch {}
}

async function loadAdminOrganizations() {
    const tbody = document.getElementById('adminOrgTableBody');
    const badge = document.getElementById('adminOrgsCountBadge');
    if (!tbody) return;

    try {
        const orgs = await apiCall('/api/admin/organizations');
        if (badge) {
            badge.textContent = `${orgs.length} Organizations`;
        }

        if (orgs.length === 0) {
            tbody.innerHTML = `<tr><td colspan="7" class="text-center text-muted py-3">No organizations found.</td></tr>`;
            return;
        }

        tbody.innerHTML = orgs.map(o => `
            <tr>
                <td>
                    <div class="d-flex align-items-center gap-2">
                        <i class="bi bi-building text-primary" style="font-size:0.85rem;"></i>
                        <strong>${escapeHtml(o.name)}</strong>
                    </div>
                    ${o.description ? `<div class="text-muted" style="font-size:0.75rem;">${escapeHtml(o.description)}</div>` : ''}
                </td>
                <td><code>${escapeHtml(o.slug)}</code></td>
                <td><span class="badge bg-primary-subtle text-primary border border-primary-subtle rounded-pill">${escapeHtml(o.subscriptionPlan || 'Free')}</span></td>
                <td><span class="badge bg-secondary-subtle text-secondary border border-secondary-subtle rounded-pill">${o.memberCount} Members</span></td>
                <td><span class="badge bg-secondary-subtle text-secondary border border-secondary-subtle rounded-pill">${o.workspaceCount} Workspaces</span></td>
                <td class="text-muted" style="font-size:0.75rem;">${formatDate(o.createdAt)}</td>
                <td>
                    <button class="btn btn-outline-danger btn-sm py-0 px-2" style="font-size:0.72rem;" onclick="handleAdminDeleteOrganization(${o.id}, '${escapeHtml(o.name)}')">
                        <i class="bi bi-trash me-1"></i> Delete
                    </button>
                </td>
            </tr>
        `).join('');
    } catch {
        tbody.innerHTML = `<tr><td colspan="7" class="text-center text-danger py-3">Failed to load organizations.</td></tr>`;
    }
}

async function handleAdminDeleteOrganization(orgId, orgName) {
    if (!confirm(`Are you sure you want to permanently delete organization "${orgName}"?\nAll associated workspaces, teams, tasks, and member assignments will be removed.`)) {
        return;
    }

    try {
        const res = await apiCall(`/api/admin/organizations/${orgId}`, 'DELETE');
        showToast(res.message || 'Organization deleted successfully.', 'success');
        await loadAdminOrganizations();
        await loadAdminWorkspaces();
        await loadWorkspaces();
        await loadRegistrationOrgsAndManagers();
    } catch {}
}

async function handleAdminClearAllOrganizations() {
    if (!confirm("Are you sure you want to delete ALL target organizations?\nThis will clean up all organizations and their collaborative workspaces completely.")) {
        return;
    }

    try {
        const res = await apiCall('/api/admin/organizations/clear-all', 'DELETE');
        showToast(res.message || 'All organizations cleared successfully.', 'success');
        await loadAdminOrganizations();
        await loadAdminWorkspaces();
        await loadWorkspaces();
        await loadRegistrationOrgsAndManagers();
    } catch {}
}

async function loadAdminWorkspaces() {
    const tbody = document.getElementById('adminWorkspaceTableBody');
    const badge = document.getElementById('adminWorkspacesCountBadge');
    if (!tbody) return;

    try {
        const workspaces = await apiCall('/api/admin/workspaces');
        if (badge) {
            badge.textContent = `${workspaces.length} Workspaces`;
        }

        if (workspaces.length === 0) {
            tbody.innerHTML = `<tr><td colspan="7" class="text-center text-muted py-3">No workspaces found.</td></tr>`;
            return;
        }

        tbody.innerHTML = workspaces.map(w => {
            const modeColors = {
                'Personal': 'secondary',
                'Team': 'primary',
                'Project': 'info',
                'Business': 'success'
            };
            const modeColor = modeColors[w.mode] || 'secondary';
            const creatorName = w.createdByFullName || w.createdByUsername || 'System';

            return `
            <tr>
                <td>
                    <div class="d-flex align-items-center gap-2">
                        <i class="bi bi-circle-fill" style="color:${w.color || '#6366f1'}; font-size:0.65rem;"></i>
                        <strong>${escapeHtml(w.name)}</strong>
                    </div>
                    ${w.description ? `<div class="text-muted" style="font-size:0.75rem;">${escapeHtml(w.description)}</div>` : ''}
                </td>
                <td><span class="badge bg-${modeColor}-subtle text-${modeColor} border border-${modeColor}-subtle rounded-pill">${w.mode}</span></td>
                <td>${w.organizationName ? `<span class="badge bg-secondary-subtle text-secondary border border-secondary-subtle rounded-pill">${escapeHtml(w.organizationName)}</span>` : '<span class="text-muted">-</span>'}</td>
                <td>
                    <div class="small fw-semibold">${escapeHtml(creatorName)}</div>
                    ${w.createdByUsername ? `<div class="text-muted" style="font-size:0.7rem;">@${escapeHtml(w.createdByUsername)}</div>` : ''}
                </td>
                <td>
                    <span class="badge bg-secondary-subtle text-secondary border border-secondary-subtle rounded-pill me-1">${w.taskCount} Tasks</span>
                    <span class="badge bg-secondary-subtle text-secondary border border-secondary-subtle rounded-pill">${w.projectCount} Projects</span>
                </td>
                <td class="text-muted" style="font-size:0.75rem;">${formatDate(w.createdAt)}</td>
                <td>
                    <button class="btn btn-outline-danger btn-sm py-0 px-2" style="font-size:0.72rem;" onclick="handleAdminDeleteWorkspace(${w.id}, '${escapeHtml(w.name)}')">
                        <i class="bi bi-trash me-1"></i> Delete
                    </button>
                </td>
            </tr>
            `;
        }).join('');
    } catch {
        tbody.innerHTML = `<tr><td colspan="7" class="text-center text-danger py-3">Failed to load workspaces list.</td></tr>`;
    }
}

async function handleAdminDeleteWorkspace(workspaceId, workspaceName) {
    if (!confirm(`Are you sure you want to permanently delete workspace "${workspaceName}"?\nAll tasks, projects, milestones, and data within it will be removed permanently.`)) {
        return;
    }

    try {
        const res = await apiCall(`/api/admin/workspaces/${workspaceId}`, 'DELETE');
        showToast(res.message || 'Workspace deleted successfully.', 'success');
        await loadAdminWorkspaces();
        await loadWorkspaces();
    } catch {}
}

async function loadAdminIncomingRequests() {
    const list = document.getElementById('adminIncomingRequestsList');
    const badge = document.getElementById('adminIncomingReqBadge');
    if (!list) return;

    try {
        const requests = await apiCall('/api/access-requests/incoming');
        const count = requests.length;

        if (badge) {
            badge.textContent = `${count} Pending`;
            badge.classList.toggle('d-none', count === 0);
        }

        if (count === 0) {
            list.innerHTML = `<div class="text-center text-muted small py-3"><i class="bi bi-check2-circle text-success fs-4 d-block mb-1"></i>No pending role or access requests. All permissions are up to date!</div>`;
            return;
        }

        list.innerHTML = requests.map(r => {
            const displayName = r.fullName || r.username || 'User';
            const email = r.email || '';
            const mgrName = r.targetManagerName || r.targetManagerFullName || '';
            return `
            <div class="p-3 mb-2 rounded shadow-sm d-flex flex-column flex-md-row justify-content-between align-items-md-center gap-3" style="background: var(--bg-card); border: 1px solid var(--border-color); border-left: 3px solid #4f46e5 !important;">
                <div class="d-flex align-items-center gap-3">
                    <div class="avatar-circle" style="width:38px; height:38px; font-size:0.9rem;">${displayName.charAt(0).toUpperCase()}</div>
                    <div>
                        <div class="fw-bold small d-flex align-items-center gap-2">
                            ${escapeHtml(displayName)}
                            <span class="badge bg-primary text-white rounded-pill px-2 py-0" style="font-size:0.7rem;">${r.requestedRole}</span>
                        </div>
                        <div class="text-muted" style="font-size:0.75rem;">
                            @${escapeHtml(r.username)}${email ? ` • ${escapeHtml(email)}` : ''}
                            ${r.organizationName ? ` • Org: <strong>${escapeHtml(r.organizationName)}</strong>` : ''}
                            ${mgrName ? ` • Mgr: <strong>${escapeHtml(mgrName)}</strong>` : ''}
                        </div>
                        ${r.comments ? `<div class="text-secondary small mt-1 fst-italic" style="font-size:0.75rem;">"${escapeHtml(r.comments)}"</div>` : ''}
                        <div class="text-muted" style="font-size:0.68rem;">Requested on ${formatDate(r.createdAt)}</div>
                    </div>
                </div>
                <div class="d-flex align-items-center gap-2">
                    <button type="button" class="btn btn-success btn-sm py-1 px-3 fw-semibold shadow-sm" onclick="handleDecideAccessRequest(${r.id}, true)">
                        <i class="bi bi-check-lg me-1"></i> Approve
                    </button>
                    <button type="button" class="btn btn-outline-danger btn-sm py-1 px-3 fw-semibold" onclick="handleDecideAccessRequest(${r.id}, false)">
                        <i class="bi bi-x-lg me-1"></i> Reject
                    </button>
                </div>
            </div>
        `;}).join('');
    } catch {
        list.innerHTML = `<div class="text-center text-danger small py-3">Failed to load incoming role requests.</div>`;
    }
}

async function changeUserRole(userId, newRole) {
    try {
        const res = await apiCall(`/api/admin/users/${userId}/role`, 'PATCH', { role: newRole });
        showToast(res.message || 'User role updated successfully!', 'success');
        await fetchCurrentUser();
        renderUserInfo();
        await loadUsers();
        await loadWorkspaces();
        updateModeLockStates();
        await loadAdminMode();
    } catch {}
}

async function toggleUserStatus(userId) {
    try {
        await apiCall(`/api/admin/users/${userId}/toggle-status`, 'PATCH');
        showToast('User status toggled!', 'success');
        await loadAdminMode();
    } catch {}
}

async function handleAdminDeleteUser(userId, username) {
    if (!confirm(`Are you sure you want to permanently delete user '${username}' and all their personal workspace data?`)) {
        return;
    }

    try {
        const res = await apiCall(`/api/admin/users/${userId}`, 'DELETE');
        showToast(res.message || 'User removed successfully.', 'success');
        await loadAdminMode();
    } catch {}
}
