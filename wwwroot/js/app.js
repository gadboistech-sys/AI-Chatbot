// ── app.js ────────────────────────────────────────────────────────────────────
// Composition root — event listeners, page-load init.
// Loaded last so all other modules are already defined.

// ── Avatar selection events ───────────────────────────────────────────────────
document.getElementById('avatar-select').addEventListener('change', () => {
    onAvatarChanged();
    loadPersona(document.getElementById('avatar-select').value);
});
document.getElementById('sandbox-check').addEventListener('change', preselectAvatar);
document.getElementById('connect-btn').addEventListener('click', connectAvatar);
document.getElementById('disconnect-btn').addEventListener('click', disconnectAvatar);

// ── Chat input events ─────────────────────────────────────────────────────────
const inputEl  = document.getElementById('user-input');
const sendBtn  = document.getElementById('send-btn');

sendBtn.addEventListener('click', () => {
    sendMessage(inputEl.value);
    inputEl.value      = '';
    inputEl.style.height = '';
});
inputEl.addEventListener('keydown', e => {
    if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); sendBtn.click(); }
});
inputEl.addEventListener('input', () => {
    inputEl.style.height = 'auto';
    inputEl.style.height = Math.min(inputEl.scrollHeight, 140) + 'px';
});

// ── Voice recognition init ────────────────────────────────────────────────────
initVoice();

// ── Page load: fetch avatars, voices, preferences ─────────────────────────────
(async () => {
    try {
        const [avatarRes, voiceRes, defaultRes, prefRes, profileRes] = await Promise.all([
            apiFetch('/avatar/list'),
            apiFetch('/voices'),
            apiFetch('/voices/defaults'),
            apiFetch('/preferences'),
            apiFetch('/profile')
        ]);
        if (!avatarRes.ok) throw new Error(await avatarRes.text());
        if (!voiceRes.ok)  throw new Error(await voiceRes.text());

        const avatarData  = await avatarRes.json();
        const voiceData   = await voiceRes.json();
        const defaultData = await defaultRes.json();
        const prefData    = await prefRes.json();
        const profileData = await profileRes.json();

        // Sync display name from server into State and localStorage
        if (profileData.displayName) {
            State.displayName = profileData.displayName;
            localStorage.setItem('display_name', profileData.displayName);
        }

        State.allVoices     = voiceData.voices ?? [];
        State.defaultVoices = defaultData;

        const avatarSelect = document.getElementById('avatar-select');
        avatarSelect.innerHTML = '';

        const wayneOpt = document.createElement('option');
        wayneOpt.value          = SANDBOX_AVATAR_ID;
        wayneOpt.textContent    = SANDBOX_AVATAR_NAME;
        wayneOpt.dataset.gender = 'male';
        avatarSelect.appendChild(wayneOpt);

        for (const av of (avatarData.data?.results ?? [])) {
            const o = document.createElement('option');
            o.value          = av.id;
            o.textContent    = av.name ?? av.id;
            o.dataset.gender = inferGender(av.name ?? '');
            avatarSelect.appendChild(o);
        }

        if (prefData.avatarId) avatarSelect.value = prefData.avatarId;
        else preselectAvatar();

        onAvatarChanged();
        await loadPersona(avatarSelect.value);
        document.getElementById('connect-btn').disabled = false;

    } catch (e) {
        document.getElementById('avatar-select').innerHTML =
            '<option value="">Failed to load</option>';
        showError('Could not load data: ' + e.message);
    }
})();