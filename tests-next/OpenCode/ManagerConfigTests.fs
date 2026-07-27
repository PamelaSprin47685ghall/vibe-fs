namespace Wanxiangshu.Next.Tests.OpenCode

open System
open Fable.Core.JsInterop
open Xunit
open Wanxiangshu.Next.OpenCode

module ManagerConfigTests =

    [<Fact>]
    let ``configureManager_disables_auto_compaction`` () =
        let config: obj = createEmpty
        ManagerConfig.configureManager config
        let compaction = config?compaction
        Assert.NotNull(compaction)
        Assert.False(unbox<bool> compaction?auto, "Expected cfg.compaction.auto === false")
