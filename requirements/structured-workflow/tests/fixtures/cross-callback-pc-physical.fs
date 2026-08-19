module Sample

/// DSL-cross-callback-proof: physical — PTY process handle registry
// DSL-MUTABLE: resource — per-session PTY handle
let ptyHandles = Dictionary<string, IntPtr>()

type PtyManager() =
    member _.TryTakeHandle(sessionId: string) =
        match ptyHandles.TryGetValue(sessionId) with
        | true, handle -> ptyHandles.Remove(sessionId) |> ignore; Some handle
        | _ -> None
