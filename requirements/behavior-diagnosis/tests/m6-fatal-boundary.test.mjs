import test from 'node:test'
import { assertFatalBoundary } from '../../structured-workflow/tests/support/m6-boundary-proof.mjs'

test('WHAT[BD-019] Enforcer fatal requires typed settlement and one injected fuse', () => assertFatalBoundary('behavior-diagnosis'))
