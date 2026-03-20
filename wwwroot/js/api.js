// ── api.js ────────────────────────────────────────────────────────────────────
// Base URL, auth token, and authorised fetch helper.
// Loaded first — all other modules depend on apiFetch.

const API   = 'https://localhost:7070';
const TOKEN = localStorage.getItem('auth_token');

// Auth guard — redirect to login if token is missing or malformed
if (!TOKEN || TOKEN === 'undefined' || !TOKEN.startsWith('eyJ')) {
    localStorage.removeItem('auth_token');
    window.location.replace('/login.html');
}

async function apiFetch(path, options = {}) {
    const res = await fetch(`${API}${path}`, {
        ...options,
        headers: { ...(options.headers ?? {}), 'Authorization': `Bearer ${TOKEN}` }
    });
    if (res.status === 401) {
        localStorage.removeItem('auth_token');
        window.location.replace('/login.html');
    }
    return res;
}