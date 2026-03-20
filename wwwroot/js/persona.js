// ── persona.js ────────────────────────────────────────────────────────────────
// Structured persona editor: load, save, field helpers, tab switching.

function switchPersonaTab(tab) {
    document.getElementById('persona-structured').classList.toggle('active', tab === 'structured');
    document.getElementById('persona-advanced').classList.toggle('active',   tab === 'advanced');
    document.querySelectorAll('.persona-tab').forEach((b, i) =>
        b.classList.toggle('active', (i === 0) === (tab === 'structured')));
}

function splitComma(str) {
    return str.split(',').map(s => s.trim()).filter(Boolean);
}

function getPersonaDefinition() {
    const avatarSelect = document.getElementById('avatar-select');
    const name = avatarSelect.options[avatarSelect.selectedIndex]
        ?.textContent?.replace(' (sandbox only)', '').trim() || 'the avatar';
    return {
        name,
        coreTraits:       splitComma(document.getElementById('p-traits').value),
        verbalStyle:      splitComma(document.getElementById('p-verbal').value),
        expressiveness:   parseInt(document.getElementById('p-expressiveness').value),
        seriousness:      parseInt(document.getElementById('p-seriousness').value),
        backstoryAnchors: document.getElementById('p-backstory').value
            .split('\n').map(s => s.trim()).filter(Boolean),
        careExpression:   document.getElementById('p-care').value.trim()   || null,
        stressResponse:   document.getElementById('p-stress').value.trim() || null,
        additionalNotes:  document.getElementById('p-notes').value.trim()  || null,
    };
}

function populateStructuredFields(def) {
    if (!def) return;
    document.getElementById('p-traits').value         = (def.coreTraits      ?? []).join(', ');
    document.getElementById('p-verbal').value         = (def.verbalStyle      ?? []).join(', ');
    document.getElementById('p-expressiveness').value = def.expressiveness    ?? 50;
    document.getElementById('p-seriousness').value    = def.seriousness       ?? 40;
    document.getElementById('p-backstory').value      = (def.backstoryAnchors ?? []).join('\n');
    document.getElementById('p-care').value           = def.careExpression    ?? '';
    document.getElementById('p-stress').value         = def.stressResponse    ?? '';
    document.getElementById('p-notes').value          = def.additionalNotes   ?? '';
}

function resetPersonaFields() {
    ['p-traits','p-verbal','p-backstory','p-care','p-stress','p-notes']
        .forEach(id => { document.getElementById(id).value = ''; });
    document.getElementById('p-expressiveness').value = 50;
    document.getElementById('p-seriousness').value    = 40;
    document.getElementById('persona-input').value    = '';
    State.compiledPersonaPrompt = null;
}

function resetPersona() { resetPersonaFields(); }

async function loadPersona(avatarId) {
    try {
        const res  = await apiFetch(`/persona?avatarId=${encodeURIComponent(avatarId)}`);
        const data = await res.json();
        if (data.definition) {
            populateStructuredFields(data.definition);
            document.getElementById('persona-input').value = data.compiled ?? '';
            State.compiledPersonaPrompt = data.compiled ?? null;
        } else {
            resetPersonaFields();
        }
    } catch (e) {
        console.warn('Persona load failed:', e);
    }
}

async function savePersona() {
    const avatarSelect = document.getElementById('avatar-select');
    const avatarId     = avatarSelect.value;
    const definition   = getPersonaDefinition();
    try {
        const res  = await apiFetch('/persona', {
            method:  'POST',
            headers: { 'Content-Type': 'application/json' },
            body:    JSON.stringify({ avatarId, definition })
        });
        const data = await res.json();
        State.compiledPersonaPrompt                    = data.compiled ?? null;
        document.getElementById('persona-input').value = data.compiled ?? '';
        const btn  = document.querySelector('.persona-actions button.primary');
        const orig = btn.textContent;
        btn.textContent = 'Saved ✓';
        setTimeout(() => { btn.textContent = orig; }, 1500);
    } catch (e) {
        console.warn('Persona save failed:', e);
    }
}