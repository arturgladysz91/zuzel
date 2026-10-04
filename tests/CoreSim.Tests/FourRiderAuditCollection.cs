using Xunit;

namespace CoreSim.Tests;

// Readers use one immutable aggregate and run in one collection, so waiting on
// the shared Lazy is not mistaken for a second expensive generation in TRX.
[CollectionDefinition("FourRiderAudit")]
public sealed class FourRiderAuditCollection { }
