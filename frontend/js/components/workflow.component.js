/**
 * =============================================================================
 * TASKFLOW ENTERPRISE - WORKFLOW & APPROVALS COMPONENT
 * =============================================================================
 */

async function decideApproval(approvalId, decision) {
    try {
        await apiCall(`/api/workflow/approvals/${approvalId}/decide`, 'POST', {
            status: decision,
            comments: `Decision made via Organization Governance Mode.`
        });
        showToast(`Approval request ${decision.toLowerCase()}!`, 'success');
        if (activeMode === 'Business') {
            await loadBusinessMode();
        }
    } catch {}
}

async function handleAdminClearAuditLogs() {
    if (!confirm("Are you sure you want to permanently clear all global security audit logs?")) {
        return;
    }

    try {
        const res = await apiCall('/api/admin/activity-logs/clear', 'DELETE');
        showToast(res.message || 'Audit logs cleared successfully.', 'success');
        if (activeMode === 'Admin') {
            await loadAdminMode();
        }
    } catch {}
}
