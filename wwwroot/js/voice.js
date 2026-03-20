// ── voice.js ──────────────────────────────────────────────────────────────────
// Speech recognition, Web Audio energy + pause measurement, voice signal assembly.
// Initialised by initVoice() called from app.js after DOM is ready.

function initVoice() {
    const micBtn    = document.getElementById('mic-btn');
    const inputEl   = document.getElementById('user-input');
    const sendBtn   = document.getElementById('send-btn');
    const SR        = window.SpeechRecognition || window.webkitSpeechRecognition;

    if (!SR) { micBtn.disabled = true; return; }

    const recog      = new SR();
    recog.lang           = 'en-US';
    recog.interimResults = true;
    recog.continuous     = true;

    let final        = '';
    let silenceTimer = null;
    let isRecording  = false;
    let wordCount    = 0;
    let recordStart  = null;
    const SILENCE_MS = 1500;

    // ── Web Audio capture ─────────────────────────────────────────────────────
    async function startAudioCapture(stream) {
        try {
            State.audioContext  = new (window.AudioContext || window.webkitAudioContext)();
            State.analyserNode  = State.audioContext.createAnalyser();
            State.analyserNode.fftSize = 256;
            State.audioContext.createMediaStreamSource(stream).connect(State.analyserNode);
            State.energySamples = [];
            State.pauseCount    = 0;

            const buf        = new Float32Array(State.analyserNode.fftSize);
            const PAUSE_RMS  = 0.02;
            let inSilence    = false;
            let silenceStart = null;

            const iv = setInterval(() => {
                if (!isRecording) { clearInterval(iv); return; }
                State.analyserNode.getFloatTimeDomainData(buf);
                const rms = Math.sqrt(buf.reduce((s, v) => s + v * v, 0) / buf.length);
                State.energySamples.push(rms);
                if (rms < PAUSE_RMS) {
                    if (!inSilence) { inSilence = true; silenceStart = Date.now(); }
                    else if (Date.now() - silenceStart > PAUSE_GAP_MS) {
                        State.pauseCount++;
                        silenceStart = Date.now();
                    }
                } else { inSilence = false; }
            }, 50);
        } catch (e) {
            console.warn('Web Audio failed (non-fatal):', e);
        }
    }

    function stopAudioCapture() {
        try {
            State.audioContext?.close();
            State.audioContext = null;
            State.analyserNode = null;
        } catch { }
    }

    // ── Voice signal assembly ─────────────────────────────────────────────────
    function buildVoiceSignal() {
        const durSec = recordStart ? (Date.now() - recordStart) / 1000 : null;
        const wps    = (durSec && wordCount > 0) ? wordCount / durSec : null;
        const pace   = wps == null ? null
            : wps > 2.8 ? 'fast'
            : wps < 1.4 ? 'slow'
            : 'normal';
        const avg = State.energySamples.length > 0
            ? State.energySamples.reduce((a, b) => a + b, 0) / State.energySamples.length
            : null;
        if (pace == null && avg == null) return null;
        return { pace, energy: avg, pauseCount: State.pauseCount, source: 'browser' };
    }

    // ── Recording lifecycle ───────────────────────────────────────────────────
    async function startRecording() {
        if (isRecording) return;
        isRecording = true;
        final = ''; wordCount = 0; State.pauseCount = 0;
        recordStart = Date.now(); State.energySamples = [];
        inputEl.value = '';
        try {
            const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
            await startAudioCapture(stream);
        } catch (e) { console.warn('Mic energy capture failed:', e); }
        recog.start();
        micBtn.classList.add('active');
        if (State.isSpeaking && State.avatarReady &&
            State.avatarWs?.readyState === WebSocket.OPEN) {
            State.avatarWs.send(JSON.stringify({ type: 'agent.interrupt' }));
            State.isSpeaking = false;
        }
    }

    function stopRecording() {
        if (!isRecording) return;
        isRecording = false;
        recog.stop();
        stopAudioCapture();
        micBtn.classList.remove('active');
        clearTimeout(silenceTimer);
        State.pendingVoiceSignal = buildVoiceSignal();
        if (final.trim()) { inputEl.value = final.trim(); sendBtn.click(); }
    }

    function resetSilenceTimer() {
        clearTimeout(silenceTimer);
        silenceTimer = setTimeout(() => stopRecording(), SILENCE_MS);
    }

    // ── Speech recognition events ─────────────────────────────────────────────
    recog.onresult = e => {
        let interim = '';
        for (let i = e.resultIndex; i < e.results.length; i++) {
            const t = e.results[i][0].transcript;
            if (e.results[i].isFinal) {
                final    += t + ' ';
                wordCount += t.trim().split(/\s+/).length;
            } else { interim = t; }
        }
        inputEl.value = (final + interim).trim();
        resetSilenceTimer();
    };
    recog.onend   = () => {
        if (isRecording) { try { recog.start(); } catch { stopRecording(); } }
        else micBtn.classList.remove('active');
    };
    recog.onerror = e => { if (e.error !== 'no-speech') stopRecording(); };

    // ── Mic button events ─────────────────────────────────────────────────────
    micBtn.addEventListener('mousedown',  ()  => startRecording());
    micBtn.addEventListener('mouseup',    ()  => resetSilenceTimer());
    micBtn.addEventListener('touchstart', ev  => { ev.preventDefault(); startRecording(); });
    micBtn.addEventListener('touchend',   ev  => { ev.preventDefault(); resetSilenceTimer(); });
}