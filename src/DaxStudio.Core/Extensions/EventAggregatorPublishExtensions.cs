using System.Threading;
using System.Threading.Tasks;

namespace Caliburn.Micro
{
    /// <summary>
    /// Adds a thread-agnostic <c>PublishAsync</c> overload to <see cref="IEventAggregator"/>.
    /// Caliburn.Micro 5 ships <c>PublishAsync(message, marshal, ct)</c> on the interface plus
    /// <c>PublishAsync</c>/<c>PublishAsync</c>/<c>PublishAsync</c>
    /// extensions, but no single-argument <c>PublishAsync(message)</c>. With every subscriber in DAX Studio
    /// declaring its own marshalling via <c>SubscribeOnUIThread</c>/<c>SubscribeOnBackgroundThread</c>/
    /// <c>SubscribeOnPublishedThread</c>, the publisher should not dictate the thread context. This
    /// extension publishes inline (no marshal wrapper) and lets each subscriber's own marshal take effect.
    /// </summary>
    public static class EventAggregatorPublishExtensions
    {
        public static Task PublishAsync(this IEventAggregator eventAggregator, object message)
            => eventAggregator.PublishAsync(message, f => f(), default);

        public static Task PublishAsync(this IEventAggregator eventAggregator, object message, CancellationToken cancellationToken)
            => eventAggregator.PublishAsync(message, f => f(), cancellationToken);
    }
}
