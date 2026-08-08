/**
 * process-stress — data-driven. Scenario: scenarios/process-stress.toml
 *
 * EXEC-011 evidence: the executor tool result the model sees must report the
 * timeout. Production renders ProcessError.TimeoutExceeded into the result body
 * (ProcessRunner.fs:110 → ExecutorTool.fs:114 ToString), so the provider wire
 * must carry 'TimeoutExceeded' in a tool/toolResult message. The data-driven
 * rewrite (f355efdb) dropped this assertion; the oracle restores it.
 */
import assert from 'node:assert/strict';
import { runCanary } from '../support/scenario-driver.mjs';
import { fileURLToPath } from 'node:url';
import { runStaticGate } from '../support/index.js';

const TIMEOUT_MARKER = 'TimeoutExceeded';

/** All text carried by tool/toolResult messages on the provider wire. */
function toolResultTexts(scenario) {
  const texts = [];
  for (const request of scenario.provider.requests ?? []) {
    for (const message of request.messages ?? []) {
      if (message?.role !== 'tool' && message?.role !== 'toolResult') continue;
      const content = message?.content;
      if (typeof content === 'string') {
        texts.push(content);
      } else if (Array.isArray(content)) {
        for (const chunk of content) {
          if (typeof chunk === 'string') texts.push(chunk);
          else if (chunk?.type === 'text' && typeof chunk?.text === 'string') texts.push(chunk.text);
        }
      }
      for (const part of message?.parts ?? []) {
        if (part?.type === 'text' && typeof part?.text === 'string') texts.push(part.text);
      }
    }
  }
  return texts;
}

async function assertTimeoutReported(scenario) {
  const texts = toolResultTexts(scenario);
  assert.ok(
    texts.some((text) => text.includes(TIMEOUT_MARKER)),
    `no executor tool result reports the timeout; marker='${TIMEOUT_MARKER}'\n` +
      texts.map((text) => text.slice(0, 200)).join('\n---\n'),
  );
}

if (!runStaticGate([fileURLToPath(import.meta.url)]).passed) {
  throw new Error('process-stress canary static gate failed');
}
process.exit(
  await runCanary('process-stress', {
    customs: { assertTimeoutReported },
  }),
);
