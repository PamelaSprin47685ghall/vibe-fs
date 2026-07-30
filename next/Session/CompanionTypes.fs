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

/// The frozen companion memory with its body already loaded, ready to inject into
/// X's projection (COMPANION-010).
///
/// Deliberately NOT named `ActivePrefixEpoch`: that name belongs to the durable
/// projection in `Wanxiangshu.Next.Journal`, which stores a `BlobRef` plus digest
/// rather than the text. Two types with one name for one concept resolve by `open`
/// order, so a caller could silently get the other one.
///
/// The distinction is real, not cosmetic. The journal records WHERE the body is;
/// this record is the body itself after a read. Only the second can be handed to
/// the transform boundary, and only the first can be folded.
type ResolvedPrefixMemory =
    { EpochId: string
      FrozenB: BlogText
      CutoffMessageIndex: int
      CoveredPrefixDigest: string }

type CompanionMemory =
    {
        LastSuccessfulProjection: ProjectionSnapshot option
        LatestB: BlogText option
        ActivePrefixEpoch: ResolvedPrefixMemory option
        /// COMPANION-003: the durable companion Blogger Session Y.
        BloggerSessionId: SessionId option
        PrefixReplacementEnabled: bool
    }

type ICompanionDurablePort =
    abstract Load: SessionId -> CompanionMemory option
    abstract AppendSuccessful: SessionId * ProjectionSnapshot * BlogText -> Result<unit, string>
    abstract AppendEpochSwitched: SessionId * ResolvedPrefixMemory -> Result<unit, string>
    abstract EnableReplacement: SessionId -> Result<unit, string>

    /// COMPANION-003. Takes the Blogger's own SessionId, not a `ChildId` plus a
    /// `"blogger"` target string: the previous shape recorded an EXEC-009 handle
    /// link and then recovered Y by searching for the literal target `"blogger"`,
    /// which is agent-string matching standing in for an identity.
    abstract LinkBlogger: SessionId * SessionId * string -> Result<unit, string>

    abstract CloseBlogger: SessionId -> Result<unit, string>

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Companion =

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

    let latestBFor (sessionId: SessionId) (companionMemory: CompanionMemory) : BlogText option = companionMemory.LatestB

    let frozenBForProjection (sessionId: SessionId) (companionMemory: CompanionMemory) : BlogText option =
        companionMemory.ActivePrefixEpoch |> Option.map (fun epoch -> epoch.FrozenB)

    let compressPrefixText (messages: HostMessage list) (currentB: BlogText) (watermarkIndex: int) : HostMessage list =
        compressPrefix messages (Some currentB) watermarkIndex
