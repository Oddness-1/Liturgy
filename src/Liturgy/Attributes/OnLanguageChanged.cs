using System;
using JetBrains.Annotations;

namespace Liturgy;

public static partial class Rite
{
    /// <summary>
    /// Invokes a static method when the game's language changes, as with <c>TranslationServer.SetLocale</c>.
    /// </summary>
    /// <remarks>
    /// Parameterless only. Fires on changes only, not for the language the game starts in. Supports <c>Priority</c> to control invocation order.
    /// </remarks>
    /// <example>
    /// <code>
    /// [Rite.OnLanguageChanged]
    /// private static void RefreshText() { }
    /// </code>
    /// </example>
    [MeansImplicitUse]
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class OnLanguageChanged : RiteAttribute
    {
        /// <summary>
        /// Execution order (lower runs first). Default is 0.
        /// </summary>
        public int Priority { get; set; } = 0;
    }
}
