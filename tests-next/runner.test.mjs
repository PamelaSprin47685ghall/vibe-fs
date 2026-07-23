import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';
import { runTestInWorker } from './runner.js';

const fixturePath = path.join(path.dirname(fileURLToPath(import.meta.url)), 'fixtures', 'hanging-test.js');

function processIsRunning(pid) {
  try {
    process.kill(pid, 0);

    if (process.platform === 'linux') {
      const state = fs.readFileSync(`/proc/${pid}/stat`, 'utf8').split(' ')[2];
      return state !== 'Z';
    }

    return true;
  } catch (error) {
    if (error.code === 'ESRCH' || error.code === 'ENOENT') return false;
    throw error;
  }
}

async function waitUntilStopped(pid) {
  for (let attempt = 0; attempt < 100 && processIsRunning(pid); attempt++) {
    await new Promise(setImmediate);
  }
}

test('hard timeout rejects and kills the isolated test process tree', async () => {
  const pidFile = path.join(os.tmpdir(), `wanxiangshu-runner-${process.pid}.pid`);
  let spawnedPid;

  process.env.TEST_RUNNER_PID_FILE = pidFile;

  try {
    const startedAt = Date.now();

    await assert.rejects(
      runTestInWorker(fixturePath, 'hangsAfterSpawningChild', 100),
      /TIMEOUT: Assertion step/
    );

    assert.ok(Date.now() - startedAt < 1000, 'timeout must reject without awaiting worker shutdown');
    spawnedPid = Number(fs.readFileSync(pidFile, 'utf8'));
    await waitUntilStopped(spawnedPid);
    assert.equal(processIsRunning(spawnedPid), false, 'timeout must not orphan child processes');
  } finally {
    delete process.env.TEST_RUNNER_PID_FILE;
    fs.rmSync(pidFile, { force: true });

    if (spawnedPid && processIsRunning(spawnedPid)) {
      process.kill(spawnedPid, 'SIGKILL');
    }
  }
});
