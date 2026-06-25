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
open VibeFs.Tests.ArchitectureTests
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
open VibeFs.Tests.MuxAiSettingsCodecTests
open VibeFs.Tests.MuxAiSettingsIntegrationTests
open VibeFs.Tests.AgentConfigApplyTests
open VibeFs.Tests.KnowledgeGraphWorkflowTests
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
    "ArchitectureTests.kernelBoundary", Sync (sync ArchitectureTests.kernelBoundary)
    "ArchitectureTests.kernelNoEmptyDefault", Sync (sync ArchitectureTests.kernelNoEmptyDefault)
    "ArchitectureTests.shellLayering", Sync (sync ArchitectureTests.shellLayering)
    "ArchitectureTests.fileBodyUnder300", Sync (sync ArchitectureTests.fileBodyUnder300)
    "ArchitectureTests.noDanglingMarkers", Sync (sync ArchitectureTests.noDanglingMarkers)
    "ArchitectureTests.noBuiltinDictionary", Sync (sync ArchitectureTests.noBuiltinDictionary)
    "ArchitectureTests.opencodeHookSchemaNoDirectZodImport", Sync (sync ArchitectureTests.opencodeHookSchemaNoDirectZodImport)
    "ArchitectureTests.hookSchemaNoDuplicateMethodologySchema", Sync (sync ArchitectureTests.hookSchemaNoDuplicateMethodologySchema)
    "ArchitectureTests.opencodeHookSchemaUsesIntentsRawFromArgs", Sync (sync ArchitectureTests.opencodeHookSchemaUsesIntentsRawFromArgs)
    "ArchitectureTests.opencodeNoMuxRef", Sync (sync ArchitectureTests.opencodeNoMuxRef)
    "ArchitectureTests.muxNoOpencodeRef", Sync (sync ArchitectureTests.muxNoOpencodeRef)
    "ArchitectureTests.muxBacklogUsesMuxHost", Sync (sync ArchitectureTests.muxBacklogUsesMuxHost)
    "ArchitectureTests.opencodeMessageTransformUsesProjectionPolicy", Sync (sync ArchitectureTests.opencodeMessageTransformUsesProjectionPolicy)
    "ArchitectureTests.muxMessageTransformUsesProjectionPolicy", Sync (sync ArchitectureTests.muxMessageTransformUsesProjectionPolicy)
    "ArchitectureTests.muxMessageTransformNoLocalCapsBuilder", Sync (sync ArchitectureTests.muxMessageTransformNoLocalCapsBuilder)
    "ArchitectureTests.muxMessageTransformUsesShellCapsCache", Sync (sync ArchitectureTests.muxMessageTransformUsesShellCapsCache)
    "ArchitectureTests.muxMessageTransformUsesCommonExtractTexts", Sync (sync ArchitectureTests.muxMessageTransformUsesCommonExtractTexts)
    "ArchitectureTests.messageTransformCommonUsesHostMessagePartCodec", Sync (sync ArchitectureTests.messageTransformCommonUsesHostMessagePartCodec)
    "ArchitectureTests.readDedupMuxPluginUsesHostMessagePartCodec", Sync (sync ArchitectureTests.readDedupMuxPluginUsesHostMessagePartCodec)
    "ArchitectureTests.messagingPartCodecExists", Sync (sync ArchitectureTests.messagingPartCodecExists)
    "ArchitectureTests.opencodeMessagingCodecUsesMessagingPartCodec", Sync (sync ArchitectureTests.opencodeMessagingCodecUsesMessagingPartCodec)
    "ArchitectureTests.muxMessagingCodecUsesMessagingPartCodec", Sync (sync ArchitectureTests.muxMessagingCodecUsesMessagingPartCodec)
    "ArchitectureTests.dualHostMessagingCodecUsesEncodeHelpers", Sync (sync ArchitectureTests.dualHostMessagingCodecUsesEncodeHelpers)
    "ArchitectureTests.messagingWireForkDocumented", Sync (sync ArchitectureTests.messagingWireForkDocumented)
    "ArchitectureTests.hostObjBoundaryDocumented", Sync (sync ArchitectureTests.hostObjBoundaryDocumented)
    "ArchitectureTests.muxMessageTransformUsesMuxWorkspaceCodec", Sync (sync ArchitectureTests.muxMessageTransformUsesMuxWorkspaceCodec)
    "ArchitectureTests.muxMessageTransformUsesReadDedupMuxPlugin", Sync (sync ArchitectureTests.muxMessageTransformUsesReadDedupMuxPlugin)
    "ArchitectureTests.muxMessageTransformUsesMuxHookInputCodec", Sync (sync ArchitectureTests.muxMessageTransformUsesMuxHookInputCodec)
    "ArchitectureTests.muxPluginToolExecuteAfterUsesMuxHookInputCodec", Sync (sync ArchitectureTests.muxPluginToolExecuteAfterUsesMuxHookInputCodec)
    "ArchitectureTests.muxWrappersCaptureUsesProjectionNotModuleCapture", Sync (sync ArchitectureTests.muxWrappersCaptureUsesProjectionNotModuleCapture)
    "ArchitectureTests.opencodeMessageTransformNoLocalCapsBuilder", Sync (sync ArchitectureTests.opencodeMessageTransformNoLocalCapsBuilder)
    "ArchitectureTests.opencodeMessageTransformUsesShellCapsCache", Sync (sync ArchitectureTests.opencodeMessageTransformUsesShellCapsCache)
    "ArchitectureTests.noReconstructReviewStateInMessageTransforms", Sync (sync ArchitectureTests.noReconstructReviewStateInMessageTransforms)
    "ArchitectureTests.messageTransformUsesHostEntry", Sync (sync ArchitectureTests.messageTransformUsesHostEntry)
    "ArchitectureTests.capsFileCacheCompositeKey", Sync (sync ArchitectureTests.capsFileCacheCompositeKey)
    "ArchitectureTests.capsFileCacheNoGetOrLoadCapsFilesDefault", Sync (sync ArchitectureTests.capsFileCacheNoGetOrLoadCapsFilesDefault)
    "ArchitectureTests.capsFileCacheUsesInflight", Sync (sync ArchitectureTests.capsFileCacheUsesInflight)
    "SubagentToolPolicyTests.run", Sync (sync SubagentToolPolicyTests.run)
    "SubagentPromptBuildTests.run", Sync (sync SubagentPromptBuildTests.run)
    "SubagentSpawnTests.run", Async SubagentSpawnTests.run
    "ArchitectureTests.subagentToolsUseKernelPromptHelpers", Sync (sync ArchitectureTests.subagentToolsUseKernelPromptHelpers)
    "ArchitectureTests.muxSubagentToolsUsesToolCopy", Sync (sync ArchitectureTests.muxSubagentToolsUsesToolCopy)
    "ArchitectureTests.muxSubagentToolsUsesFromMuxConfig", Sync (sync ArchitectureTests.muxSubagentToolsUsesFromMuxConfig)
    "ArchitectureTests.muxSubagentToolsUsesSubagentToolPolicy", Sync (sync ArchitectureTests.muxSubagentToolsUsesSubagentToolPolicy)
    "ArchitectureTests.muxSubagentToolsUsesMuxJsonSchema", Sync (sync ArchitectureTests.muxSubagentToolsUsesMuxJsonSchema)
    "ArchitectureTests.muxWrappersUsesJsonSchemaBuilders", Sync (sync ArchitectureTests.muxWrappersUsesJsonSchemaBuilders)
    "ArchitectureTests.muxSubagentToolsUsesMuxSpawnUniverse", Sync (sync ArchitectureTests.muxSubagentToolsUsesMuxSpawnUniverse)
    "ArchitectureTests.opencodeSubagentToolsUsesFromOpencode", Sync (sync ArchitectureTests.opencodeSubagentToolsUsesFromOpencode)
    "ArchitectureTests.opencodeSubagentToolsUsesOpencodeClientCodec", Sync (sync ArchitectureTests.opencodeSubagentToolsUsesOpencodeClientCodec)
    "ArchitectureTests.toolContextCodecUsesKernelType", Sync (sync ArchitectureTests.toolContextCodecUsesKernelType)
    "ArchitectureTests.toolContextCodecAbortFree", Sync (sync ArchitectureTests.toolContextCodecAbortFree)
    "ArchitectureTests.toolRuntimeContextAbortFromShellCodec", Sync (sync ArchitectureTests.toolRuntimeContextAbortFromShellCodec)
    "ArchitectureTests.sessionIoUsesToolContextCodec", Sync (sync ArchitectureTests.sessionIoUsesToolContextCodec)
    "ArchitectureTests.sessionIoUsesOpencodeContextCodec", Sync (sync ArchitectureTests.sessionIoUsesOpencodeContextCodec)
    "ArchitectureTests.sessionIoUsesOpencodeSessionPromptCodec", Sync (sync ArchitectureTests.sessionIoUsesOpencodeSessionPromptCodec)
    "ArchitectureTests.sessionIoUsesOpencodeSessionSpawnCodec", Sync (sync ArchitectureTests.sessionIoUsesOpencodeSessionSpawnCodec)
    "ArchitectureTests.sessionIoUsesOpencodeClientCodec", Sync (sync ArchitectureTests.sessionIoUsesOpencodeClientCodec)
    "ArchitectureTests.sessionIoUsesSubagentResultPath", Sync (sync ArchitectureTests.sessionIoUsesSubagentResultPath)
    "ArchitectureTests.opencodeNoDirectClientSessionDyn", Sync (sync ArchitectureTests.opencodeNoDirectClientSessionDyn)
    "ArchitectureTests.agentConfigUsesOpencodeAgentConfigWire", Sync (sync ArchitectureTests.agentConfigUsesOpencodeAgentConfigWire)
    "ArchitectureTests.muxAiSettingsUsesMuxAiSettingsCodec", Sync (sync ArchitectureTests.muxAiSettingsUsesMuxAiSettingsCodec)
    "ArchitectureTests.webToolsUsesWebToolsCodec", Sync (sync ArchitectureTests.webToolsUsesWebToolsCodec)
    "ArchitectureTests.fuzzyIteratorStoreOnRuntimeScope", Sync (sync ArchitectureTests.fuzzyIteratorStoreOnRuntimeScope)
    "ArchitectureTests.fuzzySearchNoDefaultIteratorStore", Sync (sync ArchitectureTests.fuzzySearchNoDefaultIteratorStore)
    "ArchitectureTests.muxMessageTransformNoModuleBacklogSession", Sync (sync ArchitectureTests.muxMessageTransformNoModuleBacklogSession)
    "ArchitectureTests.backlogSessionNoGetDefaultFallback", Sync (sync ArchitectureTests.backlogSessionNoGetDefaultFallback)
    "ArchitectureTests.runtimeScopeNoModuleProjectionHelpers", Sync (sync ArchitectureTests.runtimeScopeNoModuleProjectionHelpers)
    "ArchitectureTests.backlogSessionCodecNoReportFromFlatPartDefault", Sync (sync ArchitectureTests.backlogSessionCodecNoReportFromFlatPartDefault)
    "ArchitectureTests.opencodeToolSchemaDescriptionsFromCatalog", Sync (sync ArchitectureTests.opencodeToolSchemaDescriptionsFromCatalog)
    "ArchitectureTests.opencodeMessageTransformNoLocalApplyReadDedup", Sync (sync ArchitectureTests.opencodeMessageTransformNoLocalApplyReadDedup)
    "ArchitectureTests.messageTransformUsesChatTransformOutputCodec", Sync (sync ArchitectureTests.messageTransformUsesChatTransformOutputCodec)
    "ArchitectureTests.messageTransformUsesMessageTransformCore", Sync (sync ArchitectureTests.messageTransformUsesMessageTransformCore)
    "ArchitectureTests.messageTransformUsesPipeline", Sync (sync ArchitectureTests.messageTransformUsesPipeline)
    "ArchitectureTests.messageTransformUsesCapsKgHostHooks", Sync (sync ArchitectureTests.messageTransformUsesCapsKgHostHooks)
    "ArchitectureTests.messageTransformUsesBacklogSessionOpsFrom", Sync (sync ArchitectureTests.messageTransformUsesBacklogSessionOpsFrom)
    "ArchitectureTests.knowledgeGraphRuntimeUsesWorkflow", Sync (sync ArchitectureTests.knowledgeGraphRuntimeUsesWorkflow)
    "ArchitectureTests.knowledgeGraphBookkeeperLaunchInShell", Sync (sync ArchitectureTests.knowledgeGraphBookkeeperLaunchInShell)
    "ArchitectureTests.knowledgeGraphRuntimeNoLocalLaunchIfDue", Sync (sync ArchitectureTests.knowledgeGraphRuntimeNoLocalLaunchIfDue)
    "ArchitectureTests.muxReviewUsesToolCopy", Sync (sync ArchitectureTests.muxReviewUsesToolCopy)
    "ArchitectureTests.muxReviewUsesFromMuxConfig", Sync (sync ArchitectureTests.muxReviewUsesFromMuxConfig)
    "ArchitectureTests.muxReviewUsesReviewToolsCodec", Sync (sync ArchitectureTests.muxReviewUsesReviewToolsCodec)
    "ArchitectureTests.dualHostFuzzyUsesFuzzyToolsCodec", Sync (sync ArchitectureTests.dualHostFuzzyUsesFuzzyToolsCodec)
    "ArchitectureTests.executeMuxSubagentToolUsesSpawnRoleOnly", Sync (sync ArchitectureTests.executeMuxSubagentToolUsesSpawnRoleOnly)
    "ArchitectureTests.subagentToolExecuteEmptyBatchGuard", Sync (sync ArchitectureTests.subagentToolExecuteEmptyBatchGuard)
    "ArchitectureTests.opencodeReviewUsesToolCopy", Sync (sync ArchitectureTests.opencodeReviewUsesToolCopy)
    "ArchitectureTests.opencodeReviewUsesFromOpencode", Sync (sync ArchitectureTests.opencodeReviewUsesFromOpencode)
    "ArchitectureTests.opencodeReviewUsesReviewToolsCodec", Sync (sync ArchitectureTests.opencodeReviewUsesReviewToolsCodec)
    "ArchitectureTests.opencodeToolsUseWireEncodeForClient", Sync (sync ArchitectureTests.opencodeToolsUseWireEncodeForClient)
    "ArchitectureTests.opencodeKgUsesKnowledgeGraphToolsCodec", Sync (sync ArchitectureTests.opencodeKgUsesKnowledgeGraphToolsCodec)
    "ArchitectureTests.muxKgToolDefsUsesKnowledgeGraphToolsCodec", Sync (sync ArchitectureTests.muxKgToolDefsUsesKnowledgeGraphToolsCodec)
    "ArchitectureTests.muxKgToolDefsUsesFromMuxConfig", Sync (sync ArchitectureTests.muxKgToolDefsUsesFromMuxConfig)
    "ArchitectureTests.opencodeSubagentToolsUsesSimpleArgsCodec", Sync (sync ArchitectureTests.opencodeSubagentToolsUsesSimpleArgsCodec)
    "ArchitectureTests.muxSubagentToolsUsesSimpleArgsCodec", Sync (sync ArchitectureTests.muxSubagentToolsUsesSimpleArgsCodec)
    "ArchitectureTests.subagentToolsUseDecodeIntentsField", Sync (sync ArchitectureTests.subagentToolsUseDecodeIntentsField)
    "ArchitectureTests.subagentToolsUseToolCatalogRequiredKeys", Sync (sync ArchitectureTests.subagentToolsUseToolCatalogRequiredKeys)
    "ArchitectureTests.kernelToolArgsExists", Sync (sync ArchitectureTests.kernelToolArgsExists)
    "ArchitectureTests.toolExecuteWireHelperExists", Sync (sync ArchitectureTests.toolExecuteWireHelperExists)
    "ArchitectureTests.toolArgsDecodeExists", Sync (sync ArchitectureTests.toolArgsDecodeExists)
    "ArchitectureTests.toolArgsDecodeCoversMajorTools", Sync (sync ArchitectureTests.toolArgsDecodeCoversMajorTools)
    "ArchitectureTests.decodedToolInvocationNoObj", Sync (sync ArchitectureTests.decodedToolInvocationNoObj)
    "ArchitectureTests.muxSubagentToolsUsesToolArgsDecode", Sync (sync ArchitectureTests.muxSubagentToolsUsesToolArgsDecode)
    "ToolArgsDecodeTests.run", Sync (sync ToolArgsDecodeTests.run)
    "ToolResultWireTests.run", Sync (sync ToolResultWireTests.run)
    "SubagentToolExecuteTests.run", Async SubagentToolExecuteTests.run
    "ArchitectureTests.opencodeSubagentToolsUsesToolArgsDecode", Sync (sync ArchitectureTests.opencodeSubagentToolsUsesToolArgsDecode)
    "ArchitectureTests.sessionIoRunSubagentReturnsResult", Sync (sync ArchitectureTests.sessionIoRunSubagentReturnsResult)
    "ArchitectureTests.commandHooksUsesToolCopyReviewMessages", Sync (sync ArchitectureTests.commandHooksUsesToolCopyReviewMessages)
    "ArchitectureTests.subagentToolsUseSubagentSpawn", Sync (sync ArchitectureTests.subagentToolsUseSubagentSpawn)
    "ArchitectureTests.muxWrappersSyntaxUsesFromMuxConfig", Sync (sync ArchitectureTests.muxWrappersSyntaxUsesFromMuxConfig)
    "ArchitectureTests.muxHostToolsFuzzyUsesToolCopy", Sync (sync ArchitectureTests.muxHostToolsFuzzyUsesToolCopy)
    "ArchitectureTests.muxHostToolsFuzzyUsesFromMuxConfig", Sync (sync ArchitectureTests.muxHostToolsFuzzyUsesFromMuxConfig)
    "ArchitectureTests.muxHostToolsFuzzyUsesFuzzyToolsCodec", Sync (sync ArchitectureTests.muxHostToolsFuzzyUsesFuzzyToolsCodec)
    "ArchitectureTests.opencodeSearchToolsUsesFuzzyToolsCodec", Sync (sync ArchitectureTests.opencodeSearchToolsUsesFuzzyToolsCodec)
    "ArchitectureTests.fuzzyToolsCodecExists", Sync (sync ArchitectureTests.fuzzyToolsCodecExists)
    "ArchitectureTests.webToolsUsesWebfetchCodec", Sync (sync ArchitectureTests.webToolsUsesWebfetchCodec)
    "ArchitectureTests.opencodeSearchToolsUsesWebToolsCodec", Sync (sync ArchitectureTests.opencodeSearchToolsUsesWebToolsCodec)
    "ArchitectureTests.opencodeSearchToolsUsesToolCopy", Sync (sync ArchitectureTests.opencodeSearchToolsUsesToolCopy)
    "ArchitectureTests.opencodeExecutorUsesToolCopy", Sync (sync ArchitectureTests.opencodeExecutorUsesToolCopy)
    "ArchitectureTests.opencodeExecutorUsesFromOpencode", Sync (sync ArchitectureTests.opencodeExecutorUsesFromOpencode)
    "ArchitectureTests.opencodeExecutorUsesExecutorToolsCodec", Sync (sync ArchitectureTests.opencodeExecutorUsesExecutorToolsCodec)
    "ArchitectureTests.opencodePluginCoreUsesFromOpencode", Sync (sync ArchitectureTests.opencodePluginCoreUsesFromOpencode)
    "ArchitectureTests.opencodeHookExecuteUsesFromOpencode", Sync (sync ArchitectureTests.opencodeHookExecuteUsesFromOpencode)
    "ArchitectureTests.opencodeChatHooksUsesHookInputCodec", Sync (sync ArchitectureTests.opencodeChatHooksUsesHookInputCodec)
    "ArchitectureTests.chatHooksUsesChatHookOutputCodec", Sync (sync ArchitectureTests.chatHooksUsesChatHookOutputCodec)
    "ArchitectureTests.opencodeMessageTransformUsesHookInputCodec", Sync (sync ArchitectureTests.opencodeMessageTransformUsesHookInputCodec)
    "ArchitectureTests.opencodeMessageTransformUsesResolveMessagesTransformAgent", Sync (sync ArchitectureTests.opencodeMessageTransformUsesResolveMessagesTransformAgent)
    "ArchitectureTests.opencodeCommandHooksUsesFromOpencode", Sync (sync ArchitectureTests.opencodeCommandHooksUsesFromOpencode)
    "ArchitectureTests.opencodeSessionLifecycleObserverUsesHookInputCodec", Sync (sync ArchitectureTests.opencodeSessionLifecycleObserverUsesHookInputCodec)
    "ArchitectureTests.opencodeEventHooksUsesEventEnvelopeCodec", Sync (sync ArchitectureTests.opencodeEventHooksUsesEventEnvelopeCodec)
    "ArchitectureTests.opencodeToolDefinitionHooksUsesHookInputCodec", Sync (sync ArchitectureTests.opencodeToolDefinitionHooksUsesHookInputCodec)
    "ArchitectureTests.opencodeKnowledgeGraphToolsUsesFromOpencode", Sync (sync ArchitectureTests.opencodeKnowledgeGraphToolsUsesFromOpencode)
    "ArchitectureTests.muxKnowledgeGraphStartBookkeeperUsesFromMuxConfig", Sync (sync ArchitectureTests.muxKnowledgeGraphStartBookkeeperUsesFromMuxConfig)
    "ArchitectureTests.muxHostToolsReadWriteUsesToolCatalog", Sync (sync ArchitectureTests.muxHostToolsReadWriteUsesToolCatalog)
    "ArchitectureTests.muxHostToolsReadWriteUsesFileToolsCodec", Sync (sync ArchitectureTests.muxHostToolsReadWriteUsesFileToolsCodec)
    "ArchitectureTests.muxWrappersTodoUsesWorkBacklogToolsCodec", Sync (sync ArchitectureTests.muxWrappersTodoUsesWorkBacklogToolsCodec)
    "ArchitectureTests.opencodeHookExecuteUsesPatchToolsCodec", Sync (sync ArchitectureTests.opencodeHookExecuteUsesPatchToolsCodec)
    "ArchitectureTests.shellCodecFilesNoLocalStrField", Sync (sync ArchitectureTests.shellCodecFilesNoLocalStrField)
    "ArchitectureTests.muxDelegateUsesDelegateToolsCodec", Sync (sync ArchitectureTests.muxDelegateUsesDelegateToolsCodec)
    "ArchitectureTests.muxHookInputCodecExecutorReadOnlyUsesCodec", Sync (sync ArchitectureTests.muxHookInputCodecExecutorReadOnlyUsesCodec)
    "ArchitectureTests.knowledgeGraphSessionMessagesNotInRuntimeIO", Sync (sync ArchitectureTests.knowledgeGraphSessionMessagesNotInRuntimeIO)
    "ArchitectureTests.muxHostToolsExecutorUsesFromMuxConfig", Sync (sync ArchitectureTests.muxHostToolsExecutorUsesFromMuxConfig)
    "ArchitectureTests.muxHostToolsExecutorUsesExecutorToolsCodec", Sync (sync ArchitectureTests.muxHostToolsExecutorUsesExecutorToolsCodec)
    "ArchitectureTests.muxHostToolsWireDecodeFailures", Sync (sync ArchitectureTests.muxHostToolsWireDecodeFailures)
    "ArchitectureTests.muxWebToolsUsesWireDecodeFailure", Sync (sync ArchitectureTests.muxWebToolsUsesWireDecodeFailure)
    "ArchitectureTests.kernelToolCopyWebExecutorFields", Sync (sync ArchitectureTests.kernelToolCopyWebExecutorFields)
    "ArchitectureTests.sessionExecutorCreateForScope", Sync (sync ArchitectureTests.sessionExecutorCreateForScope)
    "ArchitectureTests.pluginInjectsSessionScopeForExecutor", Sync (sync ArchitectureTests.pluginInjectsSessionScopeForExecutor)
    "ArchitectureTests.knowledgeGraphRuntimeNoTestDrainMembers", Sync (sync ArchitectureTests.knowledgeGraphRuntimeNoTestDrainMembers)
    "ArchitectureTests.knowledgeGraphRuntimeNoSwapStateMembers", Sync (sync ArchitectureTests.knowledgeGraphRuntimeNoSwapStateMembers)
    "ArchitectureTests.runtimeScopeNoGetDefault", Sync (sync ArchitectureTests.runtimeScopeNoGetDefault)
    "ArchitectureTests.sessionExecutorNoModuleMutableQueues", Sync (sync ArchitectureTests.sessionExecutorNoModuleMutableQueues)
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