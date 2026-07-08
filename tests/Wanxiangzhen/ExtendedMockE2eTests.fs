module Wanxiangshu.Tests.Wanxiangzhen.ExtendedMockE2eTests

open Fable.Core

let entriesAsync () : (string * (unit -> JS.Promise<unit>)) list =
    Wanxiangshu.Tests.Wanxiangzhen.ExtendedMockE2eReplayTests.entriesAsync ()
    @ Wanxiangshu.Tests.Wanxiangzhen.ExtendedMockE2eSchedulerTests.entriesAsync ()
    @ Wanxiangshu.Tests.Wanxiangzhen.ExtendedMockE2eSubmitTests.entriesAsync ()
    @ Wanxiangshu.Tests.Wanxiangzhen.ExtendedMockE2eSlaveHttpTests.entriesAsync ()
    @ Wanxiangshu.Tests.Wanxiangzhen.ExtendedMockE2ePluginTests.entriesAsync ()
