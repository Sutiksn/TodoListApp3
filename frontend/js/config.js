/**
 * =============================================================================
 * TASKFLOW ENTERPRISE - CLIENT CONFIGURATION & CORS SETUP
 * =============================================================================
 * Configures the API Base URL for backend communication.
 * When the frontend is hosted standalone (e.g. port 3000, 5500, 8080, etc.),
 * requests are routed via CORS to http://localhost:5000.
 * When served directly by the backend API on port 5000, relative paths are used.
 */
(function () {
    const isLocalhost = window.location.hostname === 'localhost' || window.location.hostname === '127.0.0.1';
    const isLocalSeparateDev = isLocalhost && window.location.port !== '' && window.location.port !== '5000';
    
    window.TASKFLOW_CONFIG = {
        API_BASE_URL: window.TASKFLOW_API_OVERRIDE || (isLocalSeparateDev ? 'http://localhost:5000' : ''),
        ENVIRONMENT: isLocalhost ? 'development' : 'production'
    };

    console.log('[TaskFlow] Configuration initialized:', window.TASKFLOW_CONFIG);
})();
