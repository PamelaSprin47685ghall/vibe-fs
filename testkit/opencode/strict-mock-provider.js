/**
 * strict-mock-provider.js — Strict mock LLM server for OpenCode E2E.
 *
 * The provider is driven by an attached `ScenarioRuntime` compiled from one static
 * TOML scenario (VERIFY-003). Every LLM request must hit a declared turn. On
 * mismatch the request is recorded as unexpected, the first mismatch becomes fatal,
 * and a 500 is returned. A provider with no scenario attached fails closed the same
 * way: an unattached provider that answers is a mock that believes nothing is
 * covered.
 *
 * Server lifecycle / web endpoints live in strict-mock-server.js; SSE chunks in
 * strict-mock-sse.js; state record in strict-mock-state.js.
 */

import { sendJSON } from './strict-mock-sse.js';
import {
  startHttpServer,
  stopHttpServer,
  handleWebSearch,
  handleWebFetch,
  readRequestBody,
} from './strict-mock-server.js';
import { faultBody } from './delivery-plan.js';
import { StrictMockSignals } from './strict-mock-signals.js';
import { WATCHDOG_TIMEOUT_MS } from './time-budget.js';
import { respond } from './strict-mock-responses.js';
import {
  createState,
  resetState,
} from './strict-mock-state.js';

const pickToolName = (t) => t?.function?.name ?? t?.name;

export function extractToolNames(body) {
  const tools = body?.tools;
  if (!Array.isArray(tools)) return [];
  const out = [];
  for (const t of tools) {
    const name = pickToolName(t);
    if (typeof name === 'string') out.push(name);
  }
  return out;
}

export function extractLastUserMsg(body) {
  const msgs = body?.messages || [];
  const last = [...msgs].reverse().find((m) => m?.role === 'user');
  if (!last) return null;
  const c = last.content;
  if (typeof c === 'string') return c;
  if (Array.isArray(c)) return JSON.stringify(c);
  return null;
}

const headerValue = (headers, names) => {
  for (const name of names) {
    const value = headers[name];
    if (typeof value === 'string' && value !== '') return value;
  }
  return null;
};

const requestContextOf = (headers) => ({
  sessionId: headerValue(headers, ['x-session-affinity', 'x-session-id', 'x-opencode-session']),
  parentSessionId: headerValue(headers, ['x-parent-session-id']),
});

const requestRecordOf = (body, context) => {
  const record = { ...body };
  Object.defineProperties(record, {
    sessionID: { value: context.sessionId, enumerable: false },
    parentSessionID: { value: context.parentSessionId, enumerable: false },
  });
  return record;
};

export class StrictMockProvider {
  constructor() {
    this._state = createState();
    this._server = null;
    this._port = null;
    this._url = null;
    this._signals = new StrictMockSignals();
    this._afterExpectation = new Map();
    /** @type {import('./scenario-runtime.js').ScenarioRuntime | null} */
    this._scenario = null;
    this.onRequest = null;
    this.onExpectationConsumed = null;
    /** First unmatched script: canary must stop. Scenario wires process.exit. */
    this.onFatal = null;
  }

  /**
   * Drive this provider from a compiled TOML scenario (VERIFY-003).
   *
   * A scenario answers every request the flow is willing to vouch for; anything
   * else is unexpected. Attaching twice would merge two indexes into one matcher,
   * which is exactly the ambiguity the forest rebuild exists to remove.
   */
  attachScenario(runtime) {
    if (this._scenario !== null) {
      throw new Error('a scenario is already attached to this provider');
    }
    this._scenario = runtime;
  }

  expectSatisfied() {
    if (this._scenario === null) {
      throw new Error('no scenario attached: there is nothing to be satisfied');
    }

    const errors = [];
    if (this._state.fatal) {
      const f = this._state.fatal;
      errors.push(`FIRST SCRIPT MISMATCH (mock stopped): reason=${f.reason} session=${f.sessionId} lastUser=${JSON.stringify(f.lastUser)} candidates=${JSON.stringify(f.candidates)}`);
    }

    // A declared step no request reached. `internal` turns are exempt (production decides
    // whether to compose them at all) — a scenario that needs one says `must`.
    const unanswered = this._scenario.unanswered();
    if (unanswered.length > 0) {
      errors.push(`declared but never reached = ${unanswered.length}:\n${unanswered.slice(0, 5).map((e) => `  [${e.id}] lane=${e.lane ?? '(any)'} step=${e.step} respond=${e.respond?.type}`).join('\n')}`);
    }

    const unmet = this._scenario.unmetMust();
    if (unmet.length > 0) errors.push(`must not satisfied: ${unmet.join(', ')}`);

    if (this._state.unexpected.length > 0) {
      errors.push(`unexpected requests = ${this._state.unexpected.length}: ${JSON.stringify(this._state.unexpected.slice(0, 3).map((u) => u.reason))}`);
    }

    if (errors.length > 0) throw new Error(`Scenario assertions failed:\n${errors.join('\n')}`);
  }
  reset() {
    resetState(this._state);
    this._afterExpectation.clear();
  }
  /**
   * Associate a scenario alias with a real session id (HOST-008).
   *
   * An alias names a ROLE, and production forks as many children of a role as the
   * work needs — `orchestrator-publish` reviews before and after the rebase, so
   * `fast-reviewer` legitimately holds two sessions. The scenario keeps the full
   * alias → session set; `sessionBindings` keeps the latest for diagnostics.
   */
  bindSession(alias, sessionID) {
    if (typeof alias !== 'string' || alias.trim() === '') throw new Error('StrictMock session alias must be non-empty');
    if (typeof sessionID !== 'string' || sessionID.trim() === '') throw new Error('StrictMock session ID must be non-empty');

    this._scenario?.bind(alias, sessionID);
    this._state.sessionBindings.set(alias, sessionID);
  }

  /** Host restart / new process: old provider-visible seals are not comparable. */
  clearPrefixSeals() {
    this._state.sealedBySession.clear();
    this._scenario?.clearSeals();
  }
  /// Read-only: which session (if any) currently owns an alias. Lets a scenario
  /// route a second session to a distinct alias instead of throwing on rebind.
  sessionFor(alias) {
    return this._state.sessionBindings.get(alias) || null;
  }
  waitForExpectation(id, timeoutMs = WATCHDOG_TIMEOUT_MS) {
    return this._signals.waitForExpectation(id, timeoutMs);
  }
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
  get remainingExpectations() {
    return this._scenario === null ? 0 : this._scenario.unanswered().length;
  }
  get blockedExpectations() {
    if (this._scenario === null) return [];
    return this._scenario.unanswered().map((entry) => ({
      id: entry.id,
      lane: entry.lane ?? '(any)',
      blocking: true,
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
      this._dispatchChat(res, parsed, requestContextOf(req.headers));
    })
      .catch((error) => {
        // Never mask a dispatch/hook failure as a body-parse failure: the
        // diagnostic must name the real cause (alias binding, selector, etc.).
        const badBody = error instanceof SyntaxError;
        console.error(`[MOCK-DISPATCH-ERROR] ${badBody ? 'bad json' : 'dispatch'}: ${error?.stack || error}`);
        sendJSON(res, 400, { error: badBody ? 'bad json' : `dispatch failed: ${error?.message || error}` });
      });
  }

  /**
   * One request against the compiled scenario (K9).
   *
   * The seal, the fault plan and the content lookup all live in `ScenarioRuntime`; this
   * method only turns its verdict into HTTP. Nothing here inspects the request — that
   * would be a second matcher, which is what K1-K8 spent their whole effort removing.
   */
  _dispatchScenario(res, parsed, context) {
    const selection = this._scenario.select(parsed, context);

    if (selection.unmatched !== undefined) {
      const key = selection.unmatched.key;
      return this._recordUnexpected(
        res,
        parsed,
        'no-declared-turn',
        selection.unmatched.candidates.map((entry) => ({ expectation: entry })),
        `lane=${key.lane ?? '(unbound)'} kind=${key.kind} step=${key.step}`,
        context,
      );
    }
    if (selection.ambiguous !== undefined) {
      return this._recordUnexpected(
        res,
        parsed,
        'ambiguous-turn',
        selection.ambiguous.entries.map((entry) => ({ expectation: entry })),
        '',
        context,
      );
    }
    if (selection.sealBroken !== undefined) {
      return this._recordUnexpected(res, parsed, `seal-${selection.sealBroken.reason}`, [], '', context);
    }

    this._scenario.consume(parsed, selection, context);
    this._state.requests.push(requestRecordOf(parsed, context));

    // A fault is a transport outcome, so it wakes NOTHING. `wait` is a causal barrier on
    // content arriving; waking it on a refused delivery would let a flow proceed past a
    // step the model never answered.
    if (selection.fault !== undefined) {
      const fault = selection.fault;
      if (fault.kind === 'disconnect') {
        res.writeHead(200, { 'Content-Type': 'text/event-stream', 'Cache-Control': 'no-cache' });
        return res.destroy();
      }
      return sendJSON(res, fault.status ?? 500, faultBody(fault));
    }

    const entry = selection.entry;
    // Both the step id and its turn id, so a flow may wait on either granularity —
    // `wait = "mgr"` for "the turn happened" and `wait = "mgr.1"` for a specific step.
    for (const id of new Set([entry.id, entry.turnId])) {
      this._signals.consume({ id, permanent: true });
      this._runAfterExpectation(id);
    }

    if (process.env.MOCK_TRACE) {
      console.error(`[MOCK-TRACE] -> ${entry.id} lane=${entry.lane ?? '(any)'} step=${entry.step} attempt=${selection.attempt}`);
    }

    this.onExpectationConsumed?.({
      id: entry.id,
      lane: { scenario: this._scenario.scenario.name, session: entry.lane, role: entry.turnId, requestKind: entry.kind },
      blocking: true,
      requestKind: entry.kind,
    });

    return respond(this._state, res, entry, parsed);
  }

  _dispatchChat(res, parsed, context) {
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

    if (this._scenario !== null) return this._dispatchScenario(res, parsed, context);

    // No scenario attached: fail closed. An unattached provider that answers is a
    // mock that believes nothing is covered.
    this._recordUnexpected(res, parsed, 'no-scenario-attached', [], '', context);
  }

  _recordUnexpected(res, parsed, reason, candidates = [], detail = '', context = {}) {
    const sessId = context.sessionId || '(no-session-id)';
    const parentSessionId = context.parentSessionId || null;
    const msgs = parsed?.messages || [];
    const hasToolResults = msgs.some((m) => m?.role === 'tool' || m?.role === 'toolResult');
    const candidateLabels = candidates.map(({ expectation }) =>
      `${expectation.id}@${expectation.lane ?? '(any)'}/${expectation.kind}/step-${expectation.step}`,
    );
    const lastUser = extractLastUserMsg(parsed);
    const fatal = {
      id: `unexpected-${this._state.unexpected.length + 1}`,
      reason: detail === '' ? reason : `${reason} (${detail})`,
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
    console.error(`[MOCK-FATAL] first script mismatch: reason=${reason} session=${sessId} parent=${parentSessionId || '-'} model=${JSON.stringify(parsed.model)} tools=${JSON.stringify(extractToolNames(parsed))} msgs=${msgs.length} lastUser=${JSON.stringify(fatal.lastUser)} candidates=${JSON.stringify(candidateLabels)}`);
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
