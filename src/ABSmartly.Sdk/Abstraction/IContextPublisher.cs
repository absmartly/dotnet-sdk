namespace ABSmartly;

/// <summary>
/// Publishes the events a context has queued. Replaces <see cref="IContextEventHandler"/>.
/// </summary>
/// <remarks>
/// <c>Task PublishAsync(IContext context, PublishEvent publishEvent)</c> is inherited from
/// <see cref="IContextEventHandler"/>, so 1.3.0 implementations, including explicit
/// <c>IContextEventHandler.PublishAsync</c> ones, keep compiling, and every publisher is usable
/// wherever 1.3.0 accepted an <see cref="IContextEventHandler"/>.
/// </remarks>
#pragma warning disable CS0618
public interface IContextPublisher : IContextEventHandler
#pragma warning restore CS0618
{
}
