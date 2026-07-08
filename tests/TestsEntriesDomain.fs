module Wanxiangshu.Tests.TestsEntriesDomain

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.DomainTests
open Wanxiangshu.Tests.ErrorClassifyTests
open Wanxiangshu.Tests.PromiseQueueTests
open Wanxiangshu.Tests.TestsTestBody

let domainTestEntries () : (string * TestBody) list =
    [ "DomainTests.formatDomainErrorMessageAborted", TestBody.Sync(sync DomainTests.formatDomainErrorMessageAborted)
      "DomainTests.formatDomainErrorSessionBusy", TestBody.Sync(sync DomainTests.formatDomainErrorSessionBusy)
      "DomainTests.formatDomainErrorTaskWaitBackgrounded",
      TestBody.Sync(sync DomainTests.formatDomainErrorTaskWaitBackgrounded)
      "DomainTests.formatDomainErrorExecutorExecutableMissing",
      TestBody.Sync(sync DomainTests.formatDomainErrorExecutorExecutableMissing)
      "DomainTests.formatDomainErrorParseError", TestBody.Sync(sync DomainTests.formatDomainErrorParseError)
      "DomainTests.formatDomainErrorToolNotPermitted", TestBody.Sync(sync DomainTests.formatDomainErrorToolNotPermitted)
      "DomainTests.formatDomainErrorInvalidIntent", TestBody.Sync(sync DomainTests.formatDomainErrorInvalidIntent)
      "DomainTests.formatDomainErrorUpstreamTimeout", TestBody.Sync(sync DomainTests.formatDomainErrorUpstreamTimeout)
      "DomainTests.formatDomainErrorUpstreamRefused", TestBody.Sync(sync DomainTests.formatDomainErrorUpstreamRefused)
      "DomainTests.formatDomainErrorSystemPanic", TestBody.Sync(sync DomainTests.formatDomainErrorSystemPanic)
      "DomainTests.formatDomainErrorUnknownJsError", TestBody.Sync(sync DomainTests.formatDomainErrorUnknownJsError)
      "DomainTests.formatDomainErrorFileSystemFault", TestBody.Sync(sync DomainTests.formatDomainErrorFileSystemFault)
      "DomainTests.formatDomainErrorNetworkTransportFailure",
      TestBody.Sync(sync DomainTests.formatDomainErrorNetworkTransportFailure)
      "DomainTests.formatDomainErrorNetworkTransportFailureNone",
      TestBody.Sync(sync DomainTests.formatDomainErrorNetworkTransportFailureNone)
      "DomainTests.formatDomainErrorClientCancellation",
      TestBody.Sync(sync DomainTests.formatDomainErrorClientCancellation)
      "DomainTests.formatDomainErrorHostProtocolMismatch",
      TestBody.Sync(sync DomainTests.formatDomainErrorHostProtocolMismatch)
      "DomainTests.isAbortTrueForMessageAborted", TestBody.Sync(sync DomainTests.isAbortTrueForMessageAborted)
      "DomainTests.isAbortTrueForClientCancellation", TestBody.Sync(sync DomainTests.isAbortTrueForClientCancellation)
      "DomainTests.isAbortFalseForAllOthers", TestBody.Sync(sync DomainTests.isAbortFalseForAllOthers)
      "DomainTests.isAbortFalseForFileSystemFault", TestBody.Sync(sync DomainTests.isAbortFalseForFileSystemFault)
      "DomainTests.isAbortFalseForNetworkTransportFailure",
      TestBody.Sync(sync DomainTests.isAbortFalseForNetworkTransportFailure)
      "DomainTests.isAbortFalseForHostProtocolMismatch",
      TestBody.Sync(sync DomainTests.isAbortFalseForHostProtocolMismatch)
      "DomainTests.containsAbortTextLower", TestBody.Sync(sync DomainTests.containsAbortTextLower)
      "DomainTests.containsAbortTextMixed", TestBody.Sync(sync DomainTests.containsAbortTextMixed)
      "DomainTests.containsAbortTextNull", TestBody.Sync(sync DomainTests.containsAbortTextNull)
      "DomainTests.containsAbortTextEmpty", TestBody.Sync(sync DomainTests.containsAbortTextEmpty)
      "DomainTests.containsAbortTextNormal", TestBody.Sync(sync DomainTests.containsAbortTextNormal)
      "DomainTests.classifyErrorLeafAbortByName", TestBody.Sync(sync DomainTests.classifyErrorLeafAbortByName)
      "DomainTests.classifyErrorLeafAbortSignalByName",
      TestBody.Sync(sync DomainTests.classifyErrorLeafAbortSignalByName)
      "DomainTests.classifyErrorLeafAbortByTag", TestBody.Sync(sync DomainTests.classifyErrorLeafAbortByTag)
      "DomainTests.classifyErrorLeafSessionBusyByName",
      TestBody.Sync(sync DomainTests.classifyErrorLeafSessionBusyByName)
      "DomainTests.classifyErrorLeafSessionBusyByTag", TestBody.Sync(sync DomainTests.classifyErrorLeafSessionBusyByTag)
      "DomainTests.classifyErrorLeafBackgroundedByName",
      TestBody.Sync(sync DomainTests.classifyErrorLeafBackgroundedByName)
      "DomainTests.classifyErrorLeafBackgroundedByTag",
      TestBody.Sync(sync DomainTests.classifyErrorLeafBackgroundedByTag)
      "DomainTests.classifyErrorLeafFallbackAbortText",
      TestBody.Sync(sync DomainTests.classifyErrorLeafFallbackAbortText)
      "DomainTests.classifyErrorLeafHostProtocolMismatchByTag",
      TestBody.Sync(sync DomainTests.classifyErrorLeafHostProtocolMismatchByTag)
      "DomainTests.classifyErrorLeafFallbackNoAbort", TestBody.Sync(sync DomainTests.classifyErrorLeafFallbackNoAbort)
      "DomainTests.sessionIdSuccess", TestBody.Sync(sync DomainTests.sessionIdSuccess)
      "DomainTests.sessionIdEmptyFailure", TestBody.Sync(sync DomainTests.sessionIdEmptyFailure)
      "DomainTests.trySessionIdSuccess", TestBody.Sync(sync DomainTests.trySessionIdSuccess)
      "DomainTests.trySessionIdEmptyFailure", TestBody.Sync(sync DomainTests.trySessionIdEmptyFailure)
      "DomainTests.workspaceIdQuickValueRoundTrip", TestBody.Sync(sync DomainTests.workspaceIdQuickValueRoundTrip)
      "DomainTests.tryAgentIdSuccess", TestBody.Sync(sync DomainTests.tryAgentIdSuccess)
      "DomainTests.tryAgentIdEmptyFailure", TestBody.Sync(sync DomainTests.tryAgentIdEmptyFailure)
      "DomainTests.reduceEmptyNoChildren", TestBody.Sync(sync DomainTests.reduceEmptyNoChildren)
      "DomainTests.reduceIdempotentUnregister", TestBody.Sync(sync DomainTests.reduceIdempotentUnregister)
      "ErrorClassifyTests.run", TestBody.Sync(sync ErrorClassifyTests.run)
      "PromiseQueueTests.run", TestBody.Async PromiseQueueTests.run ]
