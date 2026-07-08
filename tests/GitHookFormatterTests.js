import { promises as fs } from 'fs';
import path from 'path';
import { spawnSync } from 'child_process';

export async function runAll(args) {
    const isSilent = args.includes('--silent');
    const log = (...msg) => { if (!isSilent) console.log(...msg); };

    log('Executing GitHookFormatterTests...');
    const formatterPath = path.resolve('./scripts/pre-commit-formatter.mjs');
    const setupPath = path.resolve('./scripts/setup-git-hooks.mjs');

    try {
        await fs.access(formatterPath);
        log('✓ pre-commit-formatter.mjs exists.');
    } catch (err) {
        console.error('Test failed: pre-commit-formatter.mjs is missing.');
        return 1;
    }

    try {
        await fs.access(setupPath);
        log('✓ setup-git-hooks.mjs exists.');
    } catch (err) {
        console.error('Test failed: setup-git-hooks.mjs is missing.');
        return 1;
    }

    // Verify prepare script is in package.json
    try {
        const pkgRaw = await fs.readFile(path.resolve('./package.json'), 'utf8');
        const pkg = JSON.parse(pkgRaw);
        if (pkg.scripts && pkg.scripts.prepare === 'node scripts/setup-git-hooks.mjs') {
            log('✓ package.json scripts.prepare is correctly configured.');
        } else {
            console.error('Test failed: package.json scripts.prepare is missing or incorrect.');
            return 1;
        }
    } catch (err) {
        console.error('Test failed: could not read package.json.', err);
        return 1;
    }

    // Test fsproj formatting
    const tmpRepoDir = path.resolve('./tests/tmp-fsproj-test-repo');
    try {
        await fs.rm(tmpRepoDir, { recursive: true, force: true });
        await fs.mkdir(tmpRepoDir, { recursive: true });

        const gitInit = spawnSync('git', ['init'], { cwd: tmpRepoDir });
        if (gitInit.status !== 0) {
            throw new Error(`Failed to git init: ${gitInit.stderr}`);
        }

        const fsprojPath = path.join(tmpRepoDir, 'test.fsproj');
        const unformattedContent = '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>';
        await fs.writeFile(fsprojPath, unformattedContent, 'utf8');

        const gitAdd = spawnSync('git', ['add', 'test.fsproj'], { cwd: tmpRepoDir });
        if (gitAdd.status !== 0) {
            throw new Error(`Failed to git add: ${gitAdd.stderr}`);
        }

        const runFormatter = spawnSync('node', [formatterPath], { cwd: tmpRepoDir });
        if (runFormatter.status !== 0) {
            throw new Error(`Formatter script failed to run: ${runFormatter.stderr}`);
        }

        const formattedContent = await fs.readFile(fsprojPath, 'utf8');
        if (!formattedContent.includes('\n') || formattedContent === unformattedContent) {
            console.error('Test failed: test.fsproj was not formatted as XML.');
            log('Content remained:', formattedContent);
            return 1;
        }

        log('✓ .fsproj file was formatted successfully.');
    } catch (err) {
        console.error('Fsproj formatting test failed with error:', err);
        return 1;
    } finally {
        await fs.rm(tmpRepoDir, { recursive: true, force: true });
    }

    // Test --all flag formats un-staged files
    const tmpAllDir = path.resolve('./tests/tmp-all-flag-test-repo');
    try {
        await fs.rm(tmpAllDir, { recursive: true, force: true });
        await fs.mkdir(tmpAllDir, { recursive: true });

        // Init repo and create initial commit so files are tracked
        let gitInit = spawnSync('git', ['init'], { cwd: tmpAllDir });
        if (gitInit.status !== 0) throw new Error(`Failed to git init: ${gitInit.stderr}`);

        spawnSync('git', ['config', 'user.email', 'test@test.com'], { cwd: tmpAllDir });
        spawnSync('git', ['config', 'user.name', 'Test'], { cwd: tmpAllDir });

        const trackedFsproj = '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>';
        const trackedPath = path.join(tmpAllDir, 'tracked.fsproj');
        await fs.writeFile(trackedPath, trackedFsproj, 'utf8');

        spawnSync('git', ['add', 'tracked.fsproj'], { cwd: tmpAllDir });
        spawnSync('git', ['commit', '-m', 'initial'], { cwd: tmpAllDir });

        // Now modify the file without staging
        await fs.writeFile(trackedPath, trackedFsproj, 'utf8');

        // Run WITHOUT --all: should NOT format un-staged files
        const runNoAll = spawnSync('node', [formatterPath], { cwd: tmpAllDir });
        if (runNoAll.status !== 0) {
            throw new Error(`Formatter script failed without --all: ${runNoAll.stderr}`);
        }

        const contentAfterNoAll = await fs.readFile(trackedPath, 'utf8');
        if (contentAfterNoAll.includes('\n')) {
            console.error('Test failed: file was formatted without --all flag (should only format staged).');
            return 1;
        }

        // Modify file to unformatted single-line state again
        const unformatted = '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>';
        await fs.writeFile(trackedPath, unformatted, 'utf8');

        // Run WITH --all: should format un-staged files
        const runAll = spawnSync('node', [formatterPath, '--all'], { cwd: tmpAllDir });
        if (runAll.status !== 0) {
            throw new Error(`Formatter script failed with --all: ${runAll.stderr}`);
        }

        const contentAfterAll = await fs.readFile(trackedPath, 'utf8');
        if (!contentAfterAll.includes('\n') || contentAfterAll === unformatted) {
            console.error('Test failed: --all flag did not format un-staged file.');
            log('Content remained:', contentAfterAll);
            return 1;
        }

        log('✓ --all flag formats un-staged files correctly.');
    } catch (err) {
        console.error('--all flag test failed with error:', err);
        return 1;
    } finally {
        await fs.rm(tmpAllDir, { recursive: true, force: true });
    }

    log('All GitHookFormatterTests passed.');
    return 0;
}
