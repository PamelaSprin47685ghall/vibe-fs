/**
 * strict-mock-provider.js — Strict mock LLM server for OpenCode E2E.
 *
 * Every LLM request must have a queued expectation. This includes title
 * generation and synthetic continuation requests. On mismatch, the failing
 * expectation is not consumed; the request is recorded as unexpected and a
 * 500 is returned.
 *
 * Server lifecycle / web endpoints live in strict-mock-server.js; legacy
 * args decoration in strict-mock-decorate.js; SSE chunks in strict-mock-sse.js;
 * matchers in strict-mock-matches.js; expectSatisfied in strict-mock-satisfy.js;
 * state record in strict-mock-state.js. This file stays under the
 * 200-line Kolmogorov line budget.
 */

import { sendJSON } from './strict-mock-sse.js';
import {
  extractToolNames,
  extractLastUserMsg,
  requestKindOf,
  requestRoleOf,
  requestSessionOf,
  requestParentSessionOf,
  sealProviderVisible,
  isProviderVisiblePrefix,
} from './strict-mock-matches.js';
import {
  consumeExpectation,
  edgeLabel,
  edgeWaitIds,
  laneLabel,
  pendingExpectations,
  selectExpectation,
} from './strict-mock-forest.js';
import {
  startHttpServer,
  stopHttpServer,
  handleWebSearch,
  handleWebFetch,
  readRequestBody,
} from './strict-mock-server.js';
import { checkSatisfied } from './strict-mock-satisfy.js';
import { StrictMockSignals } from './strict-mock-signals.js';
import { WATCHDOG_TIMEOUT_MS } from './watchdog-constants.js';
import { respond } from './strict-mock-responses.js';
import {
  createState,
  pushExpectation,
  resetState,
} from './strict-mock-state.js';

export class StrictMockProvider {
  constructor() {
    this._state = createState();
    this._server = null;
    this._port = null;
    this._url = null;
    this._signals = new StrictMockSignals();
    this._afterExpectation = new Map();
    this.onRequest = null;
    this.onExpectationConsumed = null;
    /** First unmatched script: canary must stop. Scenario wires process.exit. */
    this.onFatal = null;
  }

  expectToolCall(opts) { pushExpectation(this._state, { type: 'tool-call', tool: opts.tool, args: opts.args || {} }, opts); }
  expectText(opts) { pushExpectation(this._state, { type: 'text', text: opts.text ?? 'ok' }, opts); }
  expectTitle(opts) { pushExpectation(this._state, { type: 'title', text: opts.text ?? 'E2E Test Session' }, opts); }
  expectError(opts) {
    pushExpectation(this._state, {
      type: 'error',
      status: opts.status || 500,
      body: opts.body || { error: 'mock error' },
      headers: opts.headers,
    }, opts);
  }
  expectDisconnect(opts = {}) { pushExpectation(this._state, { type: 'disconnect' }, opts); }

  expectSyntheticTodoNudge(opts = {}) {
    this.expectText({
      ...opts,
      id: opts.id || 'synthetic-todo-nudge',
      lane: { ...opts.lane, requestKind: 'synthetic' },
      text: 'done',
      match: { ...opts.match, containsText: ['There are still incomplete todos. Continue working through the remaining items.'] },
    });
  }

  expectLoopNudge(opts = {}) {
    this.expectText({
      ...opts,
      id: opts.id || 'synthetic-loop-nudge',
      lane: { ...opts.lane, requestKind: 'synthetic' },
      text: 'done',
      match: { ...opts.match, containsText: ['You are in loop mode. You must call the submit_review tool'] },
    });
  }

  expectSyntheticBudgetNudge(opts = {}) {
    this.expectText({
      ...opts,
      id: opts.id || 'synthetic-budget-nudge',
      lane: { ...opts.lane, requestKind: 'synthetic' },
      text: 'done',
      match: { ...opts.match, containsText: ['the system context is about to be suspended'] },
    });
  }

  expectSatisfied() { checkSatisfied(this._state); }
  reset() {
    resetState(this._state);
    this._afterExpectation.clear();
  }
  bindSession(alias, sessionID) {
    if (typeof alias !== 'string' || alias.trim() === '') throw new Error('StrictMock session alias must be non-empty');
    if (typeof sessionID !== 'string' || sessionID.trim() === '') throw new Error('StrictMock session ID must be non-empty');
    const bound = this._state.sessionBindings.get(alias);
    if (bound && bound !== sessionID) throw new Error(`StrictMock session alias ${alias} is already bound`);
    this._state.sessionBindings.set(alias, sessionID);
  }

  /** Host restart / new process: old provider-visible seals are not comparable. */
  clearPrefixSeals() {
    this._state.sealedBySession.clear();
  }
  /// Read-only: which session (if any) currently owns an alias. Lets a scenario
  /// route a second session to a distinct alias instead of throwing on rebind.
  sessionFor(alias) {
    return this._state.sessionBindings.get(alias) || null;
  }
  /** Map authoring wait ids (including reusable aliases) onto the primary edge id. */
  _primaryWaitId(id) {
    const aliased = this._state.aliasToEdge?.get(id);
    if (aliased?.id) return aliased.id;
    return id;
  }

  waitForExpectation(id, timeoutMs = WATCHDOG_TIMEOUT_MS) {
    // Alias-merged reusable templates (perfect-3 → perfect-1) wait on the primary
    // edge. Each successive wait blocks until the next primary match — so the
    // canary flow inserts post-rebase PERFECT as a real intermediate event under
    // the default 2s causal budget, without wall-clock timeout inflation.
    return this._signals.waitForExpectation(this._primaryWaitId(id), timeoutMs);
  }
  waitForIdle(timeoutMs = WATCHDOG_TIMEOUT_MS) { return this._signals.waitForIdle(timeoutMs); }
  afterExpectation(id, callback) {
    const primary = this._primaryWaitId(id);
    // Permanent one-shot only: already satisfied ⇒ run immediately.
    // Reusable primary never permanently consumes, so later registration waits
    // for the next match (intermediate post-rebase event).
    if (this._signals.hasConsumed(primary)) return callback();
    const callbacks = this._afterExpectation.get(primary) || [];
    callbacks.push(callback);
    this._afterExpectation.set(primary, callbacks);
  }

  get requests() { return this._state.requests; }
  get url() { return this._url; }
  get port() { return this._port; }
  get unexpectedRequests() { return this._state.unexpected; }
  get remainingExpectations() { return pendingExpectations(this._state).length; }
  get blockedExpectations() {
    return pendingExpectations(this._state).map((expectation) => ({
      id: expectation.id,
      lane: edgeLabel(expectation),
      blocking: expectation.blocking,
    }));
  }
  get activeRequestCount() { return this._signals.activeRequestCount; }

  get strict() { return this._state.strict; }
  set strict(v) { this._state.strict = !!v; }

  async start() {
    if (this._server) return this._url;
    const { server, port, url } = await startHttpServer((req, res) => this._handleRequest(req, res));
    this._server = server;
    this._port = port;
    this._url = url;
    return this._url;
  }

  stopMocking() {
    this._state.stopped = true;
    this._signals.stop();
    this._afterExpectation.clear();
  }

  async stop() {
    this.stopMocking();
    await stopHttpServer(this._server);
    this._server = null;
    this._port = null;
    this._url = null;
  }

  _handleRequest(req, res) {
    if (this._state.stopped) {
      sendJSON(res, 503, { error: 'mocking stopped' });
      return;
    }
    this._signals.trackResponse(res);

    const url = new URL(req.url, `http://${req.headers.host}`);
    console.error(`[mock-req] ${req.method} ${url.pathname}`);
    if ((url.pathname === '/v1/models' || url.pathname === '/models' || url.pathname === '/api/models') && req.method === 'GET') {
      return sendJSON(res, 200, { object: 'list', data: [{ id: 'test-model', object: 'model' }] });
    }
    if (url.pathname === '/api/web_search' && req.method === 'POST') return handleWebSearch(req, res);
    if (url.pathname === '/api/web_fetch' && req.method === 'POST') return handleWebFetch(req, res);
    if ((url.pathname === '/v1/chat/completions' || url.pathname === '/v1/responses') && req.method === 'POST') {
      return this._handleChat(req, res);
    }
    sendJSON(res, 404, { error: 'not found' });
  }

  _handleChat(req, res) {
    readRequestBody(req).then((parsed) => {
      Object.defineProperty(parsed, '__testkitHeaders', { value: req.headers, enumerable: false });
      this._dispatchChat(res, parsed);
    })
      .catch((error) => {
        // Never mask a dispatch/hook failure as a body-parse failure: the
        // diagnostic must name the real cause (alias binding, selector, etc.).
        const badBody = error instanceof SyntaxError;
        console.error(`[MOCK-DISPATCH-ERROR] ${badBody ? 'bad json' : 'dispatch'}: ${error?.stack || error}`);
        sendJSON(res, 400, { error: badBody ? 'bad json' : `dispatch failed: ${error?.message || error}` });
      });
  }

  _dispatchChat(res, parsed) {
    this.onRequest?.(parsed);
    const s = this._state;
    // First script mismatch already happened: do not attempt further matches.
    if (s.fatal) {
      sendJSON(res, 503, {
        error: 'mock stopped after first script mismatch',
        reason: s.fatal.reason,
        id: s.fatal.id,
      });
      return;
    }
    if (process.env.MOCK_TRACE) {
      const tools = (parsed.tools || []).map((t) => t?.function?.name || t?.name || '?');
      const lastUser = JSON.stringify(extractLastUserMsg(parsed) || '').slice(0, 80);
      console.error(`[MOCK-TRACE] tools=${JSON.stringify(tools)} msgs=${(parsed.messages || []).length} chars=${JSON.stringify(parsed.messages || []).length} lastUser=${lastUser}`);
    }

    // Prefix-cache invariant: for a given session, each chat request must keep
    // the previous provider-visible tools+messages as a byte-prefix. Otherwise
    // the production projection broke the sealed prefix (e.g. mutating B head).
    // One allowed cold boundary: epoch freeze injects companion-b-head* at the
    // front (SSOT epoch switch). That reseals; any other mutation is fatal.
    const sessionID = requestSessionOf(parsed);
    const kind = requestKindOf(parsed);
    if (sessionID && kind === 'chat') {
      const sealed = s.sealedBySession.get(sessionID);
      if (sealed && !isProviderVisiblePrefix(sealed, parsed)) {
        // Provider requests may strip synthetic ids. Allowed cold boundary:
        // tools + leading system text unchanged, but body is not append-only
        // (epoch B-head replacement). Tools/system mutation is always fatal.
        let prev;
        try { prev = JSON.parse(sealed); } catch { prev = null; }
        const next = JSON.parse(sealProviderVisible(parsed));
        const toolsSame = prev && JSON.stringify(prev.tools) === JSON.stringify(next.tools);
        const prev0 = prev?.messages?.[0];
        const next0 = next?.messages?.[0];
        const systemSame = prev0 && next0
          && prev0.role === 'system'
          && next0.role === 'system'
          && JSON.stringify(prev0.content) === JSON.stringify(next0.content);
        const epochCold = toolsSame && systemSame;
        if (epochCold) {
          console.error(`[MOCK-PREFIX-RESEAL] session=${sessionID} tools+system stable; resealing after non-append body (epoch/B-head cold boundary)`);
          s.sealedBySession.set(sessionID, sealProviderVisible(parsed));
        } else {
          this._recordUnexpected(res, parsed, 'prefix-cache-invalidated', []);
          return;
        }
      }
    }

    const selection = selectExpectation(s, parsed);
    if (selection.match) return this._dispatchMatched(res, parsed, selection.match);
    this._recordUnexpected(res, parsed, selection.reason, selection.candidates);
  }

  _dispatchMatched(res, parsed, match) {
    const s = this._state;
    const sessionID = requestSessionOf(parsed);
    const exp = consumeExpectation(s, match, sessionID);
    // Dual-PERFECT reusable templates (reusable && !neverEnd) wake only the
    // primary wait id once per match so later waits claim the next occurrence.
    // neverEnd / pathless / one-shot edges permanently satisfy every alias wait
    // id (busy hang, blogger, title, guard nudge absorb forever).
    const multiWaitReusable = exp.reusable === true && exp.neverEnd !== true;
    const waitIds = multiWaitReusable ? [exp.id] : edgeWaitIds(exp);
    const permanent = !multiWaitReusable;
    for (const wid of waitIds) {
      this._signals.consume({ id: wid, permanent });
      // afterExpectation may be registered on primary or (one-shot) alias ids.
      this._runAfterExpectation(wid);
    }
    if (process.env.MOCK_TRACE) console.error(`[MOCK-TRACE] -> matched ${edgeLabel(exp)}`);
    s.requests.push(parsed);
    // Seal only chat turns (title/synthetic may reshuffle without product cache).
    if (sessionID && requestKindOf(parsed) === 'chat') {
      s.sealedBySession.set(sessionID, sealProviderVisible(parsed));
    }
    this.onExpectationConsumed?.({
      id: exp.id,
      lane: exp.lane,
      blocking: exp.blocking,
      requestKind: requestKindOf(parsed),
    });
    return respond(this._state, res, exp, parsed);
  }

  _recordUnexpected(res, parsed, reason, candidates = []) {
    const sessId = requestSessionOf(parsed) || '(no-session-id)';
    const parentSessionId = requestParentSessionOf(parsed) || null;
    const msgs = parsed?.messages || [];
    const hasToolResults = msgs.some((m) => m?.role === 'tool' || m?.role === 'toolResult');
    const candidateLabels = candidates.map(({ expectation }) => edgeLabel(expectation));
    const lastUser = extractLastUserMsg(parsed);
    const fatal = {
      id: `unexpected-${this._state.unexpected.length + 1}`,
      reason,
      sessionId: sessId,
      parentSessionId,
      lastUser: typeof lastUser === 'string' ? lastUser.slice(0, 400) : JSON.stringify(lastUser).slice(0, 400),
      candidates: candidateLabels,
    };
    // First script mismatch: record once, reject all waiters, notify the test to stop.
    const isFirst = !this._state.fatal;
    this._state.fatal = this._state.fatal || fatal;
    this._state.unexpected.push({
      body: parsed,
      sessId,
      parentSessionId,
      hasToolResults,
      reason,
      candidates: candidateLabels,
    });
    console.error(`[MOCK-FATAL] first script mismatch: reason=${reason} session=${sessId} parent=${parentSessionId || '-'} role=${requestRoleOf(parsed)} kind=${requestKindOf(parsed)} model=${JSON.stringify(parsed.model)} tools=${JSON.stringify(extractToolNames(parsed))} msgs=${msgs.length} lastUser=${JSON.stringify(fatal.lastUser)} candidates=${JSON.stringify(candidateLabels)}`);
    if (isFirst) {
      const err = new Error(
        `FIRST SCRIPT MISMATCH: ${reason} session=${sessId} lastUser=${JSON.stringify(fatal.lastUser)} candidates=${JSON.stringify(candidateLabels)}`,
      );
      err.fatal = fatal;
      this._signals.fail(err);
      try { this.onFatal?.(fatal, err); } catch (hookErr) {
        console.error(`[MOCK-FATAL] onFatal threw: ${hookErr?.stack || hookErr}`);
      }
    }
    // Clean 500 for this request. Later chats see state.fatal → 503.
    return sendJSON(res, 500, {
      error: 'first-script-mismatch',
      reason,
      sessionId: sessId,
      tools: extractToolNames(parsed),
      candidates: candidateLabels,
    });
  }

  _runAfterExpectation(id) {
    const callbacks = this._afterExpectation.get(id);
    if (!callbacks) return;
    this._afterExpectation.delete(id);
    for (const callback of callbacks) callback();
  }
}

export { extractToolNames };
