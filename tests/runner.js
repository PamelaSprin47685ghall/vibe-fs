const isE2e = process.argv.includes('--e2e');
const isE2eMux = process.argv.includes('--e2e-mux');

let runAll;
if (isE2eMux) {
    runAll = (await import('../build/e2e/MuxTests.js')).runAll;
} else if (isE2e) {
    runAll = (await import('../build/e2e/Tests.js')).runAll;
} else {
    runAll = (await import('../build/tests/Tests.js')).runAll;
}

const extraArgs = process.argv.slice(2).filter(a => a !== '--e2e' && a !== '--e2e-mux');
runAll(extraArgs)
    .then(code => process.exit(code))
    .catch(err => {
        console.error('RUNALL_FAILED:', err && err.message ? err.message : err);
        process.exit(2);
    });