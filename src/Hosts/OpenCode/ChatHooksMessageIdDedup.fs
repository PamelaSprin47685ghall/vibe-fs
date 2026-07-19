module Wanxiangshu.Hosts.Opencode.ChatHooksMessageIdDedup

/// Per-session "have we already observed this host message id" set.
///
/// Used by `ChatHooks.chatMessageFor` to drop duplicate `chat.message`
/// hook invocations before any side-effect (in particular
/// `OnNewHumanMessage`, which cancels active fallback leases).
///
/// Dedup must run BEFORE the human-turn transition; the
/// previous ordering deduped after cancel and could re-cancel a
/// freshly-settled fallback.
///
/// The set is a per-process map keyed by `SessionId`. It is bounded
/// to the last 256 message ids per session so a long-lived session
/// cannot leak entries indefinitely. On `OnSessionClosed` the entry
/// is removed.

let private capacity = 256

let mutable private seenBySession: Map<string, Set<string>> = Map.empty

let private trim (set: Set<string>) : Set<string> =
    if Set.count set <= capacity then
        set
    else
        // F# Set is ordered; drop the "smallest" entries (lexicographic
        // by message id), which approximates a sliding window without
        // needing a real deque.
        set |> Set.toList |> List.skip (Set.count set - capacity) |> Set.ofList

/// Record a message id as observed for the session. Returns `true`
/// if the id was already present (i.e. the caller should treat the
/// hook as a duplicate), `false` if this is the first observation.
let markSeen (sessionId: string) (messageId: string) : bool =
    let current = Map.tryFind sessionId seenBySession |> Option.defaultValue Set.empty

    if Set.contains messageId current then
        true
    else
        let next = trim (Set.add messageId current)
        seenBySession <- Map.add sessionId next seenBySession
        false

/// Drop the per-session entry. Called from the SessionClosed domain
/// command so the map never grows without bound for deleted
/// sessions.
let forget (sessionId: string) : unit =
    seenBySession <- Map.remove sessionId seenBySession
