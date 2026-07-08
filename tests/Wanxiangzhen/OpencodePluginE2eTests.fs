module Wanxiangshu.Tests.Wanxiangzhen.OpencodePluginE2eTests

let entriesAsync () : (string * (unit -> Fable.Core.JS.Promise<unit>)) list =
    Wanxiangshu.Tests.Wanxiangzhen.OpencodePluginE2eHooksTests.entriesAsync ()
    @ Wanxiangshu.Tests.Wanxiangzhen.OpencodePluginE2eFlowTests.entriesAsync ()
    @ Wanxiangshu.Tests.Wanxiangzhen.OpencodePluginE2eCancelKillTests.entriesAsync ()
    @ Wanxiangshu.Tests.Wanxiangzhen.OpencodePluginE2eSlaveQueryTests.entriesAsync ()
    @ Wanxiangshu.Tests.Wanxiangzhen.OpencodePluginE2eIdTests.entriesAsync ()
