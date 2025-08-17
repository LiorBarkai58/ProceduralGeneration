using System;

public enum Face : int { PX = 0, NX = 1, PY = 2, NY = 3, PZ = 4, NZ = 5 } // +X,-X,+Y,-Y,+Z,-Z

[Flags]
public enum Socket : ushort
{
    None = 0,
    Open = 1 << 0,   // passable empty face / air
    Solid = 1 << 1,   // wall/blocked
    DoorA = 1 << 2,
    Vent = 1 << 3,
    Pipe = 1 << 4,
    Window = 1 << 5,
    // Add more channels as needed
}