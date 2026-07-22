module Wanxiangshu.Tests.OmpHostContractTests

open Wanxiangshu.Tests.OmpHostContractCoreTests
open Wanxiangshu.Tests.OmpHostContractAsyncTests

let run () =
    promise {
        tryExtractMessageIdFromShapes ()
        modelOmitNotEmptyString ()
        checkMessagesNeverFabricatesOrderedMarker ()
        do! dispatchRejectsPromptResolveWithoutId ()
        do! dispatchAcceptsOnlyWithMessageId ()
        do! abortOncePrefersSessionNotBoth ()
        do! abortOnceFallsBackToPi ()
        do! cancelPendingDispatchIsReal ()
        do! actionExecutorOmitsEmptyModelAndRequiresReceipt ()
        do! waitForIdleAfterBaselineRequiresGrowth ()
    }
