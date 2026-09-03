import test from 'node:test'
import { assertFatalBoundary } from '../../structured-workflow/tests/support/m6-boundary-proof.mjs'

test('WHAT[EMR-016] routing fatal requires exact fence settlement and one injected fuse', () => assertFatalBoundary('execution-model-routing'))
