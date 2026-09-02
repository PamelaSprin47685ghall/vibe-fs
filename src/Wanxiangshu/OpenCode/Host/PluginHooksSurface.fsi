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

    val providerInputRejection: message: string -> obj

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
