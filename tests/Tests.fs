module VibeFs.Tests.Tests

open VibeFs.Tests.Assert
open VibeFs.Tests.ReviewTests
open VibeFs.Tests.AgentTests
open VibeFs.Tests.KernelTests
open VibeFs.Tests.FuzzyTests
open VibeFs.Tests.ShellTests
open VibeFs.Tests.DynTests

[<EntryPoint>]
let main _ =
    ReviewTests.transition' ()
    ReviewTests.registry ()
    ReviewTests.resultMapping ()
    ReviewTests.reviewerLoop ()
    ReviewTests.runtime ()
    AgentTests.role ()
    AgentTests.policy ()
    AgentTests.decision ()
    AgentTests.updateState ()
    AgentTests.coordinator ()
    KernelTests.headTail' ()
    KernelTests.dedup' ()
    KernelTests.lru' ()
    KernelTests.ipAllowlist' ()
    KernelTests.ipStrict ()
    KernelTests.muxPolicy' ()
    KernelTests.hostKernel' ()
    KernelTests.excludedDirs' ()
    FuzzyTests.grepDetect ()
    FuzzyTests.iteratorRoundTrip ()
    FuzzyTests.finderConversion ()
    FuzzyTests.formatFull ()
    FuzzyTests.fuzzyFallbackNotice ()
    FuzzyTests.findPagingDefault ()
    FuzzyTests.totalMatchedSemantics ()
    ShellTests.ollamaFetchInit ()
    ShellTests.executorMapping ()
    ShellTests.recordValidator ()
    ShellTests.capsFileShape ()
    ShellTests.capsContextFormat ()
    ShellTests.ollamaFormat ()
    DynTests.nullish ()
    summary ()
