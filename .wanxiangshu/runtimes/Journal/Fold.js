
import { Record } from "../fable_modules/fable-library-js.5.6.0/Types.js";
import { int64_type, record_type, class_type, option_type, string_type } from "../fable_modules/fable-library-js.5.6.0/Reflection.js";
import { RuntimeId_$reflection, SessionId_$reflection, ProcessId_$reflection, ChildId_$reflection, TurnId_$reflection, MessageId_$reflection } from "../Kernel/Identity.js";
import { SquadTaskResult_$reflection, ProcessResult_$reflection, ChildResult_$reflection, SessionResult_$reflection, ReviewVerdict_$reflection, TodoSnapshot_$reflection, PromptOutcome_$reflection } from "../Kernel/Fact.js";
import { tryFind, add, empty } from "../fable_modules/fable-library-js.5.6.0/Map.js";
import { comparePrimitives, compare } from "../fable_modules/fable-library-js.5.6.0/Util.js";
import { op_Addition, toInt64_unchecked } from "../fable_modules/fable-library-js.5.6.0/BigInt.js";
import { fold } from "../fable_modules/fable-library-js.5.6.0/List.js";

export class PromptHistoryRecord extends Record {
    constructor(PromptKey, UserMessageId, AssistantMessageId, Outcome, CompletedAt) {
        super();
        this.PromptKey = PromptKey;
        this.UserMessageId = UserMessageId;
        this.AssistantMessageId = AssistantMessageId;
        this.Outcome = Outcome;
        this.CompletedAt = CompletedAt;
    }
}

export function PromptHistoryRecord_$reflection() {
    return record_type("Wanxiangshu.Next.Journal.PromptHistoryRecord", [], PromptHistoryRecord, () => [["PromptKey", string_type], ["UserMessageId", option_type(MessageId_$reflection())], ["AssistantMessageId", option_type(MessageId_$reflection())], ["Outcome", option_type(PromptOutcome_$reflection())], ["CompletedAt", option_type(class_type("System.DateTimeOffset"))]]);
}

export class SessionProjection extends Record {
    constructor(Todos, LastReview, SettledResult, HumanTurnId, Children, Processes, SquadTasks, Version) {
        super();
        this.Todos = Todos;
        this.LastReview = LastReview;
        this.SettledResult = SettledResult;
        this.HumanTurnId = HumanTurnId;
        this.Children = Children;
        this.Processes = Processes;
        this.SquadTasks = SquadTasks;
        this.Version = Version;
    }
}

export function SessionProjection_$reflection() {
    return record_type("Wanxiangshu.Next.Journal.SessionProjection", [], SessionProjection, () => [["Todos", option_type(TodoSnapshot_$reflection())], ["LastReview", option_type(ReviewVerdict_$reflection())], ["SettledResult", option_type(SessionResult_$reflection())], ["HumanTurnId", option_type(TurnId_$reflection())], ["Children", class_type("Microsoft.FSharp.Collections.FSharpMap`2", [ChildId_$reflection(), ChildResult_$reflection()])], ["Processes", class_type("Microsoft.FSharp.Collections.FSharpMap`2", [ProcessId_$reflection(), ProcessResult_$reflection()])], ["SquadTasks", class_type("Microsoft.FSharp.Collections.FSharpMap`2", [string_type, SquadTaskResult_$reflection()])], ["Version", int64_type]]);
}

export class ProjectionSet extends Record {
    constructor(Todos, LastReview, SessionProjections, HistoricalPrompts, RuntimeId) {
        super();
        this.Todos = Todos;
        this.LastReview = LastReview;
        this.SessionProjections = SessionProjections;
        this.HistoricalPrompts = HistoricalPrompts;
        this.RuntimeId = RuntimeId;
    }
}

export function ProjectionSet_$reflection() {
    return record_type("Wanxiangshu.Next.Journal.ProjectionSet", [], ProjectionSet, () => [["Todos", option_type(TodoSnapshot_$reflection())], ["LastReview", option_type(ReviewVerdict_$reflection())], ["SessionProjections", class_type("Microsoft.FSharp.Collections.FSharpMap`2", [SessionId_$reflection(), SessionProjection_$reflection()])], ["HistoricalPrompts", class_type("Microsoft.FSharp.Collections.FSharpMap`2", [string_type, PromptHistoryRecord_$reflection()])], ["RuntimeId", option_type(RuntimeId_$reflection())]]);
}

export class RuntimeSnapshot extends Record {
    constructor(Frontier, Projections, OwnRuntimeId, OwnLocalSeq) {
        super();
        this.Frontier = Frontier;
        this.Projections = Projections;
        this.OwnRuntimeId = OwnRuntimeId;
        this.OwnLocalSeq = OwnLocalSeq;
    }
}

export function RuntimeSnapshot_$reflection() {
    return record_type("Wanxiangshu.Next.Journal.RuntimeSnapshot", [], RuntimeSnapshot, () => [["Frontier", class_type("Microsoft.FSharp.Collections.FSharpMap`2", [RuntimeId_$reflection(), int64_type])], ["Projections", ProjectionSet_$reflection()], ["OwnRuntimeId", option_type(RuntimeId_$reflection())], ["OwnLocalSeq", int64_type]]);
}

export const Fold_emptySessionProjection = new SessionProjection(undefined, undefined, undefined, undefined, empty({
    Compare: (x, y) => (compare(x, y) | 0),
}), empty({
    Compare: (x_1, y_1) => (compare(x_1, y_1) | 0),
}), empty({
    Compare: (x_2, y_2) => (comparePrimitives(x_2, y_2) | 0),
}), 0n);

export const Fold_empty = new ProjectionSet(undefined, undefined, empty({
    Compare: (x, y) => (compare(x, y) | 0),
}), empty({
    Compare: (x_1, y_1) => (comparePrimitives(x_1, y_1) | 0),
}), undefined);

function Fold_updatePromptRecord(key, updateFn, map) {
    let matchValue;
    return add(key, updateFn((matchValue = tryFind(key, map), (matchValue == null) ? (new PromptHistoryRecord(key, undefined, undefined, undefined, undefined)) : matchValue)), map);
}

function Fold_updateSessionProjection(sessionId, updateFn, map) {
    let matchValue;
    const updated = updateFn((matchValue = tryFind(sessionId, map), (matchValue == null) ? Fold_emptySessionProjection : matchValue));
    return add(sessionId, new SessionProjection(updated.Todos, updated.LastReview, updated.SettledResult, updated.HumanTurnId, updated.Children, updated.Processes, updated.SquadTasks, toInt64_unchecked(op_Addition(updated.Version, 1n))), map);
}

function Fold_foldPrompt(proj, env) {
    const matchValue = env.Fact;
    if (matchValue.tag === 3) {
        switch (matchValue.fields[0].tag) {
            case 1:
                return new ProjectionSet(proj.Todos, proj.LastReview, proj.SessionProjections, Fold_updatePromptRecord(matchValue.fields[0].fields[0].PromptKey, (r) => (new PromptHistoryRecord(r.PromptKey, matchValue.fields[0].fields[0].MessageId, r.AssistantMessageId, r.Outcome, r.CompletedAt)), proj.HistoricalPrompts), proj.RuntimeId);
            case 2:
                return new ProjectionSet(proj.Todos, proj.LastReview, proj.SessionProjections, Fold_updatePromptRecord(matchValue.fields[0].fields[0].PromptKey, (r_1) => {
                    let matchValue_1;
                    return new PromptHistoryRecord(r_1.PromptKey, r_1.UserMessageId, (matchValue_1 = matchValue.fields[0].fields[0].AssistantMessageId, (matchValue_1 == null) ? r_1.AssistantMessageId : matchValue_1), matchValue.fields[0].fields[0].Outcome, env.ObservedAt);
                }, proj.HistoricalPrompts), proj.RuntimeId);
            default:
                return new ProjectionSet(proj.Todos, proj.LastReview, proj.SessionProjections, Fold_updatePromptRecord(matchValue.fields[0].fields[0].PromptKey, (x) => x, proj.HistoricalPrompts), proj.RuntimeId);
        }
    }
    else {
        return proj;
    }
}

function Fold_foldTodo(proj, env, t) {
    const matchValue = env.Stream;
    switch (matchValue.tag) {
        case 0:
            return new ProjectionSet(t.Snapshot, proj.LastReview, proj.SessionProjections, proj.HistoricalPrompts, proj.RuntimeId);
        case 1:
            return new ProjectionSet(proj.Todos, proj.LastReview, Fold_updateSessionProjection(matchValue.fields[0], (s) => (new SessionProjection(t.Snapshot, s.LastReview, s.SettledResult, s.HumanTurnId, s.Children, s.Processes, s.SquadTasks, s.Version)), proj.SessionProjections), proj.HistoricalPrompts, proj.RuntimeId);
        default:
            return proj;
    }
}

function Fold_foldReview(proj, env, r) {
    const matchValue = env.Stream;
    switch (matchValue.tag) {
        case 0: {
            const proj1 = new ProjectionSet(proj.Todos, r.Verdict, proj.SessionProjections, proj.HistoricalPrompts, proj.RuntimeId);
            const matchValue_1 = r.ResultingTodo;
            if (matchValue_1 == null) {
                return proj1;
            }
            else {
                return new ProjectionSet(matchValue_1, proj1.LastReview, proj1.SessionProjections, proj1.HistoricalPrompts, proj1.RuntimeId);
            }
        }
        case 1:
            return new ProjectionSet(proj.Todos, proj.LastReview, Fold_updateSessionProjection(matchValue.fields[0], (s) => {
                const s1 = new SessionProjection(s.Todos, r.Verdict, s.SettledResult, s.HumanTurnId, s.Children, s.Processes, s.SquadTasks, s.Version);
                const matchValue_2 = r.ResultingTodo;
                return (matchValue_2 == null) ? s1 : (new SessionProjection(matchValue_2, s1.LastReview, s1.SettledResult, s1.HumanTurnId, s1.Children, s1.Processes, s1.SquadTasks, s1.Version));
            }, proj.SessionProjections), proj.HistoricalPrompts, proj.RuntimeId);
        default:
            return proj;
    }
}

function Fold_foldSession(proj, env, sFact) {
    const matchValue = env.Stream;
    if (matchValue.tag === 1) {
        return new ProjectionSet(proj.Todos, proj.LastReview, Fold_updateSessionProjection(matchValue.fields[0], (s) => ((sFact.tag === 1) ? (new SessionProjection(s.Todos, s.LastReview, sFact.fields[0].Result, s.HumanTurnId, s.Children, s.Processes, s.SquadTasks, s.Version)) : (new SessionProjection(s.Todos, s.LastReview, s.SettledResult, sFact.fields[0].TurnId, s.Children, s.Processes, s.SquadTasks, s.Version))), proj.SessionProjections), proj.HistoricalPrompts, proj.RuntimeId);
    }
    else {
        return proj;
    }
}

function Fold_foldChild(proj, env, cFact) {
    const matchValue = env.Stream;
    if (matchValue.tag === 1) {
        return new ProjectionSet(proj.Todos, proj.LastReview, Fold_updateSessionProjection(matchValue.fields[0], (s) => {
            if (cFact.tag === 1) {
                const c = cFact.fields[0];
                return new SessionProjection(s.Todos, s.LastReview, s.SettledResult, s.HumanTurnId, add(c.ChildId, c.Result, s.Children), s.Processes, s.SquadTasks, s.Version);
            }
            else {
                return s;
            }
        }, proj.SessionProjections), proj.HistoricalPrompts, proj.RuntimeId);
    }
    else {
        return proj;
    }
}

function Fold_foldProcess(proj, env, prFact) {
    const matchValue = env.Stream;
    if (matchValue.tag === 1) {
        return new ProjectionSet(proj.Todos, proj.LastReview, Fold_updateSessionProjection(matchValue.fields[0], (s) => {
            if (prFact.tag === 1) {
                const p = prFact.fields[0];
                return new SessionProjection(s.Todos, s.LastReview, s.SettledResult, s.HumanTurnId, s.Children, add(p.ProcessId, p.Result, s.Processes), s.SquadTasks, s.Version);
            }
            else {
                return s;
            }
        }, proj.SessionProjections), proj.HistoricalPrompts, proj.RuntimeId);
    }
    else {
        return proj;
    }
}

function Fold_foldSquad(proj, env, sqFact) {
    const matchValue = env.Stream;
    if (matchValue.tag === 1) {
        return new ProjectionSet(proj.Todos, proj.LastReview, Fold_updateSessionProjection(matchValue.fields[0], (s) => {
            if (sqFact.tag === 0) {
                const t = sqFact.fields[0];
                return new SessionProjection(s.Todos, s.LastReview, s.SettledResult, s.HumanTurnId, s.Children, s.Processes, add(t.TaskId, t.Result, s.SquadTasks), s.Version);
            }
            else {
                return s;
            }
        }, proj.SessionProjections), proj.HistoricalPrompts, proj.RuntimeId);
    }
    else {
        return proj;
    }
}

export function Fold_foldEnvelope(proj, env) {
    const matchValue = env.Fact;
    switch (matchValue.tag) {
        case 1:
            return Fold_foldSession(proj, env, matchValue.fields[0]);
        case 2:
            return Fold_foldTodo(proj, env, matchValue.fields[0].fields[0]);
        case 4:
            return Fold_foldReview(proj, env, matchValue.fields[0].fields[0]);
        case 3:
            return Fold_foldPrompt(proj, env);
        case 5:
            return Fold_foldChild(proj, env, matchValue.fields[0]);
        case 6:
            return Fold_foldProcess(proj, env, matchValue.fields[0]);
        case 7:
            return Fold_foldSquad(proj, env, matchValue.fields[0]);
        default:
            return new ProjectionSet(proj.Todos, proj.LastReview, proj.SessionProjections, proj.HistoricalPrompts, matchValue.fields[0].fields[0].RuntimeId);
    }
}

export function Fold_apply(proj, envelopes) {
    return fold(Fold_foldEnvelope, proj, envelopes);
}

