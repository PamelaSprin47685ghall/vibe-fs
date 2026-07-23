
import { FSharpRef, Record } from "../fable_modules/fable-library-js.5.6.0/Types.js";
import { SessionId_$reflection } from "../Kernel/Identity.js";
import { record_type, option_type, class_type } from "../fable_modules/fable-library-js.5.6.0/Reflection.js";
import { SessionDriver_$ctor_Z5603C1CE, SessionDrivers_$ctor, SessionDriver__get_Worker, SessionDriver_$reflection } from "../Session/Driver.js";
import { task } from "../fable_modules/fable-library-js.5.6.0/TaskBuilder.js";
import { Operators_Lock } from "../fable_modules/fable-library-js.5.6.0/FSharp.Core.js";
import { createCancellationToken, cancel } from "../fable_modules/fable-library-js.5.6.0/Async.js";
import { getEnumerator, defaultOf, safeHash, equals, disposeSafe } from "../fable_modules/fable-library-js.5.6.0/Util.js";
import { Dictionary } from "../fable_modules/fable-library-js.5.6.0/MutableMap.js";
import { GatewayModule_start } from "./Gateway.js";
import { FSharpResult$2 } from "../fable_modules/fable-library-js.5.6.0/Result.js";
import { tryGetValue } from "../fable_modules/fable-library-js.5.6.0/MapUtil.js";
import { FifoInbox_$ctor_Z524259A4 } from "../Session/Inbox.js";
import { map, unwrap } from "../fable_modules/fable-library-js.5.6.0/Option.js";

export class SessionRuntime extends Record {
    constructor(SessionId, Inbox, Driver, Cts) {
        super();
        this.SessionId = SessionId;
        this.Inbox = Inbox;
        this.Driver = Driver;
        this.Cts = Cts;
    }
}

export function SessionRuntime_$reflection() {
    return record_type("Wanxiangshu.Next.OpenCode.SessionRuntime", [], SessionRuntime, () => [["SessionId", SessionId_$reflection()], ["Inbox", class_type("Wanxiangshu.Next.Session.ISessionInbox")], ["Driver", option_type(SessionDriver_$reflection())], ["Cts", class_type("System.Threading.CancellationTokenSource")]]);
}

export class PluginRuntime {
    constructor(gateway, dir, port) {
        this.gateway = gateway;
        this.dir = dir;
        this.port = port;
        this.sessionRuntimes = (new Dictionary([], {
            Equals: equals,
            GetHashCode: (x) => (safeHash(x) | 0),
        }));
        this.sessionDrivers = SessionDrivers_$ctor();
        this.lockObj = {};
        this.cts = createCancellationToken();
    }
    "System.IAsyncDisposable.DisposeAsync"() {
        let builder$0040;
        const this$ = this;
        return (builder$0040 = task(), builder$0040.Run(builder$0040.Delay(() => {
            const runtimesToDispose = Operators_Lock(this$.lockObj, () => {
                const list = Array.from(this$.sessionRuntimes.values());
                this$.sessionRuntimes.clear();
                return list;
            });
            cancel(this$.cts);
            return builder$0040.Combine(builder$0040.For(runtimesToDispose, (_arg) => {
                let matchValue, d;
                const sr = _arg;
                cancel(sr.Cts);
                return builder$0040.Combine((matchValue = sr.Driver, (matchValue == null) ? (builder$0040.Zero()) : ((d = matchValue, (disposeSafe(d), builder$0040.TryWith(builder$0040.Delay(() => builder$0040.Bind(SessionDriver__get_Worker(d), () => builder$0040.Zero())), (_arg_2) => {
                    return builder$0040.Zero();
                }))))), builder$0040.Delay(() => {
                    return builder$0040.Zero();
                }));
            }), builder$0040.Delay(() => {
                return builder$0040.Bind(this$.gateway["System.IAsyncDisposable.DisposeAsync"](), () => builder$0040.Return(undefined));
            }));
        })));
    }
}

export function PluginRuntime_$reflection() {
    return class_type("Wanxiangshu.Next.OpenCode.PluginRuntime", undefined, PluginRuntime);
}

function PluginRuntime_$ctor_427E13E9(gateway, dir, port) {
    return new PluginRuntime(gateway, dir, port);
}

export function PluginRuntime__get_Gateway(_) {
    return _.gateway;
}

export function PluginRuntime__get_Directory(_) {
    return _.dir;
}

export function PluginRuntime__get_CancellationToken(_) {
    return _.cts;
}

export function PluginRuntime__get_SessionDrivers(_) {
    return _.sessionDrivers;
}

export function PluginRuntime__get_Port(_) {
    return _.port;
}

export function PluginRuntime_start(dir, port) {
    const builder$0040 = task();
    return builder$0040.Run(builder$0040.Delay(() => {
        const cts = createCancellationToken();
        return builder$0040.Bind(GatewayModule_start(dir, cts), (_arg) => {
            const gwRes = _arg;
            return (gwRes.tag === 1) ? builder$0040.Return(new FSharpResult$2(1, [gwRes.fields[0]])) : builder$0040.Return(new FSharpResult$2(0, [PluginRuntime_$ctor_427E13E9(gwRes.fields[0], dir, port)]));
        });
    }));
}

export function PluginRuntime__GetOrCreateSessionRuntime_2C0A04B3(this$, sessionId) {
    return Operators_Lock(this$.lockObj, () => {
        let matchValue;
        let outArg = defaultOf();
        matchValue = [tryGetValue(this$.sessionRuntimes, sessionId, new FSharpRef(() => outArg, (v) => {
            outArg = v;
        })), outArg];
        if (matchValue[0]) {
            return matchValue[1];
        }
        else {
            const sr_1 = new SessionRuntime(sessionId, FifoInbox_$ctor_Z524259A4(1000), undefined, createCancellationToken());
            this$.sessionRuntimes.set(sessionId, sr_1);
            return sr_1;
        }
    });
}

export function PluginRuntime__EnsureSessionDriver_2C0A04B3(this$, sessionId) {
    return Operators_Lock(this$.lockObj, () => {
        const sessionRuntime = PluginRuntime__GetOrCreateSessionRuntime_2C0A04B3(this$, sessionId);
        if (sessionRuntime.Driver == null) {
            const runningRuntime = new SessionRuntime(sessionRuntime.SessionId, sessionRuntime.Inbox, SessionDriver_$ctor_Z5603C1CE(this$.gateway, sessionId, sessionRuntime.Inbox, unwrap(map((value) => value, this$.port))), sessionRuntime.Cts);
            this$.sessionRuntimes.set(sessionId, runningRuntime);
            return runningRuntime;
        }
        else {
            return sessionRuntime;
        }
    });
}

export function PluginRuntime__GetInboxMap(this$) {
    return Operators_Lock(this$.lockObj, () => {
        const dict = new Dictionary([], {
            Equals: equals,
            GetHashCode: (x) => (safeHash(x) | 0),
        });
        let enumerator = getEnumerator(this$.sessionRuntimes);
        try {
            while (enumerator["System.Collections.IEnumerator.MoveNext"]()) {
                const kv = enumerator["System.Collections.Generic.IEnumerator`1.get_Current"]();
                dict.set(kv[0], kv[1].Inbox);
            }
        }
        finally {
            disposeSafe(enumerator);
        }
        return dict;
    });
}

