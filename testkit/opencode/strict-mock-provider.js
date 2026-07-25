/**
 * strict-mock-provider.js — Strict mock LLM server for OpenCode E2E.
 *
 * Modes:
 * - strict: false — preserves legacy behavior including warn_tdd
 *   auto-injection and synthetic-continuation bypass.
 * - strict: true (default) — every LLM request must have a queued expectation.
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
  estimatePromptTokens,
  extractLastUserMsg,
  requestKindOf,
} from './strict-mock-matches.js';
import {
  consumeExpectation,
  laneLabel,
  pendingExpectations,
  selectExpectation,
} from './strict-mock-lanes.js';
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
    this._activeResponses = new Set();
    this.onRequest = null;
    this.onExpectationConsumed = null;
  }

  expectToolCall(opts) { pushExpectation(this._state, { type: 'tool-call', tool: opts.tool, args: opts.args || {} }, opts); }
  expectText(opts) { pushExpectation(this._state, { type: 'text', text: opts.text ?? 'ok' }, opts); }
  expectError(opts) { pushExpectation(this._state, { type: 'error', status: opts.status || 500, body: opts.body || { error: 'mock error' } }, opts); }
  expectDisconnect(opts = {}) { pushExpectation(this._state, { type: 'disconnect' }, opts); }

  allowSyntheticContinuations() { this._state.allowSyntheticContinuations = true; }
  allowTitleGeneration() { this._state.allowTitleGeneration = true; }

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
  reset() { resetState(this._state); }

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
  get nudgeBypassed() { return this._state.nudgeBypassed; }
  get activeRequestCount() { return this._activeResponses.size; }
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

  stopMocking() {
    this._state.stopped = true;
    for (const res of this._activeResponses) {
      try { res.destroy(); } catch {}
    }
    this._activeResponses.clear();
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
    this._activeResponses.add(res);
    res.on('close', () => this._activeResponses.delete(res));

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
    readRequestBody(req).then((parsed) => this._dispatchChat(res, parsed))
      .catch(() => sendJSON(res, 400, { error: 'bad json' }));
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

    if (isTitleGenerationRequest(parsed) && (!s.strict || s.allowTitleGeneration)) {
      if (process.env.MOCK_TRACE) console.error('[MOCK-TRACE] -> title-bypass');
      return sendSSE(res, buildTextChunks(`title_${Date.now()}`, 'E2E Test Session', 1));
    }

    if (isSyntheticContinuation(parsed) && (!s.strict || s.allowSyntheticContinuations)) {
      return this._bypassSynthetic(res, parsed, detectSyntheticMarker(parsed));
    }
    this._recordUnexpected(res, parsed, selection.reason, selection.candidates);
  }

  _bypassSynthetic(res, parsed, marker) {
    const s = this._state;
    s.nudgeBypassed++;
    s.syntheticRequests.push({ body: parsed, marker, time: Date.now() });
    console.error(`[MOCK-SYNTH] session=${parsed?.sessionId || '?'} marker=${marker} #${s.nudgeBypassed}`);
    const text =
      marker === 'todo-nudge'
        ? 'done\n<skip-todo-check />'
        : marker === 'loop-nudge'
          ? 'done\n<skip-review-check />'
          : 'done';
    return sendSSE(res, buildTextChunks(`synth_${Date.now()}`, text, 1));
  }

  _dispatchMatched(res, parsed, match) {
    const s = this._state;
    const exp = consumeExpectation(s, match);
    if (process.env.MOCK_TRACE) console.error(`[MOCK-TRACE] -> matched ${exp.id} ${laneLabel(exp.lane)}`);
    s.requests.push(parsed);
    this.onExpectationConsumed?.({
      id: exp.id,
      lane: exp.lane,
      blocking: exp.blocking,
      requestKind: requestKindOf(parsed),
    });
    return this._respond(res, exp, parsed);
  }

  _recordUnexpected(res, parsed, reason, candidates = []) {
    const sessId = parsed?.sessionId || '(no-session-id)';
    const msgs = parsed?.messages || [];
    const hasToolResults = msgs.some((m) => m?.role === 'tool' || m?.role === 'toolResult');
    const candidateLabels = candidates.map(({ expectation }) => `${expectation.id}@${laneLabel(expectation.lane)}`);
    this._state.unexpected.push({
      body: parsed,
      sessId,
      hasToolResults,
      reason,
      candidates: candidateLabels,
    });
    const lastUser = JSON.stringify(extractLastUserMsg(parsed));
    console.error(`[MOCK-500] reason=${reason} session=${sessId} tools=${JSON.stringify(extractToolNames(parsed))} msgs=${msgs.length} lastUser=${lastUser.slice(0, 400)} candidates=${JSON.stringify(candidateLabels)}`);
    return sendJSON(res, 500, { error: reason, sessionId: sessId, tools: extractToolNames(parsed) });
  }

  async _respondStream(res, chunks, exp) {
    res.writeHead(200, {
      'Content-Type': 'text/event-stream',
      'Cache-Control': 'no-cache',
      'Connection': 'keep-alive',
      'X-Accel-Buffering': 'no',
    });

    if (exp.respond.missingUsage) {
      for (const chunk of chunks) {
        delete chunk.usage;
      }
    }

    const delayFirstToken = exp.respond.delayFirstToken || 0;
    const delayDone = exp.respond.delayDone || 0;

    for (let i = 0; i < chunks.length; i++) {
      if (i === 0 && delayFirstToken > 0) {
        await new Promise((r) => setTimeout(r, delayFirstToken));
      }
      if (exp.respond.disconnectMidSse && i === Math.floor(chunks.length / 2)) {
        res.destroy();
        return;
      }
      res.write(`data: ${JSON.stringify(chunks[i])}\n\n`);
    }

    if (exp.respond.neverEnd) {
      return;
    }

    if (delayDone > 0) {
      await new Promise((r) => setTimeout(r, delayDone));
    }
    res.write('data: [DONE]\n\n');
    res.end();
  }

  async _respond(res, exp, parsed) {
    const id = `call_${Date.now()}`;
    const promptTokens = estimatePromptTokens(parsed);

    if (exp.respond.type === 'error') {
      return sendJSON(res, exp.respond.status || 500, exp.respond.body || { error: 'mock error' });
    }

    if (exp.respond.contextOverflow) {
      return sendJSON(res, 400, {
        error: {
          message: "This model's maximum context length is 100000 tokens.",
          type: "invalid_request_error",
          code: "context_length_exceeded"
        }
      });
    }

    if (exp.respond.type === 'disconnect') {
      return this._respondDisconnect(res, id);
    }

    let chunks;
    if (exp.respond.type === 'tool-call') {
      let args;
      if (typeof exp.respond.args === 'function') args = exp.respond.args(parsed);
      else args = { ...exp.respond.args };
      if (!this._state.strict) decorateLegacyArgs(args);

      const argsStr = exp.respond.malformedArgs ? '{malformed_json_arguments:' : JSON.stringify(args);

      if (exp.respond.toolCallAsText) {
        const text = `call tool ${exp.respond.tool} with args ${argsStr}`;
        chunks = buildTextChunks(id, text, promptTokens);
      } else if (exp.respond.fragmentArgs) {
        chunks = [];
        chunks.push({ id, object: 'chat.completion.chunk', created: 1, model: MOCK_MODEL, choices: [{ index: 0, delta: { role: 'assistant', content: null }, finish_reason: null }] });
        const fragSize = exp.respond.fragmentSize || 3;
        for (let i = 0; i < argsStr.length; i += fragSize) {
          const part = argsStr.slice(i, i + fragSize);
          chunks.push({
            id,
            object: 'chat.completion.chunk',
            created: 1,
            model: MOCK_MODEL,
            choices: [{
              index: 0,
              delta: {
                tool_calls: [{
                  index: 0,
                  id,
                  type: 'function',
                  function: { name: i === 0 ? exp.respond.tool : undefined, arguments: part }
                }]
              },
              finish_reason: null
            }]
          });
        }
        chunks.push({ id, object: 'chat.completion.chunk', created: 1, model: MOCK_MODEL, choices: [{ index: 0, delta: {}, finish_reason: 'tool_calls' }], usage: { prompt_tokens: promptTokens, completion_tokens: 100, total_tokens: promptTokens + 100 } });
      } else if (exp.respond.duplicateToolCallId) {
        chunks = [
          { id, object: 'chat.completion.chunk', created: 1, model: MOCK_MODEL, choices: [{ index: 0, delta: { role: 'assistant', content: null }, finish_reason: null }] },
          { id, object: 'chat.completion.chunk', created: 1, model: MOCK_MODEL, choices: [{ index: 0, delta: { tool_calls: [{ index: 0, id: id, type: 'function', function: { name: exp.respond.tool, arguments: argsStr } }] }, finish_reason: null }] },
          { id, object: 'chat.completion.chunk', created: 1, model: MOCK_MODEL, choices: [{ index: 0, delta: { tool_calls: [{ index: 0, id: id, type: 'function', function: { name: exp.respond.tool, arguments: argsStr } }] }, finish_reason: null }] },
          { id, object: 'chat.completion.chunk', created: 1, model: MOCK_MODEL, choices: [{ index: 0, delta: {}, finish_reason: 'tool_calls' }], usage: { prompt_tokens: promptTokens, completion_tokens: 100, total_tokens: promptTokens + 100 } },
        ];
      } else {
        chunks = buildToolCallChunks(id, exp.respond.tool, argsStr, promptTokens);
      }
    } else {
      if (exp.respond.emptyAssistant) {
        chunks = buildTextChunks(id, '', promptTokens);
      } else if (exp.respond.reasoningOnly) {
        const text = typeof exp.respond.reasoningOnly === 'string' ? exp.respond.reasoningOnly : 'thinking...';
        chunks = [
          { id, object: 'chat.completion.chunk', created: 1, model: MOCK_MODEL, choices: [{ index: 0, delta: { role: 'assistant', reasoning_content: text }, finish_reason: null }] },
          { id, object: 'chat.completion.chunk', created: 1, model: MOCK_MODEL, choices: [{ index: 0, delta: {}, finish_reason: 'stop' }], usage: { prompt_tokens: promptTokens, completion_tokens: 100, total_tokens: promptTokens + 100 } },
        ];
      } else {
        chunks = buildTextChunks(id, exp.respond.text, promptTokens);
      }
    }

    if (exp.respond.errorAfterToolCall) {
      res.writeHead(200, {
        'Content-Type': 'text/event-stream',
        'Cache-Control': 'no-cache',
        'Connection': 'keep-alive',
        'X-Accel-Buffering': 'no',
      });
      res.write(`data: ${JSON.stringify(chunks[0])}\n\n`);
      res.destroy();
      return;
    }

    return this._respondStream(res, chunks, exp);
  }

  _respondDisconnect(res, id) {
    res.writeHead(200, SSE_HEADERS);
    res.write(`data: ${JSON.stringify({ id, object: 'chat.completion.chunk', created: 1, model: MOCK_MODEL, choices: [{ index: 0, delta: { role: 'assistant' }, finish_reason: null }] })}\n\n`);
    res.destroy();
  }
}

export { extractToolNames };
