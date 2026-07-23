import fs from 'node:fs';
import { spawn } from 'node:child_process';

export function hangsAfterSpawningChild() {
  const pidFile = process.env.TEST_RUNNER_PID_FILE;

  if (!pidFile) {
    throw new Error('TEST_RUNNER_PID_FILE is required');
  }

  const child = spawn(process.execPath, ['-e', 'setInterval(() => {}, 1000)'], {
    stdio: 'ignore'
  });

  fs.writeFileSync(pidFile, String(child.pid));
  return new Promise(() => {});
}
