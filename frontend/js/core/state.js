/**
 * =============================================================================
 * TASKFLOW ENTERPRISE - GLOBAL APPLICATION STATE
 * =============================================================================
 */

let currentUser = null;
let authToken = sessionStorage.getItem('taskflow_token') || null;
let activeMode = sessionStorage.getItem('taskflow_mode') || 'Personal';
let activeWorkspaceId = null;
let activeOrgId = null;
let currentTaskId = null;
let cachedTasks = [];
let cachedProjects = [];
let selectedProjectId = null;
let cachedUsers = [];
let cachedWorkspaces = [];
let calendarDate = new Date();
let searchDebounceTimer = null;
