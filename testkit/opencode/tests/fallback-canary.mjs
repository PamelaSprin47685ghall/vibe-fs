/** fallback-canary — data-driven. Scenario: scripts/fallback.toml
 *
 * FALLBACK-003/004 is proven from the Host's settled transcript and journal:
 * a non-retryable provider error becomes a completed assistant error, the
 * controller advances the cursor once, and the real ProviderRetryAttempt
 * continuation succeeds under the same Logical Run. Success writes no cursor
 * fact, so Offset remains the one recorded successor rather than being reset.
 */
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

import { runCanary } from '../canary-driver.mjs';
import { runStaticGate } from '../index.js';

function runtimeFacts(workDir, factName) {
  const common = execFileSync('git', ['-C', workDir, 'rev-parse', '--git-common-dir'], {
    encoding: 'utf8',
  }).trim();
  const runtimeDir = path.join(
    path.isAbsolute(common) ? common : path.resolve(workDir, common),
    'wanxiangshu-next',
    'runtimes',
  );
  if (!fs.existsSync(runtimeDir)) return [];

  return fs.readdirSync(runtimeDir)
    .filter((name) => name.endsWith('.ndjson'))
    .flatMap((name) => fs.readFileSync(path.join(runtimeDir, name), 'utf8').split('\n'))
    .filter((line) => line.trim() !== '')
    .map((line) => JSON.parse(line))
    .filter((fact) => JSON.stringify(fact).includes(factName));
}

function fieldValues(value, fieldName, values = []) {
  if (value === null || value === undefined) return values;
  if (Array.isArray(value)) {
    for (const item of value) fieldValues(item, fieldName, values);
    return values;
  }
  if (typeof value !== 'object') return values;

  for (const [key, child] of Object.entries(value)) {
    if (key.toLowerCase() === fieldName.toLowerCase()) {
      if (typeof child === 'string' || typeof child === 'number') values.push(String(child));
      else if (child && typeof child === 'object') {
        if (typeof child.Value === 'string') values.push(child.Value);
        if (typeof child.value === 'string') values.push(child.value);
        // A typed identity serialises as `["Tag","value"]`; the value is the
        // payload, not the Fable tag.
        if (Array.isArray(child) && child.length === 2 && typeof child[1] === 'string') {
          values.push(child[1]);
        }
      }
    }
    fieldValues(child, fieldName, values);
  }
  return values;
}

function messagesOf(response) {
  const data = response?.data?.data ?? response?.data;
  return Array.isArray(data) ? data : [];
}

function messageInfo(message) {
  return message?.info ?? message ?? {};
}

function assistantMessages(messages) {
  return messages.filter((message) => messageInfo(message).role === 'assistant');
}

function completedTime(message) {
  const info = messageInfo(message);
  return info.time?.completed ?? message?.time?.completed ?? info.completed ?? message?.completed;
}

function errorName(message) {
  const info = messageInfo(message);
  const error = info.error ?? message?.error;
  return error?.name ?? error?.type ?? info.errorName ?? message?.errorName;
}

async function oracle(scenario, ctx) {
  const response = await scenario.client.messages(ctx.sessionId);
  assert.ok(response.ok, `failed to read Host transcript: ${JSON.stringify(response.data)}`);

  const messages = messagesOf(response);
  const assistants = assistantMessages(messages);
  const failed = assistants.find((message) => completedTime(message) !== undefined && errorName(message));
  assert.ok(
    failed,
    `settled non-retryable failure must be an assistant with time.completed and error.name: ${JSON.stringify(messages)}`,
  );

  const transcript = JSON.stringify(messages);
  assert.ok(
    transcript.includes('fallback continuation completed.'),
    `same-run ProviderRetryAttempt must complete in the Host transcript: ${transcript}`,
  );

  const facts = runtimeFacts(scenario.host.workDir, 'FallbackCursorAdvanced');
  assert.equal(facts.length, 1, 'one settled provider run must advance the cursor once');
  assert.deepEqual(fieldValues(facts, 'PreviousOffset'), ['0'], 'first failure starts at Offset 0');
  assert.deepEqual(fieldValues(facts, 'NextOffset'), ['1'], 'first failure advances to Offset 1');
  assert.deepEqual(fieldValues(facts, 'ConsecutiveFailureCount'), ['1'], 'failure fact records count 1');
  assert.equal(new Set(fieldValues(facts, 'LogicalRunId')).size, 1, 'failure and continuation stay in one Logical Run');
  assert.equal(new Set(fieldValues(facts, 'ProviderRun')).size, 1, 'one ProviderRun is deduplicated to one failure fact');
}

if (!runStaticGate([fileURLToPath(import.meta.url)]).passed) {
  throw new Error('fallback canary static gate failed');
}
process.exit(await runCanary('fallback', { customs: { oracle } }));
