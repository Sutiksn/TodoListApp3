/**
 * =============================================================================
 * TASKFLOW ENTERPRISE - API CLIENT (CORS-Ready)
 * =============================================================================
 */

async function apiCall(endpoint, method = 'GET', body = null) {
    const baseUrl = (window.TASKFLOW_CONFIG && window.TASKFLOW_CONFIG.API_BASE_URL) ? window.TASKFLOW_CONFIG.API_BASE_URL : '';
    const fullUrl = endpoint.startsWith('http') ? endpoint : `${baseUrl}${endpoint}`;

    const token = authToken || sessionStorage.getItem('taskflow_token');
    const headers = { 'Content-Type': 'application/json' };
    if (token) {
        headers['Authorization'] = `Bearer ${token}`;
    }

    const options = { method, headers };
    if (body) {
        options.body = JSON.stringify(body);
    }

    try {
        const response = await fetch(fullUrl, options);
        if (response.status === 401) {
            handleLogout();
            throw new Error('Session expired. Please sign in again.');
        }

        if (response.status === 204) {
            return null;
        }

        const text = await response.text();
        let data = null;
        if (text && text.trim().length > 0) {
            try {
                data = JSON.parse(text);
            } catch {
                data = { message: text };
            }
        }

        if (!response.ok) {
            const errorMsg = (data && data.message) ? data.message : `Request failed with status ${response.status}`;
            throw new Error(errorMsg);
        }
        return data;
    } catch (err) {
        if (err && err.message && !err.message.includes('Session expired')) {
            showToast(err.message, 'danger');
        }
        throw err;
    }
}
