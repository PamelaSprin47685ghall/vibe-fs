import fs from 'node:fs';
import path from 'node:path';

const root = new URL('..', import.meta.url).pathname;
const targets = process.argv.slice(2);

for (const target of targets) {
  const resolved = path.resolve(root, target);
  if (resolved === root || resolved === path.dirname(root)) {
    throw new Error(`Refusing to remove unsafe build target: ${target}`);
  }
  fs.rmSync(resolved, { recursive: true, force: true });
}
