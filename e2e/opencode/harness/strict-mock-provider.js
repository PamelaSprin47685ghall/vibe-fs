/**
 * strict-mock-provider.js — Strict mock LLM server for OpenCode E2E.
 *
 * Rules:
 * - No expectation queued → HTTP 500 + record UnexpectedLlmRequest
 * - Expectations are FIFO only; no auto-reordering
 * - Each expectation supports rich matching
 * - Supports fault injection
 *
 * Usage:
 *   const provider = new StrictMockProvider();
 *   const url = await provider.start();
 *   provider.expectToolCall({ id: 't1', tool: 'write', args: { filePath: 'x.txt' } });
 *   provider.expectText({ id: 'final', text: 'done' });
 *   provider.expectSatisfied();
 *   await provider.stop();
 */

import http from 'node:http';

// ─── Response helpers ────────────────────────────────────────────────────────

function sendJSON(res, status, body) {
  res.writeHead(status, { 'Content-Type': 'application/json' });
  res.end(JSON.stringify(body));
}

function sendSSE(res, chunks) {
  res.writeHead(200, {
    'Content-Type': 'text/event-stream',
    'Cache-Control': 'no-cache',
    'Connection': 'keep-alive',
    'X-Accel-Buffering': 'no',
  });
  for (const chunk of chunks) {
    res.write(`data: ${JSON.stringify(chunk)}\n\n`);
  }
  res.write('data: [DONE]\n\n');
  res.end();
}

function buildToolCallChunks(id, name, argsStr, promptTokens) {
  return [
    { id, object: 'chat.completion.chunk', created: 1, model: 'mock', choices: [{ index: 0, delta: { role: 'assistant', content: null }, finish_reason: null }] },
    { id, object: 'chat.completion.chunk', created: 1, model: 'mock', choices: [{ index: 0, delta: { tool_calls: [{ index: 0, id, type: 'function', function: { name, arguments: argsStr } }] }, finish_reason: null }] },
    { id, object: 'chat.completion.chunk', created: 1, model: 'mock', choices: [{ index: 0, delta: {}, finish_reason: 'tool_calls' }], usage: { prompt_tokens: promptTokens, completion_tokens: 100, total_tokens: promptTokens + 100 } },
  ];
}

function buildTextChunks(id, text, promptTokens) {
  return [
    { id, object: 'chat.completion.chunk', created: 1, model: 'mock', choices: [{ index: 0, delta: { role: 'assistant', content: text }, finish_reason: null }] },
    { id, object: 'chat.completion.chunk', created: 1, model: 'mock', choices: [{ index: 0, delta: {}, finish_reason: 'stop' }], usage: { prompt_tokens: promptTokens, completion_tokens: 100, total_tokens: promptTokens + 100 } },
  ];
}

export function extractToolNames(body) {
  const tools = body?.tools;
  if (Array.isArray(tools)) {
    return tools.flatMap(t => {
      const name = t?.function?.name ?? t?.name;
      return typeof name === 'string' ? [name] : [];
    });
  }
  return [];
}

export function extractLastUserMsg(body) {
  const msgs = body?.messages || [];
  const last = [...msgs].reverse().find(m => m?.role === 'user');
  if (!last) return null;
  const c = last.content;
  if (typeof c === 'string') return c.slice(0, 300);
  if (Array.isArray(c)) return JSON.stringify(c[0]).slice(0, 300);
  return null;
}

function estimatePromptTokens(body) {
  return Math.max(1, Math.ceil(JSON.stringify(body?.messages || []).length / 2));
}

// ─── Nudge detection ─────────────────────────────────────────────────────────
// Matches the three nudge prose markers from src/Kernel/Nudge/NudgePromptText.fs
// against the LAST user-role message in the request body.  Scope is intentionally
// narrow: only the most-recent message, not body-wide, so already-history nudge
// turns don't fire on every subsequent request in the same session.

const NUDGE_MARKERS = [
  // todoNudgePromptProse
  'There are still incomplete todos. Continue working through the remaining items.',
  // loopNudgePromptProse (unique fragment)
  'You are in loop mode. You must call the submit_review tool',
  // runnerNudgePrompt
  'A background runner task is still active',
  // with-review command prompt (Runtime/ReviewPrompts/Commands.fs)
  // OC-REV-001 fires /loop which injects this; it's a plugin command, not model traffic.
  'command: with-review',
  // context-budget nudge (PlanHelpers.fs:contextBudgetNudgeText)
  // Injected by checkAndInjectNudge when token budget is near-exhausted.
  'the system context is about to be suspended',
  'You must immediately force an emergency stop to all work',
];

function isSyntheticContinuation(body) {
  // Mirrors src/Hosts/OpenCode/Fallback/MessageInspection.fs:isSyntheticText
  const msgs = body?.messages || [];
  if (msgs.length === 0) return false;
  const lastMsg = msgs[msgs.length - 1];
  if (lastMsg?.role !== 'user') return false;
  const content = lastMsg.content;

// ZWSP continuation (ContinuationHost.fs: continuationPayload = "\u200B")
  // NOTE: \u200B is Unicode Cf (format char), NOT White_Space — trim() does NOT remove it.
  if (typeof content === 'string' && content === '\u200B') return true;
  if (typeof content === 'string' && content.replace(/[\u200B\u200C\u200D\uFEFF]/g, '').trim() === '') return true;

  // Nudge markers (todo / loop / runner)
  if (typeof content === 'string') {
    return NUDGE_MARKERS.some(m => content.includes(m));
  }
  if (Array.isArray(content)) {
    return content.some(p => {
      if (typeof p?.text !== 'string') return false;
      const t = p.text;
      if (t === '\u200B') return true;
      if (t.replace(/[\u200B\u200C\u200D\uFEFF]/g, '').trim() === '') return true;
      return NUDGE_MARKERS.some(m => t.includes(m));
    });
  }
  return false;
}

function detectSyntheticMarker(body) {
  // Returns which synthetic marker matched the last user message.
  // Called only after isSyntheticContinuation returned true.
  const msgs = body?.messages || [];
  if (msgs.length === 0) return 'unknown';
  const lastMsg = msgs[msgs.length - 1];
  if (lastMsg?.role !== 'user') return 'unknown';
  const content = lastMsg.content;

  function matchMarker(text) {
    if (text === '\u200B' || text.replace(/[\u200B\u200C\u200D\uFEFF]/g, '').trim() === '') return 'zwsp';
    if (text.includes('There are still incomplete todos')) return 'todo-nudge';
    if (text.includes('You are in loop mode. You must call the submit_review tool')) return 'loop-nudge';
    if (text.includes('A background runner task is still active')) return 'runner-nudge';
    if (text.includes('command: with-review')) return 'with-review';
    if (text.includes('the system context is about to be suspended')) return 'budget-nudge';
    if (text.includes('You must immediately force an emergency stop to all work')) return 'budget-nudge';
    return null;
  }

  if (typeof content === 'string') {
    const m = matchMarker(content);
    if (m) return m;
  }
  if (Array.isArray(content)) {
    for (const p of content) {
      if (typeof p?.text === 'string') {
        const m = matchMarker(p.text);
        if (m) return m;
      }
    }
  }
  return 'unknown';
}

const TITLE_GENERATION_MARKER = 'Generate a title for this conversation:';

function isTitleGenerationRequest(body) {
  const messages = body?.messages || [];
  return messages.some((msg) => {
    const content = msg?.content;
    if (typeof content === 'string') return content.includes(TITLE_GENERATION_MARKER);
    if (Array.isArray(content)) {
      return content.some((p) => typeof p?.text === 'string' && p.text.includes(TITLE_GENERATION_MARKER));
    }
    return false;
  });
}

// ─── Matcher ─────────────────────────────────────────────────────────────────

function matchesExpectation(body, expectation) {
  const match = expectation.match || {};

  if (match.sessionId) {
    const headerSession = body?.sessionId || '';
    if (headerSession !== match.sessionId) return false;
  }

  if (match.model) {
    const modelId = body?.model || '';
    if (modelId !== match.model) return false;
  }

  if (match.requiredTools && match.requiredTools.length > 0) {
    const tools = extractToolNames(body);
    for (const required of match.requiredTools) {
      if (!tools.includes(required)) return false;
    }
  }

  if (match.forbiddenTools && match.forbiddenTools.length > 0) {
    const tools = extractToolNames(body);
    for (const forbidden of match.forbiddenTools) {
      if (tools.includes(forbidden)) return false;
    }
  }

  if (match.containsText && match.containsText.length > 0) {
    const bodyStr = JSON.stringify(body);
    for (const text of match.containsText) {
      if (!bodyStr.includes(text)) return false;
    }
  }

  if (match.messageCount !== undefined) {
    const messages = body?.messages || [];
    if (messages.length !== match.messageCount) return false;
  }

  return true;
}

// ─── Main class ──────────────────────────────────────────────────────────────

export class StrictMockProvider {
  constructor() {
    this._expectations = [];
    this._unexpected = [];
    this._requests = [];
    this._syntheticRequests = [];  // bypassed synthetic continuations (ZWSP + nudges)
    this._nudgeBypassed = 0;   // nudge-continuation requests auto-replied before FIFO
    this._server = null;
    this._port = null;
    this._url = null;
    this._idCounter = 0;
  }

  // ── Expectation builders ──

  expectToolCall(opts) {
    this._expectations.push({
      id: opts.id || `exp-${++this._idCounter}`,
      match: opts.match || {},
      respond: {
        type: 'tool-call',
        tool: opts.tool,
        args: opts.args || {},
      },
    });
  }

  expectText(opts) {
    this._expectations.push({
      id: opts.id || `exp-${++this._idCounter}`,
      match: opts.match || {},
      respond: {
        type: 'text',
        text: opts.text || 'ok',
      },
    });
  }

  expectError(opts) {
    this._expectations.push({
      id: opts.id || `exp-${++this._idCounter}`,
      match: opts.match || {},
      respond: {
        type: 'error',
        status: opts.status || 500,
        body: opts.body || { error: 'mock error' },
      },
    });
  }

  expectDisconnect(opts = {}) {
    this._expectations.push({
      id: opts.id || `exp-${++this._idCounter}`,
      match: opts.match || {},
      respond: { type: 'disconnect' },
    });
  }

  // ── Assertions ──

  expectSatisfied() {
    const remaining = this._expectations.length;
    const unexpected = this._unexpected.length;
    const errors = [];

    if (remaining > 0) {
      const detail = this._expectations.slice(0, 5).map(e =>
        `  [${e.id}] respond=${e.respond.type} match=${JSON.stringify(e.match)}`
      ).join('\n');
      errors.push(`${remaining} unmatched expectation(s):\n${detail}`);
    }

    if (unexpected > 0) {
      const detail = this._unexpected.slice(0, 5).map(u =>
        `  session=${u.sessId || '?'} tools=${JSON.stringify(extractToolNames(u.body))} msgs=${u.body?.messages?.length || 0} toolResults=${u.hasToolResults || false} lastUser=${extractLastUserMsg(u.body) || '(none)'}`
      ).join('\n');
      errors.push(`${unexpected} unexpected LLM request(s):\n${detail}`);
    }

    if (errors.length > 0) {
      throw new Error(`Mock provider assertions failed:\n${errors.join('\n')}`);
    }
  }

  reset() {
    this._expectations = [];
    this._unexpected = [];
    this._requests = [];
    this._syntheticRequests = [];
  }

  get requests() { return this._requests; }

  // ── Server lifecycle ──

  async start() {
    if (this._server) return this._url;

    return new Promise((resolve, reject) => {
      this._server = http.createServer((req, res) => this._handleRequest(req, res));
      this._server.listen(0, '127.0.0.1', () => {
        const addr = this._server.address();
        this._port = addr.port;
        this._url = `http://127.0.0.1:${this._port}`;
        resolve(this._url);
      });
      this._server.on('error', reject);
    });
  }

  async stop() {
    if (!this._server) return;
    return new Promise((resolve) => {
      this._server.close(() => {
        this._server = null;
        this._port = null;
        this._url = null;
        resolve();
      });
    });
  }

  get url() { return this._url; }
  get port() { return this._port; }
  get unexpectedRequests() { return this._unexpected; }
  get remainingExpectations() { return this._expectations.length; }
  get nudgeBypassed() { return this._nudgeBypassed; }
  get syntheticRequests() { return this._syntheticRequests; }

  // ── Request handler ──

  _handleRequest(req, res) {
    const url = new URL(req.url, `http://${req.headers.host}`);

    if (url.pathname === '/api/web_search' && req.method === 'POST') {
      return this._handleWebSearch(req, res);
    }
    if (url.pathname === '/api/web_fetch' && req.method === 'POST') {
      return this._handleWebFetch(req, res);
    }
    if ((url.pathname === '/v1/chat/completions' || url.pathname === '/v1/responses') && req.method === 'POST') {
      return this._handleChat(req, res);
    }

    sendJSON(res, 404, { error: 'not found' });
  }

  _handleChat(req, res) {
    let rawBody = '';
    req.on('data', (chunk) => (rawBody += chunk));
    req.on('end', () => {
      let parsed = {};
      try { parsed = JSON.parse(rawBody); } catch { /* keep {} */ }

      // Auto-bypass title generation requests
      if (isTitleGenerationRequest(parsed)) {
        const id = `title_${Date.now()}`;
        return sendSSE(res, buildTextChunks(id, 'E2E Test Session', 1));
      }

      // Pre-FIFO bypass: synthetic continuations from the plugin's fallback/nudge machinery.
      // Covers: ZWSP continuations (ContinuationHost), todo/loop/runner nudges (NudgeTrigger).
      // Auto-reply 'done' so the turn terminates cleanly without re-nudging.
      // TODO: future nudge-behavior specs need an opt-out flag (e.g. allowSyntheticContinuations).
      if (isSyntheticContinuation(parsed)) {
        const marker = detectSyntheticMarker(parsed);
        this._nudgeBypassed++;
        this._syntheticRequests.push({ body: parsed, marker, time: Date.now() });
        const id = `synth_${Date.now()}`;
        console.error(`[MOCK-SYNTH] session=${parsed?.sessionId || '?'} marker=${marker} #${this._nudgeBypassed}`);
        return sendSSE(res, buildTextChunks(id, 'done', 1));
      }

      // Strict FIFO matching
      if (this._expectations.length === 0) {
        const sessId = parsed?.sessionId || '(no-session-id)';
        const msgs = parsed?.messages || [];
        const hasToolResults = msgs.some(m => m?.role === 'tool' || m?.role === 'toolResult');
        const lastUser = extractLastUserMsg(parsed);
        // Last message of ANY role for nudge detection
        const lastMsgAnyRole = msgs.length > 0 ? JSON.stringify(msgs[msgs.length - 1]).slice(0, 300) : '(empty)';
        this._unexpected.push({ body: parsed, rawBody, sessId, hasToolResults });
        console.error(`[MOCK-500] session=${sessId} msgs=${msgs.length} toolResults=${hasToolResults} lastUser=${lastUser || '(none)'} lastMsgAny=${lastMsgAnyRole} tools=${JSON.stringify(extractToolNames(parsed))}`);
        return sendJSON(res, 500, {
          error: 'unexpected_llm_request',
          detail: `No expectations queued. Request tools: ${JSON.stringify(extractToolNames(parsed))}`,
        });
      }

      const exp = this._expectations[0];
      if (!matchesExpectation(parsed, exp)) {
        const sessId2 = parsed?.sessionId || '(no-session-id)';
        const msgs2 = parsed?.messages || [];
        const hasToolResults2 = msgs2.some(m => m?.role === 'tool' || m?.role === 'toolResult');
        const lastUser2 = extractLastUserMsg(parsed);
        const lastMsgAny2 = msgs2.length > 0 ? JSON.stringify(msgs2[msgs2.length - 1]).slice(0, 300) : '(empty)';
        this._unexpected.push({ body: parsed, rawBody, sessId: sessId2, hasToolResults: hasToolResults2 });
        console.error(`[MOCK-MISMATCH] session=${sessId2} msgs=${msgs2.length} toolResults=${hasToolResults2} lastUser=${lastUser2 || '(none)'} lastMsgAny=${lastMsgAny2} expected=${exp.id}`);
        this._expectations.shift(); // consume to prevent infinite loop on mismatched
        return sendJSON(res, 500, {
          error: 'expectation_mismatch',
          expected: exp.id,
          expectedMatch: exp.match,
          actualTools: extractToolNames(parsed),
        });
      }

      // Log request for test inspection
      this._requests.push(parsed);

      // Consume the matched expectation
      this._expectations.shift();
      const promptTokens = estimatePromptTokens(parsed);
      const id = `call_${Date.now()}`;

      switch (exp.respond.type) {
        case 'tool-call': {
          let args;
          if (typeof exp.respond.args === 'function') {
            args = exp.respond.args(parsed);
          } else {
            args = { ...exp.respond.args };
          }
          if (args.warn_tdd === null) delete args.warn_tdd;
          else if (!('warn_tdd' in args)) args.warn_tdd = 'i-am-sure-i-have-followed-tdd-and-kolmogorov-principles-and-kept-todo-updated';
          if (args.warn === null) delete args.warn;
          else if (!('warn' in args)) args.warn = 'it-is-not-possible-to-do-it-using-other-tools-and-only-run-tests-when-static-analysis-cannot-handle-it';
          if (args.warn_reuse === null) delete args.warn_reuse;
          else if (!('warn_reuse' in args)) args.warn_reuse = 'this-task-is-not-suitable-to-be-completed-via-continue-tool';
          return sendSSE(res, buildToolCallChunks(id, exp.respond.tool, JSON.stringify(args), promptTokens));
        }
        case 'text':
          return sendSSE(res, buildTextChunks(id, exp.respond.text, promptTokens));
        case 'error':
          return sendJSON(res, exp.respond.status || 500, exp.respond.body || { error: 'mock error' });
        case 'disconnect':
          res.writeHead(200, {
            'Content-Type': 'text/event-stream',
            'Cache-Control': 'no-cache',
            'Connection': 'keep-alive',
          });
          res.write(`data: ${JSON.stringify({ id, object: 'chat.completion.chunk', created: 1, model: 'mock', choices: [{ index: 0, delta: { role: 'assistant' }, finish_reason: null }] })}\n\n`);
          res.destroy();
          return;
        default:
          return sendJSON(res, 500, { error: 'unknown respond type' });
      }
    });
  }

  _handleWebSearch(req, res) {
    req.on('data', () => {});
    req.on('end', () => {
      sendJSON(res, 200, {
        results: [
          { title: 'Test Search Title', url: 'http://example.com', content: 'Test search content for E2E.' },
        ],
      });
    });
  }

  _handleWebFetch(req, res) {
    req.on('data', () => {});
    req.on('end', () => {
      sendJSON(res, 200, {
        title: 'Example Domain',
        byline: 'IANA',
        length: 500,
        content: 'Example Domain\n\nThis domain is for use in documentation examples.',
      });
    });
  }
}
