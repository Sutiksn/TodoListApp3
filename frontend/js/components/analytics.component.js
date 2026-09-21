/**
 * =============================================================================
 * TASKFLOW ENTERPRISE - ANALYTICS & METRICS COMPONENT
 * =============================================================================
 */

async function loadPersonalMode() {
    await loadPersonalStats();
    await loadTasks();
}

async function loadPersonalStats() {
    try {
        const stats = await apiCall('/api/analytics/personal');
        const container = document.getElementById('personalStatsRow');
        if (!container) return;

        container.innerHTML = `
            <div class="col-12 col-sm-6 col-xl-3">
                <div class="stats-card">
                    <div class="d-flex justify-content-between align-items-center">
                        <div>
                            <div class="text-muted small fw-semibold">Total Tasks</div>
                            <h3 class="fw-bold mb-0 mt-1">${stats.totalTasks}</h3>
                        </div>
                        <div class="stats-icon-wrapper bg-primary bg-opacity-10 text-primary"><i class="bi bi-list-task"></i></div>
                    </div>
                </div>
            </div>
            <div class="col-12 col-sm-6 col-xl-3">
                <div class="stats-card">
                    <div class="d-flex justify-content-between align-items-center">
                        <div>
                            <div class="text-muted small fw-semibold">Completion Rate</div>
                            <h3 class="fw-bold mb-0 mt-1">${stats.completionRatePercentage}%</h3>
                        </div>
                        <div class="stats-icon-wrapper bg-success bg-opacity-10 text-success"><i class="bi bi-check-circle"></i></div>
                    </div>
                </div>
            </div>
            <div class="col-12 col-sm-6 col-xl-3">
                <div class="stats-card">
                    <div class="d-flex justify-content-between align-items-center">
                        <div>
                            <div class="text-muted small fw-semibold">In Progress / Review</div>
                            <h3 class="fw-bold mb-0 mt-1">${stats.inProgressCount + stats.reviewCount}</h3>
                        </div>
                        <div class="stats-icon-wrapper bg-warning bg-opacity-10 text-warning"><i class="bi bi-lightning-charge"></i></div>
                    </div>
                </div>
            </div>
            <div class="col-12 col-sm-6 col-xl-3">
                <div class="stats-card">
                    <div class="d-flex justify-content-between align-items-center">
                        <div>
                            <div class="text-muted small fw-semibold">Hours Tracked</div>
                            <h3 class="fw-bold mb-0 mt-1">${stats.totalHoursSpent.toFixed(1)} hrs</h3>
                        </div>
                        <div class="stats-icon-wrapper bg-info bg-opacity-10 text-info"><i class="bi bi-stopwatch"></i></div>
                    </div>
                </div>
            </div>
        `;
    } catch {}
}

function switchPersonalSubView(subview) {
    const btnKanban = document.getElementById('btnPersonalKanban');
    const btnCalendar = document.getElementById('btnPersonalCalendar');
    const btnAnalytics = document.getElementById('btnPersonalAnalytics');

    if (btnKanban) btnKanban.classList.toggle('active', subview === 'kanban');
    if (btnCalendar) btnCalendar.classList.toggle('active', subview === 'calendar');
    if (btnAnalytics) btnAnalytics.classList.toggle('active', subview === 'analytics');

    const subKanban = document.getElementById('subviewKanban');
    const subCal = document.getElementById('subviewCalendar');
    const subAna = document.getElementById('subviewAnalytics');

    if (subKanban) subKanban.classList.toggle('d-none', subview !== 'kanban');
    if (subCal) subCal.classList.toggle('d-none', subview !== 'calendar');
    if (subAna) subAna.classList.toggle('d-none', subview !== 'analytics');

    if (subview === 'calendar') {
        renderCalendar();
    } else if (subview === 'analytics') {
        renderPersonalAnalyticsView();
    }
}

async function renderPersonalAnalyticsView() {
    try {
        const stats = await apiCall('/api/analytics/personal');
        const container = document.getElementById('personalAnalyticsContainer');
        if (!container) return;

        container.innerHTML = `
            <div class="col-md-6">
                <div class="p-3 border rounded" style="background:var(--bg-card);">
                    <h6 class="fw-bold mb-3"><i class="bi bi-kanban me-2 text-primary"></i> Stage Distribution</h6>
                    <div class="d-flex justify-content-between mb-2 small"><span>Plan</span><strong>${stats.planCount}</strong></div>
                    <div class="d-flex justify-content-between mb-2 small"><span>To Do</span><strong>${stats.toDoCount}</strong></div>
                    <div class="d-flex justify-content-between mb-2 small"><span>In Progress</span><strong>${stats.inProgressCount}</strong></div>
                    <div class="d-flex justify-content-between mb-2 small"><span>Review</span><strong>${stats.reviewCount}</strong></div>
                    <div class="d-flex justify-content-between mb-2 small text-success"><span>Completed</span><strong>${stats.completedCount}</strong></div>
                </div>
            </div>
            <div class="col-md-6">
                <div class="p-3 border rounded" style="background:var(--bg-card);">
                    <h6 class="fw-bold mb-3"><i class="bi bi-bullseye me-2 text-success"></i> Strategic Goals Progress</h6>
                    <div class="d-flex justify-content-between mb-2 small"><span>Total Goals</span><strong>${stats.totalGoals}</strong></div>
                    <div class="d-flex justify-content-between mb-2 small text-success"><span>Achieved</span><strong>${stats.achievedGoals}</strong></div>
                    <div class="d-flex justify-content-between mb-2 small"><span>Estimated Hours</span><strong>${stats.totalHoursEstimated} hrs</strong></div>
                    <div class="d-flex justify-content-between mb-2 small"><span>Actual Hours Spent</span><strong>${stats.totalHoursSpent} hrs</strong></div>
                </div>
            </div>
        `;
    } catch {}
}
