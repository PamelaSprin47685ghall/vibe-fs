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
} from './strict-mock-matches.js';
import {
  consumeExpectation,
  laneLabel,
  pendingExpectations,
  selectExpectation,
} from './strict-mock-lanes.js';
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
  /// Read-only: which session (if any) currently owns an alias. Lets a scenario
  /// route a second session to a distinct alias instead of throwing on rebind.
  sessionFor(alias) {
    return this._state.sessionBindings.get(alias) || null;
  }
  waitForExpectation(id, timeoutMs = WATCHDOG_TIMEOUT_MS) { return this._signals.waitForExpectation(id, timeoutMs); }
  waitForIdle(timeoutMs = WATCHDOG_TIMEOUT_MS) { return this._signals.waitForIdle(timeoutMs); }
  afterExpectation(id, callback) {
    if (this._signals.hasConsumed(id)) return callback();
    const callbacks = this._afterExpectation.get(id) || [];
    callbacks.push(callback);
    this._afterExpectation.set(id, callbacks);
  }

  get requests() { return this._state.requests; }
  get url() { return this._url; }
  get port() { return this._port; }
  get unexpectedRequests() { return this._state.unexpected; }
  get remainingExpectations() { return pendingExpectations(this._state).length; }
  get blockedExpectations() {
    return pendingExpectations(this._state).map((expectation) => ({
      id: expectation.id,
      lane: laneLabel(expectation.lane),
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
    if (process.env.MOCK_TRACE) {
      const tools = (parsed.tools || []).map((t) => t?.function?.name || t?.name || '?');
      const lastUser = JSON.stringify(extractLastUserMsg(parsed) || '').slice(0, 80);
      console.error(`[MOCK-TRACE] tools=${JSON.stringify(tools)} msgs=${(parsed.messages || []).length} chars=${JSON.stringify(parsed.messages || []).length} lastUser=${lastUser}`);
    }
    const selection = selectExpectation(s, parsed);
    if (selection.match) return this._dispatchMatched(res, parsed, selection.match);
    this._recordUnexpected(res, parsed, selection.reason, selection.candidates);
  }

  _dispatchMatched(res, parsed, match) {
    const s = this._state;
    const exp = consumeExpectation(s, match, requestSessionOf(parsed));
    this._signals.consume(exp);
    this._runAfterExpectation(exp.id);
    if (process.env.MOCK_TRACE) console.error(`[MOCK-TRACE] -> matched ${exp.id} ${laneLabel(exp.lane)}`);
    s.requests.push(parsed);
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
    const candidateLabels = candidates.map(({ expectation }) => `${expectation.id}@${laneLabel(expectation.lane)}`);
    this._state.unexpected.push({
      body: parsed,
      sessId,
      parentSessionId,
      hasToolResults,
      reason,
      candidates: candidateLabels,
    });
    const lastUser = JSON.stringify(extractLastUserMsg(parsed));
    console.error(`[MOCK-500] reason=${reason} session=${sessId} parent=${parentSessionId || '-'} role=${requestRoleOf(parsed)} kind=${requestKindOf(parsed)} model=${JSON.stringify(parsed.model)} tools=${JSON.stringify(extractToolNames(parsed))} msgs=${msgs.length} lastUser=${lastUser.slice(0, 400)} candidates=${JSON.stringify(candidateLabels)}`);
    return sendJSON(res, 500, { error: reason, sessionId: sessId, tools: extractToolNames(parsed) });
  }

  _runAfterExpectation(id) {
    const callbacks = this._afterExpectation.get(id);
    if (!callbacks) return;
    this._afterExpectation.delete(id);
    for (const callback of callbacks) callback();
  }
}

export { extractToolNames };
