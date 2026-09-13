/**
 * path-criterion-cases.mjs — fail-closed validation for runner/harness on missing roots.
 */

import { spawnSync } from 'node:child_process';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { assertEq, assertTrue, tmpScenarioDir } from './lib.mjs';

export const pathCriterionCases = [
  {
    name: 'VERIFY-004 real harness entry fails closed when test root is missing',
    fn: () => {
      const emptyRoot = tmpScenarioDir();
      const runnerPath = fileURLToPath(new URL('../../../run.mjs', import.meta.url));
      const res = spawnSync(process.execPath, [runnerPath, '--skip-staleness-check'], {
        cwd: emptyRoot,
        env: { ...process.env, TESTS_MJS_FILES: join(emptyRoot, 'nonexistent-test.mjs') },
        encoding: 'utf8',
      });
      assertTrue(res.status !== 0, 'harness runner must fail non-zero when test target does not exist');
    },
  },
];
