import assert from 'node:assert/strict';
import path from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';
import { runTest } from './runner.js';

const fixturePath = path.join(path.dirname(fileURLToPath(import.meta.url)), 'fixtures', 'hanging-test.js');

test('in-process timeout rejects and forgets the hung test', async () => {
  const startedAt = Date.now();

  await assert.rejects(
    runTest(fixturePath, 'hangs', 100),
    /TIMEOUT/
  );

  assert.ok(Date.now() - startedAt < 1000, 'timeout must reject within the timeout window');
});
