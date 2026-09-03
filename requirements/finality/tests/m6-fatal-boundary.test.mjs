import test from 'node:test'
import { assertFatalBoundary } from '../../structured-workflow/tests/support/m6-boundary-proof.mjs'

test('WHAT[FINALITY-029] Finality infrastructure incident preserves adjudication and uses one injected fuse', () => assertFatalBoundary('finality'))
