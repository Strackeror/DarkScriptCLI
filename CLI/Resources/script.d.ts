declare type Behavior = number;
declare const Restart: Behavior;
declare const End: Behavior;
declare const Default: Behavior;

declare type ONOFF = typeof ON | typeof OFF;

declare function Event(id: number, behavior: Behavior, func: (...any: any[]) => void);
declare function $Event(id: number, behavior: Behavior, func: (...any: any[]) => void);
declare function EndEvent();
declare function RestartEvent();
declare function EndIf(cond: Condition);
declare function Goto(label: Label);
declare function GotoIf(label: Label, cond: Condition);
declare function WaitFor(cond: Condition);
declare function NoOp();