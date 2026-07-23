/**
 * p0-canary-tests-tools.js — Standalone tooling E2E spec for file/executor/fuzzy gaps.
 * Kept under the 300-line Kolmogorov line budget.
 */

import { getSessionId } from '../../../testkit/opencode/scenario.js';
import { content, writeWorkFile, findToolPart, TIMEOUTS } from './p0-canary-utils.js';
import fs from 'node:fs';
import YAML from 'yaml';

function expectNoSessionError(t, sid) {
  t.events.expectCount({ type: 'session.error', sessionID: sid, count: 0 });
}

export function parseToolOutput(text) {
  if (!text || !text.startsWith('---\n')) {
    return { metadata: {}, body: text || '' };
  }
  let endIdx = text.indexOf('\n---\n', 4);
  let fenceLength = 5;
  if (endIdx === -1) {
    if (text.endsWith('\n---')) {
      endIdx = text.length - 4;
      fenceLength = 4;
    }
  }
  if (endIdx === -1) {
    return { metadata: {}, body: text };
  }
  const yamlText = text.slice(4, endIdx);
  const body = text.slice(endIdx + fenceLength);
  try {
    const metadata = YAML.parse(yamlText) || {};
    return { metadata, body };
  } catch (e) {
    return { metadata: {}, body };
  }
}

function countOccurrences(text, sub) {
  return text.split(sub).length - 1;
}

const tests = [
  {
    name: 'OC-FILE-013 write preserves exact byte length and content',
    fn: async (t) => {
      const sess = await t.client.createSession();
      const sid = getSessionId(sess);
      const byteFact = content('byte fact: 🧪 你好');
      t.provider.expectToolCall({
        id: 'tools-write-bytes',
        tool: 'write',
        args: { filePath: 'bytes.txt', content: byteFact },
      });
      t.provider.expectText({ id: 'tools-write-bytes-done', text: 'done' });
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'Write bytes.txt with exact content "byte fact: 🧪 你好\\n"');
      await turn.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });

      const messages = (await t.client.messages(sid)).data || [];
      const part = findToolPart(messages, 'write', (p) => p.state?.input?.filePath === 'bytes.txt');
      if (!part) throw new Error('write tool part not found');
      if (part.state?.status !== 'completed') throw new Error(`write state: ${part.state?.status}`);
      if (part.state?.input?.content !== byteFact) {
        throw new Error(`write input content mismatch: ${JSON.stringify(part.state?.input?.content)}`);
      }

      t.fs.expectFile('bytes.txt');
      t.fs.expectFileContent('bytes.txt', byteFact);
      const size = fs.statSync(t.host.workDir + '/bytes.txt').size;
      const expectedBytes = Buffer.byteLength(byteFact, 'utf8');
      if (size !== expectedBytes) {
        throw new Error(`file size mismatch: expected ${expectedBytes} bytes, got ${size}`);
      }

      expectNoSessionError(t, sid);
    },
  },

  {
    name: 'OC-EXEC-013 executor returns structured non-zero exit and stderr',
    fn: async (t) => {
      const sess = await t.client.createSession();
      const sid = getSessionId(sess);
      t.provider.expectToolCall({
        id: 'tools-exec-error',
        tool: 'executor',
        args: {
          language: 'shell',
          command: 'echo "tooling-stderr-marker" >&2; exit 7',
          timeout_type: 'short',
          mode: 'ro',
          what_to_summarize: 'structured error',
          max_bytes: 1000,
        },
      });
      t.provider.expectText({ id: 'tools-exec-error-done', text: 'done' });
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'run a shell command that prints to stderr and exits 7');
      await turn.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });

      const messages = (await t.client.messages(sid)).data || [];
      const part = findToolPart(messages, 'executor');
      if (!part) throw new Error('executor tool part not found');
      const parsed = parseToolOutput(part.state.output);
      if (parsed.metadata.status !== 'exit_error') {
        throw new Error(`expected status exit_error, got ${parsed.metadata.status}`);
      }
      if (parsed.metadata.exit_code !== 7) {
        throw new Error(`expected exit_code 7, got ${parsed.metadata.exit_code}`);
      }
      if (!parsed.body.includes('tooling-stderr-marker')) {
        throw new Error(`stderr marker missing from executor body: ${parsed.body}`);
      }

      expectNoSessionError(t, sid);
    },
  },

  {
    name: 'OC-FUZZY-009 fuzzy_grep no match returns explicit empty result',
    fn: async (t) => {
      writeWorkFile(t.host.workDir, 'empty_grep_target.txt', content('alpha\nbeta'));
      const sess = await t.client.createSession();
      const sid = getSessionId(sess);
      t.provider.expectToolCall({
        id: 'tools-grep-empty',
        tool: 'fuzzy_grep',
        args: { pattern: ['zzzz_no_match_12345'], limit: 50 },
      });
      t.provider.expectText({ id: 'tools-grep-empty-done', text: 'done' });
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'grep for zzzz_no_match_12345');
      await turn.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });

      const messages = (await t.client.messages(sid)).data || [];
      const part = findToolPart(messages, 'fuzzy_grep');
      if (!part) throw new Error('fuzzy_grep tool part not found');
      const parsed = parseToolOutput(part.state.output);
      if (!parsed.body.includes('No matches found')) {
        throw new Error(`expected empty result, got: ${parsed.body}`);
      }
      if (parsed.metadata.iterator) {
        throw new Error(`expected no iterator for empty result, got ${parsed.metadata.iterator}`);
      }

      expectNoSessionError(t, sid);
    },
  },

  {
    name: 'OC-FUZZY-010 fuzzy_grep pagination boundary continues until exhausted',
    fn: async (t) => {
      const marker = 'tooling-boundary-line-';
      writeWorkFile(t.host.workDir, 'paginate_grep_target.txt', content(`${marker}1\n${marker}2\n${marker}3`));
      const sess = await t.client.createSession();
      const sid = getSessionId(sess);

      t.provider.expectToolCall({
        id: 'tools-grep-page-1',
        tool: 'fuzzy_grep',
        args: { pattern: [marker], limit: 2 },
      });
      t.provider.expectText({ id: 'tools-grep-page-1-done', text: 'continue' });
      const turn1 = await t.turn.start(sid);
      await t.client.prompt(sid, `grep for ${marker} with limit 2`);
      await turn1.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });

      const messages1 = (await t.client.messages(sid)).data || [];
      const part1 = findToolPart(messages1, 'fuzzy_grep');
      if (!part1) throw new Error('fuzzy_grep page 1 not found');
      const parsed1 = parseToolOutput(part1.state.output);
      const page1Count = countOccurrences(parsed1.body, marker);
      if (page1Count !== 2) {
        throw new Error(`expected 2 matches on page 1, got ${page1Count} in ${parsed1.body}`);
      }
      const iterator = parsed1.metadata.iterator;
      if (!iterator) throw new Error(`page 1 did not return iterator: ${part1.state.output}`);

      t.provider.expectToolCall({
        id: 'tools-grep-page-2',
        tool: 'fuzzy_continue',
        args: () => ({ iterator }),
      });
      t.provider.expectText({ id: 'tools-grep-page-2-done', text: 'done' });
      const turn2 = await t.turn.start(sid);
      await t.client.prompt(sid, 'continue to the next page of grep results');
      await turn2.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });

      const messages2 = (await t.client.messages(sid)).data || [];
      const part2 = findToolPart(messages2, 'fuzzy_continue');
      if (!part2) throw new Error('fuzzy_continue page 2 not found');
      const parsed2 = parseToolOutput(part2.state.output);
      const page2Count = countOccurrences(parsed2.body, marker);
      if (page2Count !== 1) {
        throw new Error(`expected 1 match on page 2, got ${page2Count} in ${parsed2.body}`);
      }
      if (parsed2.metadata.iterator) {
        throw new Error(`expected iterator exhausted on page 2, got ${parsed2.metadata.iterator}`);
      }

      expectNoSessionError(t, sid);
    },
  },
];

export default tests;
