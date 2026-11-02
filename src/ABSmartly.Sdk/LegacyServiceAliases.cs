using System;
using Microsoft.Extensions.Logging;

namespace ABSmartly;

// Source-compatible aliases for names that used the "ABSmartly" casing before the rename to "ABsmartly".

[Obsolete("ABSmartlyServiceConfiguration has been renamed to ABsmartlyServiceConfiguration. Please use ABsmartlyServiceConfiguration instead.")]
public class ABSmartlyServiceConfiguration : ABsmartlyServiceConfiguration
{
}

[Obsolete("IABSmartlyServiceClient has been renamed to IABsmartlyServiceClient. Please use IABsmartlyServiceClient instead.")]
public interface IABSmartlyServiceClient : IABsmartlyServiceClient
{
}

[Obsolete("ABSmartlyService has been renamed to ABsmartlyService. Please use ABsmartlyService instead.")]
#pragma warning disable CS0618
public class ABSmartlyService : ABsmartlyService, IABSmartlyServiceClient
#pragma warning restore CS0618
{
    public ABSmartlyService(ABsmartlyServiceConfiguration config,
        IABsmartlyHttpClientFactory httpClientFactory,
        IContextDataDeserializer dataDeserializer,
        IContextEventSerializer eventSerializer,
        ILoggerFactory loggerFactory = null)
        : base(config, httpClientFactory, dataDeserializer, eventSerializer, loggerFactory)
    {
    }
}
