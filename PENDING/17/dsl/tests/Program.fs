// 测试入口：顺序运行各套件；任一失败 → 退出码 1。
module Meditator.Tests.Program

[<EntryPoint>]
let main _ =
    Meditator.Tests.ClaimTest.run ()
    Meditator.Tests.Properties.run ()
    Meditator.Tests.Counterexamples.run ()
    Meditator.Tests.GraphReachability.run ()
    Meditator.Tests.Crash.run ()

    printfn ""
    printfn $"passed: {Meditator.Tests.TestUtil.passed}, failed: {Meditator.Tests.TestUtil.failures}"

    if Meditator.Tests.TestUtil.failures > 0 then
        printfn "TEST SUITE FAILED"
        1
    else
        printfn "ALL TESTS PASS"
        0
