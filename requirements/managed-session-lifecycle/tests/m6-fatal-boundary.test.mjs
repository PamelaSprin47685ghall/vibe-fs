import test from 'node:test'
import { assertFatalBoundary } from '../../structured-workflow/tests/support/m6-boundary-proof.mjs'

test('WHAT[MANAGED-SESSION-021] lifecycle fatal follows exact drain and one injected fuse', () => assertFatalBoundary('managed-session-lifecycle'))
