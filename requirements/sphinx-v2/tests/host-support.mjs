import {mkdtempSync, rmSync} from 'node:fs'
import {tmpdir} from 'node:os'
import {join} from 'node:path'
import * as Host from '../../../dist/Sphinx/V2/Hosts/OpenCode/Surface.js'
import {admittedWithReceipt} from '../../../dist/Interaction/Dispatch/DispatchSurface.js'
import {body, envelope, work} from './persistence-support.mjs'

export {Host, admittedWithReceipt}

export const withHost = async (action, capturedOwner = 'nested-manager-owner') => {
  const directory = mkdtempSync(join(tmpdir(), 'sphinx-host-owner-'))
  const owner = 'nested-manager-owner'
  let probe
  try {
    probe = await Host.createDispatchProbe(directory, [{sessionId: owner, agent: 'manager'}], capturedOwner)
    return await action(probe, owner)
  }
  finally {
    try {
      if (probe) Host.disposeDispatchProbe(probe)
    } finally {
      rmSync(directory, {recursive: true, force: true})
    }
  }
}

export const hostRequest = () => body('DispatchRequested', {
  work: work('owned-work'), dispatchIntentId: 'owned-intent',
  publicEnvelope: envelope('{"question":"visible question"}'),
  privateTicket: envelope('{"labelMap":{"opaque-a":"PRIVATE-AUTHOR-7d31"}}'),
})
