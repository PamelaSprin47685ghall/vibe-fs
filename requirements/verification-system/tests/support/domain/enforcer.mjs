// tests/unit/support/domain/enforcer.mjs — enforcer + review family.
// Review witness/challenge/seal/projection, provider projection, root/continuation
// kinds, enforcer catalog/resource/codec/continuation.

import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import {
  Witness,
  Challenge,
  ReviewProj,
  ProviderProj,
  ProjectionModule,
  ReviewSealModule,
  SessionSnapshotPortModule,
  EnforcerCatalogResourceModule,
  EnforcerCatalogDomainModule,
  EnforcerCodecModule,
  EnforcerCycleModule,
  Authority,
  bind,
  member,
  caseOf,
  payloadOf,
  resultOf,
  listItems,
  toList,
  mapOf,
  unwrapOption,
  isNone,
  isSome,
  stringSet,
  mapCount,
  BUILD_ROOT,
} from './interop.mjs'
import {
  providerRun,
  toolCallId,
  gitTreeHash,
  sessionId,
  reviewBarrierId,
  sealDigest,
  physicalUser,
  idValue,
} from './identity.mjs'

// ── review (docs/what/review.md) ─────────────────────────────────────────────────────────

/**
 * REVIEW-006: one witnessed PERFECT verdict.
 *
 * Deliberately no `authorityRoot` parameter. REVIEW-003 forbids confirming on a
 * shared authority root and REVIEW-006's field list has no such field, so the
 * facade cannot offer one either — once a test can set it, comparing it is one
 * line away.
 */
export const verdictWitness = ({ run, call, tree, reviewer }) => ({
  ProviderRun: providerRun(run),
  ToolCallId: toolCallId(call),
  GitTreeHash: gitTreeHash(tree),
  ReviewerSessionId: sessionId(reviewer),
})

export const reviewWitness = {
  isConfirmed: (value) => Witness.ReviewWitnessModule_isConfirmed(value),
  isPerfectPending: (value) => Witness.ReviewWitnessModule_isPerfectPending(value),
  isRevision: (value) => Witness.ReviewWitnessModule_isRevision(value),
  gitTreeHash: (value) => unwrapOption(Witness.ReviewWitnessModule_gitTreeHash(value)),
  confirmedReviewer: (value) => unwrapOption(Witness.ReviewWitnessModule_confirmedReviewer(value)),
  isValidForTree: (tree, value) => Witness.ReviewWitnessModule_isValidForTree(tree, value),
  attemptIdentity: (barrier, witness) => Witness.ReviewWitnessModule_attemptIdentity(barrier, witness),
  isDistinctAttempt: (barrier, a, b) => Witness.ReviewWitnessModule_isDistinctAttempt(barrier, a, b),
  confirm: (barrier, challengeDigest, secondInputDigest, first, second) =>
    unwrapOption(Witness.ReviewWitnessModule_confirm(barrier, challengeDigest, secondInputDigest, first, second)),
  noReview: Witness.ReviewWitness.NoReview,

  /** The whole witness as comparable text, so a renamed field cannot read `undefined`. */
  read: (value) => {
    const readOne = (one) => ({
      run: idValue.providerRun(one.ProviderRun),
      call: idValue.toolCall(one.ToolCallId),
      tree: idValue.gitTree(one.GitTreeHash),
      reviewer: idValue.session(one.ReviewerSessionId),
    })
    const payload = payloadOf(value)

    switch (caseOf(value)) {
      case 'NoReview':
        return { state: 'NoReview' }
      case 'RevisionWitness':
        return { state: 'RevisionWitness', tree: idValue.gitTree(payload.GitTreeHash) }
      case 'PerfectPending':
        return { state: 'PerfectPending', first: readOne(payload) }
      case 'Confirmed':
        return {
          state: 'Confirmed',
          barrier: idValue.reviewBarrier(payload.BarrierId),
          tree: idValue.gitTree(payload.GitTreeHash),
          first: readOne(payload.First),
          second: readOne(payload.Second),
          challengeResultDigest: idValue.sealDigest(payload.ChallengeResultDigest),
          secondProviderInputDigest: idValue.sealDigest(payload.SecondProviderInputDigest),
        }
      default:
        throw new Error(`unknown ReviewWitness case '${caseOf(value)}'`)
    }
  },
}

/** REVIEW-003: path, version, prompt assembly, digest. English canonical from resources. */
export const reviewChallenge = (() => {
  const m = bind(Challenge, 'ReviewChallenge', ['Path', 'TextVersion', 'promptOf', 'contentDigest'])
  const englishText = readFileSync(
    join(BUILD_ROOT, '..', 'resources/provider/review/challenge/en.md'),
    'utf8',
  )
    .replace(/\r\n/g, '\n')
    .trim()
  const englishPrompt = m.promptOf(englishText)

  return {
    path: m.Path,
    /** English canonical sentence (e2e / historical EN seals). Production follows session language. */
    text: englishText,
    /** ARCH-010 instruction form (`# Text\\n`); seal / nudge / algebra AppendReviewChallenge. */
    prompt: englishPrompt,
    textVersion: m.TextVersion,
    promptOf: (text) => m.promptOf(text),
    contentDigest: (sha256, prompt) => m.contentDigest(sha256, prompt ?? englishPrompt),

    /** The `PerfectChallengeIssued` payload a first PERFECT journals. */
    issued: ({ barrier, tree, reviewer, run, call, digest, version = m.TextVersion }) => ({
      BarrierId: reviewBarrierId(barrier),
      GitTreeHash: gitTreeHash(tree),
      ReviewerSessionId: sessionId(reviewer),
      FirstProviderRun: providerRun(run),
      FirstToolCallId: toolCallId(call),
      ChallengeTextVersion: version,
      ChallengeContentDigest: digest,
    }),
  }
})()

/**
 * REVIEW-010: the canonical provider input for one run.
 *
 * `included` is an array of digest STRINGS and is converted to an `FSharpSet`
 * here. A JS array would make `Set.contains` answer `false` for everything, so
 * every confirmation would be refused while looking like fail-closed behaviour.
 */
export const providerInputSeal = ({ session, run, physical = 'msg_u1', digest, included = [], version = 1 }) => ({
  SessionId: sessionId(session),
  ProviderRun: providerRun(run),
  PhysicalUserMessageId: physicalUser(physical),
  SealDigest: sealDigest(digest),
  CanonicalVersion: version,
  IncludedToolResultDigests: stringSet(included),
})

export const reviewProjection = (() => {
  const m = bind(ReviewProj, 'ReviewProjection', [
    'empty',
    'startBarrier',
    'applySeal',
    'applyChallengeIssued',
    'applyVerdict',
    'applyConfirmedWitness',
    'hasObservedAttempt',
    'satisfiesGuard',
  ])

  /** Rejections carry no payload, so the case name is the whole answer. */
  const decided = (result) => {
    const value = resultOf(result)
    return value.ok ? value : { ok: false, error: caseOf(value.error) }
  }

  return {
    empty: m.empty,
    startBarrier: (barrier, tree, current, manager = sessionId('mgr')) => m.startBarrier(manager, barrier, tree, current),
    applySeal: (seal, current) => m.applySeal(seal, current),
    applyChallengeIssued: (challenge, current) => m.applyChallengeIssued(challenge, current),
    applyVerdict: (attempt, value, current) => decided(m.applyVerdict(attempt, value, current)),
    applyConfirmedWitness: (barrier, challengeDigest, secondInputDigest, first, second, current) =>
      decided(m.applyConfirmedWitness(barrier, challengeDigest, secondInputDigest, first, second, current)),
    hasObservedAttempt: (attempt, current) => m.hasObservedAttempt(attempt, current),
    satisfiesGuard: (tree, current) => m.satisfiesGuard(tree, current),

    /** The guard state as plain JS. */
    read: (current) => ({
      barrier: isSome(current.CurrentBarrierId) ? idValue.reviewBarrier(current.CurrentBarrierId) : undefined,
      tree: isSome(current.LastGitTreeHash) ? idValue.gitTree(current.LastGitTreeHash) : undefined,
      witness: caseOf(current.Witness),
      hasPendingChallenge: isSome(current.PendingChallenge),
      seals: mapCount(current.Seals),
      observedAttempts: listItems(current.ObservedAttempts).length,
    }),
  }
})()

export const reviewRequirements = (() => {
  const m = bind(ReviewProj, 'ReviewRequirementProjection', ['empty', 'addRequirement', 'clearOnConfirmation'])

  return {
    empty: m.empty,
    addRequirement: (session, root, current) => m.addRequirement(session, root, current),
    clearOnConfirmation: (run, current) => m.clearOnConfirmation(run, current),
  }
})()

/** VERIFY-007: the two provider projections, and the one-way downgrade. */
export const providerProjection = {
  canonicalVersion: ProviderProj.CanonicalVersion,
  toSemantic: (wire) => ProviderProj.toSemantic(wire),
  renderWire: (wire) => ProviderProj.renderWire(wire),
  renderSemantic: (semantic) => ProviderProj.renderSemantic(semantic),
  isAppendOnlyPrefix: (previous, next) => ProviderProj.isAppendOnlyPrefix(previous, next),
  sealDigest: (sha256, wire) => ProviderProj.sealDigest(sha256, wire),
  toolResultDigest: (sha256, canonical) => ProviderProj.toolResultDigest(sha256, canonical),
  toolResultDigests: (sha256, wire) => listItems(ProviderProj.toolResultDigests(sha256, wire)).map((d) => d.fields[0]),
  fixtureKey: (semantic) => ProviderProj.fixtureKey(semantic),
  semanticallyEqual: (a, b) => ProviderProj.semanticallyEqual(a, b),
  // OpenCode/Projection: Host-assembled message view (1.18.10 `tool-<tool>`
  // parts live on assistant messages; see HOST-012 tool-part test).
  decodeRequest: (requestObj) => ProjectionModule.decodeRequest(requestObj),
  decodeMessageView: (rawMessages) => ProjectionModule.decodeMessageView(rawMessages),
  decodeCapturedMessageView: (rawMessages) => listItems(ProjectionModule.decodeCapturedMessageView(toList(rawMessages))),
  wireMessageView: (capturedMessages) => ProjectionModule.wireMessageView(toList(capturedMessages)),
  // PROJ-004: the one write-back adapter of the projection DSL's prefix stage.
  applyRenderedPrefix: (rawMessages, rendered) =>
    listItems(ProjectionModule.applyRenderedPrefix(rawMessages, rendered)),
}

export const reviewSeal = (() => {
  const bindableRun = member(ReviewSealModule, 'ReviewSeal', 'bindableRun')
  const projectMessages = member(SessionSnapshotPortModule, 'SessionSnapshotPort', 'projectMessages')
  const decodeRejection = (error) => {
    const name = caseOf(error)
    if (name === 'AmbiguousRun') {
      const fields = error.fields ?? []
      return { case: name, count: fields[0] }
    }
    return { case: name }
  }

  return {
    /** Project Host-shaped message objects into SessionMessage list. */
    projectMessages: (rawMessages) => projectMessages(rawMessages),

    /**
     * HOST-010 bindableRun. `physicalUser` is the last user message id.
     * `messages` may be a projected F# list or Host-raw objects (auto-projected).
     * Returns `{ ok: true, id }` or `{ ok: false, rejection: { case, count? } }`.
     */
    bindableRun: (physicalUser, messages) => {
      // Host-raw JS array → project; already-projected F# list passes through.
      const list =
        Array.isArray(messages) ? projectMessages(messages) : messages
      const result = resultOf(bindableRun(physicalUser, list))
      if (result.ok) {
        const msg = result.value
        return {
          ok: true,
          id: msg.Id,
          parentId: unwrapOption(msg.ParentId),
          completed: Boolean(msg.Completed),
        }
      }
      return { ok: false, rejection: decodeRejection(result.error) }
    },
  }
})()

export const rootKind = {
  human: Authority.RootAuthorityKind.HumanRoot,
  agentOwner: Authority.RootAuthorityKind.AgentOwnerRoot,
}

export const continuationKind = {
  of: (name) => {
    const parsed = unwrapOption(Authority.tryParseContinuationKind(name))
    if (isNone(parsed)) throw new Error(`unknown ContinuationKind '${name}'`)
    return parsed
  },
}

/** Package enforcer rulebook folder load + domain validation fail-fast. */
export const enforcerCatalogResource = (() => {
  const api = bind(EnforcerCatalogResourceModule, 'EnforcerCatalogResource', [
    'load',
    'loadFor',
    'composeBloggerSystemPrompt',
    'composeBloggerSystemPromptFor',
  ])
  return {
    load: () => listItems(api.load()),
    loadFor: (lang) => listItems(api.loadFor(lang)),
    composeBloggerSystemPrompt: (basePrompt, rules) =>
      api.composeBloggerSystemPrompt(basePrompt, toList(rules)),
    composeBloggerSystemPromptFor: (lang, basePrompt, rules) =>
      api.composeBloggerSystemPromptFor(lang, basePrompt, toList(rules)),
  }
})()

/**
 * ENFORCER-170 pure rulebook validation + EnforcerRule construction.
 * Domain never reads files; tests hand rules via `rule(...)`.
 * TipName = Name = RuleId = FieldName after folder SSOT cutover.
 */
export const enforcerCatalog = (() => {
  // ENFORCER-170: validate + field lookup only. `triples` was a facade ghost —
  // production never exported it; binding it fail-closed every unit import.
  const api = bind(EnforcerCatalogDomainModule, 'EnforcerCatalog', [
    'validate',
    'tryFindByField',
    'fieldNames',
  ])
  const Rule = EnforcerCatalogDomainModule.EnforcerRule
  if (typeof Rule !== 'function') {
    throw new Error('Domain/EnforcerCatalog exports no EnforcerRule constructor')
  }
  return {
    /** Construct one EnforcerRule record (Fable class; field order matches Domain). */
    rule: ({
      name,
      ruleId,
      fieldName,
      enforcerText = 'enforcer body',
      mainText = 'main body',
      lexicalOrder = 1,
    } = {}) => {
      const tip = name ?? fieldName ?? ruleId ?? 'sample-field'
      const id = ruleId ?? tip
      const field = fieldName ?? tip
      // Fable record ctor order: Name, EnforcerText, MainText, RuleId, FieldName, LexicalOrder
      return new Rule(tip, enforcerText, mainText, id, field, lexicalOrder)
    },
    /**
     * Result over schemaVersion + rules list.
     * Ok value is a JS array of EnforcerRule (listItems on F# list).
     */
    validate: (schemaVersion, rules) => {
      const result = resultOf(api.validate(schemaVersion, toList(rules)))
      return result.ok ? { ok: true, value: listItems(result.value) } : result
    },
    tryFindByField: (field, rules) => {
      const found = api.tryFindByField(field, toList(rules))
      return isNone(found) ? undefined : found
    },
    fieldNames: (rules) => listItems(api.fieldNames(toList(rules))),
  }
})()

// ── docs/what/enforcer.md: Blogger as Enforcer 纯领域内核 ─────────────────────────────────

export const enforcer = (() => {
  const catalog = bind(EnforcerCatalogResourceModule, 'EnforcerCatalogResource', ['load'])
  const catalogDomain = bind(EnforcerCatalogDomainModule, 'EnforcerCatalog', [
    'tryFindByField',
    'fieldNames',
  ])
  // MissingTipError is a codec string literal export (Fable: EnforcerCodecModule.MissingTipError).
  const codec = bind(EnforcerCodecModule, 'EnforcerCodec', [
    'decodeCall',
    'hasValidText',
    'unknownTipError',
  ])
  const cycle = bind(EnforcerCycleModule, 'EnforcerCycle', ['ofCall', 'isValidCycle'])

  // Explicit load: module import no longer reads package resources (0.5.3).
  const catalogRules = listItems(catalog.load())
  const tipOf = (call) => {
    const tip = call?.Tip
    if (!tip) return undefined
    return {
      ruleId: tip.RuleId,
      fieldName: tip.FieldName,
      lexicalOrder: tip.LexicalOrder,
    }
  }

  const missingTipError =
    EnforcerCodecModule.MissingTipError ??
    EnforcerCodecModule.EnforcerCodec_MissingTipError ??
    'missing required argument: tip'

  return {
    /** Packaged rulebook rules (resources/enforcer/<tip>/ folders, ENFORCER-170). */
    rules: catalogRules,
    ruleCount: catalogRules.length,
    fieldNames: () => listItems(catalogDomain.fieldNames(toList(catalogRules))),
    MissingTipError: missingTipError,
    unknownTipError: (tipValue) => codec.unknownTipError(tipValue),

    /** ENFORCER-021: exact field → rule (no fuzzy match). */
    tryFindByField: (field) => {
      const rule = unwrapOption(catalogDomain.tryFindByField(field, toList(catalogRules)))
      if (!rule) return undefined
      return {
        ruleId: rule.RuleId,
        fieldName: rule.FieldName,
        lexicalOrder: rule.LexicalOrder,
      }
    },

    /**
     * ENFORCER-020..026 tip codec.
     * Returns `{ ok: true, value }` or `{ ok: false, error }` (VERIFY-008 full structure).
     */
    decodeCall: (rawArgs) => {
      const result = resultOf(codec.decodeCall(toList(catalogRules), mapOf(rawArgs ?? {})))
      if (!result.ok) return result
      const call = result.value
      return {
        ok: true,
        value: {
          text: call.Text,
          evidence: call.Evidence,
          tip: tipOf(call),
        },
      }
    },

    hasValidText: (decoded) => {
      // Accept facade shape or raw CanonicalBlogCall.
      if (decoded && typeof decoded === 'object' && 'text' in decoded && !('Text' in decoded)) {
        return decoded.text != null && String(decoded.text).trim().length > 0
      }
      return codec.hasValidText(decoded)
    },

    canonicalCycle: (call) => {
      const tipField = call.tipField ?? call.tip ?? call.Tip?.FieldName
      const decoded = resultOf(
        codec.decodeCall(
          toList(catalogRules),
          mapOf({
            text: call.text ?? call.Text ?? '',
            tip: tipField,
            ...(call.evidence != null || call.Evidence != null
              ? { evidence: call.evidence ?? call.Evidence }
              : {}),
          }),
        ),
      )
      if (!decoded.ok) {
        throw new Error(`canonicalCycle fixture tip decode failed: ${decoded.error}`)
      }
      const value = cycle.ofCall(decoded.value)
      return {
        mergedText: value.MergedText,
        tip: {
          ruleId: value.CanonicalTip.RuleId,
          fieldName: value.CanonicalTip.FieldName,
          lexicalOrder: value.CanonicalTip.LexicalOrder,
        },
        mergedEvidence: value.MergedEvidence,
      }
    },
    isValidCycle: (merged) => {
      if (merged && typeof merged === 'object' && 'mergedText' in merged) {
        return String(merged.mergedText ?? '').trim().length > 0
      }
      return cycle.isValidCycle(merged)
    },
  }
})()

/**
 * EnforcerHost.ContinuationOutcome facade (VERIFY-008).
 * ProjectMessages = continue with non-empty provider view.
 * StopPhysicalRun = project non-empty messages then AbortSession.
 */
export const enforcerContinuation = (() => {
  return {
    tag: (outcome) => caseOf(outcome),
    isProject: (outcome) => caseOf(outcome) === 'ProjectMessages',
    isStop: (outcome) => caseOf(outcome) === 'StopPhysicalRun',
    messages: (outcome) => {
      const tag = caseOf(outcome)
      if (tag === 'ProjectMessages' || tag === 'StopPhysicalRun') {
        return listItems(outcome.fields[0])
      }
      throw new Error(`enforcerContinuation.messages: unexpected '${tag}'`)
    },
    reason: (outcome) => {
      if (caseOf(outcome) !== 'StopPhysicalRun') return undefined
      return outcome.fields[1]
    },
  }
})()