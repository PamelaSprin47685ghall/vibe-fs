import { runAll } from '../build/tests/Tests.js';

runAll(process.argv.slice(2))
    .then(code => process.exit(code))
    .catch(err => { console.error(err); process.exit(1); });
