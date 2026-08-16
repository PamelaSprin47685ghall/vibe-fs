# Shared verification-system support inventory

Generated scanner support files: 16

## requirements/verification-system/tests/support/domain/context.mjs
Callers (3 resolved relative imports):
- requirements/verification-system/tests/support/domain.mjs <- ./domain/context.mjs
- requirements/verification-system/tests/support/domain/execution.mjs <- ./context.mjs
- requirements/verification-system/tests/support/domain/prompt.mjs <- ./context.mjs
### interop-helper (123)
- L72: const ToolRegistryModule = await prod('OpenCode/Tools/ToolRegistry')
- L99: * global, so Fable escapes it. `member()` tries the `$` spelling last, which is what
- L103: const m = bind(SyntheticTomlModule, 'SyntheticToml', [
- L125: tableArrayEntry: (name, fields) => m.tableArrayEntry(name, toList(fields)),
- L126: tableEntry: (name, fields) => m.tableEntry(name, toList(fields)),
- L132: encodeFs: (rewritten, created) => listItems(m.encodeFs(toList(rewritten), toList(created))),
- L133: document: (instructions, body) => m.document(toList(instructions), toList(body)),
- L143: const m = bind(MagicTodoModule, 'MagicTodo', [
- L176: toList(
- L201: toList(
- L223: const m = bind(MagicTodoAdmissionModule, 'MagicTodoAdmission', ['admitObligations'])
- L226: outcomeName: (outcome) => caseOf(outcome),
- L231: toList(current),
- L235: toList(submitted),
- L245: const m = bind(MagicTodoHostCodecModule, 'MagicTodoHostCodec', [
- L255: decodeInput: (args) => resultOf(m.tryDecodeInput(args)),
- L258: replaceCompatibilityArgs: (output, rows) => m.replaceCompatibilityArgs(output, toList(rows)),
- L269: const m = bind(MagicTodoProjectionModule, 'MagicTodoProjection', ['fold', 'foldConcluded'])
- L270: const codec = bind(MagicTodoFactCodecModule, 'MagicTodoFactCodec', ['encode', 'tryDecode'])
- L279: const folded = resultOf(m.fold(event, state, value))
- L282: : { ok: false, error: caseOf(folded.error) }
- L315: const m = bind(ToolResultBoundModule, 'ToolResultBound', [
- L345: const m = bind(XTraceModule, 'XTrace', [
- L365: const toCursorList = (items) => toList(items.map((item) => ({ ...item, Cursor: toCursor(item.Cursor.Sequence) })))
- L391: const result = m.flatten(toList(turns.map((turn) => ({ Role: turn.role, Parts: toList(turn.parts) }))))
- L402: toItems: (items) => toList(items),
- L412: const m = bind(XTraceCaptureModule, 'XTraceCapture', ['semanticPart', 'captureProjection', 'captureMessageView', 'captureOpening', 'captureTerminalText', 'captureLastWords', 'captureSessionMessages'])
- L427: return isNone(mapped) ? undefined : { tag: caseOf(mapped), part: mapped }
- L435: Tools: toList([]),
- L436: System: toList([]),
- L437: Messages: toList(
- L440: Parts: toList(
- L462: const result = await m.captureMessageView(journal, sessionIdValue, toList(capturedMessages))
- L467: resultOf(await m.captureSessionMessages(journal, sessionIdValue, toList(messages))),
- L471: m.captureOpening(journal, sessionIdValue, text, toList(requirements)),
- L485: const m = bind(LifecycleWorkRecordProjectionModule, 'LifecycleWorkRecordProjection', [
- L506: const m = bind(LifecycleWorkRecordModule, 'LifecycleWorkRecord', ['render', 'materialize', 'withConstitutive'])
- L509: AuthoritativeRequirements: toList(requirements),
- L517: m.withConstitutive(openingValue, toList(constitutiveItems)),
- L529: toList(frames),
- L530: toList(traceItems),
- L540: normalInstruction: CompanionPromptModule.asCommentedInstruction(toList(companionNormalLines)),
- L541: squashInstruction: CompanionPromptModule.asCommentedInstruction(toList(companionSquashLines)),
- L546: newWork: (toml) => CompanionPromptModule.newWorkMessage(toList(companionNormalLines), toml),
- L584: const m = bind(CompanionBuilderModule, 'CompanionProjectionBuilder', ['build', 'isFirstTurnShape'])
- L604: toList(frames.map((f) => [blobDigest(f.digest), f.body])),
- L606: toList(previousTips.map((t) => [t.field, t.cycleId])),
- L607: toList(companionNormalLines),
- L608: toList(companionSquashLines),
- L640: const m = bind(RecoverySlotModule, 'RecoverySlot', [
- L667: name: caseOf(decision),
- L668: clearsFailureCount: caseOf(decision) === 'CommitMain' ? payloadOf(decision) : undefined,
- L671: nextArmingName: caseOf(m.nextArming(decision)),
- L679: armingName: (arming) => caseOf(arming),
- L701: const m = bind(AssociationProj, 'SessionAssociationProjection', [
- L720: const main = unwrapOption(m.tryMainSessionOf(sessionId(id), current))
- L725: const blogger = unwrapOption(m.tryBloggerOf(sessionId(id), current))
- L731: const found = unwrapOption(m.tryFind(sessionId(id), current))
- L734: const kind = caseOf(found.Kind)
- L739: satelliteKind: kind === 'SatelliteSession' ? caseOf(found.Kind.fields[1]) : undefined,
- L749: const result = resultOf(
- L752: return result.ok ? result : { ok: false, error: caseOf(result.error), message: m.describe(result.error) }
- L767: const m = bind(CompactionPolicyModule, 'HostCompactionPolicy', [
- L785: name: caseOf(verdict),
- L806: const next = unwrapOption(
- L807: m.nextReanchor(toList(observed.map(providerRun)), (run) => handled.has(idValue.providerRun(run))),
- L832: const m = bind(ProbeSelectionModule, 'PrefixProbeSelection', ['select', 'describeNoCandidate'])
- L854: const result = resultOf(
- L870: return { ok: false, error: caseOf(result.error), message: m.describeNoCandidate(result.error) }
- L900: nameOf: (role) => caseOf(role),
- L903: [...RolesModule.Roles_permissions(roleOf(roleOrName))].some((permission) => caseOf(permission) === permissionName),
- L918: const m = bind(ManagedAgentCatalogModule, 'ManagedAgentCatalog', [
- L951: tryParseRole: (name) => unwrapOption(m.tryParseRole(name)),
- L957: tryParseTier: (name) => unwrapOption(m.tryParseTier(name)),
- L984: tryParseBookkeeperTier: (name) => unwrapOption(m.tryParseBookkeeperTier(name)),
- L986: bookkeeperPeerName: (name) => unwrapOption(m.bookkeeperPeerName(name)),
- L993: const SyncDelegateModule = await prod('Execution/Delegation/SyncDelegate/Model')
- L994: const SessionOwnershipModule = await prod('Execution/Session/Ownership')
- L1036: tryOwner: (ownership) => unwrapOption(SessionOwnershipModule.SessionOwnershipModule_tryOwner(ownership)),
- L1038: unwrapOption(SessionOwnershipModule.SessionOwnershipModule_attachmentKind(ownership)),
- L1066: const m = bind(XPrefixModule, 'XPrefixProjection', ['forSnapshot', 'forChoice', 'requiredBlob'])
- L1069: const name = caseOf(intent)
- L1081: const activation = payloadOf(intent)
- L1100: const ref = unwrapOption(m.requiredBlob(choice, committed))
- L1144: PreviousTips: toList(
- L1149: NormalInstructionLines: toList(intent.NormalInstructionLines ?? companionNormalLines),
- L1150: SquashInstructionLines: toList(intent.SquashInstructionLines ?? companionSquashLines),
- L1172: nameOf: (intent) => caseOf(intent),
- L1183: const planner = bind(ProjectionAlgebraModule, 'ProjectionPlanner', ['plan'])
- L1186: const renderer = bind(ProjectionAlgebraModule, 'ProjectionRenderer', ['renderPrefix', 'renderMessages', 'cutoffDigest'])
- L1192: const kind = caseOf(part)
- L1193: const payload = payloadOf(part)
- L1210: const name = caseOf(rendered)
- L1212: return { name, activation: payloadOf(rendered) }
- L1218: const result = resultOf(planner.plan(toList(intents)))
- L1221: return { ok: true, intents: listItems(result.value).map((intent) => caseOf(intent)) }
- L1225: const conflict = caseOf(error)
- L1226: const payload = payloadOf(error)
- L1233: first: caseOf(payload[0]),
- L1234: second: caseOf(payload[1]),
- L1241: renderPrefix: (intents) => renderOf(renderer.renderPrefix(toList(intents))),
- L1250: nameOf: (rendered) => caseOf(rendered),
- L1264: const render = member(ProjectionAlgebraModule, 'ProjectionRenderer', 'renderMessagesWithIntents')
- L1274: const parts = toList((m.parts || []).map(toWirePart))
- L1278: const encoded = toList(items.map(toWireMsg))
- L1279: return wireViewOf(render(snapshot, encoded, toList(orderedIntents)))
- L1287: const render = member(ProjectionAlgebraModule, 'ProjectionRenderer', 'renderMessagesWithHostIds')
- L1297: const parts = toList((m.parts || []).map(toWirePart))
- L1301: const encoded = toList(items.map(toWireMsg))
- L1302: const rendered = render(sha256, snapshot, encoded, toList(orderedIntents))
- L1346: out[name] = ProjectionAlgebraModule['ProjectionConstants_' + name] ?? member(ProjectionAlgebraModule, 'ProjectionConstants', name)
- L1384: BlogFrames: toList(blogFrames),
- L1395: nameOf: (choice) => caseOf(choice),
- L1420: const m = bind(MagicTodoLocalityModule, 'MagicTodoLocality', ['resolve'])
- L1424: resultOf(m.resolve(sessionIdValue, toList(messages), projection, callId)),
- L1429: const m = bind(MagicTodoMembraneModule, 'MagicTodoMembrane', ['prepare', 'accept'])
- L1433: resultOf(await m.prepare(journal, sessionIdValue, locality, inputDigest, planComplete, toList(obligations))),
- L1435: resultOf(await m.accept(journal, bridge, physicalEvidence, inputDigest, outputDigest)),
- L1444: const decodeContext = member(ToolHostCodecModule, 'ToolHostCodec', 'decodeContext')
- L1450: agent: unwrapOption(ctx.Agent),
- L1452: const id = unwrapOption(ctx.ToolCallId)
- L1456: const id = unwrapOption(ctx.ProviderRunId)
- L1459: promptText: unwrapOption(ctx.PromptText),
### du-shape (7)
- L361: const part = (kind, ...fields) => semanticPart(kind, fields)
- L416: const part = (kind, ...fields) => messagePart(kind, fields)
- L738: mainSessionId: kind === 'SatelliteSession' ? idValue.session(found.Kind.fields[0]) : undefined,
- L739: satelliteKind: kind === 'SatelliteSession' ? caseOf(found.Kind.fields[1]) : undefined,
- L1198: const [callId, result] = part.fields ?? (Array.isArray(payload) ? payload : [undefined, payload])
- L1307: const fields = id.fields
- L1416: return (name, ...fields) => build(name, fields)

## requirements/verification-system/tests/support/domain/enforcer.mjs
Callers (3 resolved relative imports):
- requirements/verification-system/tests/support/domain.mjs <- ./domain/enforcer.mjs
- requirements/verification-system/tests/support/domain/context.mjs <- ./enforcer.mjs
- requirements/verification-system/tests/support/domain/prompt.mjs <- ./enforcer.mjs
### interop-helper (44)
- L36: const EnforcerRepairModule = await prod('Enforcer/Repair')
- L40: aborted: EnforcerRepairModule.hasAbortedBlogAttempt(toList(rawMessages)),
- L41: errored: EnforcerRepairModule.hasErroredBlogAttempt(toList(rawMessages)),
- L77: gitTreeHash: (value) => unwrapOption(Witness.ReviewWitnessModule_gitTreeHash(value)),
- L78: confirmedReviewer: (value) => unwrapOption(Witness.ReviewWitnessModule_confirmedReviewer(value)),
- L83: unwrapOption(Witness.ReviewWitnessModule_confirm(barrier, challengeDigest, secondInputDigest, first, second)),
- L94: const payload = payloadOf(value)
- L96: switch (caseOf(value)) {
- L114: throw new Error(`unknown ReviewWitness case '${caseOf(value)}'`)
- L121: const m = bind(Challenge, 'ReviewChallenge', ['Path', 'TextVersion', 'promptOf', 'contentDigest'])
- L170: const m = bind(ReviewProj, 'ReviewProjection', [
- L183: const value = resultOf(result)
- L184: return value.ok ? value : { ok: false, error: caseOf(value.error) }
- L202: witness: caseOf(current.Witness),
- L211: const m = bind(ReviewProj, 'ReviewRequirementProjection', ['empty', 'addRequirement', 'clearOnConfirmation'])
- L236: decodeCapturedMessageView: (rawMessages) => listItems(ProjectionModule.decodeCapturedMessageView(toList(rawMessages))),
- L237: wireMessageView: (capturedMessages) => ProjectionModule.wireMessageView(toList(capturedMessages)),
- L244: const bindableRun = member(ReviewSealModule, 'ReviewSeal', 'bindableRun')
- L245: const projectMessages = member(SessionSnapshotPortModule, 'SessionSnapshotPort', 'projectMessages')
- L247: const name = caseOf(error)
- L268: const result = resultOf(bindableRun(physicalUser, list))
- L274: parentId: unwrapOption(msg.ParentId),
- L290: const parsed = unwrapOption(Authority.tryParseContinuationKind(name))
- L298: const api = bind(EnforcerCatalogResourceModule, 'EnforcerCatalogResource', [
- L308: api.composeBloggerSystemPrompt(basePrompt, toList(rules)),
- L310: api.composeBloggerSystemPromptFor(lang, basePrompt, toList(rules)),
- L322: const api = bind(EnforcerCatalogDomainModule, 'EnforcerCatalog', [
- L352: const result = resultOf(api.validate(schemaVersion, toList(rules)))
- L356: const found = api.tryFindByField(field, toList(rules))
- L359: fieldNames: (rules) => listItems(api.fieldNames(toList(rules))),
- L366: const catalog = bind(EnforcerCatalogResourceModule, 'EnforcerCatalogResource', ['load'])
- L367: const catalogDomain = bind(EnforcerCatalogDomainModule, 'EnforcerCatalog', [
- L372: const codec = bind(EnforcerCodecModule, 'EnforcerCodec', [
- L377: const cycle = bind(EnforcerCycleModule, 'EnforcerCycle', ['ofCall', 'isValidCycle'])
- L400: fieldNames: () => listItems(catalogDomain.fieldNames(toList(catalogRules))),
- L406: const rule = unwrapOption(catalogDomain.tryFindByField(field, toList(catalogRules)))
- L420: const result = resultOf(codec.decodeCall(toList(catalogRules), mapOf(rawArgs ?? {})))
- L443: const decoded = resultOf(
- L445: toList(catalogRules),
- L485: tag: (outcome) => caseOf(outcome),
- L486: isProject: (outcome) => caseOf(outcome) === 'ProjectMessages',
- L487: isStop: (outcome) => caseOf(outcome) === 'StopPhysicalRun',
- L489: const tag = caseOf(outcome)
- L496: if (caseOf(outcome) !== 'StopPhysicalRun') return undefined
### fsharp-type (1)
- L156: * `included` is an array of digest STRINGS and is converted to an `FSharpSet`
### du-shape (4)
- L229: toolResultDigests: (sha256, wire) => listItems(ProviderProj.toolResultDigests(sha256, wire)).map((d) => d.fields[0]),
- L249: const fields = error.fields ?? []
- L491: return listItems(outcome.fields[0])
- L497: return outcome.fields[1]

## requirements/verification-system/tests/support/domain/execution.mjs
Callers (1 resolved relative imports):
- requirements/verification-system/tests/support/domain.mjs <- ./domain/execution.mjs
### interop-helper (24)
- L50: const m = bind(Distillation, 'Distillation', [
- L70: const m = bind(BloggerTomlModule, 'BloggerToml', [
- L92: renderWith: (instructions, items) => m.renderWith(toList(instructions), toList(items)),
- L93: render: (items) => m.render(toList(items)),
- L108: kindOf: (item) => caseOf(item.Part),
- L120: const m = bind(BloggerDeltaModule, 'BloggerDelta', ['DeltaLimitBytes', 'nextChunk'])
- L135: messages: (turns) => toList(turns.map((turn) => ({ Role: turn.role, Parts: toList(turn.parts) }))),
- L141: const chunk = unwrapOption(m.nextChunk(limit, cursor, previousCutoff, messages))
- L148: kinds: listItems(chunk.Items).map((item) => caseOf(item.Part)),
- L169: const result = resultOf(TerminalValidity.check(text))
- L170: return result.ok ? { ok: true } : { ok: false, error: caseOf(result.error) }
- L188: const forkFn = fableInstanceMethod(ForkRuntimeModule, 'ForkRuntime', 'Fork')
- L189: const awaitFn = fableInstanceMethod(ForkRuntimeModule, 'ForkRuntime', 'AwaitAgent')
- L190: const cancelFn = fableInstanceMethod(ForkRuntimeModule, 'ForkRuntime', 'CancelAgent')
- L218: const distillSpool = member(Distillation, 'Distillation', 'distillSpool')
- L274: new ProcessRequest.Command(fileName, toList(args), workingDirectory, undefined, stdin, undefined, undefined),
- L304: const CausalWaitModule = await prod('Execution/Session/Wait/CausalWait')
- L305: const CausalWaitRegistryModule = await prod('Execution/Session/Wait/Registry')
- L306: const CausalAwaitModule = await prod('Execution/Session/Wait/Await')
- L313: owner: (kind, identity = []) => CausalWaitModule.CausalOwner_create(kind, toList(identity)),
- L317: buildCausalProducer('ExternalProducer', [kind, toList(identity)]),
- L343: toList(subject),
- L345: toList(escapes),
- L349: new CausalWaitModule.DiagnosticWaitSnapshot(toList(active), toList(history), sequence),
### du-shape (2)
- L82: const part = (kind, ...fields) => buildPart(kind, fields)
- L123: const part = (kind, ...fields) => semanticPart(kind, fields)

## requirements/verification-system/tests/support/domain/host.mjs
Callers (1 resolved relative imports):
- requirements/verification-system/tests/support/domain.mjs <- ./domain/host.mjs
### interop-helper (76)
- L72: const TerminalPolicyModule = await prod('OpenCode/Host/TerminalPolicy')
- L73: const ExplicitResumeSuppressionModule = await prod('OpenCode/Host/ExplicitResumeSuppression')
- L101: const m = bind(HostEventCodecModule, 'HostEventCodec', ['isHostSignalEvent', 'tryDecode'])
- L112: const m = bind(LinkageProj, 'HandleProjection', [
- L142: const value = resultOf(result)
- L143: return value.ok ? value : { ok: false, error: caseOf(value.error) }
- L165: tryFind: (handle, current) => unwrapOption(m.tryFind(handle, current)),
- L172: tryFindByChildSession: (child, current) => unwrapOption(m.tryFindByChildSession(child, current)),
- L173: tryFindByByname: (byname, current) => unwrapOption(m.tryFindByByname(byname, current)),
- L175: lifecycleOf: (record) => caseOf(record.Lifecycle),
- L183: const lifecycle = caseOf(record.Lifecycle)
- L189: const cell = payloadOf(record.Lifecycle)
- L190: completion = caseOf(cell.Kind)
- L194: abandonReason = caseOf(payloadOf(record.Lifecycle))
- L200: role: caseOf(record.CanonicalRole),
- L223: const ChildRecoveryModule = await prod('Execution/Delegation/Fork/ChildRecovery')
- L224: const terminalEvidenceCompleted = member(ChildRecoveryModule, 'TerminalEvidence', 'completed')
- L225: const terminalEvidenceFailed = member(ChildRecoveryModule, 'TerminalEvidence', 'failed')
- L226: const tryFromProvenTerminal = member(
- L233: const resolveChild = member(ChildRecoveryModule, 'ChildRecovery', 'resolveChild')
- L234: const fromDecoded = member(ChildRecoveryModule, 'JoinableCompletion', 'fromDecoded')
- L235: const falseTerminalReplacementAgentId = member(
- L240: const joinReturnedImpliesProofBeforeCommit = member(
- L295: tryFromProvenTerminal: (evidence) => resultOf(tryFromProvenTerminal(evidence)),
- L307: resolveChild(durable, snapshot, toList(observations)),
- L309: caseOf(resolveChild(durable, snapshot, toList(observations))),
- L326: joinReturnedImpliesProofBeforeCommit(toList(events)),
- L331: const m = bind(HandleControllerModule, 'HandleController', [
- L347: resultOf(await m.link(journal, parentId, agentId, childSessionId, targetAgent, role, ownership)),
- L349: const kindName = typeof kind === 'string' ? kind : caseOf(kind)
- L367: const proof = resultOf(tryFromProvenTerminal(evidence))
- L369: return resultOf(await m.recordCompletion(journal, parentId, proof.value))
- L372: resultOf(
- L381: retire: async (journal, parentId, agentId) => resultOf(await m.retire(journal, parentId, agentId)),
- L383: const value = resultOf(await m.consume(journal, parentId, handle))
- L384: return value.ok ? { ok: true, record: value.value } : { ok: false, error: caseOf(value.error) }
- L390: const m = bind(HandleCompletionCodecModule, 'HandleCompletionCodec', [
- L402: resultOf(m.tryDecode(record, agentId, json, completedAt)),
- L404: const value = resultOf(await m.tryRead(journal, record, agentId, completedAt))
- L405: return value.ok ? { ok: true, value: unwrapOption(value.value) } : { ok: false, error: value.error }
- L407: tryReadBody: (journal, record) => resultOf(m.tryReadBody(journal, record)),
- L441: const m = bind(DeadlineModule, 'Deadline', ['MaxTimerWaitMs', 'ofBudget', 'remaining', 'isExpired', 'nextWaitMs'])
- L458: const m = bind(PtyTimingModule, 'PtyTiming', [
- L525: const m = bind(PtyTimingModule, 'PtyTiming', ['createVirtualClockPort', 'nodeClockPort'])
- L548: const trySubscribeFn = bind(HostSignalSubscribeModule, 'HostSignalSubscribe', ['trySubscribe']).trySubscribe
- L577: const life = bind(HostForkRunLifecycleModule, 'HostForkRunLifecycle', [
- L583: const pending = bind(HostPendingRunModule, 'HostPendingRun', ['completionSource'])
- L629: const m = bind(DiagnosticModule, 'Diagnostic', ['emit', 'fatal'])
- L632: emit: (operation, fields) => m.emit(operation, toList(fields)),
- L634: fatal: (operation, fields) => m.fatal(operation, toList(fields)),
- L645: const m = bind(LoopDetectorModule, 'LoopDetector', [
- L658: state: caseOf(evaluation.State),
- L679: const m = bind(LoopEventCodecModule, 'LoopEventCodec', ['isLoopTextDelta', 'tryDecodeTextDelta'])
- L683: const decoded = unwrapOption(m.tryDecodeTextDelta(raw))
- L687: messageId: unwrapOption(decoded.MessageId),
- L688: partId: unwrapOption(decoded.PartId),
- L689: field: unwrapOption(decoded.Field),
- L698: const m = bind(RuntimeNudgeModule, 'RuntimeNudge', [
- L731: const recordConfirmedFailure = member(FallbackControllerModule, 'FallbackLedger', 'recordConfirmedFailure')
- L739: const result = resultOf(
- L743: let outcome = caseOf(result.value)
- L763: const api = bind(RuntimeResourcesModule, 'RuntimeResources', [
- L789: const api = bind(ManagedAgentConfigModule, 'ManagedAgentConfig', ['validate', 'configureFromHostConfig'])
- L791: validate: (config) => resultOf(api.validate(config)),
- L797: configure: (config) => resultOf(api.configureFromHostConfig(config)),
- L805: const m = bind(BloggerRequestContextModule, 'BloggerRequestContext', [
- L861: FrameDigests: toList(digests.map(blobDigest)),
- L869: toml: (ctx) => unwrapOption(m.toml(ctx)),
- L873: kindOf: (ctx) => caseOf(ctx),
- L880: const m = bind(BloggerRuntimeModule, 'BloggerRuntime', [
- L891: decideMaterial: (hasParked, hasFlight, ctx) => caseOf(m.decideMaterial(hasParked, hasFlight, ctx)),
- L899: drainOpenOf: (window) => caseOf(window) === 'Open',
- L917: const tag = caseOf(ctx)
- L971: const SessionRecoveryModule = await prod('Execution/Session/Recovery/Model')
- L974: const authorize = member(SessionRecoveryModule, 'SessionRecovery', 'authorizeFamilyResume')
- L975: const permitRoot = member(SessionRecoveryModule, 'FamilyRecoveryPermit', 'root')
### du-shape (3)
- L752: const tag = outcomeUnion?.tag ?? 0
- L919: const main = ctx.fields[0]
- L928: const squash = ctx.fields[0]

## requirements/verification-system/tests/support/domain/identity.mjs
Callers (8 resolved relative imports):
- requirements/verification-system/tests/support/domain.mjs <- ./domain/identity.mjs
- requirements/verification-system/tests/support/domain/context.mjs <- ./identity.mjs
- requirements/verification-system/tests/support/domain/enforcer.mjs <- ./identity.mjs
- requirements/verification-system/tests/support/domain/host.mjs <- ./identity.mjs
- requirements/verification-system/tests/support/domain/journal.mjs <- ./identity.mjs
- requirements/verification-system/tests/support/domain/orchestrator.mjs <- ./identity.mjs
- requirements/verification-system/tests/support/domain/persist.mjs <- ./identity.mjs
- requirements/verification-system/tests/support/domain/prompt.mjs <- ./identity.mjs
### interop-helper (1)
- L142: tryAgent: (handle) => unwrapOption(Identity.HandleIdModule_tryAgent(handle)),

## requirements/verification-system/tests/support/domain/interop.mjs
Callers (10 resolved relative imports):
- requirements/verification-system/tests/support/domain.mjs <- ./domain/interop.mjs
- requirements/verification-system/tests/support/domain/context.mjs <- ./interop.mjs
- requirements/verification-system/tests/support/domain/enforcer.mjs <- ./interop.mjs
- requirements/verification-system/tests/support/domain/execution.mjs <- ./interop.mjs
- requirements/verification-system/tests/support/domain/host.mjs <- ./interop.mjs
- requirements/verification-system/tests/support/domain/identity.mjs <- ./interop.mjs
- requirements/verification-system/tests/support/domain/journal.mjs <- ./interop.mjs
- requirements/verification-system/tests/support/domain/orchestrator.mjs <- ./interop.mjs
- requirements/verification-system/tests/support/domain/persist.mjs <- ./interop.mjs
- requirements/verification-system/tests/support/domain/prompt.mjs <- ./interop.mjs
### interop-helper (110)
- L11: //   - never read a DU `tag` ordinal; use caseOf()
- L166: prod('Foundation/Identity'),
- L167: prod('Foundation/Roles'),
- L168: prod('Composition/Durable/Fact'),
- L169: prod('Foundation/Outcome'),
- L170: prod('Persistence/Journal/Envelope'),
- L171: prod('Composition/Durable/Fold'),
- L172: prod('Persistence/Journal/FactCodec'),
- L173: prod('Persistence/Journal/Writer'),
- L174: prod('Context/Companion/Blogger/Projection'),
- L175: prod('Enforcer/Projection'),
- L176: prod('Context/Prefix/Epoch'),
- L177: prod('Participant/Provider/Attempt/Fallback/Projection'),
- L178: prod('Mission/Review/Barrier/Projection'),
- L179: prod('Execution/Delegation/LinkageProjection'),
- L180: prod('Change/Projection'),
- L181: prod('Execution/Session/Association'),
- L182: prod('Participant/Provider/Attempt/Cursor'),
- L183: prod('Participant/Provider/Attempt/TerminalValidity'),
- L184: prod('Context/Prefix/Candidate'),
- L185: prod('Participant/Provider/Attempt/RecoverySlot'),
- L186: prod('Host/CompactionPolicy'),
- L187: prod('OpenCode/Host/Diagnostic'),
- L188: prod('OpenCode/Codec/ProviderWireDecode'),
- L189: prod('OpenCode/Codec/ProviderWireCapture'),
- L190: prod('OpenCode/Codec/ProjectionMessageEdit'),
- L191: prod('Foundation/SyntheticToml'),
- L192: prod('Host/Contract/ToolResultBound'),
- L193: prod('Execution/Delegation/Fork/Payload'),
- L194: prod('Context/Companion/Blogger/Toml'),
- L195: prod('Context/Companion/Blogger/Delta'),
- L196: prod('Context/Companion/Prompt'),
- L197: prod('Context/Companion/Identity'),
- L198: prod('Context/Companion/Builder'),
- L199: prod('Context/Prefix/ProbeSelection'),
- L200: prod('Context/Prefix/Projection'),
- L201: prod('Participant/Provider/Projection/Intent'),
- L202: prod('Participant/Provider/Projection/Planner'),
- L203: prod('Participant/Provider/Projection/Renderer'),
- L204: prod('Participant/Provider/Attempt/Planner'),
- L205: prod('OpenCode/Tools/Distillation'),
- L206: prod('Interaction/Authority/Model'),
- L207: prod('Interaction/Authority/Run'),
- L208: prod('Mission/Review/Judgement/Witness'),
- L209: prod('Mission/Review/Judgement/Challenge'),
- L210: prod('Participant/Provider/Projection/Model'),
- L211: prod('Context/Trace/Model'),
- L212: prod('Mission/Obligation/Todo/Model'),
- L213: prod('Mission/Obligation/Todo/Admission'),
- L214: prod('Mission/Obligation/Todo/Facts'),
- L215: prod('Mission/Obligation/Todo/Projection'),
- L216: prod('Mission/Obligation/Todo/MagicTodoFactCodec'),
- L217: prod('Mission/WorkRecord/Model'),
- L218: prod('Participant/Persona/ManagedCatalog'),
- L219: Promise.all([prod('Participant/Provider/Language'), prod('Participant/Provider/SessionLanguage')]).then(([p, s]) => ({ ...p, ...s })),
- L220: prod('Context/Trace/Capture'),
- L221: prod('Mission/WorkRecord/Materialize'),
- L222: prod('OpenCode/Codec/HostMessageCodec'),
- L223: prod('Process/Deadline'),
- L224: prod('Process/ProcessRequest'),
- L225: prod('Foundation/Parallel'),
- L226: prod('Change/Types'),
- L227: prod('Resources/RuntimeResources'),
- L228: prod('Resources/EnforcerCatalogResource'),
- L229: prod('Resources/PackageResources'),
- L230: prod('Resources/PromptResources'),
- L231: prod('Resources/ProviderResources'),
- L232: prod('Enforcer/Catalog'),
- L233: prod('Enforcer/Codec'),
- L234: prod('Enforcer/Cycle/Model'),
- L235: prod('Context/Companion/Blogger/Request'),
- L236: prod('Context/Companion/Blogger/Runtime/State'),
- L237: prod('Context/Companion/Blogger/Runtime/ParkedTransform'),
- L238: prod('OpenCode/Host/PluginRuntimeScope'),
- L239: prod('OpenCode/Host/SharedState'),
- L240: prod('Persistence/Journal/AgentJournal'),
- L241: prod('Interaction/Dispatch/Dispatcher'),
- L242: prod('Interaction/Dispatch/Send'),
- L243: prod('OpenCode/Codec/HostEventCodec'),
- L244: prod('Execution/Session/LoopDetector'),
- L245: prod('OpenCode/Codec/LoopEventCodec'),
- L246: prod('Interaction/Dispatch/Nudge'),
- L247: prod('Participant/Provider/Attempt/Fallback/Ledger'),
- L248: prod('Execution/Delegation/Handle/Controller'),
- L249: prod('Execution/Delegation/Handle/CompletionCodec'),
- L250: prod('Execution/Delegation/Handle/JoinDrain'),
- L251: prod('Mission/Review/Assurance/Seal'),
- L252: prod('OpenCode/Host/SessionSnapshotPort'),
- L253: prod('Mission/Obligation/Todo/MagicTodoLocality'),
- L254: prod('Mission/Obligation/Todo/MagicTodoMembrane'),
- L255: prod('OpenCode/Codec/ToolHostCodec'),
- L256: prod('Mission/Obligation/Todo/OpenCode/HostCodec'),
- L257: prod('Execution/Session/Wait/CompletionMailbox'),
- L258: prod('Execution/Session/AgentCompletion'),
- L259: prod('Execution/Delegation/Fork/Host/RunLifecycle'),
- L260: prod('Execution/Delegation/Fork/Host/PendingRun'),
- L261: prod('OpenCode/Host/Events'),
- L262: prod('Composition/Turn/Supervisor'),
- L263: prod('Composition/Turn/Binding'),
- L264: prod('Execution/Delegation/Fork/Runtime'),
- L265: prod('Execution/Delegation/Fork/Model'),
- L266: prod('OpenCode/Signals/HostSignalSubscribe'),
- L267: prod('OpenCode/Host/ManagedAgentConfig'),
- L288: prod('Process/NodeProcessWait'),
- L289: prod('Process/NodeProcessHost'),
- L290: prod('Process/PtyTiming'),
- L311: // `member()` absorbs that rule and THROWS when no spelling exists. It is
- L342: Object.fromEntries(names.map((name) => [name, member(mod, moduleName, name)]))
- L394: export const mapTryFind = (key, map) => unwrapOption(FsMap.tryFind(key, map))
- L479: caseOf(value) === 'Ok' ? { ok: true, value: payloadOf(value) } : { ok: false, error: payloadOf(value) }
### fsharp-type (12)
- L15: //   - never touch FSharpMap/FSharpList internals; use mapEntries()/listItems()
- L386: /** FSharpMap → [key, value] pairs, insertion-independent (F# maps are sorted). */
- L389: /** FSharpMap → plain object, for maps keyed by a string-like identity. */
- L396: /** Plain object → FSharpMap (for string-keyed maps). */
- L414: /** [key,value][] → FSharpMap without exposing compiler-runtime imports to tests. */
- L417: /** FSharpList → array. */
- L427: * array → `FSharpSet<string>`.
- L439: * array → FSharpList.
- L456: * lacking FSharpList structure here is a mistake the caller must see — most
- L463: `${label} expects an envelope sequence (array or FSharpList), received ${JSON.stringify(value)?.slice(0, 80)}`,
- L489: export const okResult = (value) => new FsResult.FSharpResult$2(0, [value])
- L490: export const errorResult = (value) => new FsResult.FSharpResult$2(1, [value])
### fable-modules (1)
- L37: export const FABLE_MODULES = join(BUILD_ROOT, 'fable_modules')
### du-shape (5)
- L374: if (typeof value.cases !== 'function' || typeof value.tag !== 'number') {
- L377: return value.cases()[value.tag]
- L382: const fields = value?.fields ?? []
- L497: export const caseNames = (unionClass) => Object.create(unionClass.prototype).cases()
- L538: export const offsetValue = (offset) => (offset === undefined ? undefined : offset.tag)
### export-discovery (1)
- L541: const keys = Object.keys(mod)
### mangled-lookup (1)
- L545: keys.find((k) => k.endsWith(`__${methodName}`) || k.endsWith(`_${methodName}`))

## requirements/verification-system/tests/support/domain/journal.mjs
Callers (3 resolved relative imports):
- requirements/verification-system/tests/support/domain.mjs <- ./domain/journal.mjs
- requirements/verification-system/tests/support/domain/host.mjs <- ./journal.mjs
- requirements/verification-system/tests/support/domain/prompt.mjs <- ./journal.mjs
### interop-helper (30)
- L130: const dispatch = caseOf(value)
- L134: return caseOf(payloadOf(value))
- L204: const Envelopes = bind(EnvelopeModule, 'Envelope', ['serialize', 'deserialize', 'compareSortKey'])
- L205: const Folds = bind(FoldModule, 'Fold', ['empty', 'foldEnvelope', 'foldAgentFact'])
- L210: const next = resultOf(Folds.foldEnvelope(current, env))
- L219: deserialize: (line) => resultOf(Envelopes.deserialize(line)),
- L221: deserializeFact: (json) => resultOf(FactCodec.deserializeFact(json)),
- L234: apply: (projection, envelopes) => foldSequence(projection, listItems(requireList(toList(envelopes), 'fold.apply'))),
- L236: one: (projection, env) => resultOf(Folds.foldEnvelope(projection, env)),
- L279: side: (offset) => caseOf(Cursor.side(offsetOf(offset))),
- L290: recoveryVerdict: (budget, value) => caseOf(Cursor.recoveryVerdict(budget, cursorOf(value))),
- L305: const m = bind(FallbackProj, 'FallbackProjection', [
- L318: const result = resultOf(m.applyAdvance(identity, offsetOf(prevOffset), offsetOf(nextOffset), count, current))
- L319: return result.ok ? result : { ok: false, error: caseOf(result.error) }
- L346: const m = bind(BlogProj, 'BlogProjection', [
- L375: frameKinds: (state) => listItems(m.frames(state)).map((f) => caseOf(f.Kind)),
- L387: coverableFrameKinds: (state) => listItems(m.coverableFrames(state)).map((f) => caseOf(f.Kind)),
- L393: const result = resultOf(
- L405: return result.ok ? result : { ok: false, error: caseOf(result.error) }
- L409: const result = resultOf(m.applySquash(frameEpochId(previousEpoch), frameEpochId(nextEpoch), count, frame, state))
- L410: return result.ok ? result : { ok: false, error: caseOf(result.error) }
- L422: const m = bind(EnforcementProj, 'EnforcementProjection', [
- L450: ToolCallIds: toList(toolCallIds.map((id) => (typeof id === 'string' ? toolCallId(id) : id))),
- L459: applyFromEntry: (state, record) => resultOf(m.applyFromEntry(state, record)),
- L466: return unwrapOption(m.tryFindByProviderRun(key, state))
- L484: const m = bind(PrefixProj, 'PrefixEpochProjection', [
- L507: const result = resultOf(m.applyRebase(prefixEpochId(previousEpoch), prefixEpochId(nextEpoch), candidate, state))
- L508: return result.ok ? result : { ok: false, error: caseOf(result.error) }
- L519: const result = resultOf(
- L522: return result.ok ? result : { ok: false, error: caseOf(result.error) }

## requirements/verification-system/tests/support/domain/orchestrator.mjs
Callers (1 resolved relative imports):
- requirements/verification-system/tests/support/domain.mjs <- ./domain/orchestrator.mjs
### interop-helper (72)
- L42: const outcome = caseOf(completion.Outcome)
- L43: const payload = payloadOf(completion.Outcome)
- L54: const m = bind(JoinDrainModule, 'JoinDrain', [
- L63: const outcome = caseOf(c.Outcome)
- L64: const payload = payloadOf(c.Outcome)
- L108: const value = resultOf(await m.drainFromJournal(journal, parentId, maxCount, completedAt))
- L112: error: typeof value.error === 'string' ? value.error : caseOf(value.error),
- L123: const m = bind(OrchestratorProj, 'OrchestratorProjection', [
- L138: tryFind: (jobId, current) => unwrapOption(m.tryFind(jobId, current)),
- L139: tryFindByManagerSession: (session, current) => unwrapOption(m.tryFindByManagerSession(session, current)),
- L140: tryWorktreeEffect: (identity, current) => unwrapOption(m.tryWorktreeEffect(identity, current)),
- L148: recoveryAction: (currentHead, job) => caseOf(m.recoveryAction(currentHead, job)),
- L149: recoveryActionPayload: (currentHead, job) => payloadOf(m.recoveryAction(currentHead, job)),
- L150: progressOf: (job) => caseOf(job.Progress),
- L153: const status = unwrapOption(m.tryWorktreeEffect(identity, current))
- L154: return status === undefined ? undefined : caseOf(status)
- L165: const name = caseOf(verdict)
- L192: const publishPtyFn = fableInstanceMethod(
- L197: const pulseAgentFn = fableInstanceMethod(
- L202: const cancelFn = fableInstanceMethod(CompletionMailboxModule, 'CompletionMailbox', 'Cancel')
- L203: const pendingCountFn = fableInstanceMethod(
- L208: const pendingPtyCountFn = fableInstanceMethod(
- L213: const isCancelledFn = fableInstanceMethod(
- L218: const drainPtyFn = fableInstanceMethod(
- L223: const drainAgentWakesFn = fableInstanceMethod(
- L228: const waitForSignalFn = fableInstanceMethod(
- L233: const waitForWakeFn = fableInstanceMethod(
- L238: const pulseWakeFn = fableInstanceMethod(CompletionMailboxModule, 'CompletionMailbox', 'PulseWake')
- L355: const createFn = member(CompletionMailboxModule, 'JoinInterrupt', 'create')
- L365: const ofHeadTailFn = member(CompletionMailboxModule, 'NonEmptyBatch', 'ofHeadTail')
- L366: const tryOfListFn = member(CompletionMailboxModule, 'NonEmptyBatch', 'tryOfList')
- L367: const toListFn = member(CompletionMailboxModule, 'NonEmptyBatch', 'toList')
- L368: const lengthFn = member(CompletionMailboxModule, 'NonEmptyBatch', 'length')
- L370: ofHeadTail: (head, tail = []) => ofHeadTailFn(head, toList(tail)),
- L371: tryOfList: (items) => unwrapOption(tryOfListFn(toList(items))),
- L379: nameOf: (outcome) => caseOf(outcome),
- L380: isInterrupted: (outcome) => caseOf(outcome) === 'InterruptedByUserMessage',
- L382: if (caseOf(outcome) !== 'ResultsAvailable') {
- L383: throw new Error(`expected ResultsAvailable, got ${caseOf(outcome)}`)
- L385: return payloadOf(outcome)
- L391: nameOf: (reason) => caseOf(reason),
- L419: const staticFn = fableInstanceMethod(EventsModule, 'Events_HostEventPort', name)
- L459: const kickFn = fableInstanceMethod(ReconcileSupervisorModule, 'Supervisor', 'Kick')
- L460: const bindUserFn = fableInstanceMethod(ReconcileSupervisorModule, 'Supervisor', 'BindUserMessage')
- L461: const clearSessionFn = fableInstanceMethod(ReconcileSupervisorModule, 'Supervisor', 'ClearSession')
- L510: return Promise.resolve(okResult(toList(next.messages)))
- L577: const JoinModule = await prod('Execution/Delegation/Join')
- L580: const JoinResultRendererModule = await prod('Execution/Delegation/Fork/OpenCode/JoinResultRenderer')
- L581: const ManagerJobModule = await prod('Change/Job')
- L601: const ofSimpleTextFn = member(AgentCompletionModule, 'AgentCompletion', 'ofSimpleText')
- L602: const ofSimpleErrorFn = member(AgentCompletionModule, 'AgentCompletion', 'ofSimpleError')
- L603: const failedFn = member(AgentCompletionModule, 'AgentCompletion', 'failed')
- L604: const abandonedFn = member(AgentCompletionModule, 'AgentCompletion', 'abandoned')
- L605: const statusFn = member(AgentCompletionModule, 'AgentCompletion', 'status')
- L606: const textFn = member(AgentCompletionModule, 'AgentCompletion', 'text')
- L608: const joinItemOfRunCompletionFn = member(AgentCompletionModule, 'JoinItem', 'ofRunCompletion')
- L687: const renderInterruptedFn = member(JoinResultRendererModule, 'JoinResultRenderer', 'renderInterrupted')
- L688: const renderJoinItemBatchFn = member(JoinResultRendererModule, 'JoinResultRenderer', 'renderJoinItemBatch')
- L689: const renderOrchestratorBatchFn = member(JoinResultRendererModule, 'JoinResultRenderer', 'renderOrchestratorBatch')
- L690: const renderForkErrorFn = member(JoinResultRendererModule, 'JoinResultRenderer', 'renderForkError')
- L743: const publishFn = fableInstanceMethod(ManagerJobModule, 'VerdictMailbox', 'Publish')
- L744: const startJobFn = fableInstanceMethod(ManagerJobModule, 'VerdictMailbox', 'StartJob')
- L745: const drainFn = fableInstanceMethod(ManagerJobModule, 'VerdictMailbox', 'DrainAvailable')
- L746: const tryJoinBatchFn = fableInstanceMethod(ManagerJobModule, 'VerdictMailbox', 'TryJoinBatch')
- L747: const tryJoinFn = fableInstanceMethod(ManagerJobModule, 'VerdictMailbox', 'TryJoin')
- L748: const joinAvailableFn = fableInstanceMethod(ManagerJobModule, 'VerdictMailbox', 'JoinAvailable')
- L749: const pendingCountFn = fableInstanceMethod(ManagerJobModule, 'VerdictMailbox', 'get_PendingCount')
- L750: const hasActiveFn = fableInstanceMethod(ManagerJobModule, 'VerdictMailbox', 'get_HasActive')
- L771: nameOf: (verdict) => caseOf(verdict),
- L778: const ReconcileProgramModule = await prod('Composition/Turn/Program')
- L779: const ReconcileSurfaceModule = await prod('Composition/Turn/ReconcileSurface')
- L852: return { accepted: true, name: caseOf(value) }
### du-shape (7)
- L85: if (key && typeof key === 'object' && Array.isArray(key.fields)) {
- L86: return { creationOrder: key.fields[0], targetAgent: key.fields[1] }
- L166: const fields = verdict.fields ?? []
- L300: const ptyIdOf = (item) => item.fields[0].PtyId
- L315: const fields = outcomePayload.fields
- L839: return Object.create(ctor.prototype).cases()
- L845: const fields = reflection().fields() ?? []

## requirements/verification-system/tests/support/domain/persist.mjs
Callers (1 resolved relative imports):
- requirements/verification-system/tests/support/domain.mjs <- ./domain/persist.mjs
### interop-helper (18)
- L27: prod('Persistence/Journal/EventStoreJournalWriter'),
- L28: prod('Persistence/EventStore/ProcessEventLog'),
- L29: prod('Persistence/Journal/EventStoreJournalCodec'),
- L99: const decoded = resultOf(readStreams(pair.commonDir))
- L107: const decoded = resultOf(tryDecodeJournal(event))
- L116: return caseOf(result) === 'Committed'
- L117: ? { committed: true, envelope: payloadOf(result) }
- L118: : { committed: false, eventId: idValue.event(result.fields[0]), failure: caseOf(result.fields[1]), reason: result.fields[1]?.fields?.[0] }
- L160: const AgentJournalCreate = bind(AgentJournalModule, 'AgentJournal', [
- L201: const result = resultOf(AgentJournalCreate.createFromEventStore(writer, initEnvelope))
- L208: const resumed = resultOf(await esWriterResume(runtimeId(runtime), pid, utcOffset(startedAt), pair.store))
- L211: const result = resultOf(AgentJournalCreate.createFromProjection(writer, projection))
- L216: appendAgent: async (streamId, run, fact, journal) => resultOf(await AgentJournalCreate.appendAgent(streamId, run, fact, journal)),
- L217: appendMagicTodo: async (streamId, run, fact, journal) => resultOf(await AgentJournalCreate.appendMagicTodo(streamId, run, fact, journal)),
- L218: appendManagerLifecycle: async (streamId, fact, journal) => resultOf(await AgentJournalCreate.appendManagerLifecycle(streamId, fact, journal)),
- L224: writeBlob: async (content, journal) => resultOf(await AgentJournalCreate.writeBlob(content, journal)),
- L225: readBlob: async (journal, ref) => resultOf(await durableJournal.blobWriter(journal).Read(ref)),
- L262: return { Envelopes: toList(journalEnvelopes(pair)), Diagnostics: toList([]), Frontier: mapOf({}) }
### export-discovery (1)
- L34: const hit = Object.entries(mod).find(([name]) => name.startsWith(prefix))
### du-shape (2)
- L118: : { committed: false, eventId: idValue.event(result.fields[0]), failure: caseOf(result.fields[1]), reason: result.fields[1]?.fields?.[0] }
- L179: const relative = typeof blobRef === 'string' ? blobRef : blobRef?.fields?.[0]

## requirements/verification-system/tests/support/domain/prompt.mjs
Callers (3 resolved relative imports):
- requirements/verification-system/tests/support/domain.mjs <- ./domain/prompt.mjs
- requirements/verification-system/tests/support/domain/execution.mjs <- ./prompt.mjs
- requirements/verification-system/tests/support/domain/orchestrator.mjs <- ./prompt.mjs
### interop-helper (31)
- L51: const m = bind(PrefixCandidateModule, 'ProviderRequestKind', [
- L67: nameOf: (kind) => caseOf(kind),
- L82: const m = bind(AttemptPlannerModule, 'AttemptPlanner', ['plan', 'probeOf', 'promotableProbe'])
- L132: const noProbeReason = unwrapOption(plan.NoProbeReason)
- L133: const probe = unwrapOption(m.probeOf(plan))
- L137: requestKind: caseOf(plan.Profile.RequestKind),
- L138: choice: caseOf(plan.Profile.ProjectionChoice),
- L140: canonicalRole: caseOf(plan.Profile.Authority.CanonicalRole),
- L143: noProbeReason: isNone(noProbeReason) ? undefined : caseOf(noProbeReason),
- L150: const probe = unwrapOption(m.promotableProbe(plan.value, buildOutcome(outcome, [])))
- L161: tryParseContinuationKind: (name) => unwrapOption(Authority.tryParseContinuationKind(name)),
- L163: tryParseRole: (name) => unwrapOption(Authority.tryParseRole(name)),
- L165: tryParseTier: (name) => unwrapOption(Authority.tryParseTier(name)),
- L168: parseAgentName: (name) => resultOf(Authority.parseAgentNameTyped(name)),
- L195: resultOf(AuthorityRun.createAuthorityRoot(sha256, runtime, session, kind, physical, agent)),
- L197: resultOf(AuthorityRun.claimAgentOwnerRoot(key, session, payloadDigest, agent)),
- L206: caseOf(AuthorityRun.resolveKnownOrigin(physical, key, hostCompact, projection)),
- L255: const journalSnapshot = member(AgentJournalModule, 'AgentJournal', 'snapshot')
- L258: const value = resultOf(result)
- L345: const m = bind(SessionSnapshotPortModule, 'SessionSnapshotPort', ['projectMessages', 'locateToolCall'])
- L349: locateToolCall: (callId, messages) => resultOf(m.locateToolCall(callId, toList(messages))),
- L365: const api = bind(PromptResourcesModule, 'PromptResources', [
- L384: const lang = bind(ProviderLanguageModule, 'ProviderLanguage', [
- L391: const session = bind(ProviderLanguageModule, 'SessionProviderLanguage', [
- L405: nameOf: (value) => caseOf(value),
- L408: tryParse: (raw) => unwrapOption(lang.tryParse(raw)),
- L412: tryGet: (id) => unwrapOption(session.tryGet(id)),
- L414: bindOnce: (id, language) => resultOf(session.bindOnce(id, language)),
- L415: inheritFromOwner: (ownerLanguage, childId) => resultOf(session.inheritFromOwner(ownerLanguage, childId)),
- L421: const api = bind(ProviderResourcesModule, 'ProviderResources', [
- L433: tryReadText: (lang, semanticPath) => unwrapOption(api.tryReadText(lang, semanticPath)),
### export-discovery (1)
- L375: allForLanguage: (lang) => Object.values(api.loadForLanguage(lang)),

## requirements/verification-system/tests/support/glory.mjs
Callers (12 resolved relative imports):
- requirements/finality/tests/lifecycle.test.mjs <- ../../verification-system/tests/support/glory.mjs
- requirements/finality/tests/lifecycle.test.mjs <- ../../verification-system/tests/support/glory.mjs
- requirements/finality/tests/lifecycle.test.mjs <- ../../verification-system/tests/support/glory.mjs
- requirements/finality/tests/lifecycle.test.mjs <- ../../verification-system/tests/support/glory.mjs
- requirements/finality/tests/lifecycle.test.mjs <- ../../verification-system/tests/support/glory.mjs
- requirements/finality/tests/lifecycle.test.mjs <- ../../verification-system/tests/support/glory.mjs
- requirements/finality/tests/lifecycle.test.mjs <- ../../verification-system/tests/support/glory.mjs
- requirements/finality/tests/lifecycle.test.mjs <- ../../verification-system/tests/support/glory.mjs
- requirements/finality/tests/lifecycle.test.mjs <- ../../verification-system/tests/support/glory.mjs
- requirements/finality/tests/lifecycle.test.mjs <- ../../verification-system/tests/support/glory.mjs
- requirements/finality/tests/lifecycle.test.mjs <- ../../verification-system/tests/support/glory.mjs
- requirements/finality/tests/lifecycle.test.mjs <- ../../verification-system/tests/support/glory.mjs
### export-discovery (1)
- L19: const exported = module[`${name}`] ?? module[Object.keys(module).find((k) => k.endsWith(`_${name}`))]

## requirements/verification-system/tests/support/local-event-store.mjs
Callers (4 resolved relative imports):
- requirements/speculative-investigation/tests/durability-port.test.mjs <- ../../verification-system/tests/support/local-event-store.mjs
- requirements/speculative-investigation/tests/integration/strength/lifecycle.test.mjs <- ../../../../verification-system/tests/support/local-event-store.mjs
- requirements/speculative-investigation/tests/store.test.mjs <- ../../verification-system/tests/support/local-event-store.mjs
- requirements/verification-system/tests/support/domain/persist.mjs <- ../local-event-store.mjs
### deep-dist-import (2)
- L6: const Store = await import('../../../../dist/Persistence/EventStore/Store.js')
- L7: const Integrator = await import('../../../../dist/Persistence/EventStore/CanonicalIntegrator.js')

## requirements/verification-system/tests/support/orchestrator-host-harness.mjs
Callers (1 resolved relative imports):
- requirements/review-assurance/tests/host-reverify.test.mjs <- ../../verification-system/tests/support/orchestrator-host-harness.mjs
### deep-dist-import (1)
- L33: const { OrchestratorHostDeps } = await import('../../../../dist/Change/Host/Types.js')
### du-shape (3)
- L39: fields: [{ fields: [`manager/${jobId.fields?.[0] ?? jobId}`], tag: 0, cases: () => ['WorktreeIdentity'] }],
- L62: const sessionKey = (value) => value?.fields?.[0] ?? value
- L78: calls.push(['AbortSession', id.fields?.[0] ?? id])
### interop-helper (4)
- L44: ConflictedFiles: async () => ({ tag: 0, fields: [toList([])] }),
- L49: behaviour.listError ? { tag: 1, fields: [behaviour.listError] } : { tag: 0, fields: [toList([])] },
- L50: ListManagerBranches: async () => ({ tag: 0, fields: [toList([])] }),
- L108: ListChildren: async () => ({ tag: 0, fields: [toList([])] }),

## requirements/verification-system/tests/support/plugin-fixture.mjs
Callers (10 resolved relative imports):
- requirements/capability-enforcement/tests/auto-injected-tool.test.mjs <- ../../verification-system/tests/support/plugin-fixture.mjs
- requirements/capability-enforcement/tests/bash-honeypot-tool.test.mjs <- ../../verification-system/tests/support/plugin-fixture.mjs
- requirements/capability-enforcement/tests/fork-tool.test.mjs <- ../../verification-system/tests/support/plugin-fixture.mjs
- requirements/capability-enforcement/tests/integration/plugin/auto-injected-tool.test.mjs <- ../../../../verification-system/tests/support/plugin-fixture.mjs
- requirements/capability-enforcement/tests/integration/plugin/bash-honeypot-tool.test.mjs <- ../../../../verification-system/tests/support/plugin-fixture.mjs
- requirements/capability-enforcement/tests/integration/plugin/manager-tool-contract.test.mjs <- ../../../../verification-system/tests/support/plugin-fixture.mjs
- requirements/finality/tests/rewrite-consistency.test.mjs <- ../../verification-system/tests/support/plugin-fixture.mjs
- requirements/obligation-ledger/tests/magic-todo-host-canaries.test.mjs <- ../../verification-system/tests/support/plugin-fixture.mjs
- requirements/repository-programming/tests/integration/plugin/file-mutation-tools.test.mjs <- ../../../../verification-system/tests/support/plugin-fixture.mjs
- requirements/review-judgement/tests/verdict-tool.test.mjs <- ../../verification-system/tests/support/plugin-fixture.mjs
### deep-dist-import (16)
- L17: const { initSpikePlugin } = await import('../../../../dist/OpenCode/Plugin/SpikePlugin.js')
- L18: const { requiredNames: managedAgentNames } = await import('../../../../dist/Participant/Persona/ManagedCatalog.js')
- L37: const { forWorkspace, gitCommonDir } = await import('../../../../dist/Persistence/Journal/RuntimePath.js')
- L38: const { acquire: acquireJournal, release: releaseJournal } = await import('../../../../dist/Persistence/Journal/SharedAgentJournal.js')
- L39: const { acquire: acquireTerminalBus } = await import('../../../../dist/OpenCode/Host/SharedTerminalBus.js')
- L40: const { bootPort } = await import('../../../../dist/OpenCode/Host/WorkspaceEventStore.js')
- L50: const { TerminalOutcome } = await import('../../../../dist/OpenCode/Host/Events.js')
- L51: const { AgentRunResult } = await import('../../../../dist/Foundation/Outcome.js')
- L55: } = await import('../../../../dist/Execution/Delegation/Handle/Controller.js')
- L56: const ChildRecovery = await import('../../../../dist/Execution/Delegation/Fork/ChildRecovery.js')
- L57: const { ManagerLifecycleFact } = await import('../../../../dist/Composition/Durable/Fact.js')
- L58: const { StreamId } = await import('../../../../dist/Persistence/Journal/Envelope.js')
- L64: } = await import('../../../../dist/Mission/Obligation/Todo/Facts.js')
- L65: const { TodoWriteIdModule_create } = await import('../../../../dist/Mission/Obligation/Todo/Model.js')
- L66: const { XTraceCursor } = await import('../../../../dist/Context/Trace/Model.js')
- L67: const { AgentJournalModule_appendManagerLifecycle, AgentJournalModule_appendMagicTodo } = await import('../../../../dist/Persistence/Journal/AgentJournal.js')
### du-shape (23)
- L263: if (resumed.tag !== 0) return resumed
- L264: const [writer, , projection] = resumed.fields[0]
- L270: if (journalResult.tag !== 0) throw new Error(`journal acquire rejected: ${journalResult.fields?.[0]?.Reason}`)
- L272: journal: journalResult.fields[0],
- L273: runtimeId: AgentJournalModule_runtimeId(journalResult.fields[0]).fields[0],
- L295: const sessionId = sessionIdUnion.fields[0]
- L302: // outcome.tag 0 = Completed; other tags are proven Failed (not raw Aborted).
- L307: outcome.tag === 0
- L327: outcome.tag === 0
- L331: if (proof.tag !== 0) {
- L332: throw new Error(`JoinableCompletion(${agentId}) rejected: ${proof.fields?.[0]}`)
- L334: const recording = recordCompletion(runtime.journal, SessionIdModule_create(parentSessionId), proof.fields[0]).then((recorded) => {
- L335: if (recorded.tag !== 0) {
- L336: throw new Error(`HandleCompleted(${agentId}) rejected: ${recorded.fields?.[0]}`)
- L414: if (result.tag !== 0) {
- L415: throw new Error(`AcceptHumanRoot(${sessionId}, ${agent}) rejected: ${result.fields?.[0]}`)
- L433: if (result.tag !== 0) {
- L435: `AcceptAgentOwnerRoot(${childSessionId}, ${promptKey}) rejected: ${result.fields?.[0]}`,
- L500: if (preparedResult.tag !== 0) {
- L501: throw new Error(`TodoWritePrepared(${sessionId}) rejected: ${JSON.stringify(preparedResult.fields?.[0])}`)
- L507: preparedResult.fields[0].EventId,
- L519: if (acceptedResult.tag !== 0) {
- L520: throw new Error(`TodoWriteAccepted(${sessionId}) rejected: ${JSON.stringify(acceptedResult.fields?.[0])}`)

## requirements/verification-system/tests/support/run-inner.mjs
Callers (0 resolved relative imports):
### fable-modules (5)
- L49: // the totals a true whole-codebase number, every production module (dist minus fable_modules)
- L72: const modules = walk('dist', ['.js']).filter((file) => !file.includes('fable_modules'))
- L88: console.error(`coverage: pre-imported ${modules.length} production modules (excluding fable_modules)`)
- L109: // themselves, the Fable runtime (fable_modules), vendored packages and repo tooling
- L113: '**/fable_modules/**',

## requirements/verification-system/tests/support/temporal-harness.mjs
Callers (2 resolved relative imports):
- requirements/provider-attempt-recovery/tests/fallback-aabb-confluence.test.mjs <- ../../verification-system/tests/support/temporal-harness.mjs
- requirements/verification-system/tests/temporal-harness.test.mjs <- ./support/temporal-harness.mjs
### fsharp-type (1)
- L319: /** Fold a sequence of envelopes (JS array or FSharpList) through production Fold. */

## Caller groups by domain family
- requirements/verification-system/tests/support/domain/context.mjs (3)
  - requirements/verification-system/tests/support/domain.mjs <- ./domain/context.mjs
  - requirements/verification-system/tests/support/domain/execution.mjs <- ./context.mjs
  - requirements/verification-system/tests/support/domain/prompt.mjs <- ./context.mjs
- requirements/verification-system/tests/support/domain/enforcer.mjs (3)
  - requirements/verification-system/tests/support/domain.mjs <- ./domain/enforcer.mjs
  - requirements/verification-system/tests/support/domain/context.mjs <- ./enforcer.mjs
  - requirements/verification-system/tests/support/domain/prompt.mjs <- ./enforcer.mjs
- requirements/verification-system/tests/support/domain/execution.mjs (1)
  - requirements/verification-system/tests/support/domain.mjs <- ./domain/execution.mjs
- requirements/verification-system/tests/support/domain/host.mjs (1)
  - requirements/verification-system/tests/support/domain.mjs <- ./domain/host.mjs
- requirements/verification-system/tests/support/domain/identity.mjs (8)
  - requirements/verification-system/tests/support/domain.mjs <- ./domain/identity.mjs
  - requirements/verification-system/tests/support/domain/context.mjs <- ./identity.mjs
  - requirements/verification-system/tests/support/domain/enforcer.mjs <- ./identity.mjs
  - requirements/verification-system/tests/support/domain/host.mjs <- ./identity.mjs
  - requirements/verification-system/tests/support/domain/journal.mjs <- ./identity.mjs
  - requirements/verification-system/tests/support/domain/orchestrator.mjs <- ./identity.mjs
  - requirements/verification-system/tests/support/domain/persist.mjs <- ./identity.mjs
  - requirements/verification-system/tests/support/domain/prompt.mjs <- ./identity.mjs
- requirements/verification-system/tests/support/domain/interop.mjs (10)
  - requirements/verification-system/tests/support/domain.mjs <- ./domain/interop.mjs
  - requirements/verification-system/tests/support/domain/context.mjs <- ./interop.mjs
  - requirements/verification-system/tests/support/domain/enforcer.mjs <- ./interop.mjs
  - requirements/verification-system/tests/support/domain/execution.mjs <- ./interop.mjs
  - requirements/verification-system/tests/support/domain/host.mjs <- ./interop.mjs
  - requirements/verification-system/tests/support/domain/identity.mjs <- ./interop.mjs
  - requirements/verification-system/tests/support/domain/journal.mjs <- ./interop.mjs
  - requirements/verification-system/tests/support/domain/orchestrator.mjs <- ./interop.mjs
  - requirements/verification-system/tests/support/domain/persist.mjs <- ./interop.mjs
  - requirements/verification-system/tests/support/domain/prompt.mjs <- ./interop.mjs
- requirements/verification-system/tests/support/domain/journal.mjs (3)
  - requirements/verification-system/tests/support/domain.mjs <- ./domain/journal.mjs
  - requirements/verification-system/tests/support/domain/host.mjs <- ./journal.mjs
  - requirements/verification-system/tests/support/domain/prompt.mjs <- ./journal.mjs
- requirements/verification-system/tests/support/domain/orchestrator.mjs (1)
  - requirements/verification-system/tests/support/domain.mjs <- ./domain/orchestrator.mjs
- requirements/verification-system/tests/support/domain/persist.mjs (1)
  - requirements/verification-system/tests/support/domain.mjs <- ./domain/persist.mjs
- requirements/verification-system/tests/support/domain/prompt.mjs (3)
  - requirements/verification-system/tests/support/domain.mjs <- ./domain/prompt.mjs
  - requirements/verification-system/tests/support/domain/execution.mjs <- ./prompt.mjs
  - requirements/verification-system/tests/support/domain/orchestrator.mjs <- ./prompt.mjs
