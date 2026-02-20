namespace RogueEngine.Core.Runtime.Ecs;

public readonly record struct Position(int X, int Y);

public readonly record struct Velocity(int DX, int DY);

public readonly record struct Collider(bool BlocksMovement);

public readonly record struct Health(int Current, int Max);
