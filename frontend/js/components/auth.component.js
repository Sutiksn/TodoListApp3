/**
 * =============================================================================
 * TASKFLOW ENTERPRISE - AUTHENTICATION & USER PROFILE COMPONENT
 * =============================================================================
 */

let cachedOrgsWithManagers = [];

function showAuthScreen() {
    const authSec = document.getElementById('authSection');
    const appShell = document.getElementById('appShell');
    if (authSec) authSec.classList.remove('d-none');
    if (appShell) appShell.classList.add('d-none');
    if (getNormalizedPath() !== '/login') {
        window.history.replaceState(null, '', '/login');
    }
    const formRegister = document.getElementById('formRegister');
    if (!formRegister || formRegister.classList.contains('d-none')) {
        switchAuthTab('login');
    }
}

function switchAuthTab(tab) {
    const authRow = document.getElementById('authRow');
    const heroContentLogin = document.getElementById('heroContentLogin');
    const heroContentRegister = document.getElementById('heroContentRegister');
    const heroTopHeader = document.getElementById('heroTopHeader');
    const formLogin = document.getElementById('formLogin');
    const formRegister = document.getElementById('formRegister');
    const fieldsScroll = document.querySelectorAll('.auth-fields-scrollable');

    if (fieldsScroll) {
        fieldsScroll.forEach(el => el.scrollTop = 0);
    }

    if (tab === 'login') {
        if (formRegister) formRegister.reset();
        resetRegisterFormToStandardUser();

        if (formRegister) formRegister.classList.add('d-none');
        if (formLogin) {
            formLogin.classList.remove('d-none');
            formLogin.classList.remove('auth-form-animated');
            void formLogin.offsetWidth;
            formLogin.classList.add('auth-form-animated');
        }

        if (heroTopHeader) heroTopHeader.classList.remove('d-none');
        if (heroContentRegister) heroContentRegister.classList.add('d-none');
        if (heroContentLogin) {
            heroContentLogin.classList.remove('d-none');
            heroContentLogin.classList.remove('hero-content-block');
            void heroContentLogin.offsetWidth;
            heroContentLogin.classList.add('hero-content-block');
        }

        if (authRow) {
            authRow.classList.remove('auth-mode-register');
            authRow.classList.add('auth-mode-login');
        }
    } else {
        if (formLogin) formLogin.reset();
        resetRegisterFormToStandardUser();

        if (formLogin) formLogin.classList.add('d-none');
        if (formRegister) {
            formRegister.classList.remove('d-none');
            formRegister.classList.remove('auth-form-animated');
            void formRegister.offsetWidth;
            formRegister.classList.add('auth-form-animated');
        }

        if (heroTopHeader) heroTopHeader.classList.add('d-none');
        if (heroContentLogin) heroContentLogin.classList.add('d-none');
        if (heroContentRegister) {
            heroContentRegister.classList.remove('d-none');
            heroContentRegister.classList.remove('hero-content-block');
            void heroContentRegister.offsetWidth;
            heroContentRegister.classList.add('hero-content-block');
        }

        if (authRow) {
            authRow.classList.remove('auth-mode-login');
            authRow.classList.add('auth-mode-register');
        }
    }
}

function resetRegisterFormToStandardUser() {
    const formRegister = document.getElementById('formRegister');
    if (formRegister) formRegister.reset();

    const regStepFields = document.getElementById('regStepFields');
    const regStepOtp = document.getElementById('regStepOtp');
    if (regStepFields) regStepFields.classList.remove('d-none');
    if (regStepOtp) regStepOtp.classList.add('d-none');

    const roleSelect = document.getElementById('regRole');
    if (roleSelect) roleSelect.value = 'User';
    const orgNameContainer = document.getElementById('regOrgNameContainer');
    if (orgNameContainer) orgNameContainer.classList.add('d-none');
    const orgContainer = document.getElementById('regOrgSelectorContainer');
    if (orgContainer) orgContainer.classList.add('d-none');
    const mgrContainer = document.getElementById('regManagerSelectorContainer');
    if (mgrContainer) mgrContainer.classList.add('d-none');
    const orgNameInput = document.getElementById('regOrgName');
    if (orgNameInput) orgNameInput.value = '';
    const orgSelect = document.getElementById('regOrganization');
    if (orgSelect) orgSelect.value = '';
    const mgrSelect = document.getElementById('regTargetManager');
    if (mgrSelect) mgrSelect.value = '';
    const displayEmail = document.getElementById('regDisplayEmail');
    if (displayEmail) displayEmail.textContent = '';
    const regOtp = document.getElementById('regOtp');
    if (regOtp) regOtp.value = '';
}

async function handleRegRoleChange() {
    const role = document.getElementById('regRole') ? document.getElementById('regRole').value : 'User';
    const orgNameContainer = document.getElementById('regOrgNameContainer');
    const orgContainer = document.getElementById('regOrgSelectorContainer');
    const mgrContainer = document.getElementById('regManagerSelectorContainer');

    if (role === 'User') {
        if (orgNameContainer) orgNameContainer.classList.add('d-none');
        if (orgContainer) orgContainer.classList.add('d-none');
        if (mgrContainer) mgrContainer.classList.add('d-none');
    } else if (role === 'OrgAdmin') {
        if (orgNameContainer) orgNameContainer.classList.remove('d-none');
        if (orgContainer) orgContainer.classList.add('d-none');
        if (mgrContainer) mgrContainer.classList.add('d-none');
        const orgNameInput = document.getElementById('regOrgName');
        if (orgNameInput) orgNameInput.focus();
    } else if (role === 'Manager') {
        if (orgNameContainer) orgNameContainer.classList.add('d-none');
        if (orgContainer) orgContainer.classList.remove('d-none');
        if (mgrContainer) mgrContainer.classList.add('d-none');
        await loadRegistrationOrgsAndManagers();
    } else if (role === 'Employee') {
        if (orgNameContainer) orgNameContainer.classList.add('d-none');
        if (orgContainer) orgContainer.classList.remove('d-none');
        if (mgrContainer) mgrContainer.classList.remove('d-none');
        await loadRegistrationOrgsAndManagers();
    }
}

async function loadRegistrationOrgsAndManagers() {
    try {
        cachedOrgsWithManagers = await apiCall('/api/access-requests/organizations-with-managers');
        const orgSelect = document.getElementById('regOrganization');
        if (orgSelect) {
            orgSelect.innerHTML = '<option value="">-- Choose Organization --</option>' +
                cachedOrgsWithManagers.map(o => `<option value="${o.id}">${escapeHtml(o.name)}</option>`).join('');
        }
        handleRegOrgChange();
    } catch {}
}

function handleRegOrgChange() {
    const orgSelect = document.getElementById('regOrganization');
    const mgrSelect = document.getElementById('regTargetManager');
    if (!orgSelect || !mgrSelect) return;

    const orgId = parseInt(orgSelect.value);
    const org = cachedOrgsWithManagers.find(o => o.id === orgId);

    if (org && org.managers && org.managers.length > 0) {
        mgrSelect.innerHTML = '<option value="">-- Choose Manager --</option>' +
            org.managers.map(m => `<option value="${m.id}">${escapeHtml(m.fullName)} (@${escapeHtml(m.username)})</option>`).join('');
    } else {
        mgrSelect.innerHTML = '<option value="">(No managers found in this org)</option>';
    }
}

function handleRegisterFormSubmit(e) {
    if (e) e.preventDefault();
    const regStepOtp = document.getElementById('regStepOtp');
    if (regStepOtp && !regStepOtp.classList.contains('d-none')) {
        handleConfirmRegistration();
    } else {
        handleRegisterStep1(e);
    }
}

async function handleRegisterStep1(e) {
    if (e) e.preventDefault();
    const fullName = document.getElementById('regFullName').value.trim();
    const username = document.getElementById('regUsername').value.trim();
    const email = document.getElementById('regEmail').value.trim();
    const password = document.getElementById('regPassword').value;
    const role = document.getElementById('regRole') ? document.getElementById('regRole').value : 'User';
    const orgNameInput = document.getElementById('regOrgName');
    const organizationName = (role === 'OrgAdmin' && orgNameInput) ? orgNameInput.value.trim() : null;

    if (username.length < 3) {
        showToast('Username must be at least 3 characters long.', 'warning');
        return;
    }
    if (!email || !email.includes('@')) {
        showToast('Please enter a valid email address.', 'warning');
        return;
    }
    if (password.length < 6) {
        showToast('Password must be at least 6 characters long.', 'warning');
        return;
    }
    if (role === 'OrgAdmin' && !organizationName) {
        showToast('Please enter your Organization / Company name.', 'warning');
        return;
    }

    const btn = document.getElementById('btnRegisterSubmit');
    if (btn) {
        btn.disabled = true;
        btn.innerHTML = '<span class="spinner-border spinner-border-sm me-1"></span> Sending Verification Code...';
    }

    try {
        const res = await apiCall('/api/auth/send-registration-otp', 'POST', { email, fullName });
        showToast(res.message || `Verification code sent to ${email}`, 'success');

        const regStepFields = document.getElementById('regStepFields');
        const regStepOtp = document.getElementById('regStepOtp');
        const regDisplayEmail = document.getElementById('regDisplayEmail');
        const regOtpInput = document.getElementById('regOtp');

        if (regDisplayEmail) regDisplayEmail.textContent = email;
        if (regOtpInput) regOtpInput.value = '';

        if (regStepFields) regStepFields.classList.add('d-none');
        if (regStepOtp) regStepOtp.classList.remove('d-none');
        if (regOtpInput) regOtpInput.focus();
    } catch {
    } finally {
        if (btn) {
            btn.disabled = false;
            btn.innerHTML = '<i class="bi bi-shield-check me-1"></i> Verify & Create Account';
        }
    }
}

function handleBackToRegFields() {
    const regStepFields = document.getElementById('regStepFields');
    const regStepOtp = document.getElementById('regStepOtp');
    if (regStepOtp) regStepOtp.classList.add('d-none');
    if (regStepFields) regStepFields.classList.remove('d-none');
    const fieldsScroll = document.querySelectorAll('.auth-fields-scrollable');
    if (fieldsScroll) {
        fieldsScroll.forEach(el => el.scrollTop = 0);
    }
    const emailInput = document.getElementById('regEmail');
    if (emailInput) {
        emailInput.focus();
        emailInput.select();
    }
}

async function handleResendRegOtp() {
    const email = document.getElementById('regEmail') ? document.getElementById('regEmail').value.trim() : '';
    const fullName = document.getElementById('regFullName') ? document.getElementById('regFullName').value.trim() : '';
    const btn = document.getElementById('btnResendRegOtp');

    if (!email) {
        showToast('No email found to resend OTP.', 'warning');
        handleBackToRegFields();
        return;
    }

    if (btn) {
        btn.disabled = true;
        btn.innerHTML = '<span class="spinner-border spinner-border-sm me-1"></span> Resending...';
    }

    try {
        const res = await apiCall('/api/auth/send-registration-otp', 'POST', { email, fullName });
        showToast(res.message || `New verification code sent to ${email}`, 'success');
        const otpInput = document.getElementById('regOtp');
        if (otpInput) {
            otpInput.value = '';
            otpInput.focus();
        }
    } catch {
    } finally {
        if (btn) {
            btn.disabled = false;
            btn.textContent = 'Resend Code';
        }
    }
}

async function handleConfirmRegistration() {
    const fullName = document.getElementById('regFullName').value.trim();
    const username = document.getElementById('regUsername').value.trim();
    const email = document.getElementById('regEmail').value.trim();
    const password = document.getElementById('regPassword').value;
    const role = document.getElementById('regRole') ? document.getElementById('regRole').value : 'User';
    const otp = document.getElementById('regOtp') ? document.getElementById('regOtp').value.trim() : '';

    if (!otp || otp.length < 6) {
        showToast('Please enter the 6-digit verification code.', 'warning');
        const otpInput = document.getElementById('regOtp');
        if (otpInput) otpInput.focus();
        return;
    }

    const orgNameInput = document.getElementById('regOrgName');
    const organizationName = (role === 'OrgAdmin' && orgNameInput) ? orgNameInput.value.trim() : null;
    const orgVal = document.getElementById('regOrganization') ? document.getElementById('regOrganization').value : '';
    const organizationId = orgVal ? parseInt(orgVal) : null;
    const mgrVal = document.getElementById('regTargetManager') ? document.getElementById('regTargetManager').value : '';
    const targetManagerUserId = mgrVal ? parseInt(mgrVal) : null;

    const btn = document.getElementById('btnConfirmRegister');
    if (btn) {
        btn.disabled = true;
        btn.innerHTML = '<span class="spinner-border spinner-border-sm me-1"></span> Completing Registration...';
    }

    try {
        const res = await apiCall('/api/auth/register', 'POST', {
            fullName,
            username,
            email,
            password,
            department: 'General',
            otp,
            role,
            organizationName,
            organizationId,
            targetManagerUserId
        });

        resetRegisterFormToStandardUser();
        showToast(res.message || 'Account created successfully! Please sign in with your credentials.', 'success');
        switchAuthTab('login');

        const loginUserInput = document.getElementById('loginUsername');
        if (loginUserInput) {
            loginUserInput.value = username || email;
            const passInput = document.getElementById('loginPassword');
            if (passInput) passInput.focus();
        }
    } catch {
    } finally {
        if (btn) {
            btn.disabled = false;
            btn.innerHTML = '<i class="bi bi-person-check me-1"></i> Confirm & Finish Registration';
        }
    }
}

async function handleLogin(e) {
    e.preventDefault();
    const btn = document.getElementById('btnLoginSubmit');
    btn.disabled = true;
    btn.innerHTML = '<span class="spinner-border spinner-border-sm me-1"></span> Signing in...';

    const usernameOrEmail = document.getElementById('loginUsername').value.trim();
    const password = document.getElementById('loginPassword').value;

    try {
        const res = await apiCall('/api/auth/login', 'POST', { usernameOrEmail, password, selectedMode: 'Personal' });
        authToken = res.token;
        currentUser = res.user;

        const isAdmin = currentUser.role === 'PlatformAdmin';
        activeMode = isAdmin ? 'Admin' : 'Personal';

        sessionStorage.setItem('taskflow_token', authToken);
        sessionStorage.setItem('taskflow_mode', activeMode);

        const formLogin = document.getElementById('formLogin');
        if (formLogin) formLogin.reset();

        const welcomeRole = isAdmin ? 'Administrator' : currentUser.role;
        showToast(`Welcome ${welcomeRole}, ${currentUser.fullName}!`, 'success');
        
        const targetPath = isAdmin ? '/admin' : '/personal';
        navigateTo(targetPath, true);
        await initApp();
    } catch {
        const passInput = document.getElementById('loginPassword');
        if (passInput) passInput.value = '';
    } finally {
        btn.disabled = false;
        btn.innerHTML = '<i class="bi bi-box-arrow-in-right me-1"></i> Sign In to Platform';
    }
}

function handleLogout() {
    authToken = null;
    currentUser = null;
    sessionStorage.removeItem('taskflow_token');
    sessionStorage.removeItem('taskflow_mode');
    showAuthScreen();
}

async function fetchCurrentUser() {
    try {
        const currentMode = activeMode || sessionStorage.getItem('taskflow_mode') || 'Personal';
        const user = await apiCall(`/api/auth/me?activeMode=${encodeURIComponent(currentMode)}`);
        currentUser = user;
        if (user && user.role === 'PlatformAdmin') {
            activeMode = 'Admin';
            sessionStorage.setItem('taskflow_mode', 'Admin');
        }
        renderUserInfo();
        return user;
    } catch {
        handleLogout();
        return null;
    }
}

function renderUserInfo() {
    if (!currentUser) return;
    const roleLabel = currentUser.role === 'OrgAdmin' ? 'Organization Admin' : currentUser.role;
    const userDisplayName = document.getElementById('userDisplayName');
    const userAvatar = document.getElementById('userAvatar');
    const menuUserFullName = document.getElementById('menuUserFullName');
    const menuUserRole = document.getElementById('menuUserRole');

    if (userDisplayName) userDisplayName.textContent = currentUser.fullName.split(' ')[0];
    if (userAvatar) userAvatar.textContent = currentUser.fullName.charAt(0).toUpperCase();
    if (menuUserFullName) menuUserFullName.textContent = currentUser.fullName;
    if (menuUserRole) menuUserRole.textContent = `${roleLabel} • ${currentUser.department || 'General'}`;

    const mobAvatar = document.getElementById('mobileUserAvatar');
    if (mobAvatar) mobAvatar.textContent = currentUser.fullName.charAt(0).toUpperCase();
    const mobFullName = document.getElementById('mobileUserFullName');
    if (mobFullName) mobFullName.textContent = currentUser.fullName;
    const mobRole = document.getElementById('mobileUserRole');
    if (mobRole) mobRole.textContent = `${roleLabel} • ${currentUser.department || 'General'}`;

    const adminBtn = document.getElementById('navAdminBtn');
    const mobileAdminBtn = document.getElementById('mobileNavAdminBtn');
    if (currentUser.role === 'PlatformAdmin') {
        if (adminBtn) adminBtn.classList.remove('d-none');
        if (mobileAdminBtn) mobileAdminBtn.classList.remove('d-none');
    } else {
        if (adminBtn) adminBtn.classList.add('d-none');
        if (mobileAdminBtn) mobileAdminBtn.classList.add('d-none');
    }

    updateModeLockStates();
}

function openProfileModal() {
    navigateTo('/profile');
}

async function showProfilePage(pushState = true) {
    if (!currentUser) return;

    if (pushState && window.location.pathname.toLowerCase() !== '/profile') {
        window.history.pushState(null, '', '/profile');
    }

    document.querySelectorAll('.mode-btn').forEach(btn => {
        btn.classList.remove('active');
    });

    document.querySelectorAll('.mode-view').forEach(v => {
        v.classList.add('d-none');
        v.classList.remove('fade-in-motion');
    });

    const profileView = document.getElementById('viewProfile');
    if (profileView) {
        profileView.classList.remove('d-none');
        void profileView.offsetWidth;
        profileView.classList.add('fade-in-motion');
    }

    const banner = document.getElementById('modeBanner');
    if (banner) {
        banner.className = 'mode-banner mode-personal fade-in-motion';
    }

    const badge = document.getElementById('modeBadge');
    const title = document.getElementById('modeTitle');
    const subtext = document.getElementById('modeSubtext');
    const toolbar = document.getElementById('modeToolbar');

    if (badge) badge.textContent = 'PROFILE SETTINGS';
    if (title) title.textContent = 'My User Profile & Security Settings';
    if (subtext) subtext.textContent = 'Manage your personal identity, contact details, role permissions, and password security';

    const returnMode = currentUser.role === 'PlatformAdmin' ? 'Admin' : (activeMode && activeMode !== 'Admin' ? activeMode : 'Personal');
    if (toolbar) {
        toolbar.innerHTML = `
            <button class="btn btn-light btn-sm fw-semibold shadow-sm" onclick="navigateTo('/${returnMode.toLowerCase()}')">
                <i class="bi bi-arrow-left me-1"></i> Back to Dashboard
            </button>
        `;
    }

    const avatarEl = document.getElementById('profileCardAvatar');
    if (avatarEl) avatarEl.textContent = currentUser.fullName.charAt(0).toUpperCase();

    const nameEl = document.getElementById('profileCardFullName');
    if (nameEl) nameEl.textContent = currentUser.fullName;

    const usernameEl = document.getElementById('profileCardUsername');
    if (usernameEl) usernameEl.textContent = `@${currentUser.username || currentUser.email.split('@')[0]}`;

    const roleBadge = document.getElementById('profileCardRole');
    if (roleBadge) roleBadge.textContent = currentUser.role === 'OrgAdmin' ? 'Organization Admin' : currentUser.role;

    const deptBadge = document.getElementById('profileCardDept');
    if (deptBadge) deptBadge.textContent = currentUser.department || 'General';

    const emailEl = document.getElementById('profileCardEmail');
    if (emailEl) {
        emailEl.textContent = currentUser.email;
        emailEl.title = currentUser.email;
    }

    const joinedEl = document.getElementById('profileCardJoined');
    if (joinedEl) {
        if (currentUser.createdAt) {
            joinedEl.textContent = new Date(currentUser.createdAt).toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' });
        } else {
            joinedEl.textContent = 'Active Account';
        }
    }

    const activeEl = document.getElementById('profileCardLastActive');
    if (activeEl) {
        activeEl.textContent = new Date().toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' });
    }

    const roleDescEl = document.getElementById('profileRoleDescription');
    if (roleDescEl) {
        if (currentUser.role === 'PlatformAdmin') {
            roleDescEl.textContent = 'You are the Platform Administrator with unrestricted authority to manage organizations, system users, role allocations, and security audit telemetry.';
        } else if (currentUser.role === 'OrgAdmin') {
            roleDescEl.textContent = 'You are an Organization Administrator with access to manage organization workspaces, department structures, and employee memberships.';
        } else if (currentUser.role === 'Manager') {
            roleDescEl.textContent = 'You are a Team Manager with permissions to delegate tasks, oversee team workspaces, review access requests, and drive sprint velocity.';
        } else if (currentUser.role === 'Employee') {
            roleDescEl.textContent = 'You are an Organization Employee with collaboration access across assigned team workspaces, project milestones, and delivery tracks.';
        } else {
            roleDescEl.textContent = 'You are a Standard User with access to your Personal Focus board. To collaborate with teams, submit a role request in the Requests panel.';
        }
    }

    const activeWsEl = document.getElementById('profileActiveWorkspace');
    if (activeWsEl) {
        const currentWs = cachedWorkspaces ? cachedWorkspaces.find(w => w.id === activeWorkspaceId) : null;
        activeWsEl.textContent = currentWs ? currentWs.name : 'Personal Workspace';
    }

    const wsCountEl = document.getElementById('profileWorkspaceCount');
    if (wsCountEl) {
        let availableCount = cachedWorkspaces ? cachedWorkspaces.length : 1;
        if (currentUser.role === 'PlatformAdmin' && cachedWorkspaces) {
            availableCount = cachedWorkspaces.filter(w => w.mode === 'Personal' || w.mode === 'Business').length;
        }
        wsCountEl.textContent = `${availableCount} Workspace${availableCount === 1 ? '' : 's'}`;
    }

    const formFullName = document.getElementById('profileFullName');
    if (formFullName) formFullName.value = currentUser.fullName || '';

    const formUsername = document.getElementById('profileUsername');
    if (formUsername) formUsername.value = currentUser.username || '';

    const formEmail = document.getElementById('profileEmail');
    if (formEmail) formEmail.value = currentUser.email || '';

    const formDept = document.getElementById('profileDept');
    if (formDept) formDept.value = currentUser.department || '';
}

async function handleUpdateProfile(e) {
    e.preventDefault();
    const btn = document.getElementById('btnSaveProfile');
    if (btn) {
        btn.disabled = true;
        btn.innerHTML = '<span class="spinner-border spinner-border-sm me-1"></span> Saving...';
    }

    const userField = document.getElementById('profileUsername');
    const username = userField ? userField.value.trim() : currentUser.username;
    const fullName = document.getElementById('profileFullName').value.trim();
    const email = document.getElementById('profileEmail').value.trim();
    const department = document.getElementById('profileDept').value.trim();

    try {
        const res = await apiCall('/api/auth/profile', 'PUT', { username, email, fullName, department });
        if (res.token) {
            authToken = res.token;
            sessionStorage.setItem('taskflow_token', res.token);
        }
        currentUser = res.user;
        renderUserInfo();
        showToast('Profile updated successfully!', 'success');
        if (getNormalizedPath() === '/profile') {
            await showProfilePage(false);
        }
    } catch {}
    finally {
        if (btn) {
            btn.disabled = false;
            btn.innerHTML = '<i class="bi bi-check2-circle me-1"></i> Save Profile Changes';
        }
    }
}

async function handleChangePassword(e) {
    e.preventDefault();
    const btn = document.getElementById('btnUpdatePass');
    const currentPassword = document.getElementById('passCurrent').value;
    const newPassword = document.getElementById('passNew').value;
    const confirmPassEl = document.getElementById('passConfirm');
    const confirmPassword = confirmPassEl ? confirmPassEl.value : newPassword;

    if (newPassword !== confirmPassword) {
        showToast('New password and confirmation do not match.', 'warning');
        return;
    }

    if (btn) {
        btn.disabled = true;
        btn.innerHTML = '<span class="spinner-border spinner-border-sm me-1"></span> Updating...';
    }

    try {
        const res = await apiCall('/api/auth/change-password', 'POST', { currentPassword, newPassword });
        showToast(res.message || 'Password changed successfully!', 'success');
        document.getElementById('formChangePass').reset();
    } catch {}
    finally {
        if (btn) {
            btn.disabled = false;
            btn.innerHTML = '<i class="bi bi-shield-check me-1"></i> Update Password';
        }
    }
}

async function handleDeleteMyAccount() {
    if (!currentUser) return;

    if (currentUser.role === 'PlatformAdmin') {
        showToast('Platform Administrator account cannot be deleted.', 'warning');
        return;
    }

    const confirmed = confirm('Are you sure you want to delete your account? This will permanently remove your account and workspace data.');
    if (!confirmed) return;

    try {
        const res = await apiCall('/api/auth/delete-account', 'DELETE');
        showToast(res.message || 'Your account has been deleted.', 'info');
        handleLogout();
    } catch {}
}

function openForgotPasswordModal() {
    const s1 = document.getElementById('otpStep1');
    const s2 = document.getElementById('otpStep2');
    if (s1) s1.classList.remove('d-none');
    if (s2) s2.classList.add('d-none');
    const form1 = document.getElementById('formForgotStep1');
    const form2 = document.getElementById('formForgotStep2');
    if (form1) form1.reset();
    if (form2) form2.reset();
    new bootstrap.Modal(document.getElementById('modalForgotPassword')).show();
}

function handleBackToForgotStep1() {
    const s1 = document.getElementById('otpStep1');
    const s2 = document.getElementById('otpStep2');
    if (s2) s2.classList.add('d-none');
    if (s1) s1.classList.remove('d-none');
    const input = document.getElementById('otpInputUserOrEmail');
    if (input) {
        input.focus();
        input.select();
    }
}

async function handleResendForgotOtp() {
    const input = document.getElementById('otpInputUserOrEmail') ? document.getElementById('otpInputUserOrEmail').value.trim() : '';
    const btn = document.getElementById('btnResendForgotOtp');

    if (!input) {
        showToast('Please enter your username or email first.', 'warning');
        handleBackToForgotStep1();
        return;
    }

    if (btn) {
        btn.disabled = true;
        btn.innerHTML = '<span class="spinner-border spinner-border-sm me-1"></span> Resending...';
    }

    try {
        const res = await apiCall('/api/auth/forgot-password', 'POST', { emailOrUsername: input });
        showToast(res.message || 'New verification code sent.', 'success');
        const codeInput = document.getElementById('otpInputCode');
        if (codeInput) {
            codeInput.value = '';
            codeInput.focus();
        }
    } catch {}
    finally {
        if (btn) {
            btn.disabled = false;
            btn.textContent = 'Resend Code';
        }
    }
}

async function handleSendOtp(e) {
    if (e) e.preventDefault();
    const btn = document.getElementById('btnSendOtpSubmit');
    if (btn) {
        btn.disabled = true;
        btn.innerHTML = '<span class="spinner-border spinner-border-sm me-1"></span> Sending OTP...';
    }

    const input = document.getElementById('otpInputUserOrEmail').value.trim();

    try {
        const res = await apiCall('/api/auth/forgot-password', 'POST', { emailOrUsername: input });
        const targetEl = document.getElementById('otpDisplayTarget');
        if (targetEl) targetEl.textContent = input;
        const s1 = document.getElementById('otpStep1');
        const s2 = document.getElementById('otpStep2');
        if (s1) s1.classList.add('d-none');
        if (s2) s2.classList.remove('d-none');
        const codeInput = document.getElementById('otpInputCode');
        if (codeInput) {
            codeInput.value = '';
            codeInput.focus();
        }
        showToast(res.message || 'Verification code sent.', 'success');
    } catch {}
    finally {
        if (btn) {
            btn.disabled = false;
            btn.innerHTML = '<i class="bi bi-send me-1"></i> Send 6-Digit OTP';
        }
    }
}

async function handleVerifyOtpAndReset(e) {
    if (e) e.preventDefault();
    const emailOrUsername = document.getElementById('otpInputUserOrEmail').value.trim();
    const otp = document.getElementById('otpInputCode').value.trim();
    const newPassword = document.getElementById('otpInputNewPass').value;
    const confirmPassword = document.getElementById('otpInputConfirmNewPass') ? document.getElementById('otpInputConfirmNewPass').value : newPassword;

    if (!otp || otp.length < 6) {
        showToast('Please enter the 6-digit verification code.', 'warning');
        const codeInput = document.getElementById('otpInputCode');
        if (codeInput) codeInput.focus();
        return;
    }

    if (newPassword.length < 6) {
        showToast('New password must be at least 6 characters long.', 'warning');
        return;
    }

    if (newPassword !== confirmPassword) {
        showToast('New passwords do not match. Please re-type.', 'warning');
        return;
    }

    const btn = document.getElementById('btnVerifyOtpSubmit');
    if (btn) {
        btn.disabled = true;
        btn.innerHTML = '<span class="spinner-border spinner-border-sm me-1"></span> Verifying...';
    }

    try {
        const res = await apiCall('/api/auth/reset-password', 'POST', { emailOrUsername, otp, newPassword });
        showToast(res.message || 'Password reset successfully! Please log in.', 'success');
        const modalEl = document.getElementById('modalForgotPassword');
        if (modalEl) {
            const modal = bootstrap.Modal.getInstance(modalEl);
            if (modal) modal.hide();
        }
        switchAuthTab('login');
        const loginUserInput = document.getElementById('loginUsername');
        if (loginUserInput) {
            loginUserInput.value = emailOrUsername;
            const passInput = document.getElementById('loginPassword');
            if (passInput) passInput.focus();
        }
    } catch {
        const codeInput = document.getElementById('otpInputCode');
        if (codeInput) {
            codeInput.value = '';
            codeInput.focus();
        }
    } finally {
        if (btn) {
            btn.disabled = false;
            btn.innerHTML = '<i class="bi bi-check2-shield me-1"></i> Verify & Reset Password';
        }
    }
}
