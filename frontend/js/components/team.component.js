/**
 * =============================================================================
 * TASKFLOW ENTERPRISE - TEAM COLLABORATION & ACTIVITY COMPONENT
 * =============================================================================
 */

async function loadTeamMode() {
    if (!activeWorkspaceId) return;

    try {
        const analytics = await apiCall(`/api/analytics/team?workspaceId=${activeWorkspaceId}`);
        const statsRow = document.getElementById('teamStatsRow');
        if (statsRow) {
            statsRow.innerHTML = `
                <div class="col-12 col-sm-6 col-xl-3">
                    <div class="stats-card">
                        <div class="text-muted small fw-semibold">Team Members</div>
                        <h3 class="fw-bold mb-0 mt-1">${analytics.totalMembers}</h3>
                    </div>
                </div>
                <div class="col-12 col-sm-6 col-xl-3">
                    <div class="stats-card">
                        <div class="text-muted small fw-semibold">Team Active Tasks</div>
                        <h3 class="fw-bold mb-0 mt-1">${analytics.totalTasks - analytics.completedTasks}</h3>
                    </div>
                </div>
                <div class="col-12 col-sm-6 col-xl-3">
                    <div class="stats-card">
                        <div class="text-muted small fw-semibold">Completed Tasks</div>
                        <h3 class="fw-bold mb-0 mt-1 text-success">${analytics.completedTasks}</h3>
                    </div>
                </div>
                <div class="col-12 col-sm-6 col-xl-3">
                    <div class="stats-card">
                        <div class="text-muted small fw-semibold">Pending Reviews</div>
                        <h3 class="fw-bold mb-0 mt-1 text-warning">${analytics.pendingReviews}</h3>
                    </div>
                </div>
            `;
        }

        const activeWs = cachedWorkspaces.find(w => w.id === activeWorkspaceId);
        const canManageTeam = currentUser && ((activeWs && activeWs.createdByUserId === currentUser.id) || currentUser.role === 'PlatformAdmin' || currentUser.role === 'OrgAdmin' || currentUser.role === 'Manager');

        const workloadList = document.getElementById('teamWorkloadList');
        const memberList = (analytics.memberWorkloads || []).filter(m => {
            return (!currentUser || m.userId !== currentUser.id || currentUser.role === 'PlatformAdmin');
        });

        if (workloadList) {
            if (memberList.length === 0) {
                workloadList.innerHTML = `<div class="text-center text-muted small py-4"><i class="bi bi-people fs-3 d-block mb-1 text-muted"></i>No team members enrolled in this workspace yet. Click "Add Member" to assign contributors.</div>`;
            } else {
                const getRoleBadgeStyle = (role) => {
                    switch (role) {
                        case 'OrgAdmin': return 'bg-primary-subtle text-primary border border-primary-subtle';
                        case 'Manager': return 'bg-warning-subtle text-dark border border-warning-subtle';
                        case 'Employee': return 'bg-info-subtle text-info border border-info-subtle';
                        default: return 'bg-secondary-subtle text-secondary border border-secondary-subtle';
                    }
                };
                workloadList.innerHTML = memberList.map(m => `
                    <div class="d-flex align-items-center justify-content-between p-2 mb-2 rounded" style="background: rgba(0,0,0,0.02); border: 1px solid var(--border-color);">
                        <div class="d-flex align-items-center gap-2">
                            <div class="avatar-circle" style="width:28px; height:28px; font-size:0.75rem;">${m.fullName.charAt(0)}</div>
                            <div>
                                <div class="d-flex align-items-center gap-1 flex-wrap">
                                    <span class="fw-semibold small">${escapeHtml(m.fullName)}</span>
                                    <span class="badge ${getRoleBadgeStyle(m.role || 'Member')} rounded-pill px-2 py-0" style="font-size:0.65rem;">${escapeHtml(m.role || 'Member')}</span>
                                    ${(activeWs && m.userId === activeWs.createdByUserId) ? '<span class="badge bg-secondary-subtle text-secondary" style="font-size:0.62rem;">Team Owner</span>' : ''}
                                </div>
                                <div class="text-muted" style="font-size:0.72rem;">${m.department || 'General'}</div>
                            </div>
                        </div>
                        <div class="d-flex align-items-center gap-2">
                            <span class="badge bg-primary rounded-pill">${m.activeTasks} Active</span>
                            ${canManageTeam && (!activeWs || m.userId !== activeWs.createdByUserId) ? `
                                <button type="button" class="btn btn-outline-danger btn-sm py-0 px-2" style="font-size:0.72rem;" title="Remove Member from Team" onclick="handleRemoveTeamMember(${m.userId}, '${escapeHtml(m.fullName)}')">
                                    <i class="bi bi-person-x"></i> Remove
                                </button>
                            ` : ''}
                        </div>
                    </div>
                `).join('');
            }
        }

        await loadTasks();

        const activities = await apiCall(`/api/workflow/activity?workspaceId=${activeWorkspaceId}&mode=Team&limit=10`);
        const actList = document.getElementById('teamActivityList');
        if (actList) {
            if (!activities || activities.length === 0) {
                actList.innerHTML = `<div class="text-center text-muted small py-4"><i class="bi bi-clock-history fs-3 d-block mb-1 text-muted opacity-50"></i>No team activity recorded yet.</div>`;
            } else {
                actList.innerHTML = activities.map(a => `
                    <div class="d-flex gap-2 mb-3 small">
                        <div class="text-primary"><i class="bi bi-dot fs-4"></i></div>
                        <div>
                            <div><strong>${escapeHtml(a.username)}</strong>: ${escapeHtml(a.details)}</div>
                            <div class="text-muted" style="font-size:0.72rem;">${formatDate(a.timestamp)}</div>
                        </div>
                    </div>
                `).join('');
            }
        }
    } catch {}
}

async function handleClearTeamActivityStream() {
    if (!activeWorkspaceId) return;
    if (!confirm('Are you sure you want to clear the team activity stream for this workspace?')) return;

    try {
        const res = await apiCall(`/api/workflow/activity/clear?workspaceId=${activeWorkspaceId}&mode=Team`, 'DELETE');
        showToast(res.message || 'Team activity stream cleared!', 'success');
        if (activeMode === 'Team') {
            await loadTeamMode();
        }
    } catch {}
}
