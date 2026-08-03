import { spawnSync } from 'child_process';
import fs from 'fs';
import path from 'path';
import crypto from 'crypto';

function getSha256(filePath) {
    try {
        const data = fs.readFileSync(filePath);
        return crypto.createHash('sha256').update(data).digest('hex');
    } catch {
        return null;
    }
}

function checkCommand(command, args = []) {
    try {
        const res = spawnSync(command, args, { stdio: 'ignore' });
        return res.status === 0;
    } catch {
        return false;
    }
}

function log(msg) {
    console.log(`[pre-commit-formatter] ${msg}`);
}

function warn(msg) {
    console.warn(`[pre-commit-formatter] [Warning] ${msg}`);
}

function error(msg) {
    console.error(`[pre-commit-formatter] [Error] ${msg}`);
}

function scanFiles(dir) {
    const ignoreDirs = new Set(['node_modules', 'build', 'bin', 'obj', 'artifacts', '.git']);
    const result = [];
    let entries;
    try {
        entries = fs.readdirSync(dir, { withFileTypes: true });
    } catch {
        return result;
    }
    for (const entry of entries) {
        if (entry.name === '.git' || ignoreDirs.has(entry.name)) continue;
        const fullPath = path.join(dir, entry.name);
        if (entry.isDirectory()) {
            result.push(...scanFiles(fullPath));
        } else if (entry.isFile()) {
            const ext = path.extname(entry.name).toLowerCase();
            if (ext === '.fs' || ext === '.fsi' || ext === '.xml' || ext === '.fsproj') {
                result.push(fullPath);
            }
        }
    }
    return result;
}

async function run() {
    const isAll = process.argv.includes('--all');

    const hasDotnet = checkCommand('dotnet', ['--version']);
    let hasFantomas = false;
    if (hasDotnet) {
        hasFantomas = checkCommand('dotnet', ['tool', 'run', 'fantomas', '--version']);
    }
    const hasXmllint = checkCommand('xmllint', ['--version']);

    if (!hasFantomas && !hasXmllint) {
        warn('Neither dotnet-fantomas nor xmllint is available. Skipping auto-formatting.');
        process.exit(0);
    }

    let allFiles;
    let isMixed = () => false;

    if (isAll) {
        allFiles = scanFiles('.');
    } else {
        const diffCached = spawnSync('git', ['diff', '--cached', '--name-only', '--diff-filter=ACM'], { encoding: 'utf8' });
        if (diffCached.status !== 0) {
            error('Failed to run git diff --cached.');
            process.exit(0);
        }

        const stagedFiles = diffCached.stdout
            .split('\n')
            .map(f => f.trim())
            .filter(f => f.length > 0);

        if (stagedFiles.length === 0) {
            log('No staged files detected.');
            process.exit(0);
        }

        const getUnstagedDiff = () => {
            const res = spawnSync('git', ['diff', '--name-only'], { encoding: 'utf8' });
            if (res.status !== 0) return [];
            return res.stdout.split('\n').map(f => f.trim()).filter(f => f.length > 0);
        };
        const unstagedFiles = new Set(getUnstagedDiff());
        isMixed = (file) => unstagedFiles.has(file);

        allFiles = stagedFiles;
    }

    if (allFiles.length === 0) {
        log('No files to format.');
        process.exit(0);
    }

    const fsFiles = [];
    const xmlFiles = [];

    for (const file of allFiles) {
        const ext = path.extname(file).toLowerCase();
        if (ext === '.fs' || ext === '.fsi') {
            fsFiles.push(file);
        } else if (ext === '.xml' || ext === '.fsproj') {
            xmlFiles.push(file);
        }
    }

    if (fsFiles.length === 0 && xmlFiles.length === 0) {
        log('No F# or XML files to format.');
        process.exit(0);
    }

    if (fsFiles.length > 0) {
        if (!hasFantomas) {
            warn('dotnet fantomas is not available; skipping F# files formatting.');
        } else {
            const safeFsFiles = [];
            for (const file of fsFiles) {
                if (isMixed(file)) {
                    warn(`Skipping F# file '${file}' because it has both staged and unstaged changes. Staging formatted changes would accidentally stage unstaged edits.`);
                } else {
                    safeFsFiles.push(file);
                }
            }

            if (safeFsFiles.length > 0) {
                log(`Formatting F# files: ${safeFsFiles.join(', ')}`);
                const shasBefore = {};
                for (const file of safeFsFiles) {
                    shasBefore[file] = getSha256(file);
                }

                const formatRes = spawnSync('dotnet', ['tool', 'run', 'fantomas', ...safeFsFiles], { stdio: 'inherit' });
                if (formatRes.status !== 0) {
                    error('dotnet fantomas run failed.');
                }

                if (!isAll) {
                    for (const file of safeFsFiles) {
                        const shaAfter = getSha256(file);
                        if (shaAfter && shaAfter !== shasBefore[file]) {
                            log(`F# file '${file}' modified by formatting. Re-staging...`);
                            spawnSync('git', ['add', file]);
                        }
                    }
                }
            }
        }
    }

    if (xmlFiles.length > 0) {
        if (!hasXmllint) {
            warn('xmllint is not available; skipping XML files formatting.');
        } else {
            const safeXmlFiles = [];
            for (const file of xmlFiles) {
                if (isMixed(file)) {
                    warn(`Skipping XML file '${file}' because it has both staged and unstaged changes. Staging formatted changes would accidentally stage unstaged edits.`);
                } else {
                    safeXmlFiles.push(file);
                }
            }

            for (const file of safeXmlFiles) {
                const nooutRes = spawnSync('xmllint', ['--noout', file]);
                if (nooutRes.status !== 0) {
                    warn(`Skipping malformed or incomplete XML file '${file}' to prevent corruption.`);
                    continue;
                }

                try {
                    const xmlContent = fs.readFileSync(file, 'utf8');
                    const shaBefore = crypto.createHash('sha256').update(xmlContent).digest('hex');

                    log(`Formatting XML file: ${file}`);
                    const formatRes = spawnSync('xmllint', ['--format', '-'], {
                        input: xmlContent,
                        encoding: 'utf8'
                    });

                    if (formatRes.status === 0 && formatRes.stdout) {
                        const formattedContent = formatRes.stdout;
                        const shaAfter = crypto.createHash('sha256').update(formattedContent).digest('hex');

                        if (shaBefore !== shaAfter) {
                            fs.writeFileSync(file, formattedContent, 'utf8');
                            if (!isAll) {
                                log(`XML file '${file}' modified by formatting. Re-staging...`);
                                spawnSync('git', ['add', file]);
                            }
                        }
                    } else {
                        error(`xmllint format failed on '${file}': ${formatRes.stderr || 'unknown error'}`);
                    }
                } catch (e) {
                    error(`Error while formatting XML file '${file}': ${e.message}`);
                }
            }
        }
    }
}

run();
