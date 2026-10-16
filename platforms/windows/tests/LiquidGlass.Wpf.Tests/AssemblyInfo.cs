using Xunit;

// WPF tests show windows and some change process-wide settings: run them one at a time.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
