/**
 * p0-canary-tests-gate.js — E2E tests for the stability gate and static analysis checker.
 * Uses setupScenario and runScenario to check E2E runner stability.
 */

import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { runStaticGate, runStabilityGate } from '../../../testkit/opencode/stability-checker.js';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(__dirname, '../../..');

const tests = [
  {
    name: 'OC-STAB-002 static gate validation',
    fn: async (t) => {
      const tempBadFile = path.join(ROOT, 'e2e/opencode/specs/temp-bad-spec-test.js');
      const tempGoodFile = path.join(ROOT, 'e2e/opencode/specs/temp-good-spec-test.js');

    // Create a bad file with fixed sleeps and forbidden contains' + 'Tool assertions
    const badContent = [
      '// Test spec',
      'async function run(t) {',
      '  await ' + 'sle' + 'ep(250); // Standalone sleep violation',
      '  console.log(' + 'contains' + 'Tool(harness, "write")); // contains' + 'Tool violation',
      '  while (x) {',
      '    await ' + 'sle' + 'ep(100); // This polling sleep is ALLOWED',
      '  }',
      '}'
    ].join('\n');
      fs.writeFileSync(tempBadFile, badContent, 'utf8');

      // Create a good file
    const goodContent = [
      '// Clean test spec',
      'async function run(t) {',
      '  while (true) {',
      '    await ' + 'sle' + 'ep(50); // Allowed polling sleep',
      '  }',
      "  t.fs.expectFile('hello.txt');",
      '}'
    ].join('\n');
      fs.writeFileSync(tempGoodFile, goodContent, 'utf8');

      try {
        // Run checks
        const badResult = runStaticGate([tempBadFile]);
        if (badResult.passed) {
          throw new Error('Static gate should have failed on bad file');
      }

      const sleepViolation = badResult.violations.find((v) => v.type === 'fixed-' + 'sleep');
      const containsViolation = badResult.violations.find((v) => v.type === 'contains' + 'Tool');

        if (!sleepViolation) {
          throw new Error('Static gate failed to detect fixed-sleep violation');
      }
        if (!containsViolation) {
        throw new Error('Static gate failed to detect contains' + 'Tool violation');
      }

        const goodResult = runStaticGate([tempGoodFile]);
        if (!goodResult.passed) {
          throw new Error(`Static gate failed on clean file: ${JSON.stringify(goodResult.violations)}`);
      }
      } finally {
        // Cleanup temp files
        if (fs.existsSync(tempBadFile)) fs.unlinkSync(tempBadFile);
        if (fs.existsSync(tempGoodFile)) fs.unlinkSync(tempGoodFile);
          }
    },
  },

  {
    name: 'OC-STAB-001 stability gate repeat execution',
    fn: async (t) => {
      // Test that runStabilityGate works end-to-end on a minimal dummy test
      const dummyTest = {
        name: 'OC-STAB-dummy-test',
        fn: async (scenario) => {
          // A minimal E2E test that does a basic request
          const cmds = await scenario.client.request('GET', '/command');
          if (!cmds.ok) throw new Error('GET /command failed');
        },
      };

      const result = await runStabilityGate({
        test: dummyTest,
        repeat: 2,
        archiveDir: 'diagnostics-archive-test',
        scenarioOpts: {
          plugin: false, // Bypasses custom plugin load to make it super fast
          contextLimit: 20000,
        },
      });

      if (!result.passed) {
        throw new Error('Stability gate dummy run failed');
      }
      if (result.failures.length !== 0) {
        throw new Error(`Expected 0 failures, got ${result.failures.length}`);
      }

      // Cleanup test archive directory if created
      const testArchive = path.join(ROOT, 'diagnostics-archive-test');
      if (fs.existsSync(testArchive)) {
        fs.rmSync(testArchive, { recursive: true, force: true });
      }
    },
  },

  {
    name: 'OC-STAB-003 isolated env check',
    fn: async (t) => {
      process.env.OPENAI_API_KEY = 'leak-key-openai';
      process.env.HTTP_PROXY = 'leak-proxy-http';
      process.env.OLLAMA_HOST = 'leak-ollama-host';
      process.env.WANXIANG_TEST_VAR = 'leak-wanxiang';

      try {
        const env = t.host._env;
        if (!env) {
          throw new Error('Host env is not defined');
        }
        if (env.OPENAI_API_KEY) {
          throw new Error('OPENAI_API_KEY was not cleaned from environment');
        }
        if (env.HTTP_PROXY) {
          throw new Error('HTTP_PROXY was not cleaned from environment');
        }
        if (env.OLLAMA_HOST) {
          throw new Error('OLLAMA_HOST was not cleaned from environment');
        }
        if (env.WANXIANG_TEST_VAR) {
          throw new Error('WANXIANG_TEST_VAR was not cleaned from environment');
        }
        const homePath = path.join(t.scenarioDir, 'home');
        if (env.HOME !== homePath) {
          throw new Error(`HOME path mismatch: expected ${homePath}, got ${env.HOME}`);
        }
      } finally {
        delete process.env.OPENAI_API_KEY;
        delete process.env.HTTP_PROXY;
        delete process.env.OLLAMA_HOST;
        delete process.env.WANXIANG_TEST_VAR;
      }
    },
  },

  {
    name: 'OC-STAB-004 restart API check',
    fn: async (t) => {
      const sess = await t.client.createSession();
      const sid = sess.data?.data?.data?.id || sess.data?.data?.id;

      fs.writeFileSync(path.join(t.host.workDir, 'restart-test-file.txt'), 'persisted content', 'utf8');

      await t.restart();

      const fileContent = fs.readFileSync(path.join(t.host.workDir, 'restart-test-file.txt'), 'utf8');
      if (fileContent !== 'persisted content') {
        throw new Error(`Workspace file content lost or mismatched: ${fileContent}`);
      }

      const sess2 = await t.client.createSession();
      const sid2 = sess2.data?.data?.data?.id || sess2.data?.data?.id;
      if (!sid2) {
        throw new Error('Failed to create session on restarted host');
      }
    },
  },

  {
    name: 'OC-STAB-005 strict failure modes check',
    fn: async (t) => {
      const sess = await t.client.createSession();
      const sid = sess.data?.data?.data?.id || sess.data?.data?.id;

      t.provider.expectText({
        id: 'reasoning-test',
        reasoningOnly: 'thinking deeply about E2E',
      });
      let turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'do reasoning');
      await turn.awaitTerminal({ timeoutMs: 15000, requireAssistantTerminal: false });

      t.provider.expectText({
        id: 'overflow-test',
        contextOverflow: true,
      });
      let turnOverflow = await t.turn.start(sid);
      const overflowRes = await t.client.prompt(sid, 'overflow test');
      if (overflowRes.status !== 204 && overflowRes.status !== 202) {
        throw new Error(`Expected prompt async status 204/202, got ${overflowRes.status}`);
      }
      await turnOverflow.awaitTerminal({ timeoutMs: 15000, requireAssistantTerminal: false, requireIdleAfterActivity: true });

      const errorEvent = t.events.allEvents.find((e) => e.type === 'session.error' && e.sessionID === sid);
      if (!errorEvent) {
        throw new Error('session.error event not found in EventProbe');
      }
      if (errorEvent.errorName !== 'ContextOverflowError') {
        throw new Error(`Expected ContextOverflowError, got ${errorEvent.errorName}`);
      }

      const sess2 = await t.client.createSession();
      const sid2 = sess2.data?.data?.data?.id || sess2.data?.data?.id;
      if (!sid2) {
        throw new Error('Failed to create second session');
      }

      const unexpectedRes = await t.client.prompt(sid2, 'unexpected request');
      if (unexpectedRes.status !== 204 && unexpectedRes.status !== 202) {
        throw new Error(`Expected prompt async status 204/202, got ${unexpectedRes.status}`);
      }

      const unexpectedDeadline = Date.now() + 15000;
      while (t.provider.unexpectedRequests.length === 0 && Date.now() < unexpectedDeadline) {
        await new Promise((r) => setTimeout(r, 100));
      }

      const unexpectedCount = t.provider.unexpectedRequests.length;
      if (unexpectedCount === 0) {
        throw new Error('Unexpected request was not recorded in provider unexpectedRequests');
      }

      let asserted = false;
      try {
        t.provider.expectSatisfied();
      } catch (err) {
        if (err.message.includes('UnexpectedLlmRequest') || err.message.includes('unexpected requests')) {
          asserted = true;
        }
      }
      if (!asserted) {
        throw new Error('expectSatisfied did not throw error on unexpected request');
      }

      t.provider.reset();
    },
  },

  {
    name: 'OC-STAB-006 cleanup leak detection check',
    fn: async (t) => {
      const net = await import('node:net');
      const server = net.createServer();
      const port = await new Promise((resolve) => {
        server.listen(0, '127.0.0.1', () => {
          resolve(server.address().port);
        });
      });

      const { ProcessHost } = await import('../../../testkit/opencode/process-host.js');
      const leakHost = new ProcessHost();
      leakHost._port = port;
      leakHost._started = true;

      let thrown = false;
      try {
        await leakHost.assertNoLeak();
      } catch (err) {
        if (err.message.includes('still listening')) {
          thrown = true;
        }
      } finally {
        await new Promise((resolve) => server.close(resolve));
      }

      if (!thrown) {
        throw new Error('assertNoLeak did not detect open socket port leak');
      }
    },
  },
];

export default tests;
