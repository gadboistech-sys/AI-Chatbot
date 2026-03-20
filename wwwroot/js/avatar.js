// ── avatar.js ─────────────────────────────────────────────────────────────────
// LiveKit session lifecycle, WebSocket management, avatarSpeak, filler audio.
// SANDBOX_AVATAR_ID and SANDBOX_AVATAR_NAME are defined in state.js.
function setStatus(label, mode = 'ready') {
    document.getElementById('status-text').textContent = label;
    document.getElementById('status-dot').className =
        'dot ' + ({ ready:'', thinking:'thinking', speaking:'speaking', disconnected:'disconnected' }[mode] ?? '');
}

function showError(msg) {
    const el = document.getElementById('avatar-error');
    el.textContent   = msg;
    el.style.display = 'inline';
    setTimeout(() => { el.style.display = 'none'; }, 8000);
}

function signOut() {
    localStorage.removeItem('auth_token');
    window.location.replace('/login.html');
}

// ── Gender / voice helpers ────────────────────────────────────────────────────
const MALE_NAMES = new Set(['wayne','adam','brian','charlie','chris','daniel',
    'dave','drew','ethan','fin','george','harry','james','liam','marcus',
    'michael','oliver','patrick','ryan','sam','thomas','william']);

function inferGender(name) {
    return MALE_NAMES.has((name ?? '').toLowerCase().split(' ')[0]) ? 'male' : 'female';
}

function populateVoiceDropdown(gender) {
    const voiceSelect = document.getElementById('voice-select');
    const filtered    = State.allVoices.filter(v => !gender || v.gender === gender);
    voiceSelect.innerHTML = '';
    for (const v of filtered) {
        const o = document.createElement('option');
        o.value = v.voice_id; o.textContent = v.name;
        voiceSelect.appendChild(o);
    }
    const def = gender === 'male' ? State.defaultVoices.male : State.defaultVoices.female;
    if (def) voiceSelect.value = def;
}

function onAvatarChanged() {
    const avatarSelect = document.getElementById('avatar-select');
    const opt    = avatarSelect.options[avatarSelect.selectedIndex];
    const name   = opt?.textContent?.replace(' (sandbox only)', '').trim() || '—';
    const gender = opt?.dataset.gender || inferGender(name);
    document.getElementById('agent-name').textContent = name;
    document.title = `${name} — AI Companion`;
    populateVoiceDropdown(gender);
}

function preselectAvatar() {
    const avatarSelect = document.getElementById('avatar-select');
    const sandboxCheck = document.getElementById('sandbox-check');
    if (sandboxCheck.checked) avatarSelect.value = SANDBOX_AVATAR_ID;
}

// ── createStreamedSpeaker ─────────────────────────────────────────────────────
// Sends a multi-sentence response as one continuous avatar utterance.
// TTS fetches run concurrently per sentence; chunks are sent in strict order
// under a single event_id so LiveAvatar never resets between sentences.
//
// Usage:
//   const speaker = createStreamedSpeaker();
//   speaker.enqueueSentence('First sentence.');
//   speaker.enqueueSentence('Second sentence.');
//   await speaker.finish();  // sends speak_end, waits for speak_ended
function createStreamedSpeaker() {
    if (!State.avatarReady || !State.avatarWs ||
        State.avatarWs.readyState !== WebSocket.OPEN) {
        return {
            enqueueSentence: () => {},
            sentenceCount:   0,
            finish:          () => Promise.resolve()
        };
    }

    const overlay      = document.getElementById('video-overlay');
    const avatarSelect = document.getElementById('avatar-select');
    const voiceSelect  = document.getElementById('voice-select');
    const voiceId      = voiceSelect.value || null;
    const gender       = avatarSelect.options[avatarSelect.selectedIndex]?.dataset.gender || 'female';
    const eventId      = `evt_${++State.eventCounter}_${Date.now()}`;
    let   sentenceCount = 0;

    // sendQueue ensures chunks from different sentences are sent in order
    let sendQueue = Promise.resolve();

    function enqueueSentence(text) {
        const trimmed = text.trim();
        if (!trimmed) return;
        sentenceCount++;

        overlay.textContent   = trimmed;
        overlay.style.display = 'block';

        // Start TTS fetch immediately — runs concurrently with other fetches
        const fetchPromise = apiFetch('/tts', {
            method: 'POST', headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                text: trimmed, voiceId, gender,
                currentMood: State.currentMood
            })
        }).then(r => r.ok ? r.json() : Promise.reject(`TTS ${r.status}`))
          .then(data => data.chunks)
          .catch(err => { console.warn('Sentence TTS error:', err); return []; });

        // Chain onto sendQueue so chunks are always sent in sentence order
        sendQueue = sendQueue.then(async () => {
            const chunks = await fetchPromise;
            for (const chunk of chunks) {
                if (State.avatarWs?.readyState !== WebSocket.OPEN) break;
                State.avatarWs.send(JSON.stringify({
                    type: 'agent.speak', event_id: eventId, audio: chunk
                }));
            }
        });
    }

    async function finish() {
        // Wait for all chunks to be sent
        await sendQueue;

        if (State.avatarWs?.readyState === WebSocket.OPEN) {
            State.avatarWs.send(JSON.stringify({
                type: 'agent.speak_end', event_id: `${eventId}_end`
            }));
        }

        // Wait for LiveAvatar to confirm playback complete
        await new Promise(resolve => {
            const handler = e => {
                try {
                    const ev = JSON.parse(e.data);
                    if (ev.type === 'agent.speak_ended') {
                        State.avatarWs?.removeEventListener('message', handler);
                        overlay.style.display = 'none';
                        resolve();
                    }
                } catch { }
            };
            State.avatarWs?.addEventListener('message', handler);
            // Safety timeout — 60s covers even very long responses
            setTimeout(() => {
                State.avatarWs?.removeEventListener('message', handler);
                resolve();
            }, 60_000);
        });
    }

    return { enqueueSentence, get sentenceCount() { return sentenceCount; }, finish };
}
async function connectAvatar() {
    const connectBtn    = document.getElementById('connect-btn');
    const disconnectBtn = document.getElementById('disconnect-btn');
    const avatarSelect  = document.getElementById('avatar-select');
    const voiceSelect   = document.getElementById('voice-select');
    const sandboxCheck  = document.getElementById('sandbox-check');
    const placeholder   = document.getElementById('video-placeholder');
    const videoEl       = document.getElementById('avatar-video');

    connectBtn.disabled    = true;
    connectBtn.textContent = 'Connecting…';
    setStatus('Creating session…', 'thinking');

    const avatarId = avatarSelect.value;
    const [mem]    = await Promise.all([
        loadMemory(avatarId),
        loadPersona(avatarId)
    ]);
    State.sessionFactualMemory    = mem.factual;
    State.sessionRelationalMemory = mem.relational;

    try {
        const sessRes = await apiFetch('/avatar/session', {
            method: 'POST', headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ avatarId: avatarId || undefined, isSandbox: sandboxCheck.checked })
        });
        if (!sessRes.ok) throw new Error(`Session token failed: ${await sessRes.text()}`);
        State.sessionToken = (await sessRes.json()).data.session_token;

        setStatus('Starting session…', 'thinking');
        const startRes = await apiFetch('/avatar/start', {
            method: 'POST', headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ sessionToken: State.sessionToken })
        });
        if (!startRes.ok) throw new Error(`Session start failed: ${await startRes.text()}`);
        const { livekit_url, livekit_client_token, ws_url } = (await startRes.json()).data;

        setStatus('Joining room…', 'thinking');

        // Start opening line fetch immediately — runs concurrently with
        // LiveKit room join so the ~1-2s setup time is not wasted waiting
        const openingLineFetchPromise =
            (State.sessionFactualMemory || State.sessionRelationalMemory)
            ? apiFetch('/chat/opening', {
                method: 'POST', headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    messages:         [{ role: 'user', content: '[Session started]' }],
                    systemPrompt:     State.compiledPersonaPrompt ||
                                      document.getElementById('persona-input').value.trim() || null,
                    avatarId:         avatarId || null,
                    factualMemory:    State.sessionFactualMemory,
                    relationalMemory: State.sessionRelationalMemory,
                    displayName:      State.displayName
                })
            }).then(r => r.ok ? r.json() : null).catch(() => null)
            : Promise.resolve(null);
        State.livekitRoom = new LivekitClient.Room();
        State.livekitRoom.on(LivekitClient.RoomEvent.TrackSubscribed, (track) => {
            if (track.kind === LivekitClient.Track.Kind.Video) {
                const el = track.attach();
                el.style.cssText = 'display:block;';
                videoEl.replaceWith(el);
                placeholder.style.display = 'none';
                const poll = setInterval(() => {
                    if (!el.videoWidth) return;
                    clearInterval(poll);
                    el.width = el.videoWidth; el.height = el.videoHeight;
                }, 50);
            } else if (track.kind === LivekitClient.Track.Kind.Audio) {
                const el = track.attach();
                el.style.display = 'none';
                document.body.appendChild(el);
            }
        });
        State.livekitRoom.on(LivekitClient.RoomEvent.TrackUnsubscribed, (track) => {
            track.detach().forEach(el => el.remove());
        });

        await State.livekitRoom.connect(livekit_url, livekit_client_token);
        await State.livekitRoom.startAudio();

        State.avatarWs = new WebSocket(ws_url);
        await new Promise((res, rej) => {
            State.avatarWs.onopen  = res;
            State.avatarWs.onerror = rej;
            State.avatarWs.onclose = e => {
                console.warn(`WS closed — code:${e.code}, reason:${e.reason}`);
                if (State.avatarReady) setStatus('Disconnected', 'disconnected');
            };
        });

        State.avatarWs.addEventListener('message', e => {
            try {
                const ev = JSON.parse(e.data);
                if (ev.type === 'agent.speak_started') {
                    State.isSpeaking = true;
                    setStatus('Speaking…', 'speaking');
                } else if (ev.type === 'agent.speak_ended') {
                    State.isSpeaking = false;
                    document.getElementById('video-overlay').style.display = 'none';
                    setStatus('Connected', 'ready');
                }
            } catch { }
        });

        State.avatarReady    = true;
        State.isFirstMessage = true;
        State.openingLine    = null;

        apiFetch('/preferences', {
            method: 'POST', headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ avatarId })
        }).catch(e => console.warn('Pref save failed:', e));

        connectBtn.style.display    = 'none';
        disconnectBtn.style.display = 'inline-block';
        avatarSelect.disabled = voiceSelect.disabled = sandboxCheck.disabled = true;
        setStatus('Connected', 'ready');

        // ── Proactive opening line ─────────────────────────────────────────────
        // Fire automatically now that the session is ready, so the avatar speaks
        // first without waiting for the user's first message.
        if (State.sessionFactualMemory || State.sessionRelationalMemory) {
            try {
                const openingRes = await apiFetch('/chat/opening', {
                    method: 'POST', headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({
                        messages:         [{ role: 'user', content: '[Session started]' }],
                        systemPrompt:     State.compiledPersonaPrompt ||
                                          document.getElementById('persona-input').value.trim() || null,
                        avatarId:         avatarId || null,
                        factualMemory:    State.sessionFactualMemory,
                        relationalMemory: State.sessionRelationalMemory,
                        displayName:      State.displayName
                    })
                });
                if (openingRes.ok) {
                    const { openingLine } = await openingRes.json();
                    if (openingLine) {
                        State.openingLine = openingLine;
                        // Display in chat and speak — user sees/hears greeting
                        // before typing anything
                        addMessage('ai', openingLine);
                        await avatarSpeak(openingLine);
                    }
                }
            } catch (e) {
                console.warn('Opening line failed (non-fatal):', e);
            }
        }

    } catch (e) {
        console.error(e);
        showError(e.message);
        connectBtn.disabled    = false;
        connectBtn.textContent = 'Connect avatar';
        avatarSelect.disabled = voiceSelect.disabled = sandboxCheck.disabled = false;
        setStatus('Not connected', 'disconnected');
        await cleanupAvatar();
    }
}

// ── Disconnect ────────────────────────────────────────────────────────────────
async function disconnectAvatar() {
    const savingModal   = document.getElementById('saving-modal');
    const connectBtn    = document.getElementById('connect-btn');
    const disconnectBtn = document.getElementById('disconnect-btn');
    const avatarSelect  = document.getElementById('avatar-select');
    const voiceSelect   = document.getElementById('voice-select');
    const sandboxCheck  = document.getElementById('sandbox-check');
    const placeholder   = document.getElementById('video-placeholder');
    const overlay       = document.getElementById('video-overlay');

    if (State.history.length >= 2) savingModal.classList.add('visible');
    saveMemory()
        .catch(e => console.warn('Memory save failed:', e))
        .finally(() => savingModal.classList.remove('visible'));

    if (State.sessionToken) {
        await apiFetch('/avatar/stop', {
            method: 'POST', headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ sessionToken: State.sessionToken })
        }).catch(() => {});
    }

    await cleanupAvatar();
    placeholder.style.display   = 'flex';
    overlay.style.display       = 'none';
    connectBtn.style.display    = 'inline-block';
    connectBtn.disabled         = false;
    connectBtn.textContent      = 'Connect avatar';
    disconnectBtn.style.display = 'none';
    avatarSelect.disabled = voiceSelect.disabled = sandboxCheck.disabled = false;
    State.history                 = [];
    State.sessionFactualMemory    = null;
    State.sessionRelationalMemory = null;
    State.isFirstMessage          = true;
    State.openingLine             = null;
    setStatus('Not connected', 'disconnected');
    preselectAvatar();
}

async function cleanupAvatar() {
    State.avatarReady = false;
    State.isSpeaking  = false;
    State.avatarWs?.close(); State.avatarWs = null;
    await State.livekitRoom?.disconnect(); State.livekitRoom = null;
    State.sessionToken = null;
}

// ── Avatar speak ──────────────────────────────────────────────────────────────
// Returns a promise that resolves when agent.speak_ended fires,
// so callers can await sequential speech without overlap.
async function avatarSpeak(text) {
    if (!State.avatarReady || !State.avatarWs ||
        State.avatarWs.readyState !== WebSocket.OPEN) return;

    const overlay      = document.getElementById('video-overlay');
    const avatarSelect = document.getElementById('avatar-select');
    const voiceSelect  = document.getElementById('voice-select');

    overlay.textContent   = text;
    overlay.style.display = 'block';

    try {
        const voiceId = voiceSelect.value || null;
        const gender  = avatarSelect.options[avatarSelect.selectedIndex]?.dataset.gender || 'female';
        const ttsRes  = await apiFetch('/tts', {
            method: 'POST', headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ text, voiceId, gender, currentMood: State.currentMood })
        });
        if (!ttsRes.ok) throw new Error(`TTS failed: ${await ttsRes.text()}`);
        const { chunks } = await ttsRes.json();
        const eventId    = `evt_${++State.eventCounter}_${Date.now()}`;

        const speakDone = new Promise(resolve => {
            const handler = e => {
                try {
                    const ev = JSON.parse(e.data);
                    if (ev.type === 'agent.speak_ended') {
                        State.avatarWs?.removeEventListener('message', handler);
                        resolve();
                    }
                } catch { }
            };
            State.avatarWs.addEventListener('message', handler);
            setTimeout(() => {
                State.avatarWs?.removeEventListener('message', handler);
                resolve();
            }, 30_000);
        });

        for (const chunk of chunks) {
            if (State.avatarWs.readyState !== WebSocket.OPEN) break;
            State.avatarWs.send(JSON.stringify({ type: 'agent.speak', event_id: eventId, audio: chunk }));
        }
        State.avatarWs.send(JSON.stringify({ type: 'agent.speak_end', event_id: `${eventId}_end` }));

        await speakDone;
    } catch (e) {
        console.warn('avatarSpeak error:', e);
        overlay.style.display = 'none';
        setStatus('Connected', 'ready');
    }
}