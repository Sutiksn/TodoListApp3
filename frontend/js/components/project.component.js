/**
 * =============================================================================
 * TASKFLOW ENTERPRISE - PROJECTS, MILESTONES & GOALS COMPONENT
 * =============================================================================
 */

async function loadProjectMode() {
    if (!activeWorkspaceId) return;

    try {
        cachedProjects = await apiCall(`/api/projects?workspaceId=${activeWorkspaceId}`);
        const analytics = await apiCall(`/api/analytics/project?workspaceId=${activeWorkspaceId}`);

        const statsRow = document.getElementById('projectStatsRow');
        if (statsRow) {
            statsRow.innerHTML = `
                <div class="col-12 col-sm-6 col-xl-3">
                    <div class="stats-card">
                        <div class="text-muted small fw-semibold">Active Projects</div>
                        <h3 class="fw-bold mb-0 mt-1">${analytics.activeProjects}</h3>
                    </div>
                </div>
                <div class="col-12 col-sm-6 col-xl-3">
                    <div class="stats-card">
                        <div class="text-muted small fw-semibold">Overall Progress</div>
                        <h3 class="fw-bold mb-0 mt-1">${analytics.overallProjectProgressPercentage}%</h3>
                    </div>
                </div>
                <div class="col-12 col-sm-6 col-xl-3">
                    <div class="stats-card">
                        <div class="text-muted small fw-semibold">Milestones Reached</div>
                        <h3 class="fw-bold mb-0 mt-1 text-success">${analytics.reachedMilestones} / ${analytics.totalMilestones}</h3>
                    </div>
                </div>
                <div class="col-12 col-sm-6 col-xl-3">
                    <div class="stats-card">
                        <div class="text-muted small fw-semibold">Completed Projects</div>
                        <h3 class="fw-bold mb-0 mt-1">${analytics.completedProjects}</h3>
                    </div>
                </div>
            `;
        }

        const projList = document.getElementById('projectsList');
        if (cachedProjects.length === 0) {
            selectedProjectId = null;
            if (projList) projList.innerHTML = `<div class="text-muted small text-center py-3">No projects yet. Create your first project!</div>`;
            const badge = document.getElementById('milestonesProjectBadge');
            if (badge) badge.textContent = 'No Projects';
            const msList = document.getElementById('milestonesList');
            if (msList) msList.innerHTML = `<div class="text-muted small text-center py-2">Create a project to add milestones.</div>`;
        } else {
            if (!selectedProjectId || !cachedProjects.some(p => p.id === selectedProjectId)) {
                selectedProjectId = cachedProjects[0].id;
            }
            renderProjectCards();
            await loadMilestonesForSelectedProject();
        }

        await loadTasks();
    } catch {}
}

function renderProjectCards() {
    const projList = document.getElementById('projectsList');
    if (!projList) return;

    projList.innerHTML = cachedProjects.map(p => {
        const isSelected = (p.id === selectedProjectId);
        const canDeleteProj = currentUser && (p.createdByUserId === currentUser.id || currentUser.role === 'PlatformAdmin' || currentUser.role === 'OrgAdmin' || currentUser.role === 'Manager');
        return `
        <div class="p-3 mb-2 rounded shadow-sm project-card-item ${isSelected ? 'border-primary' : ''}" 
             style="cursor: pointer; background: var(--bg-card); border: ${isSelected ? '2px solid var(--primary)' : '1px solid var(--border-color)'}; border-left: 5px solid ${p.color} !important;"
             onclick="selectProjectForMilestones(${p.id})">
            <div class="d-flex justify-content-between align-items-center mb-1">
                <div class="d-flex align-items-center gap-2">
                    <strong class="small ${isSelected ? 'text-primary' : ''}">${escapeHtml(p.name)}</strong>
                    ${isSelected ? '<span class="badge bg-primary text-white" style="font-size:0.62rem;">Viewing Milestones</span>' : ''}
                </div>
                <div class="d-flex align-items-center gap-2" onclick="event.stopPropagation()">
                    <span class="badge bg-light text-dark rounded-pill" style="font-size:0.7rem;">${p.priority}</span>
                    ${canDeleteProj ? `
                        <button type="button" class="btn btn-link btn-sm text-danger text-opacity-75 p-0" title="Delete Project" onclick="handleDeleteProject(${p.id}, '${escapeHtml(p.name)}')">
                            <i class="bi bi-trash"></i>
                        </button>
                    ` : ''}
                </div>
            </div>
            <div class="text-muted small mb-2" style="font-size:0.78rem;">${p.description || 'No description'}</div>
            <div class="task-progress-mini mb-1">
                <div class="task-progress-mini-bar" style="width: ${p.progressPercentage}%; background:${p.color};"></div>
            </div>
            <div class="d-flex justify-content-between text-muted" style="font-size:0.7rem;">
                <span>${p.completedTasks}/${p.totalTasks} Tasks Done</span>
                <span>${p.progressPercentage}%</span>
            </div>
        </div>
        `;
    }).join('');
}

async function selectProjectForMilestones(projectId) {
    selectedProjectId = projectId;
    renderProjectCards();
    await loadMilestonesForSelectedProject();
}

async function loadMilestonesForSelectedProject() {
    const msList = document.getElementById('milestonesList');
    const badge = document.getElementById('milestonesProjectBadge');
    if (!msList) return;

    const currentProject = cachedProjects.find(p => p.id === selectedProjectId);
    if (!currentProject) {
        if (badge) badge.textContent = 'No Project';
        msList.innerHTML = `<div class="text-muted small text-center py-2">Select a project to view milestones.</div>`;
        return;
    }

    if (badge) {
        badge.textContent = currentProject.name;
    }

    try {
        const milestones = await apiCall(`/api/projects/${selectedProjectId}/milestones`);
        if (!milestones || milestones.length === 0) {
            msList.innerHTML = `<div class="text-muted small text-center py-4"><i class="bi bi-flag fs-3 d-block mb-1 text-muted opacity-50"></i>No milestones recorded for <strong>${escapeHtml(currentProject.name)}</strong> yet. Click "Add Milestone" to set targets.</div>`;
        } else {
            msList.innerHTML = milestones.map(m => `
                <div class="d-flex align-items-center justify-content-between p-2 mb-2 rounded" style="background: rgba(0,0,0,0.02); border: 1px solid var(--border-color);">
                    <div class="d-flex align-items-center gap-2">
                        <input type="checkbox" class="form-check-input mt-0" ${m.isReached ? 'checked' : ''} onchange="toggleMilestone(${m.id})">
                        <span class="small ${m.isReached ? 'text-decoration-line-through text-muted' : 'fw-semibold'}">${escapeHtml(m.title)}</span>
                    </div>
                    ${m.dueDate ? `<span class="badge bg-light text-muted" style="font-size:0.7rem;">${formatDate(m.dueDate)}</span>` : ''}
                </div>
            `).join('');
        }
    } catch {
        msList.innerHTML = `<div class="text-danger small text-center py-2">Failed to load milestones.</div>`;
    }
}

function openCreateProjectModal() {
    const isManagerOrAdmin = currentUser && (currentUser.role === 'PlatformAdmin' || currentUser.role === 'OrgAdmin' || currentUser.role === 'Manager');
    if (!isManagerOrAdmin) {
        showToast('Only Managers and Administrators can create projects.', 'warning');
        return;
    }
    new bootstrap.Modal(document.getElementById('modalCreateProject')).show();
}

async function handleCreateProject(e) {
    e.preventDefault();
    const name = document.getElementById('projectInputName').value.trim();
    const description = document.getElementById('projectInputDesc').value.trim();
    const priority = document.getElementById('projectInputPriority').value;

    try {
        const newProj = await apiCall('/api/projects', 'POST', {
            workspaceId: activeWorkspaceId,
            name,
            description,
            priority
        });
        bootstrap.Modal.getInstance(document.getElementById('modalCreateProject')).hide();
        document.getElementById('formCreateProject').reset();
        showToast('Project created successfully!', 'success');
        if (newProj && newProj.id) {
            selectedProjectId = newProj.id;
        }
        await loadProjectMode();
    } catch {}
}

async function handleDeleteProject(projectId, name) {
    if (!confirm(`Are you sure you want to delete project "${name}"?\n\nThis will permanently remove all associated milestones, project tasks, and team assignments.`)) return;

    try {
        const res = await apiCall(`/api/projects/${projectId}`, 'DELETE');
        if (selectedProjectId === projectId) {
            selectedProjectId = null;
        }
        showToast(res.message || 'Project and associated data deleted successfully!', 'success');
        await loadProjectMode();
        if (activeMode === 'Team') await loadTeamMode();
    } catch {}
}

function openCreateMilestoneModal() {
    const projSelect = document.getElementById('milestoneInputProject');
    if (projSelect) {
        projSelect.innerHTML = cachedProjects.map(p => `<option value="${p.id}" ${p.id === selectedProjectId ? 'selected' : ''}>${escapeHtml(p.name)}</option>`).join('');
    }
    new bootstrap.Modal(document.getElementById('modalCreateMilestone')).show();
}

async function handleCreateMilestone(e) {
    e.preventDefault();
    const projectId = document.getElementById('milestoneInputProject').value;
    const title = document.getElementById('milestoneInputTitle').value.trim();
    const dueDate = document.getElementById('milestoneInputDueDate').value || null;

    try {
        await apiCall(`/api/projects/${projectId}/milestones`, 'POST', {
            projectId: parseInt(projectId),
            title,
            dueDate: dueDate ? new Date(dueDate).toISOString() : null
        });
        selectedProjectId = parseInt(projectId);
        bootstrap.Modal.getInstance(document.getElementById('modalCreateMilestone')).hide();
        document.getElementById('formCreateMilestone').reset();
        showToast('Milestone saved!', 'success');
        await loadProjectMode();
    } catch {}
}

async function toggleMilestone(milestoneId) {
    try {
        await apiCall(`/api/projects/milestones/${milestoneId}/toggle`, 'PATCH');
        await loadMilestonesForSelectedProject();
    } catch {}
}

function openCreateGoalModal() {
    const isManagerOrAdmin = currentUser && (currentUser.role === 'PlatformAdmin' || currentUser.role === 'OrgAdmin' || currentUser.role === 'Manager');
    if (!isManagerOrAdmin) {
        showToast('Only Managers and Administrators can create strategic goals.', 'warning');
        return;
    }
    new bootstrap.Modal(document.getElementById('modalCreateGoal')).show();
}

async function handleCreateGoal(e) {
    e.preventDefault();
    const title = document.getElementById('goalInputTitle').value.trim();
    const targetDate = document.getElementById('goalInputTargetDate').value || null;

    try {
        await apiCall('/api/projects/goals', 'POST', {
            workspaceId: activeWorkspaceId,
            title,
            targetDate: targetDate ? new Date(targetDate).toISOString() : null
        });
        bootstrap.Modal.getInstance(document.getElementById('modalCreateGoal')).hide();
        document.getElementById('formCreateGoal').reset();
        showToast('Strategic goal saved!', 'success');
        await loadProjectMode();
    } catch {}
}
