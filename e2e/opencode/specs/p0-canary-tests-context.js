/**
 * p0-canary-tests-context.js — Context budget / provider / session model P0 tests.
 * Kept under the 300-line Kolmogorov line budget.
 */

import { getSessionId, runScenario } from '../harness/scenario.js';
import { TIMEOUTS } from './p0-canary-utils.js';
import { pathToFileURL } from 'node:url';

function assertSessionStatus(res, sid) {
  if (!res.ok) throw new Error(`GET /session/${sid} failed: ${res.status} ${JSON.stringify(res.data)}`);
  if (!res.data || typeof res.data !== 'object') {
    throw new Error(`GET /session/${sid} returned non-object: ${JSON.stringify(res.data)}`);
  }
}

function findTestModel(providerRes) {
  if (!providerRes.ok) throw new Error(`GET /provider failed: ${providerRes.status} ${JSON.stringify(providerRes.data)}`);
  const all = providerRes.data?.all;
  if (!Array.isArray(all)) throw new Error(`/provider response missing all array: ${JSON.stringify(Object.keys(providerRes.data || {}))}`);
  const testProvider = all.find((p) => p?.id === 'test');
  if (!testProvider) throw new Error(`Provider "test" not found in /provider catalog: got ${all.map((p) => p?.id).join(', ')}`);
  const model = testProvider.models?.['test-model'];
  if (!model) throw new Error(`Model "test/test-model" not found in /provider catalog`);
  return { provider: testProvider, model };
}

const tests = [
  {
    name: 'OC-CB-001 /provider exposes real test-model input limit',
    fn: async (t) => {
      const providerRes = await t.client.request('GET', '/provider');
      const { model } = findTestModel(providerRes);

      if (!model.limit || typeof model.limit !== 'object') {
        throw new Error(`test-model limit missing: ${JSON.stringify(model)}`);
      }
      if (typeof model.limit.input !== 'number' || model.limit.input <= 0) {
        throw new Error(`test-model limit.input missing or invalid: ${JSON.stringify(model.limit)}`);
      }
      if (typeof model.limit.context !== 'number' || model.limit.context <= 0) {
        throw new Error(`test-model limit.context missing or invalid: ${JSON.stringify(model.limit)}`);
      }
      if (typeof model.limit.output !== 'number' || model.limit.output <= 0) {
        throw new Error(`test-model limit.output missing or invalid: ${JSON.stringify(model.limit)}`);
      }

      // The configured context limit must match between input and context limits.
      if (model.limit.input !== model.limit.context) {
        throw new Error(`test-model limit.input (${model.limit.input}) !== limit.context (${model.limit.context})`);
      }

      // Sanity: the harness-configured limit is positive and finite.
      if (!Number.isFinite(model.limit.input)) {
        throw new Error(`test-model limit.input is not finite: ${model.limit.input}`);
      }
    },
  },

  {
    name: 'OC-CB-002 session API returns real model and providerID',
    fn: async (t) => {
      const sess = await t.client.createSession();
      const sid = getSessionId(sess);
      if (!sid) throw new Error('createSession did not return a session ID');

      const statusRes = await t.client.sessionStatus(sid);
      assertSessionStatus(statusRes, sid);

      const model = statusRes.data.model;
      if (!model || typeof model !== 'object') {
        throw new Error(`session status missing model: ${JSON.stringify(statusRes.data)}`);
      }
      if (model.providerID !== 'test') {
        throw new Error(`session providerID mismatch: expected "test", got ${JSON.stringify(model.providerID)}`);
      }
      if (model.id !== 'test-model') {
        throw new Error(`session model id mismatch: expected "test-model", got ${JSON.stringify(model.id)}`);
      }

      // The session model must be present in the provider catalog.
      const providerRes = await t.client.request('GET', '/provider');
      findTestModel(providerRes);
    },
  },

  {
    name: 'OC-CB-003 usage comes from real session token data',
    fn: async (t) => {
      const sess = await t.client.createSession();
      const sid = getSessionId(sess);
      if (!sid) throw new Error('createSession did not return a session ID');

      t.provider.expectText({ id: 'usage-warm', text: 'ok' });
      t.provider.expectNoMoreRequests();

      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'say ok');
      await turn.awaitTerminal({ timeoutMs: TIMEOUTS.quick });

      const statusRes = await t.client.sessionStatus(sid);
      assertSessionStatus(statusRes, sid);

      const tokens = statusRes.data.tokens;
      if (!tokens || typeof tokens !== 'object') {
        throw new Error(`session status missing real token object: ${JSON.stringify(statusRes.data)}`);
      }

      // Explicitly reject fabricated string-length proxies: demand numeric fields from the API.
      const diagnostics = {
        input: tokens.input,
        output: tokens.output,
        reasoning: tokens.reasoning,
        cache: tokens.cache,
      };

      if (typeof tokens.input !== 'number' || !Number.isFinite(tokens.input) || tokens.input <= 0) {
        throw new Error(`Real usage token.input missing or non-positive: ${JSON.stringify(diagnostics)}`);
      }
      if (typeof tokens.output !== 'number' || !Number.isFinite(tokens.output) || tokens.output < 0) {
        throw new Error(`Real usage token.output missing or negative: ${JSON.stringify(diagnostics)}`);
      }
      if (typeof tokens.reasoning !== 'number' || !Number.isFinite(tokens.reasoning)) {
        throw new Error(`Real usage token.reasoning missing or non-numeric: ${JSON.stringify(diagnostics)}`);
      }
      if (!tokens.cache || typeof tokens.cache !== 'object') {
        throw new Error(`Real usage token.cache missing or non-object: ${JSON.stringify(diagnostics)}`);
      }
      if (typeof tokens.cache.read !== 'number' || typeof tokens.cache.write !== 'number') {
        throw new Error(`Real usage token.cache.read/write missing or non-numeric: ${JSON.stringify(diagnostics)}`);
      }
    },
  },

  {
    name: 'OC-CB-004 below threshold does not inject budget nudge',
    fn: async (t) => {
      const sess = await t.client.createSession();
      const sid = getSessionId(sess);
      if (!sid) throw new Error('createSession did not return a session ID');

      t.provider.expectText({ id: 'below-th', text: 'ok' });
      t.provider.expectNoMoreRequests();

      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'say ok');
      await turn.awaitTerminal({ timeoutMs: TIMEOUTS.quick });

      const nudges = t.provider.syntheticRequests.filter((r) => r.marker === 'budget-nudge');
      if (nudges.length !== 0) {
        throw new Error(`Unexpected budget-nudge synthetic requests below threshold: ${nudges.length}`);
      }

      const statusRes = await t.client.sessionStatus(sid);
      assertSessionStatus(statusRes, sid);
      const input = Number(statusRes.data?.tokens?.input) || 0;

      // With a 20k context limit, the 75% bootstrap hard-safety threshold is 15k.
      // A short prompt should remain well below it.
      if (input >= 15000) {
        throw new Error(`Below-threshold test produced too many input tokens: ${input}`);
      }

      const messages = (await t.client.messages(sid)).data || [];
      const todowriteParts = messages
        .flatMap((m) => m.parts || [])
        .filter((p) => p.type === 'tool' && p.tool === 'todowrite');
      if (todowriteParts.length > 0) {
        throw new Error(`Unexpected todowrite tool result below threshold: ${JSON.stringify(todowriteParts)}`);
      }
    },
  },
];

export default tests;

// Standalone runner so this spec can be executed directly without modifying p0-canary.js.
if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  await runScenario(
    {
      plugin: true,
      contextLimit: 20000,
      timeoutMs: 90000,
      allowSynthetic: true,
      allowTitleGen: true,
    },
    tests,
  );
}
