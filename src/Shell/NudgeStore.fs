module VibeFs.Shell.NudgeStore

open VibeFs.Kernel.Nudge

/// The host-facing coordinator: wraps the pure decision in mutable per-session
/// state and a one-shot suppress set.  Effects only through `shouldNudge`.
type NudgeCoordinator() =
    let mutable state = freshCoordinator
    let mutable suppressed = Set.empty<string>

    member _.shouldNudge(sessionId, context: NudgeContext) : string =
        if Set.contains sessionId suppressed then
            suppressed <- Set.remove sessionId suppressed
            "none"
        else
            let next, action = update state sessionId context
            state <- next
            toString action

    member _.suppress(sessionId) = suppressed <- Set.add sessionId suppressed

    member _.clearSession(sessionId) =
        state <- { state with sessions = Map.remove sessionId state.sessions }
        suppressed <- Set.remove sessionId suppressed

    member _.clear() =
        state <- freshCoordinator
        suppressed <- Set.empty

let defaultCoordinator = NudgeCoordinator()
