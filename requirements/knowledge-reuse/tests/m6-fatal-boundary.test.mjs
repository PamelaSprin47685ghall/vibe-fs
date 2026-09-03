import test from 'node:test'
import { assertFatalBoundary } from '../../structured-workflow/tests/support/m6-boundary-proof.mjs'

test('WHAT[KNOWLEDGE-REUSE-013] Casebook fatal follows durable cut settlement and one injected fuse', () => assertFatalBoundary('knowledge-reuse'))
