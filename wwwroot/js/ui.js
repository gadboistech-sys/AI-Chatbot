// ── ui.js ─────────────────────────────────────────────────────────────────────
// Shared UI helpers used by both avatar.js and chat.js.
// Loaded before avatar.js so both modules can call addMessage.

function addMessage(role, text, streaming = false) {
    document.getElementById('empty-state')?.remove();
    const wrap   = document.createElement('div');
    const bubble = document.createElement('div');
    const av     = document.createElement('div');
    wrap.className   = `msg ${role}`;
    bubble.className = 'bubble';
    av.className     = 'msg-av';
    av.textContent   = role === 'user' ? '👤' : '✦';
    if (streaming) {
        const cur = document.createElement('span');
        cur.className = 'cursor';
        bubble.appendChild(document.createTextNode(''));
        bubble.appendChild(cur);
    } else {
        bubble.textContent = text;
    }
    wrap.appendChild(av);
    wrap.appendChild(bubble);
    document.getElementById('messages').appendChild(wrap);
    document.getElementById('messages').scrollTop =
        document.getElementById('messages').scrollHeight;
    return bubble;
}