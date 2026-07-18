import fs from 'node:fs';
import path from 'node:path';
import { execSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));

function resolveRepoRoot() {
  let cur = __dirname;
  while (cur && cur !== '/' && !fs.existsSync(path.join(cur, 'package.json'))) {
    cur = path.dirname(cur);
  }
  return cur;
}

export const REPO_ROOT = resolveRepoRoot();

export function resolvePluginJs() {
  const candidates = [
    path.join(REPO_ROOT, 'build', 'src', 'Hosts', 'OpenCode', 'PluginWanxiangzhen.js'),
    path.join(REPO_ROOT, '..', '..', 'build', 'src', 'Hosts', 'OpenCode', 'PluginWanxiangzhen.js'),
    path.join(REPO_ROOT, '..', 'build', 'src', 'Hosts', 'OpenCode', 'PluginWanxiangzhen.js'),
  ];
  for (const c of candidates) {
    if (fs.existsSync(c)) return c;
  }
  return candidates[0];
}

export const PLUGIN_JS = resolvePluginJs();
export const BUILD_SRC = path.join(REPO_ROOT, 'build');

export function gitInit(tmpDir, opts = {}) {
  const masterBranch = opts.masterBranch || 'main';
  execSync('git init', { cwd: tmpDir, stdio: 'ignore' });
  fs.writeFileSync(path.join(tmpDir, '.gitignore'), 'node_modules/\n.wanxiangzhen-e2e-meta.json\n.wanxiangshu.ndjson\n');
  execSync('git add .gitignore', { cwd: tmpDir, stdio: 'ignore' });
  const agentsMd = `---
squad:
  terminal: headless
  masterBranch: ${masterBranch}
  maxConcurrent: 100
  sharedDirs: []
---
`;
  fs.writeFileSync(path.join(tmpDir, 'AGENTS.md'), agentsMd);
  execSync('git add AGENTS.md', { cwd: tmpDir, stdio: 'ignore' });
  execSync('git config user.email e2e@test', { cwd: tmpDir, stdio: 'ignore' });
  execSync('git config user.name e2e', { cwd: tmpDir, stdio: 'ignore' });
  execSync('git commit -m init', { cwd: tmpDir, stdio: 'ignore' });
  try {
    execSync(`git branch -M ${masterBranch}`, { cwd: tmpDir, stdio: 'ignore' });
  } catch {}
}