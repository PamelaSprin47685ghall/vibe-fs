namespace Wanxiangshu.Sphinx.V2.Core

open System

/// The resource ledger.
///
/// WHAT[sphinx-v2-004]: consumption and capacity are different resources and are
/// counted separately. Model calls, tokens and money are consumed and never return;
/// concurrency slots are lent and returned. Wall-clock time is not additive across
/// parallel work and is deliberately not a ledger resource.
///
/// WHAT[sphinx-v2-013]: a failed, cancelled or duplicated physical call still costs
/// real money. Overrun is a recorded fact, never absorbed by rejecting the usage.

type ResourceKind =
    | Consumed of unitName: string
    | Capacity of slotName: string

type ResourceSpec =
    { Name: string
      Kind: ResourceKind
      AuthorizedLimit: float }

/// One reservation outstanding against the ledger. A reservation is a promise, not a
/// charge: it is released when the work settles, and only the settled usage counts.
type Reservation =
    {
        WorkId: WorkId
        Attempt: Attempt
        Resources: Map<string, float>
        /// Money is kept in the smallest currency unit so binary float addition cannot
        /// accumulate a fiscal drift across a long inquiry.
        MoneyMinor: int64 option
    }

/// One unit of provider consumption, as the provider reports it. It is a ledger record,
/// not a semantic fact, which is why it lives beside the resources it is subtracted from.
type ProviderUsage =
    { InputTokens: int64
      OutputTokens: int64
      Calls: int64
      MoneyMinor: int64
      UsageUnresolved: bool }

type SettledUsage =
    {
        WorkId: WorkId
        Attempt: Attempt
        Resources: Map<string, float>
        MoneyMinor: int64 option
        /// True when the provider never reported usage; the reserved amount stays.
        UsageUnresolved: bool
        /// True when the settled usage exceeded the reservation. Recorded, not rejected.
        Overrun: bool
    }

type BudgetError = { Code: string; Message: string }

module Budget =

    let private isFinite (value: float) =
        not (Double.IsNaN value) && not (Double.IsInfinity value)

    let private hasInvalidSpec (spec: ResourceSpec) : bool =
        String.IsNullOrWhiteSpace spec.Name
        || not (isFinite spec.AuthorizedLimit)
        || spec.AuthorizedLimit < 0.0

    let private hasDuplicateNames (specs: ResourceSpec list) : bool =
        let names = specs |> List.map (fun spec -> spec.Name) |> Set.ofList
        List.length specs <> Set.count names

    let validateSpecs (specs: ResourceSpec list) : Result<unit, BudgetError> =
        if specs |> List.isEmpty then
            Error
                { Code = "invalid-budget"
                  Message = "resource specs must not be empty" }
        elif specs |> List.exists hasInvalidSpec then
            Error
                { Code = "invalid-budget"
                  Message = "every resource needs a name and a finite nonnegative authorized limit" }
        elif hasDuplicateNames specs then
            Error
                { Code = "invalid-budget"
                  Message = "resource names must be unique" }
        else
            Ok()

    let private merge (left: Map<string, float>) (right: Map<string, float>) : Map<string, float> =
        (left, right)
        ||> Map.fold (fun acc key amount ->
            let existing = acc |> Map.tryFind key |> Option.defaultValue 0.0
            Map.add key (existing + amount) acc)

    /// signedFree = authorizedLimit - settledUsage - outstandingReservations.
    /// availableForNewWork = max(0, signedFree) and observedOverrun = max(0, -signedFree)
    /// are derived, never stored, so they cannot drift from the three facts that produce them.
    let signedFree
        (specs: ResourceSpec list)
        (settled: Map<string, float>)
        (reserved: Map<string, float>)
        (name: string)
        : float =
        let limit =
            specs
            |> List.tryFind (fun spec -> spec.Name = name)
            |> Option.map (fun spec -> spec.AuthorizedLimit)

        let used = settled |> Map.tryFind name |> Option.defaultValue 0.0
        let outstanding = reserved |> Map.tryFind name |> Option.defaultValue 0.0

        match limit with
        | Some limit -> limit - used - outstanding
        | None -> 0.0

    let availableForNewWork
        (specs: ResourceSpec list)
        (settled: Map<string, float>)
        (reserved: Map<string, float>)
        (name: string)
        : float =
        max 0.0 (signedFree specs settled reserved name)

    let observedOverrun
        (specs: ResourceSpec list)
        (settled: Map<string, float>)
        (reserved: Map<string, float>)
        (name: string)
        : float =
        max 0.0 (-(signedFree specs settled reserved name))

    /// Either the projected ledger fits, or the reason it does not.
    let private reservationOutcome
        (projected: Map<string, float>)
        (oversubscribedNames: string list)
        : Result<Map<string, float>, BudgetError> =
        let insufficiency () =
            Error
                { Code = "budget-insufficient"
                  Message = sprintf "reservation exceeds available budget: %s" (String.concat ", " oversubscribedNames) }

        match List.isEmpty oversubscribedNames with
        | true -> Ok projected
        | false -> insufficiency ()

    /// The refusal for a reservation that cannot be admitted, with its most specific
    /// reason first so the reported fault is stable.
    let private reservationRefusal
        (unknown: string list)
        (negative: string list)
        (projected: Map<string, float>)
        (oversubscribedNames: string list)
        : Result<Map<string, float>, BudgetError> =
        let unknownResource () =
            Error
                { Code = "unknown-resource"
                  Message = sprintf "reservation names unknown resources: %s" (String.concat ", " unknown) }

        let invalidAmount () =
            Error
                { Code = "invalid-budget"
                  Message =
                    sprintf "reservation amounts must be finite and nonnegative: %s" (String.concat ", " negative) }

        let oversubscribed () =
            reservationOutcome projected oversubscribedNames

        match List.isEmpty unknown, List.isEmpty negative with
        | false, _ -> unknownResource ()
        | true, false -> invalidAmount ()
        | true, true -> oversubscribed ()

    /// A new reservation may not push the projected balance below zero. The caller
    /// keeps the render reserve out of this pool, so a render can never be priced out
    /// by an investigation that was admitted before it.
    let tryReserve
        (specs: ResourceSpec list)
        (settled: Map<string, float>)
        (reserved: Map<string, float>)
        (reservation: Reservation)
        : Result<Map<string, float>, BudgetError> =
        let names = specs |> List.map (fun spec -> spec.Name) |> Set.ofList

        let unknown =
            reservation.Resources
            |> Map.toList
            |> List.filter (fun (name, _) -> not (Set.contains name names))
            |> List.map fst

        let negative =
            reservation.Resources
            |> Map.toList
            |> List.filter (fun (_, amount) -> not (isFinite amount) || amount < 0.0)
            |> List.map fst

        let projected = merge reserved reservation.Resources

        let oversubscribedNames =
            specs
            |> List.filter (fun spec ->
                let projectedForSpec = projected |> Map.tryFind spec.Name |> Option.defaultValue 0.0
                let available = availableForNewWork specs settled reserved spec.Name
                projectedForSpec > available)
            |> List.map (fun spec -> spec.Name)

        let admissible =
            List.isEmpty unknown
            && List.isEmpty negative
            && List.isEmpty oversubscribedNames

        match admissible with
        | true -> Ok projected
        | false -> reservationRefusal unknown negative projected oversubscribedNames

    /// Settlement replaces this work's own reservation with what it really spent.
    /// Releasing the reservation and booking the usage in one step is what keeps a
    /// retry from being double counted as two reservations and one charge.
    let settle
        (previous: Map<string, float>)
        (settled: Map<string, float>)
        (reservation: Reservation)
        (usage: SettledUsage)
        : Result<Map<string, float> * Map<string, float>, BudgetError> =
        let remaining =
            (previous, reservation.Resources)
            ||> Map.fold (fun acc key amount ->
                let outstanding = acc |> Map.tryFind key |> Option.defaultValue 0.0
                let left = outstanding - amount

                if left <= 0.0 then
                    Map.remove key acc
                else
                    Map.add key left acc)

        let booked =
            if usage.UsageUnresolved then
                // No usage figure from the provider: the reservation stays booked so the
                // ledger cannot claim the work was free.
                merge settled reservation.Resources
            else
                merge settled usage.Resources

        Ok(remaining, booked)

    /// Merge several reservations of the same pool. Used when a round opens.
    let mergeReservations (reservations: Reservation list) : Map<string, float> =
        reservations
        |> List.fold (fun acc reservation -> merge acc reservation.Resources) Map.empty

    /// Total outstanding reservation across every work. Derived, never stored, so it
    /// cannot drift from the individual reservations it is supposed to summarize.
    let mergeReserved (pools: Map<string, float> list) : Map<string, float> =
        pools |> List.fold (fun acc pool -> merge acc pool) Map.empty
