// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.

namespace Microsoft.Data.Entity.Scaffolding.Pluralization
{
    using JetBrains.Annotations;
    using Microsoft.Data.Entity.Utilities;
    using System.Diagnostics.CodeAnalysis;

    /// <summary>
    /// Represents a custom pluralization term to be used by the <see cref="EnglishPluralizationService" />
    /// </summary>
    [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Pluralization")]
    public class CustomPluralizationEntry
    {
        /// <summary>
        /// Get the singular.
        /// </summary>
        public string Singular { get; private set; }

        /// <summary>
        /// Get the plural.
        /// </summary>
        public string Plural { get; private set; }

        /// <summary>
        /// Create a new instance
        /// </summary>
        /// <param name="singular">A non null or empty string representing the singular.</param>
        /// <param name="plural">A non null or empty string representing the plural.</param>
        public CustomPluralizationEntry([NotNull] string singular, [NotNull] string plural)
        {
            Check.NotEmpty(singular, nameof(singular));
            Check.NotEmpty(plural, nameof(plural));

            Singular = singular;
            Plural = plural;
        }
    }
}
