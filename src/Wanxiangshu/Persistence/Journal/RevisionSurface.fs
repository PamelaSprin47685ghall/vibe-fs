namespace Wanxiangshu.Persistence.Journal

open System
open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity

/// Plain-data revision subscription surface for the opaque journal capability.
/// The journal and envelope stay behind this boundary; callers receive only the
/// revision and canonical line needed to observe one successful fold.
[<RequireQualifiedAccess>]
module JournalRevisionSurface =

    let revision (handle: JournalHandle) : int =
        AgentJournal.revision handle.Journal |> JournalRevision.value |> int

    let awaitChangeFrom (fromRevision: int64) (handle: JournalHandle) : Task<obj> =
        task {
            let! change = AgentJournal.awaitChangeFrom (JournalRevision.create fromRevision) handle.Journal

            return
                box
                    {| revision = JournalRevision.value change.Revision |> int
                       envelope = Envelope.serialize change.Envelope |}
        }
