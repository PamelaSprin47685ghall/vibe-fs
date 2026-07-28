namespace Wanxiangshu.Next.Tests.OpenCode

open System
open Fable.Core.JsInterop
open Xunit
open Wanxiangshu.Next.OpenCode

module ManagerConfigTests =

    let private seedManagedAgents (config: obj) (samePairModel: bool) =
        let agents: obj = createEmpty
        config?agent <- agents

        for name in ManagedAgent.requiredNames do
            let entry: obj = createEmpty

            let model =
                if samePairModel then
                    "provider/shared-model"
                else
                    sprintf "provider/%s-model" name

            entry?model <- model
            agents?(name) <- entry

    [<Fact>]
    let ``configureManager_validates_inventory_and_disables_auto_compaction`` () =
        let config: obj = createEmpty
        seedManagedAgents config false
        ManagerConfig.configureManager config
        let compaction = config?compaction
        Assert.NotNull(compaction)
        Assert.False(unbox<bool> compaction?auto, "Expected cfg.compaction.auto === false")

        let fastReviewer = config?agent?``fast-reviewer``
        Assert.NotNull(fastReviewer)
        Assert.equal ("provider/fast-reviewer-model", unbox<string> fastReviewer?model)
        Assert.NotNull(fastReviewer?permission)
        Assert.NotNull(fastReviewer?prompt)

    [<Fact>]
    let ``configureManager_fails_when_required_agent_missing`` () =
        let config: obj = createEmpty
        seedManagedAgents config false
        config?agent?``fast-coder`` <- null

        let mutable failed = false

        try
            ManagerConfig.configureManager config
        with :? InvalidOperationException as ex ->
            failed <- true
            Assert.Contains("fast-coder", ex.Message)

        Assert.True(failed, "expected config gate failure")

    [<Fact>]
    let ``configureManager_fails_when_fast_deep_share_model`` () =
        let config: obj = createEmpty
        seedManagedAgents config true

        let mutable failed = false

        try
            ManagerConfig.configureManager config
        with :? InvalidOperationException as ex ->
            failed <- true
            Assert.Contains("same model", ex.Message)

        Assert.True(failed, "expected duplicate pair model failure")

    [<Fact>]
    let ``configureManager_rejects_legacy_unprefixed_agent`` () =
        let config: obj = createEmpty
        seedManagedAgents config false
        let legacy: obj = createEmpty
        legacy?model <- "provider/legacy"
        config?agent?manager <- legacy

        let mutable failed = false

        try
            ManagerConfig.configureManager config
        with :? InvalidOperationException as ex ->
            failed <- true
            Assert.Contains("Legacy agent name", ex.Message)

        Assert.True(failed, "expected legacy agent rejection")
