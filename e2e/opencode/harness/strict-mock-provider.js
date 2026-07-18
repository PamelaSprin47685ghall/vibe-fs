/**
 * strict-mock-provider.js — Strict mock LLM server for OpenCode E2E.
 *
 * Modes:
 * - strict: false (default) — preserves legacy behavior including warn_tdd
 *   auto-injection and synthetic-continuation bypass, so older F# test
 *   suites (OpencodePluginTests.fs, etc.) keep working.
 * - strict: true — every LLM request must have a queued expectation.
 *   synthetic/title requests are NOT auto-bypassed unless explicitly
 *   enabled via allowSyntheticContinuations()/allowTitleGeneration().
 *   On mismatch, the failing expectation is NOT consumed; the request
 *   is recorded as unexpected and a 500 is returned.
 *
 * Server lifecycle / web endpoints live in strict-mock-server.js; legacy
 * args decoration in strict-mock-decorate.js; SSE chunks in strict-mock-sse.js;
 * matchers in strict-mock-matches.js; expectSatisfied in strict-mock-satisfy.js;
 * state record in strict-mock-state.js. This file stays under the
 * 200-line Kolmogorov line budget.
 */

import { buildToolCallChunks, buildTextChunks, sendJSON, sendSSE } from './strict-mock-sse.js';
import {
  isSyntheticContinuation,
  detectSyntheticMarker,
  isTitleGenerationRequest,
  extractToolNames,
  matchesExpectation,
  estimatePromptTokens,
} from './strict-mock-matches.js';
import { decorateLegacyArgs } from './strict-mock-decorate.js';
import {
  startHttpServer,
  stopHttpServer,
  handleWebSearch,
  handleWebFetch,
  readRequestBody,
} from './strict-mock-server.js';
import { checkSatisfied } from './strict-mock-satisfy.js';
import {
  createState,
  pushExpectation,
  pushNoMoreRequests,
  resetState,
} from './strict-mock-state.js';

const MOCK_MODEL = 'mock';
const SSE_HEADERS = {
  'Content-Type': 'text/event-stream',
  'Cache-Control': 'no-cache',
  'Connection': 'keep-alive',
};

export class StrictMockProvider {
  constructor() {
    this._state = createState();
    this._server = null;
    this._port = null;
    this._url = null;
  }

  expectToolCall(opts) { pushExpectation(this._state, { type: 'tool-call', tool: opts.tool, args: opts.args || {} }, opts); }
  expectText(opts) { pushExpectation(this._state, { type: 'text', text: opts.text || 'ok' }, opts); }
  expectError(opts) { pushExpectation(this._state, { type: 'error', status: opts.status || 500, body: opts.body || { error: 'mock error' } }, opts); }
  expectDisconnect(opts = {}) { pushExpectation(this._state, { type: 'disconnect' }, opts); }

  allowSyntheticContinuations() { this._state.allowSyntheticContinuations = true; }
  allowTitleGeneration() { this._state.allowTitleGeneration = true; }
  expectNoMoreRequests() { pushNoMoreRequests(this._state); }

  expectSatisfied() { checkSatisfied(this._state.expectations, this._state.unexpected); }
  reset() { resetState(this._state); }

  get requests() { return this._state.requests; }
  get url() { return this._url; }
  get port() { return this._port; }
  get unexpectedRequests() { return this._state.unexpected; }
  get remainingExpectations() { return this._state.expectations.length; }
  get nudgeBypassed() { return this._state.nudgeBypassed; }
  get syntheticRequests() { return this._state.syntheticRequests; }

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

  async stop() {
    await stopHttpServer(this._server);
    this._server = null;
    this._port = null;
    this._url = null;
  }

  _handleRequest(req, res) {
    const url = new URL(req.url, `http://${req.headers.host}`);
    if (url.pathname === '/api/web_search' && req.method === 'POST') return handleWebSearch(req, res);
    if (url.pathname === '/api/web_fetch' && req.method === 'POST') return handleWebFetch(req, res);
    if ((url.pathname === '/v1/chat/completions' || url.pathname === '/v1/responses') && req.method === 'POST') {
      return this._handleChat(req, res);
    }
    sendJSON(res, 404, { error: 'not found' });
  }

  _handleChat(req, res) {
    readRequestBody(req).then((parsed) => this._dispatchChat(res, parsed))
      .catch(() => sendJSON(res, 400, { error: 'bad json' }));
  }

  _dispatchChat(res, parsed) {
    const s = this._state;
    if (isTitleGenerationRequest(parsed)) {
      if (s.strict && !s.allowTitleGeneration) {
        return this._recordUnexpected(res, parsed, 'title-generation-not-allowed');
      }
      return sendSSE(res, buildTextChunks(`title_${Date.now()}`, 'E2E Test Session', 1));
    }
    if (isSyntheticContinuation(parsed)) {
      const marker = detectSyntheticMarker(parsed);
      if (s.strict && !s.allowSyntheticContinuations) {
        return this._recordUnexpected(res, parsed, `synthetic-${marker}-not-allowed`);
      }
      return this._bypassSynthetic(res, parsed, marker);
    }
    this._dispatchFifo(res, parsed);
  }

  _bypassSynthetic(res, parsed, marker) {
    const s = this._state;
    s.nudgeBypassed++;
    s.syntheticRequests.push({ body: parsed, marker, time: Date.now() });
    console.error(`[MOCK-SYNTH] session=${parsed?.sessionId || '?'} marker=${marker} #${s.nudgeBypassed}`);
    return sendSSE(res, buildTextChunks(`synth_${Date.now()}`, 'done', 1));
  }

  _dispatchFifo(res, parsed) {
    const s = this._state;
    if (s.expectations.length === 0) {
      if (s.strict) {
        return this._recordUnexpected(res, parsed, 'no-expectations-queued');
      }
      // Legacy non-strict mode: auto-respond with plain text so old F#
      // integration suites that do not queue every request can still run.
      return sendSSE(res, buildTextChunks(`auto_${Date.now()}`, 'ok', 1));
    }
    const exp = s.expectations[0];
    if (exp.respond.type === 'no-more-requests-boundary') {
      if (s.strict) {
        return this._recordUnexpected(res, parsed, 'request-after-no-more-requests-boundary');
      }
      return sendSSE(res, buildTextChunks(`auto_${Date.now()}`, 'ok', 1));
    }
    if (!matchesExpectation(parsed, exp)) {
      if (s.strict) return this._recordUnexpected(res, parsed, `expectation-mismatch:${exp.id}`);
      console.error(`[MOCK-MISMATCH] expected=${exp.id} tools=${JSON.stringify(extractToolNames(parsed))}`);
      s.expectations.shift();
      return this._respond(res, exp, parsed);
    }
    s.requests.push(parsed);
    s.expectations.shift();
    return this._respond(res, exp, parsed);
  }

  _recordUnexpected(res, parsed, reason) {
    const sessId = parsed?.sessionId || '(no-session-id)';
    const msgs = parsed?.messages || [];
    const hasToolResults = msgs.some((m) => m?.role === 'tool' || m?.role === 'toolResult');
    this._state.unexpected.push({ body: parsed, sessId, hasToolResults, reason });
    console.error(`[MOCK-500] reason=${reason} session=${sessId} tools=${JSON.stringify(extractToolNames(parsed))} msgs=${msgs.length}`);
    return sendJSON(res, 500, { error: reason, sessionId: sessId, tools: extractToolNames(parsed) });
  }

  _respond(res, exp, parsed) {
    const id = `call_${Date.now()}`;
    const promptTokens = estimatePromptTokens(parsed);
    switch (exp.respond.type) {
      case 'tool-call': return this._respondToolCall(res, id, exp, parsed, promptTokens);
      case 'text': return sendSSE(res, buildTextChunks(id, exp.respond.text, promptTokens));
      case 'error': return sendJSON(res, exp.respond.status || 500, exp.respond.body || { error: 'mock error' });
      case 'disconnect': return this._respondDisconnect(res, id);
      default: return sendJSON(res, 500, { error: 'unknown respond type' });
    }
  }

  _respondToolCall(res, id, exp, parsed, promptTokens) {
    let args;
    if (typeof exp.respond.args === 'function') args = exp.respond.args(parsed);
    else args = { ...exp.respond.args };
    if (!this._state.strict) decorateLegacyArgs(args);
    return sendSSE(res, buildToolCallChunks(id, exp.respond.tool, JSON.stringify(args), promptTokens));
  }

  _respondDisconnect(res, id) {
    res.writeHead(200, SSE_HEADERS);
    res.write(`data: ${JSON.stringify({ id, object: 'chat.completion.chunk', created: 1, model: MOCK_MODEL, choices: [{ index: 0, delta: { role: 'assistant' }, finish_reason: null }] })}\n\n`);
    res.destroy();
  }
}

export { extractToolNames };
