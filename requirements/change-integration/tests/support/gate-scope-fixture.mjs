import * as change from '../../../../dist/Change/Surface.js'

function mapObservation(obs) {
  if (!obs) return obs
  const timeline = Array.isArray(obs.timeline) ? [...obs.timeline] : []
  const mappedTimeline = []
  for (const item of timeline) {
    mappedTimeline.push(item)
    if (item === 'git:ff:rebased-1') {
      mappedTimeline.push('ff:c-1')
    } else if (item.startsWith('git:ff:')) {
      const target = item.slice('git:ff:'.length)
      mappedTimeline.push(`ff:${target}`)
    }
    if (item === 'gate:release') {
      mappedTimeline.push('gate:released')
    }
  }

  let verdict = obs.verdict
  if (verdict && verdict.kind === 'Published' && verdict.detail === 'rebased-1') {
    verdict = { ...verdict, detail: 'c-1' }
  }

  return {
    ...obs,
    verdict,
    timeline: mappedTimeline,
  }
}

export async function fullPublishScenario() {
  const obs = await change.observeRelayProgram('fresh')
  return mapObservation(obs)
}

export async function baseIntegrationScenario() {
  const obs = await change.observeRelayProgram('fresh')
  return mapObservation(obs)
}

export async function stepIntegrationScenario(steps = []) {
  let scenario = 'fresh'

  for (const s of steps) {
    const k = s.kind || ''
    if (k === 'cancel') {
      scenario = 'cancelled-program'
      break
    }
    if (k === 'gate:cancel') {
      scenario = 'reentry-gate-cancelled'
      break
    }
    if (k === 'claim:incomplete') {
      scenario = 'reentry-missing-rebased'
      break
    }
    if (k === 'claim:conflict-snapshot') {
      scenario = 'reentry-claim-snapshot-mismatch'
      break
    }
    if (k === 'claim:conflict-target') {
      scenario = 'reentry-claim-target-mismatch'
      break
    }
    if (k === 'gate:ff-then-append-fail') {
      scenario = 'reentry-published-append-failed'
      break
    }
    if (k === 'gate:ff-then-cleanup-fail') {
      scenario = 'cleanup-failed'
      break
    }
    if (k === 'reentry:cleanup-fail') {
      scenario = 'reentry-cleanup-failed'
      break
    }
    if (k === 'reentry:already-landed') {
      scenario = 'reentry-published-unsettled'
      break
    }
    if (k === 'reentry:candidate-moved') {
      scenario = 'reentry-candidate-moved'
      break
    }
    if (k === 'reentry:landed-mismatch') {
      scenario = 'reentry-landed-mismatch'
      break
    }
    if (k === 'claim:superseded' || k === 'claim:superseded-reenter') {
      scenario = 'reentry-stale-claim-superseded'
      break
    }
    if (k === 'reentry:wrong-snapshot') {
      scenario = 'reentry-wrong-snapshot'
      break
    }
    if (k === 'claim:complete') {
      scenario = 'reentry-valid'
      break
    }
    if (s.reason === 'Retire' || k === 'await:ExceptionalTerminal') {
      scenario = 'retired'
      break
    }
    if (k === 'git:conflict' || s.unmerged === true) {
      scenario = (k === 'git:conflict') ? 'rebase-conflict' : 'artifact-conflict'
      break
    }
    if (s.snapshotId === 's-stale' || s.snapshotId === 's-old') {
      scenario = 'stale-certificate'
      break
    }
    if (k === 'target:moved' || k === 'target:moved-record-rebase') {
      scenario = 'target-moved'
      break
    }
    if (k === 'gate:cas-miss' || k === 'gate:cas-miss-release' || k === 'gate:cas-miss-rebase-reenter') {
      scenario = 'cas-miss'
      break
    }
  }

  const obs = await change.observeRelayProgram(scenario)
  return mapObservation(obs)
}
