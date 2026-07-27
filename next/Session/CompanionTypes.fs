namespace Wanxiangshu.Next.Session

open System
open System.Threading.Tasks
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Tools

type BlogText = string

type CompanionOutcome =
    | Submitted
    | SkippedBusy

type ActivePrefixEpoch =
    { EpochId: string
      FrozenB: BlogText
      CutoffMessageIndex: int
      CoveredPrefixDigest: string }

type CompanionMemory =
    { LastSuccessfulProjection: ProjectionSnapshot option
      LatestB: BlogText option
      ActivePrefixEpoch: ActivePrefixEpoch option
      BloggerBusy: bool
      ReplacementActive: bool }

    member this.PrefixReplacementEnabled = this.ReplacementActive

type ICompanionDurablePort =
    abstract Load: SessionId -> CompanionMemory option
    abstract AppendSuccessful: SessionId * ProjectionSnapshot * BlogText -> Result<unit, string>
    abstract AppendEpochSwitched: SessionId * ActivePrefixEpoch -> Result<unit, string>
    abstract EnableReplacement: SessionId -> Result<unit, string>
    abstract AppendLink: SessionId * ChildId * string * string option -> Result<unit, string>
    abstract AppendUnlink: SessionId * ChildId -> Result<unit, string>

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Companion =

    let canCreateForRole (role: Role) : bool =
        MessageTransform.companionAllowedRole role

    let shouldCreateForAgent (agent: string option) : bool =
        MessageTransform.shouldCreateCompanion agent

    let jsonDelta = CompanionDelta.jsonDelta

    /// Pure compressPrefix: delegates to MessageTransform.replacePrefix using currentB and explicit watermark index.
    let compressPrefix
        (messages: HostMessage list)
        (currentB: BlogText option)
        (watermarkIndex: int)
        : HostMessage list =
        match currentB with
        | None -> messages
        | Some b -> MessageTransform.replacePrefix messages b (Index watermarkIndex)

    let compressPrefixText (messages: HostMessage list) (currentB: BlogText) (watermarkIndex: int) : HostMessage list =
        compressPrefix messages (Some currentB) watermarkIndex
