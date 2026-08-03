// xunit 入口：三个套件各一个 Fact；断言失败 → 套件失败（dotnet test 可识别）。
module Meditator.Tests.Entry

open Xunit

type Suite() =
    [<Fact>]
    member _.``ClaimTest closed loop``() =
        Assert.True(Meditator.Tests.ClaimTest.run (), "ClaimTest 闭环断言全部通过")

    [<Fact>]
    member _.``Property tests``() =
        Assert.True(Meditator.Tests.Properties.run (), "属性测试断言全部通过")

    [<Fact>]
    member _.``Crash injection``() =
        Assert.True(Meditator.Tests.Crash.run (), "崩溃注入测试断言全部通过")
