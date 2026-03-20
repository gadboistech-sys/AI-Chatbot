// Sentence boundary — split on . ! ? followed by whitespace or end of string
const SENTENCE_END_RE = /(?<=[.!?])\s+/;

// Minimum word count before treating accumulated text as a flushable sentence.
// Prevents very short fragments like "Oh." from getting their own TTS call.
const MIN_SENTENCE_WORDS = 5;

async function sendMessage(userText) {
    if (!userText.trim()) return;

    const sendBtn  = document.getElementById('send-btn');
    const messages = document.getElementById('messages');

    sendBtn.disabled = true;
    setStatus('Thinking…', 'thinking');
    addMessage('user', userText);
    State.history.push({ role: 'user', content: userText });

    const isFirstMessage     = State.isFirstMessage;
    State.isFirstMessage     = false;

    const voiceSignal        = State.pendingVoiceSignal;
    State.pendingVoiceSignal = null;
    let   openingLineSpoken  = null;

    try {
        // ── Opening line already spoken at connect time ───────────────────────
        // Inject into history so Sonnet sees it as prior context, and pass it
        // to /chat so the no-re-greet instruction fires correctly.
        if (isFirstMessage && State.openingLine) {
            openingLineSpoken = State.openingLine;
            State.history.unshift(
                { role: 'user',      content: '[Conversation resuming]' },
                { role: 'assistant', content: State.openingLine }
            );
        }

        // Streaming bubble
        const bubble = addMessage('ai', '', true);

        // ── Main response (streaming) ─────────────────────────────────────────
        const res = await apiFetch('/chat', {
            method: 'POST', headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                messages:         State.history,
                systemPrompt:     State.compiledPersonaPrompt ||
                                  document.getElementById('persona-input').value.trim() || null,
                avatarId:         document.getElementById('avatar-select').value || null,
                factualMemory:    State.sessionFactualMemory,
                relationalMemory: State.sessionRelationalMemory,
                voiceSignal,
                openingLine:      openingLineSpoken,
                displayName:      State.displayName
            })
        });
        if (!res.ok) throw new Error(`HTTP ${res.status}`);

        const reader   = res.body.getReader();
        const dec      = new TextDecoder();
        let full       = '';
        let buf        = '';
        let sentenced  = ''; // accumulates tokens until a sentence boundary

        function tryFlushSentence() {
            const parts = sentenced.split(SENTENCE_END_RE);
            if (parts.length < 2) return; // no complete sentence yet
            // All parts except the last are complete sentences
            for (let i = 0; i < parts.length - 1; i++) {
                const s = parts[i].trim();
                const words = s.split(/\s+/).filter(Boolean).length;
                if (words >= MIN_SENTENCE_WORDS) speaker?.enqueueSentence(s);
            }
            // Keep trailing fragment for next iteration
            sentenced = parts[parts.length - 1];
        }
        while (true) {
            const { done, value } = await reader.read();
            if (done) break;
            buf += dec.decode(value, { stream: true });
            const lines = buf.split('\n');
            buf = lines.pop() ?? '';
            for (const line of lines) {
                if (!line.startsWith('data: ')) continue;
                const payload = line.slice(6).trim();
                if (payload === '[DONE]') continue;
                try {
                    const parsed = JSON.parse(payload);
                    if (typeof parsed === 'string') {
                        full      += parsed;
                        sentenced += parsed;
                        bubble.childNodes[0].textContent = full;
                        messages.scrollTop = messages.scrollHeight;
                        tryFlushSentence();
                    }
                } catch { }
            }
        }

        // Start TTS fetch immediately when stream ends
        const ttsSpeakPromise = State.avatarReady && full.trim()
            ? avatarSpeak(full.trim())
            : null;

        bubble.querySelector('.cursor')?.remove();
        bubble.textContent = full;
        State.history.push({ role: 'assistant', content: full });

        if (speaker) {
            // Flush any remaining text that didn't end with punctuation
            const tail = sentenced.trim();
            if (tail) {
                const words = tail.split(/\s+/).filter(Boolean).length;
                // Always enqueue tail regardless of word count —
                // it's the end of the response, not a mid-stream fragment
                speaker.enqueueSentence(tail);
            }

            if (speaker.sentenceCount > 0) {
                // Sentence pipeline was used — finish the streamed speaker
                await speaker.finish();
            } else {
                // No sentences were enqueued (very short response) —
                // fall back to speaking the full response as one unit
                if (ttsSpeakPromise) await ttsSpeakPromise;
            }
        } else if (!State.avatarReady) {
            setStatus('Not connected', 'disconnected');
        }

    } catch (e) {
        document.querySelector('.msg.ai:last-child .bubble .cursor')?.remove();
        const lastBubble = document.querySelector('.msg.ai:last-child .bubble');
        if (lastBubble) {
            lastBubble.textContent = `⚠ ${e.message}`;
            lastBubble.style.color = '#e05580';
        }
        setStatus('Error', 'disconnected');
    }

    sendBtn.disabled   = false;
    messages.scrollTop = messages.scrollHeight;
}