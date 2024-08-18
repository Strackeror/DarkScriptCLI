declare type Behavior = number;
declare const Restart: Behavior;
declare const End: Behavior;
declare const Default: Behavior;

declare type Body = (...any: EventParam[]) => void;
declare type ONOFF = typeof ON | typeof OFF;

declare function Event(id: number, behavior: Behavior, func: Body);
declare function $Event(id: number, behavior: Behavior, func: Body);
declare function JsEvent(id: number, behavior: Behavior, func: Body);
declare function EndEvent();
declare function RestartEvent();
declare function RestartIf(cond: Condition);
declare function EndIf(cond: Condition);
declare function Goto(label: Label);
declare function GotoIf(label: Label, cond: Condition);
declare function Skip(count: number);
declare function SkipIf(count: number, cond: Condition);
declare function SkipTo(label: string);
declare function SkipToIf(label: string, cond: Condition);
declare function WaitFor(cond: Condition);
declare function NoOp();
declare function NamedLabel(str: string);

declare const Else: undefined;
declare const mainGroupAbuse: Condition;

// prettier-ignore
type Alternating<A, B> = [A, B, A?, B?, A?, B?, A?, B?, A?, B?, A?, B?, A?, B?, A?, B?, A?, B?, A?, B?, A?, B?, A?, B?, A?, B?, A?, B?, A?, B?, A?, B?, A?, B?, A?, B?, A?, B?, A?, B?, A?, B?, A?, B?, A?, B?, A?, B?, A?, B?];
type IfArgs = Alternating<Condition, () => void>;

declare function If(...args: IfArgs);

declare class Condition {
  get Passed(): Condition;
  And(cond: Condition): Condition;
  Or(cond: Condition): Condition;
  Not(): Condition;
  Get(): Condition;

  static New(): Condition;
}

declare function Not(cond: Condition): Condition;
declare function Get(cond: Condition): Condition;

declare class Comparable {
  Eq(comp: Comparable | number): Condition;
  NEq(comp: Comparable | number): Condition;
  Gt(comp: Comparable | number): Condition;
  GtE(comp: Comparable | number): Condition;
  Lt(comp: Comparable | number): Condition;
  LtE(comp: Comparable | number): Condition;
}

declare interface Number {
  Eq(comp: Comparable): Condition;
  NEq(comp: Comparable): Condition;
  Gt(comp: Comparable): Condition;
  GtE(comp: Comparable): Condition;
  Lt(comp: Comparable): Condition;
  LtE(comp: Comparable): Condition;
}

declare type EventParam = string & { __tag: "EventParam" };
declare type Arg<T> = T | EventParam;

// Cheating a bit, this actually returns the string representations
declare function X(startByte: number, byteCount: number): EventParam;

declare function L(label: Label);
declare const Label0: Label;
declare const Label1: Label;
declare const Label2: Label;
declare const Label3: Label;
declare const Label4: Label;
declare const Label5: Label;
declare const Label6: Label;
declare const Label7: Label;
declare const Label8: Label;
declare const Label9: Label;
declare const Label10: Label;
declare const Label11: Label;
declare const Label12: Label;
declare const Label13: Label;
declare const Label14: Label;
declare const Label15: Label;
declare const Label16: Label;
declare const Label17: Label;
declare const Label18: Label;
declare const Label19: Label;
declare const Label20: Label;
