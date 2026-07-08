import { spawnSync } from 'child_process';
import { performance } from 'perf_hooks';

const startTime = performance.now();

const fableResult = spawnSync('dotnet', ['fable', 'wanxiangshu.fsproj', '--outDir', 'build'], {
  stdio: 'inherit',
  env: { ...process.env, FORCE_COLOR: '1' }
});

if (fableResult.status !== 0) {
  process.exit(fableResult.status ?? 1);
}

const postbuildResult = spawnSync('node', ['scripts/postbuild.mjs'], {
  stdio: 'inherit',
  env: { ...process.env, FORCE_COLOR: '1' }
});

if (postbuildResult.status !== 0) {
  process.exit(postbuildResult.status ?? 1);
}

const testResult = spawnSync('node', ['tests/e2e.js'], {
  stdio: 'inherit',
  env: { ...process.env, FORCE_COLOR: '1' }
});

const endTime = performance.now();
const elapsed = ((endTime - startTime) / 1000).toFixed(2);

if (testResult.status !== 0) {
  process.exit(testResult.status ?? 1);
}

console.log(`Build and test completed in ${elapsed}s`);
