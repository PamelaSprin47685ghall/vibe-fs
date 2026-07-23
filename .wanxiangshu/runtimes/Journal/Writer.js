
import { class_type } from "../fable_modules/fable-library-js.5.6.0/Reflection.js";
import { Operators_Lock } from "../fable_modules/fable-library-js.5.6.0/FSharp.Core.js";
import { op_Addition, op_Subtraction, toInt64_unchecked } from "../fable_modules/fable-library-js.5.6.0/BigInt.js";
import { closeSync, fdatasyncSync, fsyncSync, writeSync, openSync, mkdirSync, existsSync } from "node:fs";
import { join } from "node:path";
import { LocalSeqModule_create, EventIdModule_create, RuntimeIdModule_value } from "../Kernel/Identity.js";
import { printf, toText } from "../fable_modules/fable-library-js.5.6.0/String.js";
import { newGuid, toString } from "../fable_modules/fable-library-js.5.6.0/Guid.js";
import { Fact, RuntimeFact } from "../Kernel/Fact.js";
import { EnvelopeModule_serialize, Envelope, StreamId } from "./Envelope.js";
import { get_UTF8 } from "../fable_modules/fable-library-js.5.6.0/Encoding.js";
import { CommitResult$1, JournalFailure } from "../Kernel/Outcome.js";
import { utcNow } from "../fable_modules/fable-library-js.5.6.0/DateOffset.js";

export class JournalWriter {
    constructor(runtimeId, filePath, fd) {
        this.runtimeId = runtimeId;
        this.filePath = filePath;
        this.fd = (fd | 0);
        this.gate = {};
        this.currentSeq = (2n);
        this.poisoned = false;
        this.disposed = false;
    }
    Dispose() {
        const this$ = this;
        JournalWriter__DisposeInternal(this$);
    }
    "System.IAsyncDisposable.DisposeAsync"() {
        const this$ = this;
        JournalWriter__DisposeInternal(this$);
        return Promise.resolve();
    }
}

export function JournalWriter_$reflection() {
    return class_type("Wanxiangshu.Next.Journal.JournalWriter", undefined, JournalWriter);
}

function JournalWriter_$ctor_5D59BA56(runtimeId, filePath, fd) {
    return new JournalWriter(runtimeId, filePath, fd);
}

export function JournalWriter__get_RuntimeId(_) {
    return _.runtimeId;
}

export function JournalWriter__get_FilePath(_) {
    return _.filePath;
}

export function JournalWriter__get_LocalSeq(this$) {
    return Operators_Lock(this$.gate, () => this$.currentSeq);
}

export function JournalWriter__get_LastCommittedLocalSeq(this$) {
    return Operators_Lock(this$.gate, () => toInt64_unchecked(op_Subtraction(this$.currentSeq, 1n)));
}

export function JournalWriter__get_IsPoisoned(this$) {
    return Operators_Lock(this$.gate, () => this$.poisoned);
}

export function JournalWriter_create(directory, runtimeId, processId, startedAt) {
    let arg;
    if (!existsSync(directory)) {
        const value = mkdirSync(directory, {
            recursive: true,
        });
    }
    const filePath = join(directory, (arg = RuntimeIdModule_value(runtimeId), toText(printf("%s.ndjson"))(arg)));
    const fd = openSync(filePath, "wx") | 0;
    const initEventId = EventIdModule_create(toString(newGuid(), "N"));
    const initFact = new Fact(0, [new RuntimeFact({
        ProcessId: processId,
        RuntimeId: runtimeId,
        StartedAt: startedAt,
    })]);
    const initEnvelope = new Envelope(runtimeId, LocalSeqModule_create(1n), startedAt, initEventId, new StreamId(0, []), undefined, initFact);
    const jsonLine = EnvelopeModule_serialize(initEnvelope) + "\n";
    writeSync(fd, get_UTF8().getBytes(jsonLine));
    try {
        fdatasyncSync(fd);
    }
    catch (matchValue) {
        fsyncSync(fd);
    }
    return [JournalWriter_$ctor_5D59BA56(runtimeId, filePath, fd), initEnvelope];
}

function JournalWriter__WriteAndFlush(this$, env, eventId) {
    const line = EnvelopeModule_serialize(env) + "\n";
    const bytes = get_UTF8().getBytes(line);
    try {
        writeSync(this$.fd, bytes);
        try {
            fdatasyncSync(this$.fd);
        }
        catch (matchValue) {
            fsyncSync(this$.fd);
        }
        this$.currentSeq = toInt64_unchecked(op_Addition(this$.currentSeq, 1n));
        return new CommitResult$1(0, [env]);
    }
    catch (ex) {
        this$.poisoned = true;
        return new CommitResult$1(1, [eventId, new JournalFailure(0, [ex.message])]);
    }
}

export function JournalWriter__Append(this$, streamKind, turnId, fact) {
    return Operators_Lock(this$.gate, () => {
        const eventId = EventIdModule_create(toString(newGuid(), "N"));
        return (this$.poisoned ? true : this$.disposed) ? (new CommitResult$1(1, [eventId, new JournalFailure(0, ["Writer is poisoned or disposed"])])) : JournalWriter__WriteAndFlush(this$, new Envelope(this$.runtimeId, LocalSeqModule_create(this$.currentSeq), utcNow(), eventId, streamKind, turnId, fact), eventId);
    });
}

function JournalWriter__DisposeInternal(this$) {
    Operators_Lock(this$.gate, () => {
        if (!this$.disposed) {
            this$.disposed = true;
            try {
                closeSync(this$.fd);
            }
            catch (matchValue) {
            }
        }
    });
}

