import test from 'node:test'
import { assertFatalBoundary } from '../../structured-workflow/tests/support/m6-boundary-proof.mjs'

test('WHAT[OBLIGATION-LEDGER-028] ledger fatal follows exact checkpoint settlement and one injected fuse', () => assertFatalBoundary('obligation-ledger'))
