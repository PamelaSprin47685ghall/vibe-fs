/**
 * p0-canary-tests-gate.js — E2E tests for the stability gate and static analysis checker.
 * Uses setupScenario and runScenario to check E2E runner stability.
 */

import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { runStaticGate, runStabilityGate } from '../harness/stability-checker.js';

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
];

export default tests;
