import fs from 'fs';
import path from 'path';

function log(msg) {
    console.log(`[setup-git-hooks] ${msg}`);
}

function warn(msg) {
    console.warn(`[setup-git-hooks] [Warning] ${msg}`);
}

async function run() {
    const gitDir = path.resolve('.git');
    try {
        const stat = fs.statSync(gitDir);
        if (!stat.isDirectory()) {
            warn('.git is not a directory. Skipping git hook installation.');
            process.exit(0);
        }
    } catch {
        warn('.git directory not found. Skipping git hook installation.');
        process.exit(0);
    }

    const hooksDir = path.join(gitDir, 'hooks');
    try {
        if (!fs.existsSync(hooksDir)) {
            fs.mkdirSync(hooksDir, { recursive: true });
        }
    } catch (e) {
        warn(`Failed to create git hooks directory: ${e.message}`);
        process.exit(0);
    }

    const hookPath = path.join(hooksDir, 'pre-commit');
    const hookScript = `#!/bin/sh
# wanxiangshu pre-commit hook: Auto-formats staged .fs and .xml files
node scripts/pre-commit-formatter.mjs
`;

    try {
        fs.writeFileSync(hookPath, hookScript, { encoding: 'utf8', mode: 0o755 });
        log(`Successfully installed git pre-commit hook to ${hookPath}`);
    } catch (e) {
        warn(`Failed to write git pre-commit hook: ${e.message}`);
    }
}

run();
