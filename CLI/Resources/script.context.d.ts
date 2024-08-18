// C# Shared stuff
declare class INSTRUCTION {}

declare class EVENT {
  ID: number;
  RestBehavior: number;
  Instructions: {
    Count: number;
    Add(ins: INSTRUCTION): void;
    [id: number]: INSTRUCTION;
  };
}

declare const REST: {
  Restart: number;
  End: number;
  Default: number;
};

declare const EVD: {
  Events: {
    Add(event: EVENT): void;
  };
};

declare class Scripter {
  static MakeInstruction(
    _: EVENT,
    bank: number,
    index: number,
    replace: number | null,
    args: any[]
  ): INSTRUCTION;
  static MakeInstruction(
    _: EVENT,
    bank: number,
    index: number,
    replace: number | null,
    layer: number,
    args: any[]
  ): INSTRUCTION;

  static FillSkipPlaceholder(event: EVENT, index: number): unknown;
  static FillGotoPlaceholder(
    event: EVENT,
    index: number,
    label: Label
  ): unknown;
  static ConvertFloatToIntBytes(float: number): number;
  static CurrentInsName: string;
}

declare class $$$_host {
  static newArr(len: number): any[];
}

declare class Console {
  static WriteLine(str: string): void;
}

declare type Label = number;
declare type IfFunc = (cond: ConditionGroup, ...args: any[]) => void;
declare type SkipFunc = (skip: number, ...args: any[]) => void;
declare type Endfunc = (end: EventEndType, ...args: any[]) => void;
declare type GotoFunc = (label: Label, ...args: any[]) => void;
declare type WaitFunc = (...args: any[]) => void;

declare interface Number {
  Eq(c: Comparable): Condition;
  NEq(c: Comparable): Condition;
  Gt(c: Comparable): Condition;
  Lt(c: Comparable): Condition;
  GtE(c: Comparable): Condition;
  LtE(c: Comparable): Condition;
}
// Common event stuff
declare function InitializeEvent(
  slot: number,
  id: number,
  ...args: number[]
): void;
declare const CondAlways: ConditionType & { Skip: Endfunc, Goto: GotoFunc, End: Endfunc };
declare function Always(): Condition;
declare function CondGroup(group: ConditionGroup): Condition;
declare function CompiledConditionGroup(group: ConditionGroup): Condition;

declare function Label0(): void;
declare function Label1(): void;
declare function Label2(): void;
declare function Label3(): void;
declare function Label4(): void;
declare function Label5(): void;
declare function Label6(): void;
declare function Label7(): void;
declare function Label8(): void;
declare function Label9(): void;
declare function Label10(): void;
declare function Label11(): void;
declare function Label12(): void;
declare function Label13(): void;
declare function Label14(): void;
declare function Label15(): void;
declare function Label16(): void;
declare function Label17(): void;
declare function Label18(): void;
declare function Label19(): void;
declare function Label20(): void;

declare enum EventEndType {
  End,
  Restart,
}
declare enum ComparisonType {
  Equal = 0,
  NotEqual = 1,
  Greater = 2,
  Less = 3,
  GreaterOrEqual = 4,
  LessOrEqual = 5,
}
declare enum ConditionGroup {
  OR_15 = -15,
  OR_14 = -14,
  OR_13 = -13,
  OR_12 = -12,
  OR_11 = -11,
  OR_10 = -10,
  OR_09 = -9,
  OR_08 = -8,
  OR_07 = -7,
  OR_06 = -6,
  OR_05 = -5,
  OR_04 = -4,
  OR_03 = -3,
  OR_02 = -2,
  OR_01 = -1,
  MAIN = 0,
  AND_01 = 1,
  AND_02 = 2,
  AND_03 = 3,
  AND_04 = 4,
  AND_05 = 5,
  AND_06 = 6,
  AND_07 = 7,
  AND_08 = 8,
  AND_09 = 9,
  AND_10 = 10,
  AND_11 = 11,
  AND_12 = 12,
  AND_13 = 13,
  AND_14 = 14,
  AND_15 = 15,
}

declare enum ConditionState {
  FAIL = 0,
  PASS = 1,
}
