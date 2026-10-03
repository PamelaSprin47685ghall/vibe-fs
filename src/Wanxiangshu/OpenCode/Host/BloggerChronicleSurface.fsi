namespace Wanxiangshu.OpenCode

open System.Threading.Tasks

/// Blogger chronicle text injection owner surface (COGNITIVE-ENVIRONMENT-015).
/// The transform owns its model gate, journal read and placement invariants;
/// JS tests reach the real production entry and observe only JSON results.
module BloggerChronicleSurface =

    /// Boot the production EventStore journal behind one opaque capability.
    val createJournal: directory: string -> Task<obj>

    val disposeJournal: journal: obj -> unit

    /// Append one durable companion-link fact; the owner projection decides
    /// companion eligibility exactly as the production transform reads it.
    val appendCompanionLink: journal: obj -> payload: obj -> Task<obj>

    /// Run the real production injection entry in place; the caller observes
    /// only the resulting message array.
    val maybeInject: journal: obj -> session: string -> language: obj -> outObj: obj -> obj
