import fs from 'node:fs';
import path from 'node:path';

const isSilent = process.argv.includes('--silent');
const log = (...args) => { if (!isSilent) console.log(...args); };
const warn = (...args) => { if (!isSilent) console.warn(...args); };

const root = new URL('..', import.meta.url).pathname;
const buildDir = path.join(root, 'build');

// 1. Check build directory
if (!fs.existsSync(buildDir)) {
  console.error('Error: build/ missing.');
  process.exit(1);
}

// 2. Copy build-package.json to build/package.json
const pkgSrc = path.join(root, 'build-package.json');
const pkgDst = path.join(buildDir, 'package.json');
if (fs.existsSync(pkgSrc)) {
  fs.copyFileSync(pkgSrc, pkgDst);
  log('✓ Copied package.json');
} else {
  warn('Warning: build-package.json not found');
}

// 3. Recursively copy non-F# assets into build/
function syncAssets(sourceDir, targetDir) {
  if (!fs.existsSync(sourceDir)) return;
  fs.mkdirSync(targetDir, { recursive: true });
  
  const entries = fs.readdirSync(sourceDir, { withFileTypes: true });
  for (const entry of entries) {
    const srcPath = path.join(sourceDir, entry.name);
    const dstPath = path.join(targetDir, entry.name);

    if (entry.isDirectory()) {
      // Skip .git or node_modules if they somehow appear in source
      if (entry.name === '.git' || entry.name === 'node_modules') continue;
      fs.mkdirSync(dstPath, { recursive: true });
      syncAssets(srcPath, dstPath);
    } else if (!entry.name.endsWith('.fs')) {
      fs.copyFileSync(srcPath, dstPath);
    }
  }
}

log('Syncing assets...');
syncAssets(
  path.join(root, 'testkit'),
  path.join(buildDir, 'testkit')
);
log('✓ Assets synced');

// 4. Clean Fable artifacts
const gitignore = path.join(
  buildDir, 'fable_modules', '.gitignore'
);
if (fs.existsSync(gitignore)) {
  fs.rmSync(gitignore);
  log('✓ Cleaned .gitignore');
}

log('Postbuild done.');
