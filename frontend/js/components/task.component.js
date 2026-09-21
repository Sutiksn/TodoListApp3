/**
 * =============================================================================
 * TASKFLOW ENTERPRISE - TASK & KANBAN / CALENDAR COMPONENT
 * =============================================================================
 */

let selectedCalendarDayStr = null;

function buildKanbanBoard(containerElement, tasks, containerId) {
    if (!containerElement) return;

    const stages = [
        { id: 'Plan', label: '<i class="bi bi-compass text-secondary me-1"></i> Plan', badge: 'bg-secondary' },
        { id: 'ToDo', label: '<i class="bi bi-circle text-info me-1"></i> To Do', badge: 'bg-info text-dark' },
        { id: 'InProgress', label: '<i class="bi bi-arrow-repeat text-warning me-1"></i> In Progress', badge: 'bg-warning text-dark' },
        { id: 'Review', label: '<i class="bi bi-eye text-purple me-1" style="color:#7c3aed;"></i> Review', badge: 'bg-purple text-white', style: 'background:#7c3aed;' },
        { id: 'Completed', label: '<i class="bi bi-check2-circle text-success me-1"></i> Completed', badge: 'bg-success' }
    ];

    containerElement.innerHTML = stages.map(s => {
        const stageTasks = tasks.filter(t => t.status === s.id);
        const taskCardsHtml = stageTasks.length === 0
            ? `<div class="text-center text-muted small py-4 opacity-50"><i class="bi bi-inbox d-block fs-4 mb-1"></i>No tasks</div>`
            : stageTasks.map(t => renderTaskCard(t)).join('');

        return `
            <div class="kanban-column" data-stage="${s.id}">
                <div class="kanban-header">
                    <span>${s.label}</span>
                    <span class="badge ${s.badge} rounded-pill" ${s.style ? `style="${s.style}"` : ''}>${stageTasks.length}</span>
                </div>
                <div class="kanban-task-list" ondragover="allowDrop(event)" ondrop="handleDrop(event, '${s.id}')">
                    ${taskCardsHtml}
                </div>
            </div>
        `;
    }).join('');
}

async function loadTasks(search = null) {
    if (!activeWorkspaceId) return;

    let url = `/api/tasks?workspaceId=${activeWorkspaceId}`;
    if (search) {
        url += `&search=${encodeURIComponent(search)}`;
    }

    try {
        cachedTasks = await apiCall(url);
        
        buildKanbanBoard(document.getElementById('subviewKanban'), cachedTasks, 'personal');
        buildKanbanBoard(document.getElementById('teamKanbanBoard'), cachedTasks, 'team');
        buildKanbanBoard(document.getElementById('projectKanbanBoard'), cachedTasks, 'project');
    } catch {}
}

function debounceSearchTasks(val) {
    clearTimeout(searchDebounceTimer);
    searchDebounceTimer = setTimeout(() => {
        loadTasks(val.trim());
    }, 300);
}

function renderTaskCard(t) {
    const overdueBadge = t.isOverdue ? `<span class="badge bg-danger text-white rounded-pill px-2 py-0" style="font-size: 0.7rem;"><i class="bi bi-clock me-1"></i>Overdue</span>` : '';
    const assigneeAvatar = t.assigneeFullName ? `<div class="avatar-circle" style="width:22px; height:22px; font-size:0.65rem;" title="Assigned to ${escapeHtml(t.assigneeFullName)}">${t.assigneeFullName.charAt(0)}</div>` : '';
    
    let subtaskHtml = '';
    if (t.totalSubtasks > 0) {
        subtaskHtml = `
            <div class="task-progress-mini">
                <div class="task-progress-mini-bar" style="width: ${t.subtaskProgressPercentage}%;"></div>
            </div>
            <div class="d-flex justify-content-between align-items-center text-muted" style="font-size: 0.7rem;">
                <span><i class="bi bi-check2-square me-1"></i>${t.completedSubtasks}/${t.totalSubtasks} subtasks</span>
                <span>${t.subtaskProgressPercentage}%</span>
            </div>
        `;
    }

    return `
        <div class="task-card ${t.isOverdue ? 'is-overdue' : ''} ${t.isCompleted ? 'is-completed' : ''}" 
             draggable="true" ondragstart="handleDragStart(event, ${t.id})" 
             onclick="openTaskDetails(${t.id})">
            
            <div class="d-flex justify-content-between align-items-start mb-1">
                <span class="badge badge-priority-${t.priority} rounded-pill px-2 py-0" style="font-size: 0.7rem;">${t.priority}</span>
                ${overdueBadge}
            </div>

            <div class="task-title">${escapeHtml(t.title)}</div>

            ${subtaskHtml}

            <div class="task-meta justify-content-between">
                <div class="d-flex align-items-center gap-2">
                    ${t.dueDate ? `<span><i class="bi bi-calendar3 me-1"></i>${formatDate(t.dueDate)}</span>` : ''}
                    ${t.commentCount > 0 ? `<span><i class="bi bi-chat-left me-1"></i>${t.commentCount}</span>` : ''}
                </div>
                ${assigneeAvatar}
            </div>
        </div>
    `;
}

function allowDrop(e) {
    e.preventDefault();
}

function handleDragStart(e, taskId) {
    e.dataTransfer.setData('text/plain', taskId);
}

async function handleDrop(e, targetStage) {
    e.preventDefault();
    const taskId = e.dataTransfer.getData('text/plain');
    if (!taskId) return;

    try {
        await apiCall(`/api/tasks/${taskId}/status?mode=${activeMode}`, 'PATCH', { status: targetStage });
        await loadTasks();
        showToast(`Task moved to ${targetStage}`, 'success');
    } catch {}
}

async function openTaskDetails(taskId) {
    currentTaskId = taskId;
    try {
        const task = await apiCall(`/api/tasks/${taskId}`);
        document.getElementById('drawerTaskTitle').textContent = task.title;
        document.getElementById('drawerTaskDescription').textContent = task.description || 'No description provided.';
        
        document.getElementById('drawerStatusBadge').textContent = task.status;
        document.getElementById('drawerPriorityBadge').className = `badge badge-priority-${task.priority} rounded-pill`;
        document.getElementById('drawerPriorityBadge').textContent = task.priority;

        document.getElementById('drawerMetaWorkspace').textContent = task.workspaceName;
        document.getElementById('drawerMetaProject').textContent = task.projectName || 'None';
        document.getElementById('drawerMetaAssignee').textContent = task.assigneeFullName || 'Unassigned';
        document.getElementById('drawerMetaDueDate').textContent = task.dueDate ? formatDate(task.dueDate) : 'No due date';
        document.getElementById('drawerMetaEstHours').textContent = `${task.estimatedHours} hrs`;
        document.getElementById('drawerMetaCreatedBy').textContent = task.createdByUsername;

        const canDeleteTask = currentUser && (task.createdByUserId === currentUser.id || currentUser.role === 'PlatformAdmin' || currentUser.role === 'OrgAdmin' || currentUser.role === 'Manager');
        const deleteBtn = document.getElementById('btnDeleteTask');
        if (deleteBtn) deleteBtn.classList.toggle('d-none', !canDeleteTask);

        renderDrawerSubtasks(task.subtasks || []);
        renderDrawerComments(task.comments || []);

        const drawer = new bootstrap.Offcanvas(document.getElementById('taskDetailsDrawer'));
        drawer.show();
    } catch {}
}

function renderDrawerSubtasks(subtasks) {
    const list = document.getElementById('drawerSubtasksList');
    const total = subtasks.length;
    const completed = subtasks.filter(s => s.isCompleted).length;
    const pct = total > 0 ? Math.round((completed / total) * 100) : 0;

    const progText = document.getElementById('drawerSubtasksProgressText');
    const progBar = document.getElementById('drawerSubtasksProgressBar');
    if (progText) progText.textContent = `${completed} / ${total} Completed (${pct}%)`;
    if (progBar) progBar.style.width = `${pct}%`;

    if (!list) return;

    if (total === 0) {
        list.innerHTML = `<div class="text-muted small text-center py-2">No subtasks yet. Add one below!</div>`;
        return;
    }

    list.innerHTML = subtasks.map(s => `
        <div class="subtask-item ${s.isCompleted ? 'completed' : ''}">
            <div class="d-flex align-items-center gap-2">
                <input type="checkbox" class="form-check-input mt-0" ${s.isCompleted ? 'checked' : ''} onchange="toggleSubtask(${s.id})">
                <span class="small">${escapeHtml(s.title)}</span>
            </div>
            <button class="btn btn-link btn-sm text-danger p-0" onclick="deleteSubtask(${s.id})"><i class="bi bi-x"></i></button>
        </div>
    `).join('');
}

function renderDrawerComments(comments) {
    const list = document.getElementById('drawerCommentsList');
    if (!list) return;

    if (comments.length === 0) {
        list.innerHTML = `<div class="text-muted small text-center py-3">No comments yet. Start the discussion!</div>`;
        return;
    }

    list.innerHTML = comments.map(c => `
        <div class="comment-bubble">
            <div class="d-flex justify-content-between align-items-center mb-1">
                <strong class="small">${escapeHtml(c.fullName)}</strong>
                <span class="text-muted" style="font-size: 0.72rem;">${formatDate(c.createdAt)}</span>
            </div>
            <div class="small">${escapeHtml(c.content)}</div>
        </div>
    `).join('');
}

async function updateCurrentTaskStatus(newStage) {
    if (!currentTaskId) return;
    try {
        await apiCall(`/api/tasks/${currentTaskId}/status?mode=${activeMode}`, 'PATCH', { status: newStage });
        await openTaskDetails(currentTaskId);
        await loadTasks();
        showToast(`Task status updated to ${newStage}`, 'success');
    } catch {}
}

async function handleAddSubtask(e) {
    e.preventDefault();
    if (!currentTaskId) return;
    const input = document.getElementById('newSubtaskInput');
    const title = input.value.trim();
    if (!title) return;

    try {
        await apiCall(`/api/tasks/${currentTaskId}/subtasks`, 'POST', { title });
        input.value = '';
        await openTaskDetails(currentTaskId);
        await loadTasks();
    } catch {}
}

async function toggleSubtask(subtaskId) {
    try {
        await apiCall(`/api/tasks/subtasks/${subtaskId}/toggle`, 'PATCH');
        await openTaskDetails(currentTaskId);
        await loadTasks();
    } catch {}
}

async function deleteSubtask(subtaskId) {
    try {
        await apiCall(`/api/tasks/subtasks/${subtaskId}`, 'DELETE');
        await openTaskDetails(currentTaskId);
        await loadTasks();
    } catch {}
}

async function handleAddComment(e) {
    e.preventDefault();
    if (!currentTaskId) return;
    const input = document.getElementById('newCommentInput');
    const content = input.value.trim();
    if (!content) return;

    try {
        await apiCall(`/api/tasks/${currentTaskId}/comments`, 'POST', { content });
        input.value = '';
        await openTaskDetails(currentTaskId);
    } catch {}
}

async function handleDeleteCurrentTask() {
    if (!currentTaskId || !confirm('Are you sure you want to delete this task?')) return;

    try {
        await apiCall(`/api/tasks/${currentTaskId}`, 'DELETE');
        bootstrap.Offcanvas.getInstance(document.getElementById('taskDetailsDrawer')).hide();
        await loadTasks();
        showToast('Task deleted successfully.', 'success');
    } catch {}
}

function openCreateTaskModal() {
    if (!activeWorkspaceId) {
        showToast('Please select a workspace first.', 'warning');
        return;
    }

    const projSelect = document.getElementById('taskInputProject');
    if (projSelect) {
        projSelect.innerHTML = '<option value="">(None - Workspace Level)</option>' +
            cachedProjects.map(p => `<option value="${p.id}">${escapeHtml(p.name)}</option>`).join('');
    }

    new bootstrap.Modal(document.getElementById('modalCreateTask')).show();
}

async function handleCreateTask(e) {
    e.preventDefault();
    const title = document.getElementById('taskInputTitle').value.trim();
    const description = document.getElementById('taskInputDesc').value.trim();
    const status = document.getElementById('taskInputStatus').value;
    const priority = document.getElementById('taskInputPriority').value;
    const dueDate = document.getElementById('taskInputDueDate').value || null;
    const assigneeVal = document.getElementById('taskInputAssignee').value;
    const projectVal = document.getElementById('taskInputProject').value;

    const payload = {
        workspaceId: activeWorkspaceId,
        title,
        description,
        status,
        priority,
        dueDate: dueDate ? new Date(dueDate).toISOString() : null,
        assigneeUserId: assigneeVal ? parseInt(assigneeVal) : null,
        projectId: projectVal ? parseInt(projectVal) : null
    };

    try {
        await apiCall(`/api/tasks?mode=${activeMode}`, 'POST', payload);
        bootstrap.Modal.getInstance(document.getElementById('modalCreateTask')).hide();
        document.getElementById('formCreateTask').reset();
        showToast('Task created successfully!', 'success');
        await loadTasks();
    } catch {}
}

function renderCalendar() {
    const grid = document.getElementById('calendarGrid');
    if (!grid) return;
    const title = document.getElementById('calendarMonthTitle');
    const monthNames = ["January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December"];
    const year = calendarDate.getFullYear();
    const month = calendarDate.getMonth();

    if (title) title.textContent = `${monthNames[month]} ${year}`;

    const daysOfWeek = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];
    let html = daysOfWeek.map(d => `<div class="calendar-day-header">${d}</div>`).join('');

    const firstDay = new Date(year, month, 1).getDay();
    const totalDays = new Date(year, month + 1, 0).getDate();
    const today = new Date();

    for (let i = 0; i < firstDay; i++) {
        html += `<div class="calendar-cell opacity-25" style="cursor: default;"></div>`;
    }

    for (let d = 1; d <= totalDays; d++) {
        const isToday = today.getFullYear() === year && today.getMonth() === month && today.getDate() === d;
        const currentDayStr = `${year}-${String(month + 1).padStart(2, '0')}-${String(d).padStart(2, '0')}`;

        const dayTasks = cachedTasks.filter(t => t.dueDate && t.dueDate.startsWith(currentDayStr));

        const displayedTasks = dayTasks.slice(0, 2);
        const eventPills = displayedTasks.map(t => {
            const color = t.isCompleted ? '#10b981' : (t.priority === 'Urgent' ? '#ef4444' : '#6366f1');
            return `<div class="calendar-event-pill" style="background:${color}20; color:${color};" onclick="event.stopPropagation(); openTaskDetails(${t.id})">
                ${escapeHtml(t.title)}
            </div>`;
        }).join('');

        const moreBadge = dayTasks.length > 2
            ? `<div class="calendar-more-badge">+${dayTasks.length - 2} more</div>`
            : '';

        html += `
            <div class="calendar-cell ${isToday ? 'today' : ''}" onclick="openDayTasksModal('${currentDayStr}')" title="Click to view tasks for ${currentDayStr}">
                <div class="fw-bold mb-1 ${isToday ? 'text-primary' : 'text-muted'}">${d}</div>
                ${eventPills}
                ${moreBadge}
            </div>
        `;
    }

    grid.innerHTML = html;
}

function openDayTasksModal(dateStr) {
    selectedCalendarDayStr = dateStr;
    const dateObj = new Date(dateStr + 'T00:00:00');
    const dateFormatted = dateObj.toLocaleDateString(undefined, { weekday: 'long', year: 'numeric', month: 'long', day: 'numeric' });

    document.getElementById('modalDayTasksTitle').innerHTML = `<i class="bi bi-calendar-event me-2 text-primary"></i> ${escapeHtml(dateFormatted)}`;

    const dayTasks = cachedTasks.filter(t => t.dueDate && t.dueDate.startsWith(dateStr));
    const body = document.getElementById('modalDayTasksBody');

    if (dayTasks.length === 0) {
        body.innerHTML = `
            <div class="text-center text-muted py-4">
                <i class="bi bi-calendar-check fs-1 d-block mb-2 opacity-50"></i>
                <div class="fw-semibold">No tasks scheduled for this day</div>
                <div class="small">Click 'Add Task for this Day' below to plan work.</div>
            </div>
        `;
    } else {
        body.innerHTML = `
            <div class="list-group list-group-flush">
                ${dayTasks.map(t => `
                    <div class="list-group-item list-group-item-action d-flex justify-content-between align-items-center p-3 rounded mb-2 border shadow-sm" style="cursor: pointer;" onclick="openTaskFromModal(${t.id})">
                        <div>
                            <div class="d-flex align-items-center gap-2 mb-1">
                                <span class="badge badge-priority-${t.priority} rounded-pill">${t.priority}</span>
                                <span class="badge bg-secondary-subtle text-secondary rounded-pill">${t.status}</span>
                                ${t.isOverdue ? '<span class="badge bg-danger rounded-pill">Overdue</span>' : ''}
                            </div>
                            <h6 class="fw-bold mb-1">${escapeHtml(t.title)}</h6>
                            <div class="text-muted small">
                                ${t.assigneeFullName ? `<i class="bi bi-person me-1"></i>${escapeHtml(t.assigneeFullName)}` : '<i class="bi bi-person-dash me-1"></i>Unassigned'}
                                ${t.projectName ? ` • <i class="bi bi-folder me-1"></i>${escapeHtml(t.projectName)}` : ''}
                            </div>
                        </div>
                        <div>
                            <i class="bi bi-chevron-right text-muted"></i>
                        </div>
                    </div>
                `).join('')}
            </div>
        `;
    }

    new bootstrap.Modal(document.getElementById('modalDayTasks')).show();
}

function openTaskFromModal(taskId) {
    const modalEl = document.getElementById('modalDayTasks');
    if (modalEl) {
        const modal = bootstrap.Modal.getInstance(modalEl);
        if (modal) modal.hide();
    }
    openTaskDetails(taskId);
}

function changeCalendarMonth(delta) {
    calendarDate.setMonth(calendarDate.getMonth() + delta);
    renderCalendar();
}
