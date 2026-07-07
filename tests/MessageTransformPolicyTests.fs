module Wanxiangshu.Tests.MessageTransformPolicyTests

open Fable.Core
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.MessageTransformPolicy
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Kernel.CapsFormat
open Wanxiangshu.Shell.MessageTransformCore
open Wanxiangshu.Shell.MessageTransformPipeline
open Wanxiangshu.Shell.MessageTransformHostEntry
open Wanxiangshu.Shell.ReviewRuntime

let defaultExcludedTrue () =
    let agents = [ "browser"; "investigator"; "executor"; "title"; "compaction" ]
    agents
    |> List.iter (fun a ->
        check (sprintf "default excluded: %s" a) (shouldExcludeAgentFromProjection a false))

let defaultExcludedFalse () =
    let agents = [ "main"; "agent"; "manager"; "user" ]
    agents
    |> List.iter (fun a ->
        check (sprintf "not excluded: %s" a) (not (shouldExcludeAgentFromProjection a false)))

let childWorkspaceExtraExcluded () =
    let agents = [ "exec"; "explore" ]
    agents
    |> List.iter (fun a ->
        check (sprintf "child excluded: %s" a) (shouldExcludeAgentFromProjection a true))

let childWorkspaceNotExcluded () =
    let agents = [ "browser"; "investigator"; "executor"; "title"; "compaction" ]
    agents
    |> List.iter (fun a ->
        check (sprintf "child still excluded: %s" a) (shouldExcludeAgentFromProjection a true))
    check "main still not excluded even in child workspace" (not (shouldExcludeAgentFromProjection "main" true))
    check "agent still not excluded even in child workspace" (not (shouldExcludeAgentFromProjection "agent" true))

let testTransformO1Cache () = promise {
    pipelineRunCount <- 0
    let reviewStore = createReviewStore ()
    let plan = {
        SessionID = ""
        Agent = "main"
        Directory = ""
        Excluded = false
        IsSubagentSession = false
        Cleaned = []
        RawArray = None
    }
    let backlogOps = {
        Host = opencode
        GetOrRebuildBacklog = fun _ _ -> []
    }
    let encodeMessages (msgs: Message<obj> list) = [||]
    let injectFn (_excluded: bool) (arr: obj array) = promise { return arr }
    let dedupFn (_excluded: bool) (arr: obj array) = arr
    let loadCaps () = promise { return [] }
    let buildCaps (arr: obj array) (_caps: CapsFile list) (_hint: string option) = arr
    let plan2 = { plan with Cleaned = [{ info = { id = "msg1"; sessionID = "test"; role = User; agent = "manager"; isError = false; toolName = ""; details = null; time = null }; parts = []; source = Native; raw = null }] }
    let! _ = runHostMessagesTransform reviewStore "" IfStoreEmpty (fun _ -> promise { return Seq.empty }) plan2 backlogOps encodeMessages injectFn dedupFn loadCaps buildCaps
    equal "count after first call" 1 pipelineRunCount
    let! _ = runHostMessagesTransform reviewStore "" IfStoreEmpty (fun _ -> promise { return Seq.empty }) plan2 backlogOps encodeMessages injectFn dedupFn loadCaps buildCaps
    equal "count after second call should stay 1 (cache hit)" 1 pipelineRunCount
}

let run () = promise {
    defaultExcludedTrue ()
    defaultExcludedFalse ()
    childWorkspaceExtraExcluded ()
    childWorkspaceNotExcluded ()
    do! testTransformO1Cache ()
}
