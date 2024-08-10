declare type Behavior = number;
declare const Restart: Behavior;
declare const End: Behavior;
declare const Default: Behavior;

declare function Event(id: number, behavior: Behavior, func: (...any: any[]) => void);
declare function $Event(id: number, behavior: Behavior, func: (...any: any[]) => void);
declare type ONOFF = ONOFFCHANGE;
