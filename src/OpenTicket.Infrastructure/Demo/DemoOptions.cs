using System.ComponentModel.DataAnnotations;

namespace OpenTicket.Infrastructure.Demo;

/// <summary>
/// Public demo mode. When enabled: sample data is seeded, visitors can sign in with one click as each role,
/// sign-up is closed, demo accounts are protected, and demo data is reset every <see cref="ResetIntervalHours"/>.
/// Only data created by the demo accounts is ever reset; other users' data is left alone.
/// </summary>
public sealed class DemoOptions
{
    public const string Section = "Demo";

    public bool Enabled { get; init; }

    /// <summary>0 turns the periodic reset off (data is still seeded once).</summary>
    [Range(0, 24 * 30)]
    public int ResetIntervalHours { get; init; } = 6;
}
