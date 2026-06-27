module Wanxiangshu.Tests.TestsEntriesDomain

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.DomainTests
open Wanxiangshu.Tests.TestsTestBody

let domainTestEntries () : (string * TestBody) list =
    [
    "DomainTests.formatDomainErrorMessageAborted", Sync (sync DomainTests.formatDomainErrorMessageAborted)
    "DomainTests.formatDomainErrorSessionBusy", Sync (sync DomainTests.formatDomainErrorSessionBusy)
    "DomainTests.formatDomainErrorTaskWaitBackgrounded", Sync (sync DomainTests.formatDomainErrorTaskWaitBackgrounded)
    "DomainTests.formatDomainErrorExecutorExecutableMissing", Sync (sync DomainTests.formatDomainErrorExecutorExecutableMissing)
    "DomainTests.formatDomainErrorParseError", Sync (sync DomainTests.formatDomainErrorParseError)
    "DomainTests.formatDomainErrorToolNotPermitted", Sync (sync DomainTests.formatDomainErrorToolNotPermitted)
    "DomainTests.formatDomainErrorInvalidIntent", Sync (sync DomainTests.formatDomainErrorInvalidIntent)
    "DomainTests.formatDomainErrorUpstreamTimeout", Sync (sync DomainTests.formatDomainErrorUpstreamTimeout)
    "DomainTests.formatDomainErrorUpstreamRefused", Sync (sync DomainTests.formatDomainErrorUpstreamRefused)
    "DomainTests.formatDomainErrorSystemPanic", Sync (sync DomainTests.formatDomainErrorSystemPanic)
    "DomainTests.formatDomainErrorUnknownJsError", Sync (sync DomainTests.formatDomainErrorUnknownJsError)
    "DomainTests.isAbortTrueForMessageAborted", Sync (sync DomainTests.isAbortTrueForMessageAborted)
    "DomainTests.isAbortFalseForAllOthers", Sync (sync DomainTests.isAbortFalseForAllOthers)
    "DomainTests.containsAbortTextLower", Sync (sync DomainTests.containsAbortTextLower)
    "DomainTests.containsAbortTextMixed", Sync (sync DomainTests.containsAbortTextMixed)
    "DomainTests.containsAbortTextNull", Sync (sync DomainTests.containsAbortTextNull)
    "DomainTests.containsAbortTextEmpty", Sync (sync DomainTests.containsAbortTextEmpty)
    "DomainTests.containsAbortTextNormal", Sync (sync DomainTests.containsAbortTextNormal)
    "DomainTests.classifyErrorLeafAbortByName", Sync (sync DomainTests.classifyErrorLeafAbortByName)
    "DomainTests.classifyErrorLeafAbortByTag", Sync (sync DomainTests.classifyErrorLeafAbortByTag)
    "DomainTests.classifyErrorLeafSessionBusyByName", Sync (sync DomainTests.classifyErrorLeafSessionBusyByName)
    "DomainTests.classifyErrorLeafSessionBusyByTag", Sync (sync DomainTests.classifyErrorLeafSessionBusyByTag)
    "DomainTests.classifyErrorLeafBackgroundedByName", Sync (sync DomainTests.classifyErrorLeafBackgroundedByName)
    "DomainTests.classifyErrorLeafBackgroundedByTag", Sync (sync DomainTests.classifyErrorLeafBackgroundedByTag)
    "DomainTests.classifyErrorLeafFallbackAbortText", Sync (sync DomainTests.classifyErrorLeafFallbackAbortText)
    "DomainTests.classifyErrorLeafFallbackNoAbort", Sync (sync DomainTests.classifyErrorLeafFallbackNoAbort)
    "DomainTests.sessionIdSuccess", Sync (sync DomainTests.sessionIdSuccess)
    "DomainTests.sessionIdEmptyFailure", Sync (sync DomainTests.sessionIdEmptyFailure)
    "DomainTests.trySessionIdSuccess", Sync (sync DomainTests.trySessionIdSuccess)
    "DomainTests.trySessionIdEmptyFailure", Sync (sync DomainTests.trySessionIdEmptyFailure)
    "DomainTests.workspaceIdQuickValueRoundTrip", Sync (sync DomainTests.workspaceIdQuickValueRoundTrip)
    "DomainTests.tryAgentIdSuccess", Sync (sync DomainTests.tryAgentIdSuccess)
    "DomainTests.tryAgentIdEmptyFailure", Sync (sync DomainTests.tryAgentIdEmptyFailure)
    "DomainTests.reduceEmptyNoChildren", Sync (sync DomainTests.reduceEmptyNoChildren)
    "DomainTests.reduceIdempotentUnregister", Sync (sync DomainTests.reduceIdempotentUnregister)
    ]
