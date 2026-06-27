module Wanxiangshu.Tests.MessageTransformPolicyTests

open Fable.Core
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.MessageTransformPolicy

let defaultExcludedTrue () =
    let agents = [ "browser"; "investigator"; "executor"; "title"; "compaction"; "bookkeeper" ]
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
    let agents = [ "browser"; "investigator"; "executor"; "title"; "compaction"; "bookkeeper" ]
    agents
    |> List.iter (fun a ->
        check (sprintf "child still excluded: %s" a) (shouldExcludeAgentFromProjection a true))
    check "main still not excluded even in child workspace" (not (shouldExcludeAgentFromProjection "main" true))
    check "agent still not excluded even in child workspace" (not (shouldExcludeAgentFromProjection "agent" true))

let run () =
    defaultExcludedTrue ()
    defaultExcludedFalse ()
    childWorkspaceExtraExcluded ()
    childWorkspaceNotExcluded ()
