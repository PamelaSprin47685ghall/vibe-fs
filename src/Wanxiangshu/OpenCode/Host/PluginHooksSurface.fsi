namespace Wanxiangshu.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Persistence.Journal

module PluginHooksSurface =

    /// Opaque Host-owned observation for the Blogger adapter proof.
    type BloggerAdapterObservation =
        private new: first: string * second: string -> BloggerAdapterObservation
        member internal First: string
        member internal Second: string
        static member internal Create: first: string * second: string -> BloggerAdapterObservation

    val policyAwareHook: operation: string -> adaptedHook: obj -> obj

    /// Classify a thrown JS value through the real failure membrane: typed
    /// failure kind, lifecycle, settlement evidence, and whether the hook
    /// arguments proved an owned execution key.
    val normalizeHookFailureOutcome: args: obj -> context: obj -> error: obj -> obj

    /// HOST-BOUNDARY-032: run the real tool.definition decoration for one tool
    /// id. Non-review ids are returned unmodified.
    val decorateReviewToolDefinition: toolID: string -> definition: obj -> unit

    val hookFailurePolicy: failure: string -> settlement: string -> string

    /// Real Coordinator -> CompanionHost -> PromptDispatcher Host adapter. The
    /// same frozen context is offered twice while one physical flight remains
    /// unresolved, proving the second decision stops before Host submission.
    val coordinateBloggerUnresolvedTwice:
        port: obj ->
        handle: JournalHandle ->
        mainSession: string ->
        bloggerSession: string ->
        requestId: string ->
            Task<BloggerAdapterObservation>

    val firstBloggerEffect: observation: BloggerAdapterObservation -> string

    val secondBloggerEffect: observation: BloggerAdapterObservation -> string

    /// run the real read-only delegation schema decoration
    /// for one tool id (same contract function production uses).
    val decorateReadonlyDelegationToolDefinition: toolID: string -> definition: obj -> unit

    /// JS-boundary budget validation projected as a
    /// JS-native view: { ok = true; rounds: number } or { ok: false; error: string }.
    val readonlyDelegationBudgetOf: value: obj -> obj

    /// self_note validation projected as a JS-native view:
    /// { ok = true; note: string | null } or { ok: false; error: string }.
    val readonlyDelegationSelfNoteOf: arguments: obj -> obj

    /// Production tool.execute.before hide for the two protocol fields.
    val hideReadonlyDelegationArgs: args: obj -> unit

    /// Production tool.execute.after restore for the two protocol fields.
    val restoreReadonlyDelegationArgs: args: obj -> unit
