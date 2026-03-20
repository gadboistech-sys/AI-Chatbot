// ── memory.js ─────────────────────────────────────────────────────────────────
// Persistent memory load / save.

async function loadMemory(avatarId) {
    try {
        const res  = await apiFetch(`/memory?avatarId=${encodeURIComponent(avatarId)}`);
        const data = await res.json();
        return { factual: data.factual ?? null, relational: data.relational ?? null };
    } catch {
        return { factual: null, relational: null };
    }
}

async function saveMemory() {
    if (State.history.length < 2) return;
    const avatarSelect = document.getElementById('avatar-select');
    const opt      = avatarSelect.options[avatarSelect.selectedIndex];
    const name     = opt?.textContent?.replace(' (sandbox only)', '').trim() || 'the avatar';
    const avatarId = avatarSelect.value;
    const res  = await apiFetch('/memory', {
        method:  'POST',
        headers: { 'Content-Type': 'application/json' },
        body:    JSON.stringify({ messages: State.history, avatarName: name, avatarId })
    });
    const data = await res.json();
    State.sessionFactualMemory    = data.factual    ?? State.sessionFactualMemory;
    State.sessionRelationalMemory = data.relational ?? State.sessionRelationalMemory;
    console.log('Memory saved:', data);
}