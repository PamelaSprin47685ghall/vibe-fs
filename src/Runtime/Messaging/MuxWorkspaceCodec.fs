module Wanxiangshu.Runtime.MuxWorkspaceCodec

open Wanxiangshu.Runtime.Dyn

let private findWorkspaceEntry (deps: obj) (workspaceId: string) : obj =
    if Dyn.isNullish deps || workspaceId = "" then
        null
    else
        let loadConfig = Dyn.get deps "loadConfigOrDefault"
        let findEntry = Dyn.get deps "findWorkspaceEntry"

        if Dyn.isNullish loadConfig || Dyn.isNullish findEntry then
            null
        else
            try
                let configFile = Dyn.call1 loadConfig null
                Dyn.call2 findEntry configFile workspaceId
            with _ ->
                null

let isChildWorkspace (deps: obj) (workspaceId: string) : bool =
    let entry = findWorkspaceEntry deps workspaceId

    if Dyn.isNullish entry then
        false
    else
        let workspace = Dyn.get entry "workspace"
        not (Dyn.isNullish workspace) && Dyn.str workspace "parentWorkspaceId" <> ""

let tryGetParentWorkspaceId (deps: obj) (workspaceId: string) : string option =
    let entry = findWorkspaceEntry deps workspaceId

    if Dyn.isNullish entry then
        None
    else
        let workspace = Dyn.get entry "workspace"

        if Dyn.isNullish workspace then
            None
        else
            let parent = Dyn.str workspace "parentWorkspaceId"
            if parent <> "" then Some parent else None
