module Wanxiangshu.Tests.TestsArchitectureRegistry

open Fable.Core
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.ArchitectureTestsFoundation
open Wanxiangshu.Tests.ArchitectureTestsMessageTransform
open Wanxiangshu.Tests.ArchitectureTestsMessageTransformCaps
open Wanxiangshu.Tests.ArchitectureTestsSubagent
open Wanxiangshu.Tests.ArchitectureTestsSubagentToolExec
open Wanxiangshu.Tests.ArchitectureTestsRuntime
open Wanxiangshu.Tests.ArchitectureTestsRuntimeSession
open Wanxiangshu.Tests.ArchitectureTestsWireToolExec
open Wanxiangshu.Tests.ArchitectureTestsWireHook
open Wanxiangshu.Tests.ArchitectureTestsWirePipeline
open Wanxiangshu.Tests.ArchitectureTestsWirePayload
open Wanxiangshu.Tests.ArchitectureTestsMuxToolCore
open Wanxiangshu.Tests.ArchitectureTestsMuxToolAux
open Wanxiangshu.Tests.ArchitectureTestsOpencodeTools
open Wanxiangshu.Tests.ArchitectureTestsOmp
open Wanxiangshu.Tests.TestsTestBody

let architectureTestEntriesPartA () : (string * TestBody) list =
    [ "ArchitectureTests.kernelBoundary", Sync(sync ArchitectureTestsFoundation.kernelBoundary)
      "ArchitectureTests.kernelNoEmptyDefault", Sync(sync ArchitectureTestsFoundation.kernelNoEmptyDefault)
      "ArchitectureTests.shellLayering", Sync(sync ArchitectureTestsFoundation.shellLayering)
      "ArchitectureTests.fileBodyUnder300", Sync(sync ArchitectureTestsFoundation.fileBodyUnder300)
      "ArchitectureTests.returnReviewerCatalogAndHostRegistration",
      Sync(sync ArchitectureTestsFoundation.returnReviewerCatalogAndHostRegistration)
      "ArchitectureTests.noDanglingMarkers", Sync(sync ArchitectureTestsFoundation.noDanglingMarkers)
      "ArchitectureTests.noBuiltinDictionary", Sync(sync ArchitectureTestsFoundation.noBuiltinDictionary)
      "ArchitectureTests.opencodeHookSchemaNoDirectZodImport",
      Sync(sync ArchitectureTestsFoundation.opencodeHookSchemaNoDirectZodImport)
      "ArchitectureTests.noLegacyInjectedToolOutputMarkers",
      Sync(sync ArchitectureTestsFoundation.noLegacyInjectedToolOutputMarkers)
      "ArchitectureTests.hookSchemaNoDuplicateMethodologySchema",
      Sync(sync ArchitectureTestsFoundation.hookSchemaNoDuplicateMethodologySchema)
      "ArchitectureTests.opencodeHookSchemaUsesIntentsRawFromArgs",
      Sync(sync ArchitectureTestsFoundation.opencodeHookSchemaUsesIntentsRawFromArgs)
      "ArchitectureTests.opencodeNoMuxRef", Sync(sync ArchitectureTestsFoundation.opencodeNoMuxRef)
      "ArchitectureTests.muxNoOpencodeRef", Sync(sync ArchitectureTestsFoundation.muxNoOpencodeRef)
      "ArchitectureTests.muxBacklogUsesMuxHost", Sync(sync ArchitectureTestsFoundation.muxBacklogUsesMuxHost)
      "ArchitectureTests.eventLogUsesAdvisoryFlock", Sync(sync ArchitectureTestsFoundation.eventLogUsesAdvisoryFlock)
      "ArchitectureTests.ompBoundary", Sync(sync ArchitectureTestsFoundation.ompBoundary)
      "ArchitectureTests.ompNoEngineRef", Sync(sync ArchitectureTestsFoundation.ompNoEngineRef)
      "ArchitectureTests.noDuplicateStateHolder", Sync(sync ArchitectureTestsFoundation.noDuplicateStateHolder)
      "ArchitectureTests.noDuplicateRunNudgeFlowCore",
      Sync(sync ArchitectureTestsFoundation.noDuplicateRunNudgeFlowCore)
      "ArchitectureTests.nudgeDedupMustUseEventLogFold",
      Sync(sync ArchitectureTestsFoundation.nudgeDedupMustUseEventLogFold)
      "ArchitectureTests.nudgeLoopStateMustReplayHistory",
      Sync(sync ArchitectureTestsFoundation.nudgeLoopStateMustReplayHistory)
      "ArchitectureTests.ompMessageTransformUsesProjectionPolicy",
      Sync(sync ArchitectureTestsOmp.ompMessageTransformUsesProjectionPolicy)
      "ArchitectureTests.ompMessageTransformUsesShellCaps",
      Sync(sync ArchitectureTestsOmp.ompMessageTransformUsesShellCaps)
      "ArchitectureTests.ompMessageTransformUsesMessagingCodec",
      Sync(sync ArchitectureTestsOmp.ompMessageTransformUsesMessagingCodec)
      "ArchitectureTests.ompMessagingCodecUsesShellPartCodec",
      Sync(sync ArchitectureTestsOmp.ompMessagingCodecUsesShellPartCodec)
      "ArchitectureTests.ompCodecUsesDynModule", Sync(sync ArchitectureTestsOmp.ompCodecUsesDynModule)
      "ArchitectureTests.ompHookExecuteUsesSubagentIntentsCodec",
      Sync(sync ArchitectureTestsOmp.ompHookExecuteUsesSubagentIntentsCodec)
      "ArchitectureTests.ompToolsRegisterAll", Sync(sync ArchitectureTestsOmp.ompToolsRegisterAll)
      "ArchitectureTests.ompPluginUsesPluginCore", Sync(sync ArchitectureTestsOmp.ompPluginUsesPluginCore)
      "ArchitectureTests.ompPluginNoOpencodeMuxRefs", Sync(sync ArchitectureTestsOmp.ompPluginNoOpencodeMuxRefs)
      "ArchitectureTests.ompUsesOmpToolSchema", Sync(sync ArchitectureTestsOmp.ompUsesOmpToolSchema)
      "ArchitectureTests.ompReviewUsesReviewRuntime", Sync(sync ArchitectureTestsOmp.ompReviewUsesReviewRuntime)
      "ArchitectureTests.ompCapsCodecExists", Sync(sync ArchitectureTestsOmp.ompCapsCodecExists)
      "ArchitectureTests.ompChildSessionExists", Sync(sync ArchitectureTestsOmp.ompChildSessionExists)
      "ArchitectureTests.ompSourceFilesUnder300", Sync(sync ArchitectureTestsOmp.ompSourceFilesUnder300)
      "ArchitectureTests.ompFuzzyToolsUsesShellFinder", Sync(sync ArchitectureTestsOmp.ompFuzzyToolsUsesShellFinder)
      "ArchitectureTests.ompExecutorUsesShellExecute", Sync(sync ArchitectureTestsOmp.ompExecutorUsesShellExecute)
      "ArchitectureTests.ompReadDedupModule", Sync(sync ArchitectureTestsOmp.ompReadDedupModule)
      "ArchitectureTests.ompNudgeRuntimeModule", Sync(sync ArchitectureTestsOmp.ompNudgeRuntimeModule)
      "ArchitectureTests.ompNudgeHooksDoNotReadReviewStoreForLoopState",
      Sync(sync ArchitectureTestsOmp.ompNudgeHooksDoNotReadReviewStoreForLoopState)
      "ArchitectureTests.ompSessionLifecycleHooks", Sync(sync ArchitectureTestsOmp.ompSessionLifecycleHooks)
      "ArchitectureTests.ompPiResolveNoEngine", Sync(sync ArchitectureTestsOmp.ompPiResolveNoEngine)
      "ArchitectureTests.opencodeMessageTransformUsesProjectionPolicy",
      Sync(sync ArchitectureTestsMessageTransform.opencodeMessageTransformUsesProjectionPolicy)
      "ArchitectureTests.muxMessageTransformUsesProjectionPolicy",
      Sync(sync ArchitectureTestsMessageTransform.muxMessageTransformUsesProjectionPolicy)
      "ArchitectureTests.muxMessageTransformNoLocalCapsBuilder",
      Sync(sync ArchitectureTestsMessageTransform.muxMessageTransformNoLocalCapsBuilder)
      "ArchitectureTests.muxMessageTransformUsesShellCapsCache",
      Sync(sync ArchitectureTestsMessageTransform.muxMessageTransformUsesShellCapsCache)
      "ArchitectureTests.muxMessageTransformUsesCommonExtractTexts",
      Sync(sync ArchitectureTestsMessageTransform.muxMessageTransformUsesCommonExtractTexts)
      "ArchitectureTests.messageTransformCommonUsesHostMessagePartCodec",
      Sync(sync ArchitectureTestsMessageTransform.messageTransformCommonUsesHostMessagePartCodec)
      "ArchitectureTests.readDedupMuxPluginUsesHostMessagePartCodec",
      Sync(sync ArchitectureTestsMessageTransform.readDedupMuxPluginUsesHostMessagePartCodec)
      "ArchitectureTests.messagingPartCodecExists",
      Sync(sync ArchitectureTestsMessageTransform.messagingPartCodecExists)
      "ArchitectureTests.opencodeMessagingCodecUsesMessagingPartCodec",
      Sync(sync ArchitectureTestsMessageTransform.opencodeMessagingCodecUsesMessagingPartCodec)
      "ArchitectureTests.muxMessagingCodecUsesMessagingPartCodec",
      Sync(sync ArchitectureTestsMessageTransform.muxMessagingCodecUsesMessagingPartCodec)
      "ArchitectureTests.dualHostMessagingCodecUsesEncodeHelpers",
      Sync(sync ArchitectureTestsWirePipeline.dualHostMessagingCodecUsesEncodeHelpers)
      "ArchitectureTests.messagingWireForkDocumented",
      Sync(sync ArchitectureTestsWirePipeline.messagingWireForkDocumented)
      "ArchitectureTests.hostObjBoundaryDocumented", Sync(sync ArchitectureTestsWirePipeline.hostObjBoundaryDocumented)
      "ArchitectureTests.muxMessageTransformUsesMuxWorkspaceCodec",
      Sync(sync ArchitectureTestsMessageTransform.muxMessageTransformUsesMuxWorkspaceCodec)
      "ArchitectureTests.muxMessageTransformUsesReadDedupMuxPlugin",
      Sync(sync ArchitectureTestsMessageTransformCaps.muxMessageTransformUsesReadDedupMuxPlugin)
      "ArchitectureTests.muxMessageTransformUsesMuxHookInputCodec",
      Sync(sync ArchitectureTestsMessageTransformCaps.muxMessageTransformUsesMuxHookInputCodec)
      "ArchitectureTests.muxWrappersCaptureUsesProjectionNotModuleCapture",
      Sync(sync ArchitectureTestsMessageTransformCaps.muxWrappersCaptureUsesProjectionNotModuleCapture)
      "ArchitectureTests.opencodeMessageTransformNoLocalCapsBuilder",
      Sync(sync ArchitectureTestsMessageTransformCaps.opencodeMessageTransformNoLocalCapsBuilder)
      "ArchitectureTests.opencodeMessageTransformUsesShellCapsCache",
      Sync(sync ArchitectureTestsMessageTransformCaps.opencodeMessageTransformUsesShellCapsCache)
      "ArchitectureTests.noReconstructReviewStateInMessageTransforms",
      Sync(sync ArchitectureTestsMessageTransformCaps.noReconstructReviewStateInMessageTransforms)
      "ArchitectureTests.messageTransformUsesHostEntry",
      Sync(sync ArchitectureTestsMessageTransformCaps.messageTransformUsesHostEntry)
      "ArchitectureTests.capsFileCacheCompositeKey",
      Sync(sync ArchitectureTestsMessageTransformCaps.capsFileCacheCompositeKey)
      "ArchitectureTests.capsFileCacheNoGetOrLoadCapsFilesDefault",
      Sync(sync ArchitectureTestsMessageTransformCaps.capsFileCacheNoGetOrLoadCapsFilesDefault)
      "ArchitectureTests.capsFileCacheUsesInflight",
      Sync(sync ArchitectureTestsMessageTransformCaps.capsFileCacheUsesInflight)
      "SubagentToolPolicyTests.run", Sync(sync SubagentToolPolicyTests.run)
      "SubagentPromptBuildTests.run", Sync(sync SubagentPromptBuildTests.run)
      "SubagentSpawnTests.run", Async SubagentSpawnTests.run
      "ArchitectureTests.subagentToolsUseKernelPromptHelpers",
      Sync(sync ArchitectureTestsSubagent.subagentToolsUseKernelPromptHelpers)
      "ArchitectureTests.opencodeSubagentToolExecuteUsesHostNotLiteralOpencode",
      Sync(sync ArchitectureTestsSubagent.opencodeSubagentToolExecuteUsesHostNotLiteralOpencode)
      "ArchitectureTests.muxSubagentToolsUsesToolCopy",
      Sync(sync ArchitectureTestsSubagent.muxSubagentToolsUsesToolCopy)
      "ArchitectureTests.muxSubagentToolsUsesFromMuxConfig",
      Sync(sync ArchitectureTestsSubagent.muxSubagentToolsUsesFromMuxConfig)
      "ArchitectureTests.muxSubagentToolsUsesSubagentToolPolicy",
      Sync(sync ArchitectureTestsSubagent.muxSubagentToolsUsesSubagentToolPolicy)
      "ArchitectureTests.muxSubagentToolsUsesMuxJsonSchema",
      Sync(sync ArchitectureTestsMuxToolAux.muxSubagentToolsUsesMuxJsonSchema)
      "ArchitectureTests.muxWrappersUsesJsonSchemaBuilders",
      Sync(sync ArchitectureTestsMuxToolAux.muxWrappersUsesJsonSchemaBuilders)
      "ArchitectureTests.muxSubagentToolsUsesMuxSpawnUniverse",
      Sync(sync ArchitectureTestsMuxToolAux.muxSubagentToolsUsesMuxSpawnUniverse)
      "ArchitectureTests.opencodeSubagentToolsUsesFromOpencode",
      Sync(sync ArchitectureTestsSubagentSession.opencodeSubagentToolsUsesFromOpencode)
      "ArchitectureTests.opencodeSubagentToolsUsesOpencodeClientCodec",
      Sync(sync ArchitectureTestsOpencodeTools.opencodeSubagentToolsUsesOpencodeClientCodec)
      "ArchitectureTests.toolContextCodecUsesKernelType",
      Sync(sync ArchitectureTestsRuntime.toolContextCodecUsesKernelType)
      "ArchitectureTests.toolContextCodecAbortFree", Sync(sync ArchitectureTestsRuntime.toolContextCodecAbortFree)
      "ArchitectureTests.toolRuntimeContextAbortFromShellCodec",
      Sync(sync ArchitectureTestsRuntime.toolRuntimeContextAbortFromShellCodec)
      "ArchitectureTests.sessionIoUsesToolContextCodec",
      Sync(sync ArchitectureTestsRuntime.sessionIoUsesToolContextCodec)
      "ArchitectureTests.sessionIoUsesOpencodeContextCodec",
      Sync(sync ArchitectureTestsRuntime.sessionIoUsesOpencodeContextCodec)
      "ArchitectureTests.sessionIoUsesOpencodeSessionPromptCodec",
      Sync(sync ArchitectureTestsRuntime.sessionIoUsesOpencodeSessionPromptCodec)
      "ArchitectureTests.sessionIoUsesOpencodeSessionSpawnCodec",
      Sync(sync ArchitectureTestsRuntime.sessionIoUsesOpencodeSessionSpawnCodec)
      "ArchitectureTests.sessionIoUsesOpencodeClientCodec",
      Sync(sync ArchitectureTestsRuntimeSession.sessionIoUsesOpencodeClientCodec)
      "ArchitectureTests.sessionIoUsesSubagentResultPath",
      Sync(sync ArchitectureTestsRuntimeSession.sessionIoUsesSubagentResultPath)
      "ArchitectureTests.opencodeNoDirectClientSessionDyn",
      Sync(sync ArchitectureTestsRuntimeSession.opencodeNoDirectClientSessionDyn)
      "ArchitectureTests.agentConfigUsesOpencodeAgentConfigWire",
      Sync(sync ArchitectureTestsRuntime.agentConfigUsesOpencodeAgentConfigWire)
      "ArchitectureTests.fuzzyIteratorStoreOnRuntimeScope",
      Sync(sync ArchitectureTestsRuntime.fuzzyIteratorStoreOnRuntimeScope)
      "ArchitectureTests.fuzzySearchNoDefaultIteratorStore",
      Sync(sync ArchitectureTestsRuntime.fuzzySearchNoDefaultIteratorStore)
      "ArchitectureTests.muxMessageTransformNoModuleBacklogSession",
      Sync(sync ArchitectureTestsRuntime.muxMessageTransformNoModuleBacklogSession)
      "ArchitectureTests.backlogSessionNoGetDefaultFallback",
      Sync(sync ArchitectureTestsRuntime.backlogSessionNoGetDefaultFallback)
      "ArchitectureTests.runtimeScopeNoModuleProjectionHelpers",
      Sync(sync ArchitectureTestsRuntime.runtimeScopeNoModuleProjectionHelpers)
      "ArchitectureTests.backlogSessionCodecNoReportFromFlatPartDefault",
      Sync(sync ArchitectureTestsRuntime.backlogSessionCodecNoReportFromFlatPartDefault)
      "ArchitectureTests.opencodeMessageTransformNoLocalApplyReadDedup",
      Sync(sync ArchitectureTestsRuntime.opencodeMessageTransformNoLocalApplyReadDedup)
      "ArchitectureTests.messageTransformUsesChatTransformOutputCodec",
      Sync(sync ArchitectureTestsWirePipeline.messageTransformUsesChatTransformOutputCodec)
      "ArchitectureTests.messageTransformUsesMessageTransformCore",
      Sync(sync ArchitectureTestsWirePipeline.messageTransformUsesMessageTransformCore)
      "ArchitectureTests.messageTransformUsesPipeline",
      Sync(sync ArchitectureTestsWirePipeline.messageTransformUsesPipeline) ]

open Wanxiangshu.Tests.TestsArchitectureRegistryB

let architectureTestEntries () : (string * TestBody) list =
    architectureTestEntriesPartA () @ architectureTestEntriesPartB ()
