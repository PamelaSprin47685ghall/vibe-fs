import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { passed, failed } from '../build/tests/Assert.js';
import { runAll } from '../build/tests/Tests.js';

process.on('unhandledRejection', (reason, promise) => {
  console.error('UNHANDLED REJECTION AT:', promise, 'REASON:', reason?.stack || reason);
});

process.env.WANXIANGSHU_TEST = 'true';

const args = process.argv.slice(2);

runAll(args)
    .then(code => {
        const passedCount = passed();
        const failedCount = failed();

        let suiteKey = 'all';
        if (args.includes('L0')) {
            if (args.includes('codec')) {
                suiteKey = 'contract';
            } else {
                suiteKey = 'unit';
            }
        } else if (args.includes('L2')) {
            suiteKey = 'integration';
        } else if (args.includes('L4')) {
            suiteKey = 'gates';
        }

        const __dirname = path.dirname(fileURLToPath(import.meta.url));
        const baselinePath = path.join(__dirname, 'assert-baseline.json');
        
        try {
            if (fs.existsSync(baselinePath)) {
                const baseline = JSON.parse(fs.readFileSync(baselinePath, 'utf8'));
                const minExpected = baseline[suiteKey];
                if (typeof minExpected === 'number') {
                    const tolerance = 0.95;
                    const threshold = Math.floor(minExpected * tolerance);
                    const silent = args.includes('--silent') || args.includes('--quiet');
                    if (!silent) {
                        console.log(`[Baseline Check] Suite: ${suiteKey}, Passed: ${passedCount}, Expected Minimum: ${minExpected} (Threshold: ${threshold})`);
                    }
                    if (passedCount < threshold) {
                        console.error(`[Baseline Check] ERROR: Passed assertion count (${passedCount}) dropped significantly below the baseline of ${minExpected} (Threshold: ${threshold}).`);
                        process.exit(3);
                    }
                }
            }
        } catch (e) {
            console.error('[Baseline Check] Warning: failed to validate baseline:', e);
        }

        process.exit(code);
    })
    .catch(err => {
        console.error('RUNALL_FAILED:', err?.stack || err);
        process.exit(2);
    });
