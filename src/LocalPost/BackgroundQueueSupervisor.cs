namespace LocalPost;

//internal sealed partial class BackgroundQueue<T>
//{
//    internal sealed class Supervisor : IConcurrentHostedService
//    {
//        // Health checks later?.. Like full or not.
//
//        private readonly IBackgroundQueueManager _queue;
//
//        public Supervisor(IBackgroundQueueManager queue)
//        {
//            _queue = queue;
//        }
//
//        // TODO Run the enumarable... Like Batched.
//        public Task StartAsync(CancellationToken ct) => Task.CompletedTask;
//
//        public async Task StopAsync(CancellationToken forceExitToken) => await _queue.CompleteAsync(forceExitToken);
//    }
//}
