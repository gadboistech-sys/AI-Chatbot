// ── login.js ──────────────────────────────────────────────────────────────────
// Handles registration and sign-in, including display name collection.

const API  = 'https://localhost:7070';
let   mode = 'login';

// Redirect if already authenticated
const existingToken = localStorage.getItem('auth_token');
if (existingToken && existingToken !== 'undefined' && existingToken.startsWith('eyJ')) {
    window.location.replace('/index.html');
}

function togglePassword() {
    const input = document.getElementById('password');
    const btn   = document.getElementById('toggle-pw');
    const isText = input.type === 'text';
    input.type    = isText ? 'password' : 'text';
    btn.textContent = isText ? '👁' : '🙈';
}

function switchTab(newMode) {
    mode = newMode;
    document.getElementById('tab-login')   .classList.toggle('active', mode === 'login');
    document.getElementById('tab-register').classList.toggle('active', mode === 'register');
    document.getElementById('submit-btn').textContent = mode === 'login' ? 'Sign in' : 'Create account';
    document.getElementById('error-msg').classList.remove('visible');

    // Show display name + password toggle only on register tab
    document.getElementById('display-name-row').style.display = mode === 'register' ? 'block' : 'none';

    const pw        = document.getElementById('password');
    const toggleBtn = document.getElementById('toggle-pw');
    pw.type               = 'password';
    toggleBtn.textContent = '👁';
    toggleBtn.style.display = mode === 'register' ? 'block' : 'none';
}

function showError(msg) {
    const el = document.getElementById('error-msg');
    el.textContent = msg;
    el.classList.add('visible');
}

async function handleSubmit() {
    const email       = document.getElementById('email').value.trim();
    const password    = document.getElementById('password').value;
    const displayName = document.getElementById('display-name').value.trim();
    const btn         = document.getElementById('submit-btn');

    if (!email || !password) { showError('Please enter your email and password.'); return; }

    btn.disabled    = true;
    btn.textContent = mode === 'login' ? 'Signing in…' : 'Creating account…';
    document.getElementById('error-msg').classList.remove('visible');

    try {
        const body = mode === 'register'
            ? { email, password, displayName: displayName || null }
            : { email, password };

        const res = await fetch(`${API}/auth/${mode}`, {
            method:  'POST',
            headers: { 'Content-Type': 'application/json' },
            body:    JSON.stringify(body)
        });

        if (res.status === 401) {
            showError(mode === 'login'
                ? 'Incorrect email or password.'
                : 'Registration failed. Please try again.');
            return;
        }
        if (!res.ok) {
            const data = await res.json().catch(() => ({}));
            showError(Array.isArray(data) ? data.join(' ') : (data.title ?? 'Something went wrong.'));
            return;
        }

        const data = await res.json();
        localStorage.setItem('auth_token', data.token);
        if (data.displayName) localStorage.setItem('display_name', data.displayName);
        window.location.replace('/index.html');

    } catch (e) {
        showError('Could not reach the server. Is it running?');
    } finally {
        btn.disabled    = false;
        btn.textContent = mode === 'login' ? 'Sign in' : 'Create account';
    }
}

// Allow Enter key to submit
document.addEventListener('keydown', e => {
    if (e.key === 'Enter') handleSubmit();
});