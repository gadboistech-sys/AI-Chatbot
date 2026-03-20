// ── state.js ──────────────────────────────────────────────────────────────────
// Shared mutable state for the application.
// All modules read and write State.x directly.
// Loaded immediately after api.js.

const SANDBOX_AVATAR_ID   = 'dd73ea75-1218-4ef3-92ce-606d5f7fbc0a';
const SANDBOX_AVATAR_NAME = 'Wayne (sandbox only)';

const State = {
    // ── Conversation ──────────────────────────────────────────────────────────
    history: [],

    // ── Avatar / LiveKit session ───────────────────────────────────────────────
    sessionToken:  null,
    avatarWs:      null,
    livekitRoom:   null,
    avatarReady:   false,
    isSpeaking:    false,
    eventCounter:  0,

    // ── Voice / TTS ───────────────────────────────────────────────────────────
    allVoices:     [],
    defaultVoices: { male: '', female: '' },
    currentMood:   'neutral',

    // ── Memory ────────────────────────────────────────────────────────────────
    sessionFactualMemory:    null,
    sessionRelationalMemory: null,

    // ── Persona ───────────────────────────────────────────────────────────────
    compiledPersonaPrompt: null,

    // ── Session flags ─────────────────────────────────────────────────────────
    isFirstMessage: true,    // true until first message of a session is sent
    openingLine:    null,    // opening line spoken at connect time, passed to /chat
    // Keyed by mood label → array of chunk arrays (pre-fetched PCM).
    // Populated at connect time by preCacheFillerClips() in avatar.js.
    fillerClips: {},         // { [mood]: string[][] }
    fillerPlaying: false,    // true while a filler clip is in flight
    pendingVoiceSignal: null,
    audioContext:       null,
    analyserNode:       null,
    energySamples:      [],
    pauseCount:         0,
};

// Pause gap used by the Web Audio analyser (ms)
const PAUSE_GAP_MS = 500;