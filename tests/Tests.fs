module VibeFs.Tests.Tests

open Fable.Core
open VibeFs.Tests.Assert
open VibeFs.Tests.ReviewTests
open VibeFs.Tests.AgentTests
open VibeFs.Tests.AgentNudgeSpecs
open VibeFs.Tests.KernelTests
open VibeFs.Tests.KernelPromptSpecs
open VibeFs.Tests.FuzzyTests
open VibeFs.Tests.ShellTests
open VibeFs.Tests.DynTests
open VibeFs.Tests.DelegateTests
open VibeFs.Tests.DelegateToolsCodecTests
open VibeFs.Tests.ResolveAiSettingsTests
open VibeFs.Tests.IntegrationPluginTests
open VibeFs.Tests.IntegrationEventTests
open VibeFs.Tests.IntegrationDedupTests
open VibeFs.Tests.IntegrationToolTests
open VibeFs.Tests.IntegrationOpencodeReviewSpecs
open VibeFs.Tests.IntegrationChatTests
open VibeFs.Tests.WorkBacklogTests
open VibeFs.Tests.MethodologyTests
open VibeFs.Tests.KnowledgeGraphTests
open VibeFs.Tests.KnowledgeGraphFileTests
open VibeFs.Tests.KnowledgeGraphKernelTests
open VibeFs.Tests.TitleFetchGuardTests
open VibeFs.Tests.ArchitectureTestsFoundation
open VibeFs.Tests.ArchitectureTestsMessageTransform
open VibeFs.Tests.ArchitectureTestsSubagent
open VibeFs.Tests.ArchitectureTestsSubagentToolExec
open VibeFs.Tests.ArchitectureTestsRuntime
open VibeFs.Tests.ArchitectureTestsRuntimeKg
open VibeFs.Tests.ArchitectureTestsWireToolExec
open VibeFs.Tests.ArchitectureTestsWireHook
open VibeFs.Tests.ArchitectureTestsWirePipeline
open VibeFs.Tests.ArchitectureTestsWirePayload
open VibeFs.Tests.ArchitectureTestsMuxToolCore
open VibeFs.Tests.ArchitectureTestsMuxToolAux
open VibeFs.Tests.ArchitectureTestsOpencodeTools
open VibeFs.Tests.ReviewReplaySyncTests
open VibeFs.Tests.CapsSynthCommonTests
open VibeFs.Tests.CapsFileCacheTests
open VibeFs.Tests.SubagentPromptBuildTests
open VibeFs.Tests.SubagentSpawnTests
open VibeFs.Tests.WebToolsCodecTests
open VibeFs.Tests.ReviewToolsCodecTests
open VibeFs.Tests.KnowledgeGraphToolsCodecTests
open VibeFs.Tests.ExecutorToolsCodecTests
open VibeFs.Tests.ToolArgsDecodeTests
open VibeFs.Tests.ToolResultWireTests
open VibeFs.Tests.SubagentToolExecuteTests
open VibeFs.Tests.FileToolsCodecTests
open VibeFs.Tests.FuzzyToolsCodecTests
open VibeFs.Tests.WorkBacklogToolsCodecTests
open VibeFs.Tests.PatchToolsCodecTests
open VibeFs.Tests.HostMessagePartCodecTests
open VibeFs.Tests.MessagingPartCodecTests
open VibeFs.Tests.ToolContextCodecTests
open VibeFs.Tests.OpencodeContextCodecTests
open VibeFs.Tests.OpencodeSessionPromptCodecTests
open VibeFs.Tests.OpencodeSessionSpawnCodecTests
open VibeFs.Tests.SessionIoPromptBodyTests
open VibeFs.Tests.OpencodeAgentConfigCodecTests
open VibeFs.Tests.OpencodeSessionEventCodecTests
open VibeFs.Tests.MuxAiSettingsCodecTests
open VibeFs.Tests.MuxAiSettingsIntegrationTests
open VibeFs.Tests.AgentConfigApplyTests
open VibeFs.Tests.KnowledgeGraphWorkflowTests
open VibeFs.Tests.KnowledgeGraphBookkeeperLaunchTests
open VibeFs.Tests.KnowledgeGraphMaintenanceRunTests
open VibeFs.Tests.SessionExecutorScopeTests

type private TestBody =
    | Sync of (unit -> unit)
    | Async of (unit -> JS.Promise<unit>)

let private sync (f: unit -> 'a) : unit -> unit = fun () -> ignore (f ())

let private tests : (string * TestBody) list = [
    "ReviewTests.transition'", Sync (sync ReviewTests.transition')
    "ReviewTests.registry", Sync (sync ReviewTests.registry)
    "ReviewTests.resultMapping", Sync (sync ReviewTests.resultMapping)
    "ReviewTests.reviewerLoop", Sync (sync ReviewTests.reviewerLoop)
    "ReviewTests.runtime", Sync (sync ReviewTests.runtime)
    "ReviewTests.promptPartsBranches", Sync (sync ReviewTests.promptPartsBranches)
    "ReviewTests.resolvePendingClearsSuppressor", Sync (sync ReviewTests.resolvePendingClearsSuppressor)
    "ReviewTests.disposeSessionTreeTerminatesAll", Sync (sync ReviewTests.disposeSessionTreeTerminatesAll)
    "ReviewTests.inferReviewTaskFromTexts'", Sync (sync ReviewTests.inferReviewTaskFromTexts')
    "ReviewTests.parseFrontMatterScalars'", Sync (sync ReviewTests.parseFrontMatterScalars')
    "ReviewTests.doubleCheckAnchorReplay", Sync (sync ReviewTests.doubleCheckAnchorReplay)
    "ReviewTests.doubleCheckPromptFormat", Sync (sync ReviewTests.doubleCheckPromptFormat)
    "ReviewTests.reviewerPromptFormat", Sync (sync ReviewTests.reviewerPromptFormat)
    "ReviewTests.muxReviewerVerdictPromptFormat", Sync (sync ReviewTests.muxReviewerVerdictPromptFormat)
    "ReviewTests.muxPreReviewVerdictPromptFormat", Sync (sync ReviewTests.muxPreReviewVerdictPromptFormat)
    "ReviewTests.reviewInstructionsFrontMatter", Sync (sync ReviewTests.reviewInstructionsFrontMatter)
    "AgentTests.canUse'", Sync (sync AgentTests.canUse')
    "AgentTests.canUseMatrix", Sync (sync AgentTests.canUseMatrix)
    "AgentTests.deniedTools'", Sync (sync AgentTests.deniedTools')
    "AgentNudgeSpecs.decision", Sync (sync AgentNudgeSpecs.decision)
    "AgentNudgeSpecs.updateState", Sync (sync AgentNudgeSpecs.updateState)
    "AgentNudgeSpecs.coordinatorRuntime", Sync (sync AgentNudgeSpecs.coordinatorRuntime)
    "AgentNudgeSpecs.shouldSuppress'", Sync (sync AgentNudgeSpecs.shouldSuppress')
    "AgentNudgeSpecs.decideNudge'", Sync (sync AgentNudgeSpecs.decideNudge')
    "AgentNudgeSpecs.decodeLastAssistantNudge", Sync (sync AgentNudgeSpecs.decodeLastAssistantNudge)
    "KernelTests.headTail'", Sync (sync KernelTests.headTail')
    "KernelTests.stripLexer'", Sync (sync KernelTests.stripLexer')
    "KernelTests.dedup'", Sync (sync KernelTests.dedup')
    "KernelTests.jsBoundary'", Sync (sync KernelTests.jsBoundary')
    "KernelPromptSpecs.hostKernel'", Sync (sync KernelPromptSpecs.hostKernel')
    "KernelTests.knowledgeGraphFetchAnswer", Sync (sync KernelTests.knowledgeGraphFetchAnswer)
    "KernelPromptSpecs.toolCatalogCentralized", Sync (sync KernelPromptSpecs.toolCatalogCentralized)
    "KernelPromptSpecs.hostToolsKnowledgeGraphNames", Sync (sync KernelPromptSpecs.hostToolsKnowledgeGraphNames)
    "KernelPromptSpecs.subagentDispatch", Sync (sync KernelPromptSpecs.subagentDispatch)
    "KernelPromptSpecs.subagentJoinReports", Sync (sync KernelPromptSpecs.subagentJoinReports)
    "KernelTests.dynDeleteKey", Sync (sync KernelTests.dynDeleteKey)
    "KernelPromptSpecs.loopMessagesShared", Sync (sync KernelPromptSpecs.loopMessagesShared)
    "KernelPromptSpecs.reviewerVerdictPromptsShared", Sync (sync KernelPromptSpecs.reviewerVerdictPromptsShared)
    "KernelPromptSpecs.reviewResultFormattingShared", Sync (sync KernelPromptSpecs.reviewResultFormattingShared)
    "KernelPromptSpecs.reviewVerdictDecode", Sync (sync KernelPromptSpecs.reviewVerdictDecode)
    "KernelPromptSpecs.reviewDecisionPolicy", Sync (sync KernelPromptSpecs.reviewDecisionPolicy)
    "KernelPromptSpecs.reviewMarkdownCodec", Sync (sync KernelPromptSpecs.reviewMarkdownCodec)
    "KernelPromptSpecs.executorSummarizerNoExitStatus", Sync (sync KernelPromptSpecs.executorSummarizerNoExitStatus)
    "KernelPromptSpecs.domainErrorsShared", Sync (sync KernelPromptSpecs.domainErrorsShared)
    "FuzzyTests.grepDetect", Sync (sync FuzzyTests.grepDetect)
    "FuzzyTests.iteratorRoundTrip", Sync (sync FuzzyTests.iteratorRoundTrip)
    "FuzzyTests.finderConversion", Sync (sync FuzzyTests.finderConversion)
    "FuzzyTests.formatFull", Sync (sync FuzzyTests.formatFull)
    "FuzzyTests.fuzzyFallbackNotice", Sync (sync FuzzyTests.fuzzyFallbackNotice)
    "FuzzyTests.findPagingDefault", Sync (sync FuzzyTests.findPagingDefault)
    "FuzzyTests.emptyIteratorNotRendered", Sync (sync FuzzyTests.emptyIteratorNotRendered)
    "FuzzyTests.totalMatchedSemantics", Sync (sync FuzzyTests.totalMatchedSemantics)
    "FuzzyTests.grepOutputNotices", Sync (sync FuzzyTests.grepOutputNotices)
    "FuzzyTests.iteratorNamespaceConstants", Sync (sync FuzzyTests.iteratorNamespaceConstants)
    "FuzzyTests.iteratorStoreStronglyTyped", Sync (sync FuzzyTests.iteratorStoreStronglyTyped)
    "FuzzyTests.runWithFinderSharedPipeline", Sync (sync FuzzyTests.runWithFinderSharedPipeline)
    "FuzzyTests.resolveStoreRequiresInjection", Sync (sync FuzzyTests.resolveStoreRequiresInjection)
    "FuzzyTests.emptyIteratorTreatedAsAbsent", Sync (sync FuzzyTests.emptyIteratorTreatedAsAbsent)
    "ShellTests.webApiFetchInit", Sync (sync ShellTests.webApiFetchInit)
    "ShellTests.webApiResponseMethodCall", Sync (sync ShellTests.webApiResponseMethodCall)
    "ShellTests.webApiKeyValidation", Sync (sync ShellTests.webApiKeyValidation)
    "ShellTests.executorMapping", Sync (sync ShellTests.executorMapping)
    "ShellTests.safetyWarning", Sync (sync ShellTests.safetyWarning)
    "ShellTests.capsFileShape", Sync (sync ShellTests.capsFileShape)
    "ShellTests.capsContextFormat", Sync (sync ShellTests.capsContextFormat)
    "ShellTests.capsFileSizeLimit", Sync (sync ShellTests.capsFileSizeLimit)
    "ShellTests.webApiSearchFormat", Sync (sync ShellTests.webApiSearchFormat)
    "ShellTests.summarizerInputCap", Sync (sync ShellTests.summarizerInputCap)
    "ShellTests.executorToolResponseFormatting", Sync (sync ShellTests.executorToolResponseFormatting)
    "ShellTests.summarizerPromptOmitsReturnValue", Sync (sync ShellTests.summarizerPromptOmitsReturnValue)
    "ShellTests.readDirectoryListing", Async ShellTests.readDirectoryListing
    "ShellTests.ensureJavascriptProjectRepairsModuleType", Async ShellTests.ensureJavascriptProjectRepairsModuleType
    "ShellTests.rewriteJavascriptRelativeImports", Async ShellTests.rewriteJavascriptRelativeImports
    "ShellTests.knowledgeGraphPortRangeSpec", Async ShellTests.knowledgeGraphPortRangeSpec
    "ShellTests.knowledgeGraphPortSerialSpec", Async ShellTests.knowledgeGraphPortSerialSpec
    "DynTests.nullish", Sync (sync DynTests.nullish)
    "DelegateTests.run", Sync (sync DelegateTests.run)
    "DelegateToolsCodecTests.run", Sync (sync DelegateToolsCodecTests.run)
    "ResolveAiSettingsTests.run", Sync (sync ResolveAiSettingsTests.run)
    "IntegrationPluginTests.run", Async IntegrationPluginTests.run
    "IntegrationEventTests.run", Async IntegrationEventTests.run
    "IntegrationDedupTests.run", Async IntegrationDedupTests.run
    "IntegrationToolTests.run", Async IntegrationToolTests.run
    "IntegrationOpencodeReviewSpecs.run", Async IntegrationOpencodeReviewSpecs.run
    "IntegrationChatTests.run", Async IntegrationChatTests.run
    "KnowledgeGraphTests.run", Async KnowledgeGraphTests.run
    "KnowledgeGraphFileTests.run", Async KnowledgeGraphFileTests.run
    "KnowledgeGraphKernelTests.run", Async KnowledgeGraphKernelTests.run
    "WorkBacklogTests.run", Sync (sync WorkBacklogTests.run)
    "MethodologyTests.run", Sync (sync MethodologyTests.run)
    "TitleFetchGuardTests.signature", Sync (sync TitleFetchGuardTests.signature)
    "TitleFetchGuardTests.wrap", Sync (sync TitleFetchGuardTests.wrap)
    "TitleFetchGuardTests.detect", Sync (sync TitleFetchGuardTests.detect)
    "TitleFetchGuardTests.tryWrapString", Sync (sync TitleFetchGuardTests.tryWrapString)
    "TitleFetchGuardTests.rewriteStringContent", Sync (sync TitleFetchGuardTests.rewriteStringContent)
    "TitleFetchGuardTests.rewriteArrayContent", Sync (sync TitleFetchGuardTests.rewriteArrayContent)
    "TitleFetchGuardTests.skipProbeMessage", Sync (sync TitleFetchGuardTests.skipProbeMessage)
    "ArchitectureTests.kernelBoundary", Sync (sync ArchitectureTestsFoundation.kernelBoundary)
    "ArchitectureTests.kernelNoEmptyDefault", Sync (sync ArchitectureTestsFoundation.kernelNoEmptyDefault)
    "ArchitectureTests.shellLayering", Sync (sync ArchitectureTestsFoundation.shellLayering)
    "ArchitectureTests.fileBodyUnder300", Sync (sync ArchitectureTestsFoundation.fileBodyUnder300)
    "ArchitectureTests.noDanglingMarkers", Sync (sync ArchitectureTestsFoundation.noDanglingMarkers)
    "ArchitectureTests.noBuiltinDictionary", Sync (sync ArchitectureTestsFoundation.noBuiltinDictionary)
    "ArchitectureTests.opencodeHookSchemaNoDirectZodImport", Sync (sync ArchitectureTestsFoundation.opencodeHookSchemaNoDirectZodImport)
    "ArchitectureTests.hookSchemaNoDuplicateMethodologySchema", Sync (sync ArchitectureTestsFoundation.hookSchemaNoDuplicateMethodologySchema)
    "ArchitectureTests.opencodeHookSchemaUsesIntentsRawFromArgs", Sync (sync ArchitectureTestsFoundation.opencodeHookSchemaUsesIntentsRawFromArgs)
    "ArchitectureTests.opencodeNoMuxRef", Sync (sync ArchitectureTestsFoundation.opencodeNoMuxRef)
    "ArchitectureTests.muxNoOpencodeRef", Sync (sync ArchitectureTestsFoundation.muxNoOpencodeRef)
    "ArchitectureTests.muxBacklogUsesMuxHost", Sync (sync ArchitectureTestsFoundation.muxBacklogUsesMuxHost)
    "ArchitectureTests.opencodeMessageTransformUsesProjectionPolicy", Sync (sync ArchitectureTestsMessageTransform.opencodeMessageTransformUsesProjectionPolicy)
    "ArchitectureTests.muxMessageTransformUsesProjectionPolicy", Sync (sync ArchitectureTestsMessageTransform.muxMessageTransformUsesProjectionPolicy)
    "ArchitectureTests.muxMessageTransformNoLocalCapsBuilder", Sync (sync ArchitectureTestsMessageTransform.muxMessageTransformNoLocalCapsBuilder)
    "ArchitectureTests.muxMessageTransformUsesShellCapsCache", Sync (sync ArchitectureTestsMessageTransform.muxMessageTransformUsesShellCapsCache)
    "ArchitectureTests.muxMessageTransformUsesCommonExtractTexts", Sync (sync ArchitectureTestsMessageTransform.muxMessageTransformUsesCommonExtractTexts)
    "ArchitectureTests.messageTransformCommonUsesHostMessagePartCodec", Sync (sync ArchitectureTestsMessageTransform.messageTransformCommonUsesHostMessagePartCodec)
    "ArchitectureTests.readDedupMuxPluginUsesHostMessagePartCodec", Sync (sync ArchitectureTestsMessageTransform.readDedupMuxPluginUsesHostMessagePartCodec)
    "ArchitectureTests.messagingPartCodecExists", Sync (sync ArchitectureTestsMessageTransform.messagingPartCodecExists)
    "ArchitectureTests.opencodeMessagingCodecUsesMessagingPartCodec", Sync (sync ArchitectureTestsMessageTransform.opencodeMessagingCodecUsesMessagingPartCodec)
    "ArchitectureTests.muxMessagingCodecUsesMessagingPartCodec", Sync (sync ArchitectureTestsMessageTransform.muxMessagingCodecUsesMessagingPartCodec)
    "ArchitectureTests.dualHostMessagingCodecUsesEncodeHelpers", Sync (sync ArchitectureTestsWirePipeline.dualHostMessagingCodecUsesEncodeHelpers)
    "ArchitectureTests.messagingWireForkDocumented", Sync (sync ArchitectureTestsWirePipeline.messagingWireForkDocumented)
    "ArchitectureTests.hostObjBoundaryDocumented", Sync (sync ArchitectureTestsWirePipeline.hostObjBoundaryDocumented)
    "ArchitectureTests.muxMessageTransformUsesMuxWorkspaceCodec", Sync (sync ArchitectureTestsMessageTransform.muxMessageTransformUsesMuxWorkspaceCodec)
    "ArchitectureTests.muxMessageTransformUsesReadDedupMuxPlugin", Sync (sync ArchitectureTestsMessageTransform.muxMessageTransformUsesReadDedupMuxPlugin)
    "ArchitectureTests.muxMessageTransformUsesMuxHookInputCodec", Sync (sync ArchitectureTestsMessageTransform.muxMessageTransformUsesMuxHookInputCodec)
    "ArchitectureTests.muxWrappersCaptureUsesProjectionNotModuleCapture", Sync (sync ArchitectureTestsMessageTransform.muxWrappersCaptureUsesProjectionNotModuleCapture)
    "ArchitectureTests.opencodeMessageTransformNoLocalCapsBuilder", Sync (sync ArchitectureTestsMessageTransform.opencodeMessageTransformNoLocalCapsBuilder)
    "ArchitectureTests.opencodeMessageTransformUsesShellCapsCache", Sync (sync ArchitectureTestsMessageTransform.opencodeMessageTransformUsesShellCapsCache)
    "ArchitectureTests.noReconstructReviewStateInMessageTransforms", Sync (sync ArchitectureTestsMessageTransform.noReconstructReviewStateInMessageTransforms)
    "ArchitectureTests.messageTransformUsesHostEntry", Sync (sync ArchitectureTestsMessageTransform.messageTransformUsesHostEntry)
    "ArchitectureTests.capsFileCacheCompositeKey", Sync (sync ArchitectureTestsMessageTransform.capsFileCacheCompositeKey)
    "ArchitectureTests.capsFileCacheNoGetOrLoadCapsFilesDefault", Sync (sync ArchitectureTestsMessageTransform.capsFileCacheNoGetOrLoadCapsFilesDefault)
    "ArchitectureTests.capsFileCacheUsesInflight", Sync (sync ArchitectureTestsMessageTransform.capsFileCacheUsesInflight)
    "SubagentToolPolicyTests.run", Sync (sync SubagentToolPolicyTests.run)
    "SubagentPromptBuildTests.run", Sync (sync SubagentPromptBuildTests.run)
    "SubagentSpawnTests.run", Async SubagentSpawnTests.run
    "ArchitectureTests.subagentToolsUseKernelPromptHelpers", Sync (sync ArchitectureTestsSubagent.subagentToolsUseKernelPromptHelpers)
    "ArchitectureTests.muxSubagentToolsUsesToolCopy", Sync (sync ArchitectureTestsSubagent.muxSubagentToolsUsesToolCopy)
    "ArchitectureTests.muxSubagentToolsUsesFromMuxConfig", Sync (sync ArchitectureTestsSubagent.muxSubagentToolsUsesFromMuxConfig)
    "ArchitectureTests.muxSubagentToolsUsesSubagentToolPolicy", Sync (sync ArchitectureTestsSubagent.muxSubagentToolsUsesSubagentToolPolicy)
    "ArchitectureTests.muxSubagentToolsUsesMuxJsonSchema", Sync (sync ArchitectureTestsMuxToolAux.muxSubagentToolsUsesMuxJsonSchema)
    "ArchitectureTests.muxWrappersUsesJsonSchemaBuilders", Sync (sync ArchitectureTestsMuxToolAux.muxWrappersUsesJsonSchemaBuilders)
    "ArchitectureTests.muxSubagentToolsUsesMuxSpawnUniverse", Sync (sync ArchitectureTestsMuxToolAux.muxSubagentToolsUsesMuxSpawnUniverse)
    "ArchitectureTests.opencodeSubagentToolsUsesFromOpencode", Sync (sync ArchitectureTestsSubagent.opencodeSubagentToolsUsesFromOpencode)
    "ArchitectureTests.opencodeSubagentToolsUsesOpencodeClientCodec", Sync (sync ArchitectureTestsOpencodeTools.opencodeSubagentToolsUsesOpencodeClientCodec)
    "ArchitectureTests.toolContextCodecUsesKernelType", Sync (sync ArchitectureTestsRuntime.toolContextCodecUsesKernelType)
    "ArchitectureTests.toolContextCodecAbortFree", Sync (sync ArchitectureTestsRuntime.toolContextCodecAbortFree)
    "ArchitectureTests.toolRuntimeContextAbortFromShellCodec", Sync (sync ArchitectureTestsRuntime.toolRuntimeContextAbortFromShellCodec)
    "ArchitectureTests.sessionIoUsesToolContextCodec", Sync (sync ArchitectureTestsRuntime.sessionIoUsesToolContextCodec)
    "ArchitectureTests.sessionIoUsesOpencodeContextCodec", Sync (sync ArchitectureTestsRuntime.sessionIoUsesOpencodeContextCodec)
    "ArchitectureTests.sessionIoUsesOpencodeSessionPromptCodec", Sync (sync ArchitectureTestsRuntime.sessionIoUsesOpencodeSessionPromptCodec)
    "ArchitectureTests.sessionIoUsesOpencodeSessionSpawnCodec", Sync (sync ArchitectureTestsRuntime.sessionIoUsesOpencodeSessionSpawnCodec)
    "ArchitectureTests.sessionIoUsesOpencodeClientCodec", Sync (sync ArchitectureTestsRuntime.sessionIoUsesOpencodeClientCodec)
    "ArchitectureTests.sessionIoUsesSubagentResultPath", Sync (sync ArchitectureTestsRuntime.sessionIoUsesSubagentResultPath)
    "ArchitectureTests.opencodeNoDirectClientSessionDyn", Sync (sync ArchitectureTestsRuntime.opencodeNoDirectClientSessionDyn)
    "ArchitectureTests.agentConfigUsesOpencodeAgentConfigWire", Sync (sync ArchitectureTestsRuntime.agentConfigUsesOpencodeAgentConfigWire)
    "ArchitectureTests.fuzzyIteratorStoreOnRuntimeScope", Sync (sync ArchitectureTestsRuntime.fuzzyIteratorStoreOnRuntimeScope)
    "ArchitectureTests.fuzzySearchNoDefaultIteratorStore", Sync (sync ArchitectureTestsRuntime.fuzzySearchNoDefaultIteratorStore)
    "ArchitectureTests.muxMessageTransformNoModuleBacklogSession", Sync (sync ArchitectureTestsRuntime.muxMessageTransformNoModuleBacklogSession)
    "ArchitectureTests.backlogSessionNoGetDefaultFallback", Sync (sync ArchitectureTestsRuntime.backlogSessionNoGetDefaultFallback)
    "ArchitectureTests.runtimeScopeNoModuleProjectionHelpers", Sync (sync ArchitectureTestsRuntime.runtimeScopeNoModuleProjectionHelpers)
    "ArchitectureTests.backlogSessionCodecNoReportFromFlatPartDefault", Sync (sync ArchitectureTestsRuntime.backlogSessionCodecNoReportFromFlatPartDefault)
    "ArchitectureTests.opencodeMessageTransformNoLocalApplyReadDedup", Sync (sync ArchitectureTestsRuntime.opencodeMessageTransformNoLocalApplyReadDedup)
    "ArchitectureTests.messageTransformUsesChatTransformOutputCodec", Sync (sync ArchitectureTestsWirePipeline.messageTransformUsesChatTransformOutputCodec)
    "ArchitectureTests.messageTransformUsesMessageTransformCore", Sync (sync ArchitectureTestsWirePipeline.messageTransformUsesMessageTransformCore)
    "ArchitectureTests.messageTransformUsesPipeline", Sync (sync ArchitectureTestsWirePipeline.messageTransformUsesPipeline)
    "ArchitectureTests.messageTransformUsesCapsKgHostHooks", Sync (sync ArchitectureTestsWirePipeline.messageTransformUsesCapsKgHostHooks)
    "ArchitectureTests.messageTransformUsesBacklogSessionOpsFrom", Sync (sync ArchitectureTestsWirePipeline.messageTransformUsesBacklogSessionOpsFrom)
    "ArchitectureTests.knowledgeGraphRuntimeUsesWorkflow", Sync (sync ArchitectureTestsRuntimeKg.knowledgeGraphRuntimeUsesWorkflow)
    "ArchitectureTests.knowledgeGraphBookkeeperLaunchInShell", Sync (sync ArchitectureTestsRuntimeKg.knowledgeGraphBookkeeperLaunchInShell)
    "ArchitectureTests.knowledgeGraphRuntimeNoLocalLaunchIfDue", Sync (sync ArchitectureTestsRuntimeKg.knowledgeGraphRuntimeNoLocalLaunchIfDue)
    "ArchitectureTests.muxReviewUsesToolCopy", Sync (sync ArchitectureTestsMuxToolAux.muxReviewUsesToolCopy)
    "ArchitectureTests.muxReviewUsesFromMuxConfig", Sync (sync ArchitectureTestsMuxToolAux.muxReviewUsesFromMuxConfig)
    "ArchitectureTests.muxReviewUsesReviewToolsCodec", Sync (sync ArchitectureTestsMuxToolAux.muxReviewUsesReviewToolsCodec)
    "ArchitectureTests.executeMuxSubagentToolUsesSpawnRoleOnly", Sync (sync ArchitectureTestsSubagentToolExec.executeMuxSubagentToolUsesSpawnRoleOnly)
    "ArchitectureTests.subagentToolExecuteEmptyBatchGuard", Sync (sync ArchitectureTestsSubagentToolExec.subagentToolExecuteEmptyBatchGuard)
    "ArchitectureTests.opencodeReviewUsesToolCopy", Sync (sync ArchitectureTestsOpencodeTools.opencodeReviewUsesToolCopy)
    "ArchitectureTests.opencodeReviewUsesFromOpencode", Sync (sync ArchitectureTestsOpencodeTools.opencodeReviewUsesFromOpencode)
    "ArchitectureTests.opencodeReviewUsesReviewToolsCodec", Sync (sync ArchitectureTestsOpencodeTools.opencodeReviewUsesReviewToolsCodec)
    "ArchitectureTests.opencodeKgUsesKnowledgeGraphToolsCodec", Sync (sync ArchitectureTestsOpencodeTools.opencodeKgUsesKnowledgeGraphToolsCodec)
    "ArchitectureTests.muxKgToolDefsUsesKnowledgeGraphToolsCodec", Sync (sync ArchitectureTestsMuxToolAux.muxKgToolDefsUsesKnowledgeGraphToolsCodec)
    "ArchitectureTests.muxKgToolDefsUsesFromMuxConfig", Sync (sync ArchitectureTestsMuxToolAux.muxKgToolDefsUsesFromMuxConfig)
    "ArchitectureTests.opencodeSubagentToolsUsesSimpleArgsCodec", Sync (sync ArchitectureTestsSubagent.opencodeSubagentToolsUsesSimpleArgsCodec)
    "ArchitectureTests.muxSubagentToolsUsesSimpleArgsCodec", Sync (sync ArchitectureTestsMuxToolAux.muxSubagentToolsUsesSimpleArgsCodec)
    "ArchitectureTests.subagentToolsUseDecodeIntentsField", Sync (sync ArchitectureTestsSubagent.subagentToolsUseDecodeIntentsField)
    "ArchitectureTests.subagentToolsUseToolCatalogRequiredKeys", Sync (sync ArchitectureTestsSubagent.subagentToolsUseToolCatalogRequiredKeys)
    "ArchitectureTests.kernelToolArgsExists", Sync (sync ArchitectureTestsSubagent.kernelToolArgsExists)
    "ArchitectureTests.toolArgsDecodeExists", Sync (sync ArchitectureTestsSubagent.toolArgsDecodeExists)
    "ArchitectureTests.toolArgsDecodeCoversMajorTools", Sync (sync ArchitectureTestsSubagent.toolArgsDecodeCoversMajorTools)
    "ArchitectureTests.decodedToolInvocationNoObj", Sync (sync ArchitectureTestsSubagent.decodedToolInvocationNoObj)
    "ArchitectureTests.muxSubagentToolsUsesToolArgsDecode", Sync (sync ArchitectureTestsSubagentToolExec.muxSubagentToolsUsesToolArgsDecode)
    "ToolArgsDecodeTests.run", Sync (sync ToolArgsDecodeTests.run)
    "ToolResultWireTests.run", Sync (sync ToolResultWireTests.run)
    "SubagentToolExecuteTests.run", Async SubagentToolExecuteTests.run
    "ArchitectureTests.opencodeSubagentToolsUsesToolArgsDecode", Sync (sync ArchitectureTestsSubagentToolExec.opencodeSubagentToolsUsesToolArgsDecode)
    "ArchitectureTests.sessionIoRunSubagentReturnsResult", Sync (sync ArchitectureTestsSubagent.sessionIoRunSubagentReturnsResult)
    "ArchitectureTests.commandHooksUsesToolCopyReviewMessages", Sync (sync ArchitectureTestsSubagent.commandHooksUsesToolCopyReviewMessages)
    "ArchitectureTests.commandHooksUsesRegisterLoopReviewCommands", Sync (sync ArchitectureTestsWireHook.commandHooksUsesRegisterLoopReviewCommands)
    "ArchitectureTests.opencodeHookExecuteUsesHookArgsHelpers", Sync (sync ArchitectureTestsWireToolExec.opencodeHookExecuteUsesHookArgsHelpers)
    "ArchitectureTests.opencodeCommandHooksUsesPartsWriter", Sync (sync ArchitectureTestsWireToolExec.opencodeCommandHooksUsesPartsWriter)
    "ArchitectureTests.muxHookOutputUsesMuxHookInputCodec", Sync (sync ArchitectureTestsWireToolExec.muxHookOutputUsesMuxHookInputCodec)
    "ArchitectureTests.subagentToolsUseSubagentSpawn", Sync (sync ArchitectureTestsSubagentToolExec.subagentToolsUseSubagentSpawn)
    "ArchitectureTests.muxWrappersSyntaxUsesFromMuxConfig", Sync (sync ArchitectureTestsMuxToolAux.muxWrappersSyntaxUsesFromMuxConfig)
    "ArchitectureTests.muxHostToolsFuzzyUsesToolCopy", Sync (sync ArchitectureTestsMuxToolCore.muxHostToolsFuzzyUsesToolCopy)
    "ArchitectureTests.muxHostToolsFuzzyUsesFromMuxConfig", Sync (sync ArchitectureTestsMuxToolCore.muxHostToolsFuzzyUsesFromMuxConfig)
    "ArchitectureTests.muxHostToolsFuzzyUsesFuzzyToolsCodec", Sync (sync ArchitectureTestsMuxToolCore.muxHostToolsFuzzyUsesFuzzyToolsCodec)
    "ArchitectureTests.opencodeSearchToolsUsesFuzzyToolsCodec", Sync (sync ArchitectureTestsOpencodeTools.opencodeSearchToolsUsesFuzzyToolsCodec)
    "ArchitectureTests.fuzzyToolsCodecExists", Sync (sync ArchitectureTestsMuxToolCore.fuzzyToolsCodecExists)
    "ArchitectureTests.webToolsUsesWebfetchCodec", Sync (sync ArchitectureTestsMuxToolCore.webToolsUsesWebfetchCodec)
    "ArchitectureTests.opencodeSearchToolsUsesWebToolsCodec", Sync (sync ArchitectureTestsOpencodeTools.opencodeSearchToolsUsesWebToolsCodec)
    "ArchitectureTests.opencodeSearchToolsUsesToolCopy", Sync (sync ArchitectureTestsOpencodeTools.opencodeSearchToolsUsesToolCopy)
    "ArchitectureTests.opencodeExecutorUsesToolCopy", Sync (sync ArchitectureTestsOpencodeTools.opencodeExecutorUsesToolCopy)
    "ArchitectureTests.opencodeExecutorUsesFromOpencode", Sync (sync ArchitectureTestsOpencodeTools.opencodeExecutorUsesFromOpencode)
    "ArchitectureTests.opencodeExecutorUsesExecutorToolsCodec", Sync (sync ArchitectureTestsOpencodeTools.opencodeExecutorUsesExecutorToolsCodec)
    "ArchitectureTests.opencodePluginCoreUsesFromOpencode", Sync (sync ArchitectureTestsWireHook.opencodePluginCoreUsesFromOpencode)
    "ArchitectureTests.opencodeHookExecuteUsesFromOpencode", Sync (sync ArchitectureTestsWireHook.opencodeHookExecuteUsesFromOpencode)
    "ArchitectureTests.opencodeChatHooksUsesHookInputCodec", Sync (sync ArchitectureTestsWireHook.opencodeChatHooksUsesHookInputCodec)
    "ArchitectureTests.chatHooksUsesChatHookOutputCodec", Sync (sync ArchitectureTestsWireHook.chatHooksUsesChatHookOutputCodec)
    "ArchitectureTests.opencodeMessageTransformUsesHookInputCodec", Sync (sync ArchitectureTestsWirePipeline.opencodeMessageTransformUsesHookInputCodec)
    "ArchitectureTests.opencodeMessageTransformUsesResolveMessagesTransformAgent", Sync (sync ArchitectureTestsWirePipeline.opencodeMessageTransformUsesResolveMessagesTransformAgent)
    "ArchitectureTests.opencodeCommandHooksUsesFromOpencode", Sync (sync ArchitectureTestsWireHook.opencodeCommandHooksUsesFromOpencode)
    "ArchitectureTests.opencodeSessionLifecycleObserverUsesHookInputCodec", Sync (sync ArchitectureTestsWireHook.opencodeSessionLifecycleObserverUsesHookInputCodec)
    "ArchitectureTests.opencodeEventHooksUsesEventEnvelopeCodec", Sync (sync ArchitectureTestsWireHook.opencodeEventHooksUsesEventEnvelopeCodec)
    "ArchitectureTests.opencodeToolDefinitionHooksUsesHookInputCodec", Sync (sync ArchitectureTestsWireHook.opencodeToolDefinitionHooksUsesHookInputCodec)
    "ArchitectureTests.opencodeKnowledgeGraphToolsUsesFromOpencode", Sync (sync ArchitectureTestsOpencodeTools.opencodeKnowledgeGraphToolsUsesFromOpencode)
    "ArchitectureTests.muxKnowledgeGraphStartBookkeeperUsesFromMuxConfig", Sync (sync ArchitectureTestsMuxToolAux.muxKnowledgeGraphStartBookkeeperUsesFromMuxConfig)
    "ArchitectureTests.muxHostToolsReadWriteUsesToolCatalog", Sync (sync ArchitectureTestsMuxToolCore.muxHostToolsReadWriteUsesToolCatalog)
    "ArchitectureTests.muxHostToolsReadWriteUsesFileToolsCodec", Sync (sync ArchitectureTestsMuxToolCore.muxHostToolsReadWriteUsesFileToolsCodec)
    "ArchitectureTests.muxWrappersTodoUsesWorkBacklogToolsCodec", Sync (sync ArchitectureTestsMuxToolAux.muxWrappersTodoUsesWorkBacklogToolsCodec)
    "ArchitectureTests.opencodeHookExecuteUsesPatchToolsCodec", Sync (sync ArchitectureTestsWireHook.opencodeHookExecuteUsesPatchToolsCodec)
    "ArchitectureTests.shellCodecFilesNoLocalStrField", Sync (sync ArchitectureTestsWireToolExec.shellCodecFilesNoLocalStrField)
    "ArchitectureTests.muxDelegateUsesDelegateToolsCodec", Sync (sync ArchitectureTestsMuxToolAux.muxDelegateUsesDelegateToolsCodec)
    "ArchitectureTests.muxHookInputCodecExecutorReadOnlyUsesCodec", Sync (sync ArchitectureTestsMuxToolAux.muxHookInputCodecExecutorReadOnlyUsesCodec)
    "ArchitectureTests.knowledgeGraphSessionMessagesNotInRuntimeIO", Sync (sync ArchitectureTestsRuntimeKg.knowledgeGraphSessionMessagesNotInRuntimeIO)
    "ArchitectureTests.muxHostToolsExecutorUsesFromMuxConfig", Sync (sync ArchitectureTestsMuxToolCore.muxHostToolsExecutorUsesFromMuxConfig)
    "ArchitectureTests.muxHostToolsExecutorUsesExecutorToolsCodec", Sync (sync ArchitectureTestsMuxToolCore.muxHostToolsExecutorUsesExecutorToolsCodec)
    "ArchitectureTests.muxHostToolsWireDecodeFailures", Sync (sync ArchitectureTestsWireToolExec.muxHostToolsWireDecodeFailures)
    "ArchitectureTests.muxWebToolsUsesWireDecodeFailure", Sync (sync ArchitectureTestsWireToolExec.muxWebToolsUsesWireDecodeFailure)
    "ArchitectureTests.kernelToolCopyWebExecutorFields", Sync (sync ArchitectureTestsWireToolExec.kernelToolCopyWebExecutorFields)
    "ArchitectureTests.sessionExecutorCreateForScope", Sync (sync ArchitectureTestsRuntime.sessionExecutorCreateForScope)
    "ArchitectureTests.pluginInjectsSessionScopeForExecutor", Sync (sync ArchitectureTestsRuntime.pluginInjectsSessionScopeForExecutor)
    "ArchitectureTests.knowledgeGraphRuntimeNoTestDrainMembers", Sync (sync ArchitectureTestsRuntimeKg.knowledgeGraphRuntimeNoTestDrainMembers)
    "ArchitectureTests.knowledgeGraphRuntimeNoSwapStateMembers", Sync (sync ArchitectureTestsRuntimeKg.knowledgeGraphRuntimeNoSwapStateMembers)
    "ArchitectureTests.runtimeScopeNoGetDefault", Sync (sync ArchitectureTestsRuntime.runtimeScopeNoGetDefault)
    "ArchitectureTests.sessionExecutorNoModuleMutableQueues", Sync (sync ArchitectureTestsRuntime.sessionExecutorNoModuleMutableQueues)
    "ArchitectureTests.muxAiSettingsUsesMuxAiSettingsCodec", Sync (sync ArchitectureTestsMuxToolAux.muxAiSettingsUsesMuxAiSettingsCodec)
    "ArchitectureTests.webToolsUsesWebToolsCodec", Sync (sync ArchitectureTestsMuxToolCore.webToolsUsesWebToolsCodec)
    "ArchitectureTests.dualHostFuzzyUsesFuzzyToolsCodec", Sync (sync ArchitectureTestsMuxToolCore.dualHostFuzzyUsesFuzzyToolsCodec)
    "ArchitectureTests.opencodeToolsUseWireEncodeForClient", Sync (sync ArchitectureTestsWireToolExec.opencodeToolsUseWireEncodeForClient)
    "ArchitectureTests.toolExecuteWireHelperExists", Sync (sync ArchitectureTestsWireToolExec.toolExecuteWireHelperExists)
    "ArchitectureTests.muxPluginToolExecuteAfterUsesMuxHookInputCodec", Sync (sync ArchitectureTestsWireHook.muxPluginToolExecuteAfterUsesMuxHookInputCodec)
    "ArchitectureTests.opencodeToolSchemaDescriptionsFromCatalog", Sync (sync ArchitectureTestsOpencodeTools.opencodeToolSchemaDescriptionsFromCatalog)
    "WebToolsCodecTests.run", Sync (sync WebToolsCodecTests.run)
    "ReviewToolsCodecTests.run", Sync (sync ReviewToolsCodecTests.run)
    "KnowledgeGraphToolsCodecTests.run", Sync (sync KnowledgeGraphToolsCodecTests.run)
    "FileToolsCodecTests.run", Sync (sync FileToolsCodecTests.run)
    "FuzzyToolsCodecTests.run", Sync (sync FuzzyToolsCodecTests.run)
    "WorkBacklogToolsCodecTests.run", Sync (sync WorkBacklogToolsCodecTests.run)
    "PatchToolsCodecTests.run", Sync (sync PatchToolsCodecTests.run)
    "HostMessagePartCodecTests.run", Sync (sync HostMessagePartCodecTests.run)
    "MessagingPartCodecTests.run", Sync (sync MessagingPartCodecTests.run)
    "ExecutorToolsCodecTests.run", Sync (sync ExecutorToolsCodecTests.run)
    "ToolContextCodecTests.run", Sync (sync ToolContextCodecTests.run)
    "OpencodeContextCodecTests.run", Sync (sync OpencodeContextCodecTests.run)
    "OpencodeSessionPromptCodecTests.run", Sync (sync OpencodeSessionPromptCodecTests.run)
    "OpencodeSessionSpawnCodecTests.run", Sync (sync OpencodeSessionSpawnCodecTests.run)
    "SessionIoPromptBodyTests.run", Sync (sync SessionIoPromptBodyTests.run)
    "OpencodeAgentConfigCodecTests.run", Sync (sync OpencodeAgentConfigCodecTests.run)
    "OpencodeSessionEventCodecTests.run", Sync (sync OpencodeSessionEventCodecTests.run)
    "MuxAiSettingsCodecTests.run", Sync (sync MuxAiSettingsCodecTests.run)
    "MuxAiSettingsIntegrationTests.run", Async MuxAiSettingsIntegrationTests.run
    "AgentConfigApplyTests.run", Sync (sync AgentConfigApplyTests.run)
    "KnowledgeGraphWorkflowTests.run", Async KnowledgeGraphWorkflowTests.run
    "KnowledgeGraphBookkeeperLaunchTests.run", Async KnowledgeGraphBookkeeperLaunchTests.run
    "KnowledgeGraphMaintenanceRunTests.run", Async KnowledgeGraphMaintenanceRunTests.run
    "SessionExecutorScopeTests.run", Async SessionExecutorScopeTests.run
    "CapsSynthCommonTests.run", Sync (sync CapsSynthCommonTests.run)
    "CapsFileCacheTests.run", Async CapsFileCacheTests.run
    "ReviewReplaySyncTests.run", Sync (sync ReviewReplaySyncTests.run)
    "ArchitectureTests.opencodeSessionEventCodecExists", Sync (sync ArchitectureTestsWirePayload.opencodeSessionEventCodecExists)
    "ArchitectureTests.opencodeNudgeEventCodecIsShellAlias", Sync (sync ArchitectureTestsWirePayload.opencodeNudgeEventCodecIsShellAlias)
    "ArchitectureTests.nudgeEffectRecoversViaCodec", Sync (sync ArchitectureTestsWirePayload.nudgeEffectRecoversViaCodec)
    "ArchitectureTests.sessionLifecycleObserverUsesCodecDecoders", Sync (sync ArchitectureTestsWirePayload.sessionLifecycleObserverUsesCodecDecoders)
    "ArchitectureTests.commandHooksUsesCodecSessionID", Sync (sync ArchitectureTestsWirePayload.commandHooksUsesCodecSessionID)
    "ArchitectureTests.eventHooksUsesCodecSessionID", Sync (sync ArchitectureTestsWirePayload.eventHooksUsesCodecSessionID)
]

let private matchesSelector (selectors: string array) (label: string) =
    selectors.Length = 0
    || selectors
       |> Array.exists (fun selector ->
           let trimmed = selector.Trim ()
           trimmed.Length > 0 && label.StartsWith trimmed)

let private selectedTests (selectors: string array) =
    tests |> List.filter (fun (label, _) -> matchesSelector selectors label)

let runAll (args: string array) : JS.Promise<int> =
    promise {
        let runnableTests = selectedTests args
        if List.isEmpty runnableTests then
            printfn "No tests matched selectors: %A" args
            return 1
        else
            for (label, body) in runnableTests do
                match body with
                | Sync f -> let _ = timed label f in ()
                | Async f -> do! timedAsync label f
            return summary ()
    }