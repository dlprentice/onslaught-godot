// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Core;

/// <summary>
/// <c>IScript::FollowWaypoint</c> — the self-follow
/// <c>mov dword ptr [esi+0x18], 1</c> store.
/// Isolated <see cref="Level100ActorScriptCommandKind.FollowWaypoint"/>
/// names the rebuild command / path. FollowWaypointWait's
/// early-out, CVM snapshot, <c>+0x1c</c>, <c>+0x14</c>
/// cursor, <c>+0x24</c> flag, and AddEvent 2000 stay
/// unclaimed.
/// </summary>
/// <remarks>
/// <para>
/// Retail identity: independently re-read this session from
/// <c>local-lab/pristine-verification-2026-07-26/pristine-target/BEA.exe</c>
/// (2,506,752 bytes, SHA-256
/// <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>).
/// Twin
/// <c>local-lab/safe-copy-bea-pristine/BEA.exe.original.backup</c>
/// matches. File offset = VA − 0x400000.
/// <c>IScript__FollowWaypoint</c> at <c>0x00537d70</c> through
/// <c>ret 0xc</c> at <c>0x00537e34</c> is 199 bytes, SHA-256
/// <c>6f03ae4ab14b8cd1007f09c0ccb2ba559d183d5eb35bc4e00b68993bcced9fd3</c>.
/// Function note <c>IScript.cpp.md</c> already names this
/// body (byte-exact 2026-08-18).
/// </para>
/// <para>
/// <b>Self-follow writes literal 1 at <c>IScript+0x18</c>.</b>
/// <c>0x00537df7</c> is <c>b8 01 00 00 00</c> =
/// <c>mov eax, 1</c>. <c>0x00537e20</c> is
/// <c>89 46 18</c> = <c>mov [esi+0x18], eax</c> after
/// <c>cmp [esi+0x18], eax</c> / <c>je</c> skips the
/// already-1 path. Isolated FollowWaypoint emit-command
/// names the rebuild path / flag 0; skip this store still
/// leaves that command. One live store of 1 is not unique
/// versus increment from 0. Mutation: increment so
/// Start(1) becomes 2. Level 100's one compiled native-0
/// site is <c>AirborneDrone1.ready()</c>
/// <c>FollowWaypoint("Drone Path 1", 0)</c> on the
/// SimInput Won path (beat 7).
/// </para>
/// <para>
/// FollowWaypointWait early-out / CVM snapshot / 0.05f /
/// <c>+0x1c</c> arrived-pending / <c>+0x14</c> cursor /
/// <c>+0x24</c> args[1] / AddEvent 2000 / PlayAnimationWait
/// stay unclaimed. ChargeWeapon stays unclaimed. Live
/// <c>GAME.mSlots</c> stay unclaimed. No new secondaries.
/// </para>
/// </remarks>
public static class RetailIScriptFollowWaypoint
{
    /// <summary>
    /// <c>UpdateWaypointFollowing</c>'s arrival (<c>0x00538470-0x005384d6</c>):
    /// the unit has arrived when its stored 2D distance to the node,
    /// <c>√(dy² + dx²)</c> with node minus unit at PC24, is below the class
    /// radius (slot 94: 5.0 for <c>CPlane</c>, 8.0 for <c>CDropship</c>).
    /// </summary>
    public static bool Arrived(Level100FloatVector3Bits position, Level100FloatVector4Bits node, float radius)
    {
        double dx = RetailFloat24.Subtract(
            BitConverter.Int32BitsToSingle(node.X), BitConverter.Int32BitsToSingle(position.X));
        double dy = RetailFloat24.Subtract(
            BitConverter.Int32BitsToSingle(node.Y), BitConverter.Int32BitsToSingle(position.Y));
        float distance = (float)RetailFloat24.Sqrt(RetailFloat24.Add(
            RetailFloat24.Multiply(dy, dy), RetailFloat24.Multiply(dx, dx)));
        return distance < radius;
    }

    /// <summary>
    /// Registry 0 handler — <c>0x00537d70</c>.
    /// </summary>
    public const int HandlerAddress = 0x00537d70;

    /// <summary>
    /// <c>IScript</c> start-follow flag — <c>this+0x18</c>.
    /// </summary>
    public const int FlagOffset = 0x18;

    /// <summary>
    /// The <c>mov [esi+0x18], eax</c> immediate at
    /// <c>0x00537e20</c> after <c>mov eax, 1</c>.
    /// </summary>
    public const int FlagStarted = 1;

    /// <summary>
    /// Opening dword before the first FollowWaypoint.
    /// FollowWaypointWait's early-out on 1 is not this pin.
    /// </summary>
    public const int FlagIdle = 0;

    /// <summary>
    /// The literal-1 store. Mutation: increment so
    /// Start(1) becomes 2.
    /// </summary>
    public static int Start(int currentFlag)
    {
        _ = currentFlag;
        return FlagStarted;
    }
}
